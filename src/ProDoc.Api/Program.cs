using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using ProDoc.Api.Contracts;
using ProDoc.Api.Errors;
using ProDoc.Api.Security;
using ProDoc.Application;
using ProDoc.Infrastructure;
using ProDoc.Infrastructure.Queue;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

ProductionGuards.EnsureSafeForEnvironment(builder.Configuration, builder.Environment);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ProDoc.Api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .Filter.ByExcluding(logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var path)
            && path.ToString().Contains("/payments/webhooks", StringComparison.OrdinalIgnoreCase)
            && logEvent.Level < LogEventLevel.Warning)
        .WriteTo.Console();
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear known networks/proxies so reverse-proxy deployments (Azure App Service, nginx) work by default.
    // Operators should tighten this in locked-down networks.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<RequestValidationFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<RequestValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<RequestOtpRequestValidator>();
ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
    member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ProDoc API",
        Version = "v1",
        Description = "Offline doctor booking and clinic management."
    });
    options.SwaggerDoc("partner", new OpenApiInfo
    {
        Title = "ProDoc Partner API",
        Version = "v1",
        Description = "Versioned partner ecosystem API. Uses OAuth 2.0 client_credentials. Does not expose internal admin or unrestricted medical records."
    });
    // Avoid schema-id collisions (e.g. Admin.NamedCount vs Automation.NamedCount) that break /swagger/v1/swagger.json.
    options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace('+', '.'));
    options.DocInclusionPredicate((docName, api) =>
    {
        var path = api.RelativePath ?? string.Empty;
        var isPartner = path.StartsWith("api/partner", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("oauth/partner", StringComparison.OrdinalIgnoreCase);
        return docName == "partner" ? isPartner : !isPartner;
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token"
    });
    options.AddSecurityDefinition("partner", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Partner access token from /oauth/partner/token"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("client", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();
        if (builder.Environment.IsDevelopment())
        {
            // Expo web + devices on the LAN use http://<host>:8081 (not only localhost).
            policy.SetIsOriginAllowed(origin =>
                origins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                || DevelopmentCors.IsClientOrigin(origin));
            return;
        }

        if (origins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
            return;
        }

        policy.WithOrigins(origins);
    }));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests. Wait a moment and try again.",
            Type = "urn:prodoc:rate_limited"
        }, cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 2000 : 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    var authPermitLimit = builder.Environment.IsDevelopment() ? 500 : 30;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
    var assistantPermitLimit = builder.Environment.IsDevelopment() ? 120 : 20;
    options.AddPolicy("assistant", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("sub")?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = assistantPermitLimit,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
    options.AddPolicy("webhooks", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 200 : 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("partner_oauth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 120 : 20,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
    options.AddPolicy("partner_api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("client_id")?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ =>
            {
                var limit = 60;
                if (int.TryParse(httpContext.User.FindFirst("rate_limit")?.Value, out var configured))
                {
                    limit = Math.Clamp(configured, 10, 6000);
                }

                return new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Environment.IsDevelopment() ? Math.Max(limit, 200) : limit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                };
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
        ex is not null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value ?? string.Empty);
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProDoc API v1");
        options.SwaggerEndpoint("/swagger/partner/swagger.json", "ProDoc Partner API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("client");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");
if (app.Environment.IsDevelopment())
{
    // Opening the site root (or a profile without launchUrl) lands on Swagger in Development.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health");

var migrate = ProductionGuards.ShouldMigrate(app.Configuration, app.Environment);
await app.Services.InitializeDatabaseAsync(migrate: migrate, CancellationToken.None);
app.Run();

file static class DevelopmentCors
{
    public static bool IsClientOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (uri.Host is "localhost" or "127.0.0.1" or "[::1]")
        {
            return true;
        }

        if (!System.Net.IPAddress.TryParse(uri.Host, out var ip)
            || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}

public partial class Program;
