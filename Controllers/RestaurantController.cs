using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Web.Http;
using FoodieProject.Helpers;
using FoodieProject.Models;

namespace FoodieProject.Controllers
{
    /// <summary>
    /// API Restaurants: danh sách, tìm gần, review.
    /// </summary>
    [RoutePrefix("api/restaurants")]
    public class RestaurantController : ApiController
    {
        // GET /api/restaurants
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var data = DatabaseHelper.GetAllRestaurants();
                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET /api/restaurants/nearby?lat=...&lng=...&radius=5
        [HttpGet]
        [Route("nearby")]
        public IHttpActionResult GetNearby(double lat, double lng, double radius = 5)
        {
            try
            {
                var data = DatabaseHelper.GetNearbyRestaurants(lat, lng, radius);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST /api/restaurants/review
        [HttpPost]
        [Route("review")]
        public IHttpActionResult PostReview([FromBody] ReviewRequest request)
        {
            if (request == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            try
            {
                int userId = GetCurrentUserId();
                DatabaseHelper.AddReview(userId, request);
                return Ok(new { message = "Đánh giá thành công!" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private int GetCurrentUserId()
        {
            var principal = Thread.CurrentPrincipal as ClaimsPrincipal
                         ?? System.Web.HttpContext.Current?.User as ClaimsPrincipal;
            if (principal == null)
                throw new UnauthorizedAccessException();
            return JwtHelper.GetUserIdFromPrincipal(principal);
        }
    }
}
