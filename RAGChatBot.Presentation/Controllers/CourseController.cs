using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly IAuthService _authService;

        public CourseController(ICourseService courseService, IAuthService authService)
        {
            _courseService = courseService;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string search)
        {
            ViewData["SearchKeyword"] = search;
            var courses = await _courseService.SearchCoursesAsync(search);

            if (User.IsInRole("Admin"))
            {
                var users = await _authService.GetAllUsersAsync();
                var lecturers = users.Where(u => u.Role == "Lecturer" || u.Role == "Admin").ToList();
                ViewBag.Lecturers = lecturers;
            }

            return View(courses);
        }

        [HttpGet]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Create()
        {
            var users = await _authService.GetAllUsersAsync();
            var lecturers = users.Where(u => u.Role == "Lecturer" || u.Role == "Admin").ToList();
            ViewBag.Lecturers = lecturers;
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Create(CourseDto request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                ModelState.AddModelError(string.Empty, "Lỗi định danh người dùng.");
                return View(request);
            }

            try
            {
                await _courseService.CreateCourseAsync(request, userId);
                TempData["SuccessMessage"] = $"Tạo môn học {request.Code} thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Có lỗi xảy ra: {ex.Message}");
                return View(request);
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignLeader(Guid courseId, Guid subjectLeaderId)
        {
            try
            {
                await _courseService.UpdateSubjectLeaderAsync(courseId, subjectLeaderId);
                TempData["SuccessMessage"] = "Đã phân công Trưởng bộ môn mới thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể phân công Trưởng bộ môn: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(CourseDto request)
        {
            try
            {
                await _courseService.UpdateCourseAsync(request);
                TempData["SuccessMessage"] = $"Cập nhật môn học {request.Code} thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể cập nhật môn học: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _courseService.DeleteCourseAsync(id);
                TempData["SuccessMessage"] = "Đã xóa môn học và toàn bộ tài liệu liên quan thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể xóa môn học: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
