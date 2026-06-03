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
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAuthService authService, ILogger<AdminController> logger)
        {
            _authService = authService;
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
        public async Task<IActionResult> Create(string username, string password, string role, string subscriptionTier)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ tên tài khoản và mật khẩu!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _authService.RegisterAsync(username.Trim(), password, role, subscriptionTier);
                TempData["SuccessMessage"] = $"Đã tạo thành công tài khoản '{username}' với vai trò {role}!";
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
    }
}
