using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<UserDto?> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null) return null;

            var isValid = _passwordHasher.Verify(request.Password, user.PasswordHash);
            if (!isValid) return null;

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            };
        }

        public async Task<UserDto> RegisterAsync(string username, string password, string role, string subscriptionTier, string fullName)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(username);
            if (existingUser != null)
            {
                throw new Exception("Tên tài khoản này đã tồn tại trong hệ thống!");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Role = role,
                SubscriptionTier = subscriptionTier,
                FullName = fullName.Trim()
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            };
        }

        public async Task<bool> UpgradeToPremiumAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.SubscriptionTier = "Premium";
            await _userRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleSubscriptionTierAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.SubscriptionTier = user.SubscriptionTier == "Premium" ? "Free" : "Premium";
            await _userRepository.SaveChangesAsync();
            return true;
        }

        public async Task<UserDto?> GetUserByUsernameAsync(string username)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null) return null;
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            };
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(user => new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            }).OrderBy(u => u.Role).ThenBy(u => u.Username);
        }

        public async Task DeleteUserAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài khoản người dùng cần xóa!");
            }

            // Bảo mật: Không cho phép tự xóa tài khoản Admin để tránh mất quyền quản trị
            if (user.Role == "Admin")
            {
                throw new InvalidOperationException("Không được phép xóa tài khoản quản trị hệ thống (Admin)!");
            }

            await _userRepository.DeleteAsync(user);
            await _userRepository.SaveChangesAsync();
        }
    }
}
