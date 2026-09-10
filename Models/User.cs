using System;

namespace FoodieProject.Models
{
    // ─── Entity: User ─────────────────────────────
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string AvatarUrl { get; set; }
        public string Bio { get; set; }
        public string Location { get; set; }
        public DateTime CreatedAt { get; set; }
        // ✅ THÊM MỚI: phân quyền ("user" hoặc "admin")
        public string Role { get; set; } = "user";
    }

    // ─── DTO: Profile (trả về cho frontend) ───────
    public class UserProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string AvatarUrl { get; set; }
        public string Bio { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public int PostsCount { get; set; }
    }

    // ─── Request: Đăng ký ─────────────────────────
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
    }

    // ─── Request: Đăng nhập ───────────────────────
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    // ─── Response: Auth (trả về sau login/register) ─
    public class AuthResponse
    {
        public string Token { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        // ✅ THÊM MỚI: frontend dùng để hiện menu admin
        public string Role { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string FullName { get; set; }
        public string Bio { get; set; }
        public string Location { get; set; }
    }

    // ─── DTO: Admin User List ─────────────────────
    public class AdminUserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PostsCount { get; set; }
    }

    // ─── DTO: Admin Stats ─────────────────────────
    public class AdminStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalPosts { get; set; }
        public long TotalLikes { get; set; }
        public long TotalComments { get; set; }
        public int PostsToday { get; set; }
        public int NewUsersThisWeek { get; set; }
    }

    // ─── DTO: Follow Response ─────────────────────
    
}