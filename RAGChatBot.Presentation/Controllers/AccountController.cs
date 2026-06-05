using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IWhitelistService _whitelistService;

        public AccountController(IAuthService authService, IWhitelistService whitelistService)
        {
            _authService = authService;
            _whitelistService = whitelistService;
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

        [HttpGet]
        public IActionResult Register()
        {
            // Vô hiệu hóa đăng ký tự do ngoài trang login
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Register(RegisterRequest request)
        {
            // Vô hiệu hóa đăng ký tự do ngoài trang login
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequest request, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập đầy đủ tên tài khoản và mật khẩu!");
                return View(request);
            }

            var userDto = await _authService.LoginAsync(request);
            if (userDto == null)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác!");
                return View(request);
            }

            // Ghi nhận thông tin Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userDto.Id.ToString()),
                new Claim(ClaimTypes.Name, !string.IsNullOrEmpty(userDto.FullName) ? userDto.FullName : userDto.Username),
                new Claim(ClaimTypes.Role, userDto.Role),
                new Claim("SubscriptionTier", userDto.SubscriptionTier)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (userDto.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ExternalLogin(string returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi từ Google: {remoteError}");
                return View("Login");
            }

            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                // Thử authenticate với ExternalScheme nếu có, nhưng ở đây ta dùng Cookie Auth làm default.
                // Khi callback về, do ta config AddGoogle mà không dùng Identity, properties sẽ nằm trong cookie.
                // Thường thì result sẽ Succeeded nếu Google trả về đúng.
                var info = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                if (!info.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "Lỗi đăng nhập bằng Google.");
                    return View("Login");
                }
                result = info;
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "Không lấy được Email từ Google.");
                return View("Login");
            }

            var userDto = await _authService.GetUserByUsernameAsync(email);
            
            if (userDto == null)
            {
                // Kiểm tra xem email có hợp lệ không (đuôi FPT hoặc nằm trong Whitelist)
                bool isAllowed = email.EndsWith("@fpt.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 email.EndsWith("@fe.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 await _whitelistService.IsEmailWhitelistedAsync(email);

                if (!isAllowed)
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản của bạn chưa được cấp quyền truy cập hệ thống. Vui lòng liên hệ Admin!");
                    return View("Login");
                }

                // Tự động đăng ký
                var fullName = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
                var randomPassword = Guid.NewGuid().ToString(); // Google users won't use password
                userDto = await _authService.RegisterAsync(email, randomPassword, "Student", "Free", fullName);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userDto.Id.ToString()),
                new Claim(ClaimTypes.Name, !string.IsNullOrEmpty(userDto.FullName) ? userDto.FullName : userDto.Username),
                new Claim(ClaimTypes.Role, userDto.Role),
                new Claim("SubscriptionTier", userDto.SubscriptionTier)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            // Đăng nhập người dùng bằng cookie của hệ thống
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (userDto.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
