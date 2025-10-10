using System.Collections.Generic;
using System.Text.Json;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserPreferencesController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<UserPreferencesController> _logger;

        public UserPreferencesController(UserManager<User> userManager, ILogger<UserPreferencesController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("sidebar-menu-order")]
        public async Task<IActionResult> GetSidebarMenuOrder()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            try
            {
                var storedOrder = string.IsNullOrWhiteSpace(user.SidebarMenuOrder)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(user.SidebarMenuOrder) ?? new List<string>();

                return Ok(new SidebarMenuOrderResponse { Order = storedOrder });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize sidebar menu order for user {UserId}", user.Id);
                return Ok(new SidebarMenuOrderResponse { Order = new List<string>() });
            }
        }

        [HttpPost("sidebar-menu-order")]
        public async Task<IActionResult> SaveSidebarMenuOrder([FromBody] SidebarMenuOrderRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var sanitizedOrder = request?.Order?
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct()
                .ToList() ?? new List<string>();

            user.SidebarMenuOrder = JsonSerializer.Serialize(sanitizedOrder);
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok(new SidebarMenuOrderResponse { Order = sanitizedOrder });
        }

        public class SidebarMenuOrderRequest
        {
            public List<string> Order { get; set; } = new();
        }

        public class SidebarMenuOrderResponse
        {
            public List<string> Order { get; set; } = new();
        }
    }
}
