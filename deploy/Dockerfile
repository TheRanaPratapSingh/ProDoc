FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ProDoc.slnx ./
COPY src/ProDoc.Domain/ProDoc.Domain.csproj src/ProDoc.Domain/
COPY src/ProDoc.Application/ProDoc.Application.csproj src/ProDoc.Application/
COPY src/ProDoc.Infrastructure/ProDoc.Infrastructure.csproj src/ProDoc.Infrastructure/
COPY src/ProDoc.Api/ProDoc.Api.csproj src/ProDoc.Api/
RUN dotnet restore src/ProDoc.Api/ProDoc.Api.csproj
COPY src/ ./src/
RUN dotnet publish src/ProDoc.Api/ProDoc.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Staging
EXPOSE 8080
RUN groupadd --system prodoc && useradd --system --gid prodoc --create-home prodoc
USER prodoc
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProDoc.Api.dll"]
