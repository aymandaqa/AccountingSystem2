using System.Security.Claims;

namespace Roadfn.Services
{
    public class UserResolverService
    {
        private readonly IHttpContextAccessor _context;
        public UserResolverService(IHttpContextAccessor context)
        {
            _context = context;
        }

        public string GetUser()
        {
            if (_context.HttpContext.User?.Identity.IsAuthenticated == true)
            {
                string userId = _context.HttpContext.User?.Claims.SingleOrDefault(x => x.Type.Equals(ClaimTypes.NameIdentifier)).Value;

                string fullname = "AccountingSystem-" + "|" + _context.HttpContext.User?.Identity?.Name;
                return fullname;
            }

            return "";
        }
    }
}
