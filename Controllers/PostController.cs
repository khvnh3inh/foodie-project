using System;
using System.Net;
using System.Threading;
using System.Security.Claims;
using System.Web.Http;
using FoodieProject.Helpers;
using FoodieProject.Models;

namespace FoodieProject.Controllers
{

    /// <summary>
    /// GET  /api/posts/feed?type=foryou|following|recipe|review  – Feed trang chủ
    /// GET  /api/posts/user/{id}                                  – Bài của user X
    /// GET  /api/posts/{id}                                       – Chi tiết 1 bài
    /// POST /api/posts                                            – Đăng bài mới
    /// POST /api/posts/{id}/like                                  – Toggle like
    /// POST /api/posts/{id}/save                                  – Toggle save
    /// GET  /api/posts/{id}/comments                              – Lấy bình luận
    /// POST /api/posts/{id}/comments                              – Thêm bình luận
    /// GET  /api/posts/saved                                      – Bài đã lưu (cần JWT)
    /// </summary>
    [RoutePrefix("api/posts")]
    public class PostsController : ApiController
    {
        // ── GET /api/posts/feed ───────────────────────────────────────
        [HttpGet]
        [Route("feed")]
        [AllowAnonymous]
        public IHttpActionResult GetFeed(string type = "foryou", int limit = 20)
        {
            int currentUserId = TryGetUserId();

            // "following" yêu cầu đăng nhập
            if (type == "following" && currentUserId <= 0)
                return Content(HttpStatusCode.Unauthorized,
                    new { error = "Vui lòng đăng nhập để xem bài từ người đang theo dõi." });

            try
            {
                var posts = DatabaseHelper.GetFeedPosts(currentUserId, type, Math.Min(limit, 50));
                return Ok(new { success = true, data = posts, count = posts.Count });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ── GET /api/posts/saved ──────────────────────────────────────
        [HttpGet]
        [Route("saved")]
        [Authorize]
        public IHttpActionResult GetSaved()
        {
            try
            {
                int userId = GetCurrentUserId();
                var posts = DatabaseHelper.GetSavedPosts(userId);
                return Ok(new { success = true, data = posts, count = posts.Count });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        // ── GET /api/posts/user/{id} ──────────────────────────────────
        [HttpGet]
        [Route("user/{id:int}")]
        [AllowAnonymous]
        public IHttpActionResult GetUserPosts(int id)
        {
            int currentUserId = TryGetUserId();
            try
            {
                var posts = DatabaseHelper.GetPostsByUser(id, currentUserId);
                return Ok(new { success = true, data = posts, count = posts.Count });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ── GET /api/posts/{id} ───────────────────────────────────────
        [HttpGet]
        [Route("{id:int}")]
        [AllowAnonymous]
        public IHttpActionResult GetPost(int id)
        {
            int currentUserId = TryGetUserId();
            var post = DatabaseHelper.GetPostById(id, currentUserId);

            if (post == null)
                return Content(HttpStatusCode.NotFound, new { error = "Bài viết không tồn tại." });

            return Ok(new { success = true, data = post });
        }

        // ── POST /api/posts ───────────────────────────────────────────
        [HttpPost]
        [Route("")]
        [Authorize]
        public IHttpActionResult CreatePost([FromBody] CreatePostRequest req)
        {
            if (req == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            // Chỉ cần có content HOẶC title – không bắt buộc ảnh
            if (string.IsNullOrWhiteSpace(req.Content) && string.IsNullOrWhiteSpace(req.Title))
                return BadRequest("Vui lòng nhập nội dung hoặc tiêu đề bài viết.");

            // Validate PostType
            var validTypes = new[] { "recipe", "review", "photo", "video", "location" };
            if (string.IsNullOrEmpty(req.PostType) || Array.IndexOf(validTypes, req.PostType) < 0)
                req.PostType = "photo";

            try
            {
                int userId = GetCurrentUserId();
                int postId = DatabaseHelper.CreatePost(userId, req);
                var post = DatabaseHelper.GetPostById(postId, userId);

                return Content(HttpStatusCode.Created, new { success = true, data = post });
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

        // ── POST /api/posts/{id}/like ─────────────────────────────────
        [HttpPost]
        [Route("{id:int}/like")]
        [Authorize]
        public IHttpActionResult ToggleLike(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool isLiked = DatabaseHelper.ToggleLike(id, userId);
                var post = DatabaseHelper.GetPostById(id, userId);

                return Ok(new
                {
                    success = true,
                    isLiked = isLiked,
                    likesCount = post?.LikesCount ?? 0
                });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ── POST /api/posts/{id}/save ─────────────────────────────────
        [HttpPost]
        [Route("{id:int}/save")]
        [Authorize]
        public IHttpActionResult ToggleSave(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool isSaved = DatabaseHelper.ToggleSave(id, userId);
                var post = DatabaseHelper.GetPostById(id, userId);

                return Ok(new
                {
                    success = true,
                    isSaved = isSaved,
                    savesCount = post?.SavesCount ?? 0
                });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ── GET /api/posts/{id}/comments ──────────────────────────────
        [HttpGet]
        [Route("{id:int}/comments")]
        [AllowAnonymous]
        public IHttpActionResult GetComments(int id)
        {
            try
            {
                var comments = DatabaseHelper.GetComments(id);
                return Ok(new { success = true, data = comments, count = comments.Count });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ── POST /api/posts/{id}/comments ─────────────────────────────
        [HttpPost]
        [Route("{id:int}/comments")]
        [Authorize]
        public IHttpActionResult AddComment(int id, [FromBody] CreateCommentRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Content))
                return BadRequest("Nội dung bình luận không được để trống.");

            try
            {
                int userId = GetCurrentUserId();
                var comment = DatabaseHelper.AddComment(id, userId, req.Content.Trim());
                return Content(HttpStatusCode.Created, new { success = true, data = comment });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private int GetCurrentUserId()
        {
            return JwtHelper.GetUserIdFromPrincipal(
                Thread.CurrentPrincipal as ClaimsPrincipal
                ?? System.Web.HttpContext.Current?.User as ClaimsPrincipal
            );
        }

        private int TryGetUserId()
        {
            try { return GetCurrentUserId(); }
            catch { return 0; }
        }

        // ── POST /api/posts/{id}/images ───────────────────────────────
        [HttpPost]
        [Route("{id:int}/images")]
        [Authorize]
        public IHttpActionResult AddImage(int id, [FromBody] AddImageRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ImageUrl))
                return BadRequest("URL ảnh không hợp lệ.");
            try
            {
                int userId = GetCurrentUserId();
                var post = DatabaseHelper.GetPostById(id, userId);
                if (post == null) return NotFound();
                DatabaseHelper.AddPostImage(id, req.ImageUrl, req.DisplayOrder);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // POST /api/posts/{id}/images/upload
        [HttpPost]
        [Route("{id:int}/images/upload")]
        [Authorize]
        public IHttpActionResult UploadImage(int id, [FromBody] UploadImageRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Base64))
                return BadRequest("Không có dữ liệu ảnh.");
            try
            {
                int userId = GetCurrentUserId();
                var post = DatabaseHelper.GetPostById(id, userId);
                if (post == null) return NotFound();

                // Decode base64 → lưu file vào ~/Uploads/Posts/
                byte[] bytes = Convert.FromBase64String(req.Base64);
                string ext = (req.MimeType ?? "").Contains("png") ? ".png"
                           : (req.MimeType ?? "").Contains("gif") ? ".gif" : ".jpg";
                string fileName = $"post_{id}_{Guid.NewGuid().ToString("N")}{ext}";
                string folder = System.Web.HttpContext.Current.Server.MapPath("~/Uploads/Posts/");

                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);

                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, fileName), bytes);

                string imageUrl = "/Uploads/Posts/" + fileName;
                DatabaseHelper.AddPostImage(id, imageUrl, req.DisplayOrder);

                return Ok(new { success = true, imageUrl });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // DELETE /api/posts/{id}
        [HttpDelete]
        [Route("{id:int}")]
        [Authorize]
        public IHttpActionResult DeletePost(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                bool ok = DatabaseHelper.DeletePost(id, userId);
                if (!ok) return Content(HttpStatusCode.Forbidden,
                    new { error = "Bạn không có quyền xóa bài này." });
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // PUT /api/posts/{id}
        [HttpPut]
        [Route("{id:int}")]
        [Authorize]
        public IHttpActionResult UpdatePost(int id, [FromBody] UpdatePostRequest req)
        {
            if (req == null) return BadRequest("Dữ liệu không hợp lệ.");
            try
            {
                int userId = GetCurrentUserId();
                var post = DatabaseHelper.UpdatePost(id, userId, req);
                if (post == null) return Content(HttpStatusCode.Forbidden,
                    new { error = "Bạn không có quyền sửa bài này." });
                return Ok(new { success = true, data = post });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // GET /api/posts/search?q=...&type=...&limit=20
        [HttpGet]
        [Route("search")]
        [AllowAnonymous]
        public IHttpActionResult Search(string q = "", string type = null, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest("Vui lòng nhập từ khóa tìm kiếm.");
            try
            {
                int currentUserId = TryGetUserId();
                var posts = DatabaseHelper.SearchPosts(currentUserId, q.Trim(), type, Math.Min(limit, 50));
                return Ok(new { success = true, data = posts, count = posts.Count, query = q });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // POST /api/posts/{id}/repost
        [HttpPost]
        [Route("{id:int}/repost")]
        [Authorize]
        public IHttpActionResult ToggleRepost(int id, [FromBody] RepostRequest req)
        {
            try
            {
                int userId = GetCurrentUserId();
                string comment = req?.Comment;
                bool isReposted = DatabaseHelper.ToggleRepost(id, userId, comment);
                return Ok(new { success = true, isReposted });
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // GET /api/posts/reposts/user/{id}
        [HttpGet]
        [Route("reposts/user/{id:int}")]
        [AllowAnonymous]
        public IHttpActionResult GetUserReposts(int id)
        {
            int currentUserId = TryGetUserId();
            try
            {
                var posts = DatabaseHelper.GetUserReposts(id, currentUserId);
                return Ok(new { success = true, data = posts, count = posts.Count });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }   // ← đóng class PostsController
}       // ← đóng namespace