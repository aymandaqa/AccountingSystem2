using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IUserSessionService _userSessionService;
        private readonly INotificationService _notificationService;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<AccountController> logger,
            IUserSessionService userSessionService,
            INotificationService notificationService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _userSessionService = userSessionService;
            _notificationService = notificationService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                if (!model.LocationConsent || !model.Latitude.HasValue || !model.Longitude.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "يجب السماح بالوصول إلى الموقع لإتمام تسجيل الدخول.");
                    return View(model);
                }

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && user.IsActive)
                {
                    var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);

                    if (result.Succeeded)
                    {
                        var session = await _userSessionService.CreateSessionAsync(user, HttpContext, new SessionCreationOptions
                        {
                            Latitude = model.Latitude,
                            Longitude = model.Longitude,
                            LocationAccuracy = model.LocationAccuracy,
                            LocationTimestamp = model.LocationTimestamp,
                            BrowserName = model.BrowserName,
                            BrowserIcon = model.BrowserIcon,
                            LocationConsent = model.LocationConsent
                        });
                        var revokedSessions = await _userSessionService.InvalidateOtherSessionsAsync(user.Id, session.SessionId);

                        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[]
                        {
                            new Claim("SessionId", session.SessionId)
                        });

                        user.LastLoginAt = DateTime.Now;
                        await _userManager.UpdateAsync(user);

                        _logger.LogInformation("User {Email} logged in.", model.Email);

                        await _notificationService.CreateNotificationAsync(new Notification
                        {
                            UserId = user.Id,
                            Title = "تسجيل دخول جديد",
                            Message = $"تم تسجيل الدخول من {(session.DeviceName ?? session.DeviceType ?? "جهاز غير معروف")} ({session.OperatingSystem ?? "نظام غير معروف"}) - IP: {session.IpAddress ?? "غير معروف"}.",
                            Icon = "fa-sign-in-alt"
                        });

                        if (revokedSessions.Count > 0)
                        {
                            var revoked = revokedSessions.First();
                            await _notificationService.CreateNotificationAsync(new Notification
                            {
                                UserId = user.Id,
                                Title = "تسجيل خروج تلقائي",
                                Message = $"تم تسجيل خروج جلسة سابقة على {(revoked.DeviceName ?? revoked.DeviceType ?? "جهاز غير معروف")} (IP: {revoked.IpAddress ?? "غير معروف"}) بسبب تسجيل الدخول من جهاز آخر.",
                                Icon = "fa-exclamation-triangle"
                            });
                        }

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        return RedirectToAction("Index", "Dashboard");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "محاولة دخول غير صحيحة.");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var sessionId = User.FindFirst("SessionId")?.Value;
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _userSessionService.InvalidateSessionByIdentifierAsync(sessionId, "تسجيل خروج من المستخدم");
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Sessions()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var activeSessions = await _userSessionService.GetActiveSessionsAsync(user.Id);
            var recentSessions = await _userSessionService.GetRecentSessionsAsync(user.Id);
            var currentSessionId = User.FindFirst("SessionId")?.Value;

            var model = new UserSessionsViewModel
            {
                CurrentSessionId = currentSessionId,
                ActiveSessions = activeSessions.Select(MapSession).ToList(),
                RecentSessions = recentSessions.Select(MapSession).ToList()
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeSession(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var session = await _userSessionService.FindByIdAsync(id);
            if (session == null || session.UserId != user.Id)
            {
                return NotFound();
            }

            var isCurrent = session.SessionId == User.FindFirst("SessionId")?.Value;
            await _userSessionService.InvalidateSessionAsync(id, "تم إنهاء الجلسة بواسطة المستخدم");

            if (isCurrent)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login");
            }

            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = user.Id,
                Title = "تم إنهاء جلسة",
                Message = $"تم إنهاء جلسة على {(session.DeviceName ?? session.DeviceType ?? "جهاز غير معروف")} ({session.OperatingSystem ?? "نظام غير معروف"}) - IP: {session.IpAddress ?? "غير معروف"}.",
                Icon = "fa-power-off"
            });

            return RedirectToAction(nameof(Sessions));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        //[HttpGet]
        //public IActionResult Register()
        //{
        //    return View();
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Register(RegisterViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        var user = new User
        //        {
        //            UserName = model.Email,
        //            Email = model.Email,
        //            FirstName = model.FirstName,
        //            LastName = model.LastName,
        //            EmailConfirmed = true,
        //            IsActive = true
        //        };

        //        var result = await _userManager.CreateAsync(user, model.Password);

        //        if (result.Succeeded)
        //        {
        //            await _userManager.AddToRoleAsync(user, "User");

        //            _logger.LogInformation("User created a new account with password.");

        //            await _signInManager.SignInAsync(user, isPersistent: false);
        //            return RedirectToAction("Index", "Home");
        //        }

        //        foreach (var error in result.Errors)
        //        {
        //            ModelState.AddModelError(string.Empty, error.Description);
        //        }
        //    }

        //    return View(model);
        //}

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new ProfileViewModel
            {
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                LastLoginAt = user.LastLoginAt
            };
            return View(model);
        }

        private static UserSessionItemViewModel MapSession(UserSession session)
        {
            return new UserSessionItemViewModel
            {
                Id = session.Id,
                SessionId = session.SessionId,
                DeviceName = session.DeviceName,
                DeviceType = session.DeviceType,
                OperatingSystem = session.OperatingSystem,
                IpAddress = session.IpAddress,
                BrowserName = session.BrowserName,
                BrowserIcon = session.BrowserIcon,
                Latitude = session.Latitude,
                Longitude = session.Longitude,
                LocationAccuracy = session.LocationAccuracy,
                LocationCapturedAt = session.LocationCapturedAt,
                CreatedAt = session.CreatedAt,
                LastActivityAt = session.LastActivityAt,
                EndedAt = session.EndedAt,
                IsActive = session.IsActive
            };
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Email = user.Email ?? string.Empty;
                model.FullName = user.FullName ?? string.Empty;
                model.LastLoginAt = user.LastLoginAt;
                return View(model);
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "تم تحديث كلمة المرور بنجاح.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.Email = user.Email ?? string.Empty;
            model.FullName = user.FullName ?? string.Empty;
            model.LastLoginAt = user.LastLoginAt;
            return View(model);
        }
    }
}

