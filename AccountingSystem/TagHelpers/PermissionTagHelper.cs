using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace AccountingSystem.TagHelpers
{
    [HtmlTargetElement(Attributes = "asp-require-permission")]
    public class PermissionTagHelper : TagHelper
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionTagHelper(
            IAuthorizationService authorizationService,
            IHttpContextAccessor httpContextAccessor)
        {
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        [HtmlAttributeName("asp-require-permission")]
        public string? Permission { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (string.IsNullOrWhiteSpace(Permission))
            {
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                output.SuppressOutput();
                return;
            }

            var authorizationResult = await _authorizationService.AuthorizeAsync(httpContext.User, Permission);
            if (!authorizationResult.Succeeded)
            {
                output.SuppressOutput();
            }
        }
    }
}
