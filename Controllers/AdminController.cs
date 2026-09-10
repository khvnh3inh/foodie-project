using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Web.Http;
using FoodieProject.Helpers;
using FoodieProject.Models;

namespace FoodieProject.Controllers
{
    [RoutePrefix("api/admin")]

    [Authorize] // Bắt buộc phải cấu hình JWT Token hợp lệ
    public class AdminController : ApiController
    {
        // Hàm trợ giúp kiểm tra quyền hạn nội bộ
        private void ValidateAdminRole()
        {
            var principal = Thread.CurrentPrincipal as ClaimsPrincipal
                         ?? System.Web.HttpContext.Current?.User as ClaimsPrincipal;
            if (principal == null) throw new UnauthorizedAccessException();

            int userId = JwtHelper.GetUserIdFromPrincipal(principal);
            if (!DatabaseHelper.IsAdmin(userId))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(
                    HttpStatusCode.Forbidden, "Yêu cầu quyền Quản trị viên để thực hiện thao tác này."));
            }
        }

        // ═══════════════════════════════════════════
        // USE CASE 1: QUẢN LÝ NHÀ HÀNG (RESTAURANTS)
        // ═══════════════════════════════════════════B
        [HttpPost]
        [Route("restaurants")]
        public IHttpActionResult AddRestaurant([FromBody] Restaurant restaurant)
        {
            ValidateAdminRole();
            if (restaurant == null || string.IsNullOrWhiteSpace(restaurant.Name))
                return BadRequest("Tên địa điểm không được để trống.");

            try
            {
                DatabaseHelper.AdminAddRestaurant(restaurant);
                return Ok(new { success = true, message = "Thêm địa điểm ăn uống thành công!" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        [HttpGet]
        [Route("restaurants")]
        public IHttpActionResult GetRestaurants()
        {
            ValidateAdminRole();
            try
            {
                var list = DatabaseHelper.GetAllRestaurants();
                return Ok(new { success = true, data = list });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPut]
        [Route("restaurants/{id:int}")]
        public IHttpActionResult UpdateRestaurant(int id, [FromBody] Restaurant restaurant)
        {
            ValidateAdminRole();
            if (restaurant == null) return BadRequest("Dữ liệu cập nhật không hợp lệ.");

            try
            {
                DatabaseHelper.AdminUpdateRestaurant(id, restaurant);
                return Ok(new { success = true, message = "Cập nhật thông tin địa điểm thành công!" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpDelete]
        [Route("restaurants/{id:int}")]
        public IHttpActionResult DeleteRestaurant(int id)
        {
            ValidateAdminRole();
            try
            {
                DatabaseHelper.AdminDeleteRestaurant(id);
                return Ok(new { success = true, message = "Đã xóa địa điểm khỏi hệ thống." });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ═══════════════════════════════════════════
        // USE CASE 2: KIỂM DUYỆT BÀI VIẾT & BÌNH LUẬN
        // ═══════════════════════════════════════════

        [HttpDelete]
        [Route("posts/{id:int}")]
        public IHttpActionResult DeletePost(int id)
        {
            ValidateAdminRole();
            try
            {
                DatabaseHelper.AdminDeletePost(id);
                return Ok(new { success = true, message = "Đã gỡ bỏ bài viết vi phạm tiêu chuẩn cộng đồng." });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpDelete]
        [Route("comments/{id:int}")]
        public IHttpActionResult DeleteComment(int id)
        {
            ValidateAdminRole();
            try
            {
                DatabaseHelper.AdminDeleteComment(id);
                return Ok(new { success = true, message = "Đã xóa bình luận vi phạm." });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ═══════════════════════════════════════════
        // USE CASE 3: QUẢN LÝ NGƯỜI DÙNG (USERS)
        // ═══════════════════════════════════════════

        [HttpGet]
        [Route("users")]
        public IHttpActionResult GetAllUsers()
        {
            ValidateAdminRole();
            try
            {
                var users = DatabaseHelper.AdminGetAllUsers();
                return Ok(new { success = true, data = users });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpDelete]
        [Route("users/{id:int}")]
        public IHttpActionResult BanUser(int id)
        {
            ValidateAdminRole();
            try
            {
                DatabaseHelper.AdminDeleteUser(id);
                return Ok(new { success = true, message = "Đã khóa và xóa toàn bộ dữ liệu của tài khoản này." });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // GET /api/admin/stats
        [HttpGet]
        [Route("stats")]
        public IHttpActionResult GetStats()
        {
            ValidateAdminRole();
            try
            {
                var stats = DatabaseHelper.AdminGetStats();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // GET /api/admin/posts
        [HttpGet]
        [Route("posts")]
        public IHttpActionResult GetAllPosts(int page = 1, int limit = 20)
        {
            ValidateAdminRole();
            try
            {
                int currentUserId = 0;
                try
                {
                    currentUserId = JwtHelper.GetUserIdFromPrincipal(
                    Thread.CurrentPrincipal as ClaimsPrincipal);
                }
                catch { }
                var posts = DatabaseHelper.GetFeedPosts(currentUserId, "foryou", limit);
                return Ok(new { success = true, data = posts });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}