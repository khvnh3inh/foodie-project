using System;
using System.Net;
using System.Web.Http;
using FoodieProject.Helpers;
using FoodieProject.Models;

namespace FoodieProject.Controllers
{
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        // POST /api/auth/register
        [HttpPost]
        [Route("register")]
        public IHttpActionResult Register([FromBody] RegisterRequest request)
        {
            if (request == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest("Vui lòng điền đầy đủ: Username, Password, Email, FullName.");

            if (request.Username.Length < 3 || request.Username.Length > 50)
                return BadRequest("Username phải từ 3–50 ký tự.");

            if (request.Password.Length < 6)
                return BadRequest("Password phải từ 6 ký tự trở lên.");

            if (DatabaseHelper.GetUserByUsername(request.Username) != null)
                return Content(HttpStatusCode.Conflict, new { error = "Username đã tồn tại." });

            if (DatabaseHelper.GetUserByEmail(request.Email) != null)
                return Content(HttpStatusCode.Conflict, new { error = "Email đã được sử dụng." });

            string hash = PasswordHelper.HashPassword(request.Password);
            int userId = DatabaseHelper.CreateUser(request.Username, hash, request.Email, request.FullName);
            string token = JwtHelper.GenerateToken(userId, request.Username);

            return Created("/api/users/profile", new AuthResponse
            {
                Token = token,
                UserId = userId,
                Username = request.Username,
                FullName = request.FullName,
                Role = "user"           // ✅ Tài khoản mới luôn là "user"
            });
        }

        // POST /api/auth/login
        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Vui lòng nhập Username và Password.");

            var user = DatabaseHelper.GetUserByUsername(request.Username);
            if (user == null)
                return Content(HttpStatusCode.Unauthorized, new { error = "Tài khoản không tồn tại." });

            if (!PasswordHelper.VerifyPassword(request.Password, user.PasswordHash))
                return Content(HttpStatusCode.Unauthorized, new { error = "Mật khẩu không đúng." });

            string token = JwtHelper.GenerateToken(user.UserId, user.Username);
            bool isAdmin = DatabaseHelper.IsAdmin(user.UserId);

            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Role = isAdmin ? "admin" : "user"
            });
        }
    }
}