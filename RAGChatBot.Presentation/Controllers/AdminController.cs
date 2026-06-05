using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IWhitelistService _whitelistService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAuthService authService, IWhitelistService whitelistService, ILogger<AdminController> logger)
        {
            _authService = authService;
            _whitelistService = whitelistService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách người dùng cho Admin Dashboard");
                TempData["ErrorMessage"] = "Không thể tải danh sách tài khoản: " + ex.Message;
                return View(new System.Collections.Generic.List<UserDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(string username, string password, string role, string subscriptionTier, string fullName)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ họ tên, tên tài khoản và mật khẩu!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _authService.RegisterAsync(username.Trim(), password, role, subscriptionTier, fullName.Trim());
                TempData["SuccessMessage"] = $"Đã tạo thành công tài khoản '{fullName}' với vai trò {role}!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi Admin tạo tài khoản {Username}", username);
                TempData["ErrorMessage"] = "Không thể tạo tài khoản: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _authService.DeleteUserAsync(id);
                TempData["SuccessMessage"] = "Đã xóa tài khoản thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tài khoản {UserId}", id);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTier(Guid id)
        {
            try
            {
                // Gọi API chuyển đổi nhanh gói cước
                var authServiceConcrete = _authService as AuthService;
                if (authServiceConcrete != null)
                {
                    var success = await authServiceConcrete.ToggleSubscriptionTierAsync(id);
                    if (success)
                    {
                        TempData["SuccessMessage"] = "Đã thay đổi gói cước thành công!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy người dùng!";
                    }
                }
                else
                {
                    // Fallback
                    var success = await _authService.UpgradeToPremiumAsync(id);
                    if (success) TempData["SuccessMessage"] = "Đã nâng cấp Premium thành công!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chuyển đổi gói cước cho tài khoản {UserId}", id);
                TempData["ErrorMessage"] = "Không thể đổi gói cước: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Whitelist()
        {
            try
            {
                var whitelist = await _whitelistService.GetAllAsync();
                return View(whitelist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách Whitelist cho Admin Dashboard");
                TempData["ErrorMessage"] = "Không thể tải danh sách Whitelist: " + ex.Message;
                return View(new System.Collections.Generic.List<WhitelistEmailDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddToWhitelist(string email, string? fullName, string? studentId)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Email không được để trống!";
                return RedirectToAction(nameof(Whitelist));
            }

            try
            {
                await _whitelistService.AddAsync(email.Trim(), fullName, studentId);
                TempData["SuccessMessage"] = $"Đã thêm email '{email}' vào Whitelist thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi Admin thêm email vào whitelist: {Email}", email);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Whitelist));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFromWhitelist(Guid id)
        {
            try
            {
                await _whitelistService.DeleteAsync(id);
                TempData["SuccessMessage"] = "Đã xóa email khỏi Whitelist thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa email khỏi whitelist: {Id}", id);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Whitelist));
        }

        [HttpPost]
        public async Task<IActionResult> ImportWhitelist(Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một file Excel (.xlsx hoặc .xls)!";
                return RedirectToAction(nameof(Whitelist));
            }

            var extension = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                TempData["ErrorMessage"] = "Định dạng file không được hỗ trợ! Vui lòng chọn file Excel (.xlsx hoặc .xls).";
                return RedirectToAction(nameof(Whitelist));
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var count = await _whitelistService.ImportFromExcelAsync(stream);
                    TempData["SuccessMessage"] = $"Import thành công {count} email vào danh sách Whitelist!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi import file whitelist Excel");
                TempData["ErrorMessage"] = "Lỗi khi import file Excel: " + ex.Message;
            }

            return RedirectToAction(nameof(Whitelist));
        }
    }
}
