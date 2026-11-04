using Microsoft.AspNetCore.Authorization;
using ProDoc.Infrastructure.Auth;

namespace ProDoc.Api.Auth;

internal sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = PermissionPolicy.Prefix + permission;
    }
}
