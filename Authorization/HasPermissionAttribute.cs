using Microsoft.AspNetCore.Authorization;

namespace WiseMonitor.Api.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission)
            : base(policy: $"Permission:{permission}")
        {
        }
    }
}
