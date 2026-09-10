using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Web.Http;
using FoodieProject.Helpers;
using FoodieProject.Models;

namespace FoodieProject.Controllers
{
    /// <summary>
    /// API Users: profile, follow/unfollow.
    /// Yêu cầu JWT token.
    /// </summary>
    [RoutePrefix("api/users")]
    public class UserController : ApiController
    {
        // GET /api/users/profile
        [HttpGet]
        [Route("profile")]
        public IHttpActionResult GetMyProfile()
        {
            try
            {
                int userId = GetCurrentUserId();
                var profile = DatabaseHelper.GetUserProfile(userId);
                if (profile == null) return NotFound();
                return Ok(profile);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        // GET /api/users/profile/{id}
        [HttpGet]
        [Route("profile/{id:int}")]
        public IHttpActionResult GetUserProfile(int id)
        {
            var profile = DatabaseHelper.GetUserProfile(id);
            if (profile == null) return NotFound();

            bool isFollowing = false;
            try
            {
                int currentUserId = GetCurrentUserId();
                isFollowing = DatabaseHelper.IsFollowing(currentUserId, id);
            }
            catch { }

            return Ok(new
            {
                profile.UserId,
                profile.Username,
                profile.Email,
                profile.FullName,
                profile.AvatarUrl,
                profile.Bio,
                profile.CreatedAt,
                profile.FollowersCount,
                profile.FollowingCount,
                profile.PostsCount,
                IsFollowing = isFollowing
            });
        }

        // POST /api/users/follow/{id}
        [HttpPost]
        [Route("follow/{id:int}")]
        public IHttpActionResult ToggleFollow(int id)
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                if (currentUserId == id)
                    return BadRequest("Không thể tự follow chính mình.");

                var targetUser = DatabaseHelper.GetUserById(id);
                if (targetUser == null)
                    return Content(HttpStatusCode.NotFound,
                        new { error = "Người dùng không tồn tại." });

                bool alreadyFollowing = DatabaseHelper.IsFollowing(currentUserId, id);

                if (alreadyFollowing)
                {
                    DatabaseHelper.UnfollowUser(currentUserId, id);
                    return Ok(new FollowResponse
                    {
                        IsFollowing = false,
                        Message = $"Đã bỏ theo dõi {targetUser.Username}."
                    });
                }
                else
                {
                    DatabaseHelper.FollowUser(currentUserId, id);
                    return Ok(new FollowResponse
                    {
                        IsFollowing = true,
                        Message = $"Đã theo dõi {targetUser.Username}."
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }
        // PUT /api/users/profile
        [HttpPut]
        [Route("profile")]
        [Authorize]
        public IHttpActionResult UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            if (req == null) return BadRequest("Dữ liệu không hợp lệ.");
            try
            {
                int userId = GetCurrentUserId();
                DatabaseHelper.UpdateUserProfile(userId, req.FullName, req.Bio, req.Location);
                var profile = DatabaseHelper.GetUserProfile(userId);
                return Ok(profile);
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
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
