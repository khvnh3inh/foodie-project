using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using FoodieProject.Models;
using Newtonsoft.Json;

namespace FoodieProject.Helpers
{
    public static class DatabaseHelper
    {
        private static readonly string ConnectionString =
            ConfigurationManager.ConnectionStrings["FoodieDB"].ConnectionString;

        private static SqlConnection GetConnection()
        {
            var conn = new SqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        // =============================================
        //  USER
        // =============================================

        public static User GetUserByUsername(string username)
        {
            const string sql = @"
                SELECT UserId, Username, PasswordHash, Email, FullName, AvatarUrl, Bio, Location,
                       CreatedAt, ISNULL(Role,'user') AS Role
                FROM Users WHERE Username = @Username";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);
                using (var r = cmd.ExecuteReader())
                    if (r.Read()) return MapUser(r);
            }
            return null;
        }

        public static User GetUserById(int userId)
        {
            const string sql = @"
                SELECT UserId, Username, PasswordHash, Email, FullName, AvatarUrl, Bio, Location, CreatedAt
                FROM Users WHERE UserId = @UserId";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using (var r = cmd.ExecuteReader())
                    if (r.Read()) return MapUser(r);
            }
            return null;
        }

        public static User GetUserByEmail(string email)
        {
            const string sql = @"
                SELECT UserId, Username, PasswordHash, Email, FullName, AvatarUrl, Bio, Location, CreatedAt
                FROM Users WHERE Email = @Email";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Email", email);
                using (var r = cmd.ExecuteReader())
                    if (r.Read()) return MapUser(r);
            }
            return null;
        }

        public static int CreateUser(string username, string passwordHash, string email, string fullName)
        {
            const string sql = @"
                INSERT INTO Users (Username, PasswordHash, Email, FullName, CreatedAt, UpdatedAt)
                VALUES (@Username, @PasswordHash, @Email, @FullName, GETUTCDATE(), GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                return (int)cmd.ExecuteScalar();
            }
        }

        public static UserProfileDto GetUserProfile(int userId)
        {
            const string sql = @"
                SELECT u.UserId, u.Username, u.Email, u.FullName, u.AvatarUrl, u.Bio, u.Location, u.CreatedAt,
                    (SELECT COUNT(*) FROM Follows WHERE FollowingId = u.UserId) AS FollowersCount,
                    (SELECT COUNT(*) FROM Follows WHERE FollowerId  = u.UserId) AS FollowingCount,
                    (SELECT COUNT(*) FROM Posts   WHERE UserId      = u.UserId) AS PostsCount
                FROM Users u WHERE u.UserId = @UserId";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                        return new UserProfileDto
                        {
                            UserId = r.GetInt32(r.GetOrdinal("UserId")),
                            Username = r.GetString(r.GetOrdinal("Username")),
                            Email = r.GetString(r.GetOrdinal("Email")),
                            FullName = r.GetString(r.GetOrdinal("FullName")),
                            AvatarUrl = r.IsDBNull(r.GetOrdinal("AvatarUrl")) ? null : r.GetString(r.GetOrdinal("AvatarUrl")),
                            Bio = r.IsDBNull(r.GetOrdinal("Bio")) ? null : r.GetString(r.GetOrdinal("Bio")),
                            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
                            FollowersCount = r.GetInt32(r.GetOrdinal("FollowersCount")),
                            FollowingCount = r.GetInt32(r.GetOrdinal("FollowingCount")),
                            PostsCount = r.GetInt32(r.GetOrdinal("PostsCount"))
                        };
                }
            }
            return null;
        }

        // =============================================
        //  FOLLOW
        // =============================================

        public static bool IsFollowing(int followerId, int followingId)
        {
            const string sql = "SELECT COUNT(1) FROM Follows WHERE FollowerId=@A AND FollowingId=@B";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@A", followerId);
                cmd.Parameters.AddWithValue("@B", followingId);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public static bool FollowUser(int followerId, int followingId)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM Follows WHERE FollowerId=@A AND FollowingId=@B)
                BEGIN
                    INSERT INTO Follows (FollowerId, FollowingId) VALUES (@A, @B);
                    SELECT 1;
                END ELSE SELECT 0;";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@A", followerId);
                cmd.Parameters.AddWithValue("@B", followingId);
                return (int)cmd.ExecuteScalar() == 1;
            }
        }

        public static bool UnfollowUser(int followerId, int followingId)
        {
            const string sql = "DELETE FROM Follows WHERE FollowerId=@A AND FollowingId=@B";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@A", followerId);
                cmd.Parameters.AddWithValue("@B", followingId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // =============================================
        //  POSTS
        // =============================================

        public static List<PostDto> GetFeedPosts(int currentUserId, string feedType = "foryou", int limit = 20)
        {
            string sql;
            if (feedType == "following")
            {
                sql = @"
                    SELECT TOP (@Limit) p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                           p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                           p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                           u.Username, u.FullName, u.AvatarUrl
                    FROM Posts p
                    INNER JOIN Users u ON p.UserId = u.UserId
                    INNER JOIN Follows f ON f.FollowingId = p.UserId AND f.FollowerId = @CurrentUserId
                    ORDER BY p.CreatedAt DESC";
            }
            else
            {
                sql = @"
                    SELECT TOP (@Limit) p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                           p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                           p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                           u.Username, u.FullName, u.AvatarUrl
                    FROM Posts p
                    INNER JOIN Users u ON p.UserId = u.UserId
                    ORDER BY p.CreatedAt DESC";
            }

            var posts = new List<PostDto>();
            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId > 0 ? (object)currentUserId : DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) posts.Add(MapPostDto(r));
                }
                foreach (var post in posts)
                {
                    post.Images = GetPostImages(conn, int.Parse(post.Id));
                    if (currentUserId > 0)
                        FillInteractionState(conn, int.Parse(post.Id), currentUserId, post);
                }
            }
            return posts;
        }

        public static List<PostDto> GetPostsByUser(int userId, int currentUserId)
        {
            const string sql = @"
                SELECT p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                       p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                       p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                       u.Username, u.FullName, u.AvatarUrl
                FROM Posts p
                INNER JOIN Users u ON p.UserId = u.UserId
                WHERE p.UserId = @UserId
                ORDER BY p.CreatedAt DESC";

            var posts = new List<PostDto>();
            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) posts.Add(MapPostDto(r));
                }
                foreach (var post in posts)
                {
                    post.Images = GetPostImages(conn, int.Parse(post.Id));
                    if (currentUserId > 0)
                        FillInteractionState(conn, int.Parse(post.Id), currentUserId, post);
                }
            }
            return posts;
        }

        public static List<PostDto> GetTrendingPosts(int currentUserId, int limit = 12)
        {
            const string sql = @"
                SELECT TOP (@Limit) p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                       p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                       p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                       u.Username, u.FullName, u.AvatarUrl
                FROM Posts p
                INNER JOIN Users u ON p.UserId = u.UserId
                ORDER BY p.LikesCount DESC, p.CreatedAt DESC";

            var posts = new List<PostDto>();
            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) posts.Add(MapPostDto(r));
                }
                foreach (var post in posts)
                {
                    post.Images = GetPostImages(conn, int.Parse(post.Id));
                    if (currentUserId > 0)
                        FillInteractionState(conn, int.Parse(post.Id), currentUserId, post);
                }
            }
            return posts;
        }

        public static List<PostDto> GetSavedPosts(int userId)
        {
            const string sql = @"
                SELECT p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                       p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                       p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                       u.Username, u.FullName, u.AvatarUrl
                FROM Posts p
                INNER JOIN Users u  ON p.UserId  = u.UserId
                INNER JOIN Saves s  ON s.PostId  = p.PostId AND s.UserId = @UserId
                ORDER BY s.CreatedAt DESC";

            var posts = new List<PostDto>();
            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) posts.Add(MapPostDto(r));
                }
                foreach (var post in posts)
                {
                    post.Images = GetPostImages(conn, int.Parse(post.Id));
                    post.IsSaved = true;
                }
            }
            return posts;
        }

        public static List<PostDto> SearchPosts(int currentUserId, string query, string type = null, int limit = 20)
        {
            string sql = @"
                SELECT TOP (@Limit) p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                       p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                       p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                       u.Username, u.FullName, u.AvatarUrl
                FROM Posts p
                INNER JOIN Users u ON p.UserId = u.UserId
                WHERE (p.Title LIKE @Q OR p.Content LIKE @Q OR p.LocationName LIKE @Q OR u.Username LIKE @Q)";

            if (!string.IsNullOrEmpty(type))
                sql += " AND p.PostType = @Type";

            sql += " ORDER BY p.LikesCount DESC, p.CreatedAt DESC";

            var posts = new List<PostDto>();
            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    cmd.Parameters.AddWithValue("@Q", $"%{query}%");
                    if (!string.IsNullOrEmpty(type))
                        cmd.Parameters.AddWithValue("@Type", type);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) posts.Add(MapPostDto(r));
                }
                foreach (var post in posts)
                {
                    post.Images = GetPostImages(conn, int.Parse(post.Id));
                    if (currentUserId > 0)
                        FillInteractionState(conn, int.Parse(post.Id), currentUserId, post);
                }
            }
            return posts;
        }

        public static PostDto GetPostById(int postId, int currentUserId)
        {
            const string sql = @"
                SELECT p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                       p.RecipeData, p.LocationName, p.LocationAddress, p.RestaurantId,
                       p.LikesCount, p.CommentsCount, p.SavesCount, p.ViewsCount, p.CreatedAt,
                       u.Username, u.FullName, u.AvatarUrl
                FROM Posts p
                INNER JOIN Users u ON p.UserId = u.UserId
                WHERE p.PostId = @PostId";

            using (var conn = GetConnection())
            {
                PostDto post = null;
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PostId", postId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) post = MapPostDto(r);
                }
                if (post != null)
                {
                    post.Images = GetPostImages(conn, postId);
                    if (currentUserId > 0)
                        FillInteractionState(conn, postId, currentUserId, post);
                }
                return post;
            }
        }

        public static int CreatePost(int userId, CreatePostRequest req)
        {
            const string sql = @"
                INSERT INTO Posts (UserId, Content, Title, PostType, LocationName, LocationAddress, RecipeData, RestaurantId, CreatedAt, UpdatedAt)
                VALUES (@UserId, @Content, @Title, @PostType, @LocName, @LocAddr, @Recipe, @RestId, GETUTCDATE(), GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int postId;
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Content", (object)req.Content ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Title", (object)req.Title ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PostType", req.PostType ?? "photo");
                cmd.Parameters.AddWithValue("@LocName", (object)req.LocationName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LocAddr", (object)req.LocationAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Recipe", (object)req.RecipeData ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RestId", (object)req.RestaurantId ?? DBNull.Value);
                postId = (int)cmd.ExecuteScalar();
            }

            // Cập nhật PostsCount cho user
            UpdateUserPostsCount(userId);
            return postId;
        }

        public static void AddPostImage(int postId, string imageUrl, int displayOrder = 0)
        {
            const string sql = @"
                INSERT INTO PostImages (PostId, ImageUrl, DisplayOrder)
                VALUES (@PostId, @ImageUrl, @Order)";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@PostId", postId);
                cmd.Parameters.AddWithValue("@ImageUrl", imageUrl);
                cmd.Parameters.AddWithValue("@Order", displayOrder);
                cmd.ExecuteNonQuery(); // ← thêm dòng này
            }
        }
        // =============================================
        //  DELETE / UPDATE POST
        // =============================================

        public static bool DeletePost(int postId, int userId)
        {
            using (var conn = GetConnection())
            {
                // Kiểm tra quyền sở hữu
                const string check = "SELECT UserId FROM Posts WHERE PostId=@P";
                using (var cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    var owner = cmd.ExecuteScalar();
                    if (owner == null || (int)owner != userId) return false;
                }

                // Xóa Likes, Saves, Comments trước (tránh FK constraint)
                foreach (var sql in new[] {
                    "DELETE FROM Likes    WHERE PostId=@P",
                    "DELETE FROM Saves    WHERE PostId=@P",
                    "DELETE FROM Comments WHERE PostId=@P",
                    "DELETE FROM PostImages WHERE PostId=@P"
                })
                {
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Xóa bài
                using (var cmd = new SqlCommand(
                    "DELETE FROM Posts WHERE PostId=@P AND UserId=@U", conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.Parameters.AddWithValue("@U", userId);
                    cmd.ExecuteNonQuery();
                }

                UpdateUserPostsCount(userId);
                return true;
            }
        }

        public static PostDto UpdatePost(int postId, int userId, UpdatePostRequest req)
        {
            using (var conn = GetConnection())
            {
                const string check = "SELECT UserId FROM Posts WHERE PostId=@P";
                using (var cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    var owner = cmd.ExecuteScalar();
                    if (owner == null || (int)owner != userId) return null;
                }

                const string sql = @"
                    UPDATE Posts SET
                        Title           = @Title,
                        Content         = @Content,
                        RecipeData      = @Recipe,
                        LocationName    = @LocName,
                        LocationAddress = @LocAddr,
                        UpdatedAt       = GETUTCDATE()
                    WHERE PostId = @PostId AND UserId = @UserId";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", (object)req.Title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Content", (object)req.Content ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Recipe", (object)req.RecipeData ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@LocName", (object)req.LocationName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@LocAddr", (object)req.LocationAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PostId", postId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.ExecuteNonQuery();
                }

                return GetPostById(postId, userId);
            }
        }
        
        // =============================================
        //  LIKE / SAVE / COMMENT  (dùng đúng bảng)
        // =============================================

        public static bool ToggleLike(int postId, int userId)
        {
            using (var conn = GetConnection())
            {
                const string check = "SELECT COUNT(1) FROM Likes WHERE PostId=@P AND UserId=@U";
                bool exists;
                using (var cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.Parameters.AddWithValue("@U", userId);
                    exists = (int)cmd.ExecuteScalar() > 0;
                }

                if (exists)
                {
                    using (var cmd = new SqlCommand("DELETE FROM Likes WHERE PostId=@P AND UserId=@U", conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.Parameters.AddWithValue("@U", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var cmd = new SqlCommand("INSERT INTO Likes (PostId,UserId) VALUES (@P,@U)", conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.Parameters.AddWithValue("@U", userId);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Cập nhật LikesCount
                using (var cmd = new SqlCommand(
                    "UPDATE Posts SET LikesCount=(SELECT COUNT(*) FROM Likes WHERE PostId=@P) WHERE PostId=@P", conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.ExecuteNonQuery();
                }

                return !exists; // true = đã like, false = đã unlike
            }
        }

        public static bool ToggleSave(int postId, int userId)
        {
            using (var conn = GetConnection())
            {
                const string check = "SELECT COUNT(1) FROM Saves WHERE PostId=@P AND UserId=@U";
                bool exists;
                using (var cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.Parameters.AddWithValue("@U", userId);
                    exists = (int)cmd.ExecuteScalar() > 0;
                }

                if (exists)
                {
                    using (var cmd = new SqlCommand("DELETE FROM Saves WHERE PostId=@P AND UserId=@U", conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.Parameters.AddWithValue("@U", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var cmd = new SqlCommand("INSERT INTO Saves (PostId,UserId) VALUES (@P,@U)", conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.Parameters.AddWithValue("@U", userId);
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = new SqlCommand(
                    "UPDATE Posts SET SavesCount=(SELECT COUNT(*) FROM Saves WHERE PostId=@P) WHERE PostId=@P", conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.ExecuteNonQuery();
                }

                return !exists;
            }
        }
        // ── REPOST ────────────────────────────────────────────────
        public static bool ToggleRepost(int postId, int userId, string comment = null)
        {
            using (var conn = GetConnection())
            {
                // Kiểm tra đã repost chưa
                const string check = "SELECT COUNT(1) FROM Reposts WHERE UserId=@U AND PostId=@P";
                using (var cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@U", userId);
                    cmd.Parameters.AddWithValue("@P", postId);
                    bool already = (int)cmd.ExecuteScalar() > 0;

                    if (already)
                    {
                        // Bỏ repost
                        using (var del = new SqlCommand(
                            "DELETE FROM Reposts WHERE UserId=@U AND PostId=@P", conn))
                        {
                            del.Parameters.AddWithValue("@U", userId);
                            del.Parameters.AddWithValue("@P", postId);
                            del.ExecuteNonQuery();
                        }
                        // Giảm count
                        using (var upd = new SqlCommand(
                            "UPDATE Posts SET RepostsCount = CASE WHEN RepostsCount>0 THEN RepostsCount-1 ELSE 0 END WHERE PostId=@P;" +
                            "UPDATE Users SET RepostsCount = CASE WHEN RepostsCount>0 THEN RepostsCount-1 ELSE 0 END WHERE UserId=@U", conn))
                        {
                            upd.Parameters.AddWithValue("@P", postId);
                            upd.Parameters.AddWithValue("@U", userId);
                            upd.ExecuteNonQuery();
                        }
                        return false; // đã bỏ repost
                    }
                    else
                    {
                        // Thêm repost
                        using (var ins = new SqlCommand(
                            "INSERT INTO Reposts (UserId, PostId, Comment) VALUES (@U, @P, @C)", conn))
                        {
                            ins.Parameters.AddWithValue("@U", userId);
                            ins.Parameters.AddWithValue("@P", postId);
                            ins.Parameters.AddWithValue("@C", (object)comment ?? DBNull.Value);
                            ins.ExecuteNonQuery();
                        }
                        // Tăng count
                        using (var upd = new SqlCommand(
                            "UPDATE Posts SET RepostsCount = RepostsCount+1 WHERE PostId=@P;" +
                            "UPDATE Users SET RepostsCount = RepostsCount+1 WHERE UserId=@U", conn))
                        {
                            upd.Parameters.AddWithValue("@P", postId);
                            upd.Parameters.AddWithValue("@U", userId);
                            upd.ExecuteNonQuery();
                        }
                        return true; // đã repost
                    }
                }
            }
        }

        public static List<PostDto> GetUserReposts(int userId, int currentUserId)
        {
            using (var conn = GetConnection())
            {
                const string sql = @"
            SELECT p.PostId, p.UserId, p.PostType, p.Title, p.Content,
                   p.RecipeData, p.LocationName, p.LocationAddress,
                   p.LikesCount, p.CommentsCount, p.SavesCount,
                   p.CreatedAt,
                   u.Username, u.FullName, u.AvatarUrl,
                   r.CreatedAt AS RepostedAt, r.Comment AS RepostComment,
                   CASE WHEN l.UserId IS NOT NULL THEN 1 ELSE 0 END AS IsLiked,
                   CASE WHEN s.UserId IS NOT NULL THEN 1 ELSE 0 END AS IsSaved,
                   CASE WHEN rp.UserId IS NOT NULL THEN 1 ELSE 0 END AS IsReposted
            FROM Reposts r
            JOIN Posts p ON p.PostId = r.PostId
            JOIN Users u ON u.UserId = p.UserId
            LEFT JOIN Likes l ON l.PostId = p.PostId AND l.UserId = @Current
            LEFT JOIN Saves s ON s.PostId = p.PostId AND s.UserId = @Current
            LEFT JOIN Reposts rp ON rp.PostId = p.PostId AND rp.UserId = @Current
            WHERE r.UserId = @UserId
            ORDER BY r.CreatedAt DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Current", currentUserId);

                    var list = new List<PostDto>();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var post = MapPostDto(rd);
                            // Đánh dấu là bài repost
                            if (post.Author != null)
                            {
                                // Thêm info repost vào content nếu có caption
                                var repostComment = rd["RepostComment"] as string;
                                if (!string.IsNullOrEmpty(repostComment))
                                    post.Content = "[Đăng lại] " + repostComment + (post.Content != null ? "\n" + post.Content : "");
                            }
                            post.IsLiked = Convert.ToInt32(rd["IsLiked"]) == 1;
                            post.IsSaved = Convert.ToInt32(rd["IsSaved"]) == 1;
                            list.Add(post);
                        }
                    }
                    // Load ảnh
                    foreach (var p in list)
                    {
                        if (int.TryParse(p.Id, out int pid))
                            p.Images = GetPostImages(conn, pid);
                    }
                    return list;
                }
            }
        }
        public static List<CommentDto> GetComments(int postId)
        {
            const string sql = @"
                SELECT c.CommentId, c.Content, c.CreatedAt, c.UserId,
                       u.FullName, u.Username, u.AvatarUrl
                FROM Comments c
                INNER JOIN Users u ON c.UserId = u.UserId
                WHERE c.PostId = @PostId AND c.ParentId IS NULL
                ORDER BY c.CreatedAt DESC";

            var list = new List<CommentDto>();
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@PostId", postId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new CommentDto
                        {
                            Id = r.GetInt32(r.GetOrdinal("CommentId")),
                            Content = r.GetString(r.GetOrdinal("Content")),
                            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
                            LikesCount = 0,
                            Author = new PostAuthorDto
                            {
                                UserId = r.GetInt32(r.GetOrdinal("UserId")),
                                Name = r.GetString(r.GetOrdinal("FullName")),
                                Username = r.GetString(r.GetOrdinal("Username")),
                                AvatarUrl = r.IsDBNull(r.GetOrdinal("AvatarUrl")) ? null : r.GetString(r.GetOrdinal("AvatarUrl"))
                            }
                        });
                    }
                }
            }
            return list;
        }

        public static CommentDto AddComment(int postId, int userId, string content)
        {
            const string sql = @"
                INSERT INTO Comments (PostId, UserId, Content, CreatedAt)
                VALUES (@P, @U, @C, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int commentId;
            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.Parameters.AddWithValue("@U", userId);
                    cmd.Parameters.AddWithValue("@C", content);
                    commentId = (int)cmd.ExecuteScalar();
                }
                // Cập nhật CommentsCount
                using (var cmd = new SqlCommand(
                    "UPDATE Posts SET CommentsCount=(SELECT COUNT(*) FROM Comments WHERE PostId=@P) WHERE PostId=@P", conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    cmd.ExecuteNonQuery();
                }
            }

            var user = GetUserById(userId);
            return new CommentDto
            {
                Id = commentId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                LikesCount = 0,
                Author = new PostAuthorDto
                {
                    UserId = userId,
                    Name = user?.FullName ?? "Ẩn danh",
                    Username = user?.Username ?? "user",
                    AvatarUrl = user?.AvatarUrl
                }
            };
        }

        // =============================================
        //  RESTAURANTS
        // =============================================

        public static List<Restaurant> GetAllRestaurants()
        {
            const string sql = "SELECT * FROM Restaurants ORDER BY AvgRating DESC";
            var list = new List<Restaurant>();
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) list.Add(MapRestaurant(r));
            return list;
        }

        public static List<Restaurant> GetNearbyRestaurants(double lat, double lng, double radiusKm = 5)
        {
            const string sql2 = @"
                SELECT * FROM (
                    SELECT *,
                        (6371 * ACOS(COS(RADIANS(@Lat)) * COS(RADIANS(Latitude))
                        * COS(RADIANS(Longitude) - RADIANS(@Lng))
                        + SIN(RADIANS(@Lat)) * SIN(RADIANS(Latitude)))) AS Distance
                    FROM Restaurants
                    WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL
                ) AS sub
                WHERE Distance <= @Radius
                ORDER BY Distance ASC";

            var list = new List<Restaurant>();
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql2, conn))
            {
                cmd.Parameters.AddWithValue("@Lat", lat);
                cmd.Parameters.AddWithValue("@Lng", lng);
                cmd.Parameters.AddWithValue("@Radius", radiusKm);
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapRestaurant(r));
            }
            return list;
        }

        public static void AddReview(int userId, ReviewRequest req)
        {

            double overall = (req.RatingTaste + req.RatingPrice + req.RatingSpace + req.RatingService) / 4.0;

            using (var conn = GetConnection())
            {
                const string sql = @"
                    INSERT INTO Reviews (UserId, RestaurantId, RatingTaste, RatingPrice, RatingSpace, RatingService, RatingOverall, Content)
                    VALUES (@U, @R, @T, @P, @Sp, @Sv, @O, @C)";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@U", userId);
                    cmd.Parameters.AddWithValue("@R", req.RestaurantId);
                    cmd.Parameters.AddWithValue("@T", req.RatingTaste);
                    cmd.Parameters.AddWithValue("@P", req.RatingPrice);
                    cmd.Parameters.AddWithValue("@Sp", req.RatingSpace);
                    cmd.Parameters.AddWithValue("@Sv", req.RatingService);
                    cmd.Parameters.AddWithValue("@O", overall);
                    cmd.Parameters.AddWithValue("@C", (object)req.Content ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                const string update = @"
                    UPDATE Restaurants SET
                        ReviewCount = (SELECT COUNT(*) FROM Reviews WHERE RestaurantId=@R),
                        AvgRating   = (SELECT AVG(RatingOverall) FROM Reviews WHERE RestaurantId=@R)
                    WHERE RestaurantId = @R";
                using (var cmd = new SqlCommand(update, conn))
                {
                    cmd.Parameters.AddWithValue("@R", req.RestaurantId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static int FindOrCreateRestaurant(string name, string address, string city, string priceRange)
        {
            using (var conn = GetConnection())
            {
                // Tìm theo tên + địa chỉ chính xác
                const string find = @"
            SELECT TOP 1 RestaurantId FROM Restaurants
            WHERE Name = @Name
              AND (@Address IS NULL OR Address = @Address)";

                using (var cmd = new SqlCommand(find, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Address", (object)address ?? DBNull.Value);
                    var found = cmd.ExecuteScalar();
                    if (found != null) return (int)found;
                }

                // Chưa có → tạo mới
                const string insert = @"
            INSERT INTO Restaurants (Name, Address, City, PriceRange, CreatedAt)
            VALUES (@Name, @Address, @City, @Price, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(insert, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Address", (object)address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@City", (object)city ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Price", (object)priceRange ?? DBNull.Value);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }
      
        // =============================================
        //  ADMIN USE CASES
        // =============================================

        // Kiểm tra quyền Admin dựa trên UserId mã hóa trong JWTB
        /// <summary>Kiểm tra userId có phải admin không.</summary>
        public static bool IsAdmin(int userId)
        {
            const string sql = "SELECT Role FROM Users WHERE UserId = @U";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@U", userId);
                var role = cmd.ExecuteScalar() as string;
                return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
            }
        }
        /// <summary>Lấy danh sách tất cả user (cho admin).</summary>
        public static List<AdminUserDto> AdminGetAllUsers()
        {
            const string sql = @"
                SELECT u.UserId, u.Username, u.FullName, u.Email,
                       ISNULL(u.Role,'user') AS Role, u.CreatedAt,
                       (SELECT COUNT(*) FROM Posts WHERE UserId = u.UserId) AS PostsCount
                FROM Users u
                ORDER BY u.CreatedAt DESC";

            var list = new List<AdminUserDto>();
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new AdminUserDto
                    {
                        UserId = r.GetInt32(r.GetOrdinal("UserId")),
                        Username = r.GetString(r.GetOrdinal("Username")),
                        FullName = r.GetString(r.GetOrdinal("FullName")),
                        Email = r.GetString(r.GetOrdinal("Email")),
                        Role = r.GetString(r.GetOrdinal("Role")),
                        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
                        PostsCount = r.GetInt32(r.GetOrdinal("PostsCount"))
                    });
                }
            }
            return list;
        }
        /// <summary>Xóa user (admin) — xóa cả bài viết, likes, saves, comments.</summary>
        public static void AdminDeleteUser(int userId)
        {
            using (var conn = GetConnection())
            {
                // Xóa theo thứ tự tránh FK
                foreach (var sql in new[]
                {
                    "DELETE FROM Likes    WHERE UserId = @U",
                    "DELETE FROM Saves    WHERE UserId = @U",
                    "DELETE FROM Comments WHERE UserId = @U",
                    "DELETE FROM Follows  WHERE FollowerId = @U OR FollowingId = @U",
                    // Xóa likes/saves/comments của bài viết user này
                    "DELETE L FROM Likes    L INNER JOIN Posts P ON L.PostId = P.PostId WHERE P.UserId = @U",
                    "DELETE S FROM Saves    S INNER JOIN Posts P ON S.PostId = P.PostId WHERE P.UserId = @U",
                    "DELETE C FROM Comments C INNER JOIN Posts P ON C.PostId = P.PostId WHERE P.UserId = @U",
                    "DELETE I FROM PostImages I INNER JOIN Posts P ON I.PostId = P.PostId WHERE P.UserId = @U",
                    "DELETE FROM Posts WHERE UserId = @U",
                    "DELETE FROM Users WHERE UserId = @U"
                })
                {
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@U", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
        /// <summary>Xóa bất kỳ bài viết nào (không kiểm tra ownership).</summary>
        public static void AdminDeletePost(int postId)
        {
            using (var conn = GetConnection())
            {
                // Lấy userId để update PostsCount sau
                int ownerId = 0;
                using (var cmd = new SqlCommand("SELECT UserId FROM Posts WHERE PostId=@P", conn))
                {
                    cmd.Parameters.AddWithValue("@P", postId);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null) ownerId = (int)obj;
                }

                foreach (var sql in new[]
                {
                    "DELETE FROM Likes      WHERE PostId=@P",
                    "DELETE FROM Saves      WHERE PostId=@P",
                    "DELETE FROM Comments   WHERE PostId=@P",
                    "DELETE FROM PostImages WHERE PostId=@P",
                    "DELETE FROM Posts      WHERE PostId=@P"
                })
                {
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.ExecuteNonQuery();
                    }
                }

                if (ownerId > 0)
                    UpdateUserPostsCount(ownerId);
            }
        }
        /// <summary>Xóa bình luận bất kỳ (admin).</summary>
        public static void AdminDeleteComment(int commentId)
        {
            using (var conn = GetConnection())
            {
                // Lấy postId để cập nhật CommentsCount
                int postId = 0;
                using (var cmd = new SqlCommand("SELECT PostId FROM Comments WHERE CommentId=@C", conn))
                {
                    cmd.Parameters.AddWithValue("@C", commentId);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null) postId = (int)obj;
                }

                using (var cmd = new SqlCommand("DELETE FROM Comments WHERE CommentId=@C", conn))
                {
                    cmd.Parameters.AddWithValue("@C", commentId);
                    cmd.ExecuteNonQuery();
                }

                if (postId > 0)
                {
                    using (var cmd = new SqlCommand(
                        "UPDATE Posts SET CommentsCount=(SELECT COUNT(*) FROM Comments WHERE PostId=@P) WHERE PostId=@P", conn))
                    {
                        cmd.Parameters.AddWithValue("@P", postId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
        /// <summary>Thống kê tổng quan cho trang admin.</summary>
        public static AdminStatsDto AdminGetStats()
        {
            using (var conn = GetConnection())
            {
                const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM Users)    AS TotalUsers,
                (SELECT COUNT(*) FROM Posts)    AS TotalPosts,
                (SELECT COUNT(*) FROM Likes)    AS TotalLikes,
                (SELECT COUNT(*) FROM Comments) AS TotalComments,
                (SELECT COUNT(*) FROM Posts WHERE CreatedAt >= CAST(GETUTCDATE() AS DATE)) AS PostsToday,
                (SELECT COUNT(*) FROM Users WHERE CreatedAt >= DATEADD(day,-7,GETUTCDATE())) AS NewUsersThisWeek";
                using (var cmd = new SqlCommand(sql, conn))
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        return new AdminStatsDto
                        {
                            TotalUsers = Convert.ToInt32(rd["TotalUsers"]),
                            TotalPosts = Convert.ToInt32(rd["TotalPosts"]),
                            TotalLikes = Convert.ToInt64(rd["TotalLikes"]),
                            TotalComments = Convert.ToInt64(rd["TotalComments"]),
                            PostsToday = Convert.ToInt32(rd["PostsToday"]),
                            NewUsersThisWeek = Convert.ToInt32(rd["NewUsersThisWeek"])
                        };
                    }
                    return new AdminStatsDto();
                }
            }
        }
        /// <summary>Thêm nhà hàng mới (admin).</summary>
        public static void AdminAddRestaurant(Restaurant r)
        {
            const string sql = @"
                INSERT INTO Restaurants (Name, Address, City, PriceRange, AvgRating, ReviewCount, CreatedAt)
                VALUES (@Name, @Addr, @City, @Price, 0, 0, GETUTCDATE())";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Name", r.Name);
                cmd.Parameters.AddWithValue("@Addr", (object)r.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", (object)r.City ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Price", (object)r.PriceRange ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        /// <summary>Cập nhật thông tin nhà hàng (admin).</summary>
        public static void AdminUpdateRestaurant(int id, Restaurant r)
        {
            const string sql = @"
                UPDATE Restaurants SET
                    Name       = @Name,
                    Address    = @Addr,
                    City       = @City,
                    PriceRange = @Price
                WHERE RestaurantId = @Id";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Name", r.Name);
                cmd.Parameters.AddWithValue("@Addr", (object)r.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", (object)r.City ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Price", (object)r.PriceRange ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        /// <summary>Xóa nhà hàng (admin).</summary>
        public static void AdminDeleteRestaurant(int id)
        {
            const string sql = "DELETE FROM Restaurants WHERE RestaurantId = @Id";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
        // =============================================
        //  PRIVATE HELPERS
        // =============================================

        private static void UpdateUserPostsCount(int userId)
        {
            const string sql = "UPDATE Users SET PostsCount=(SELECT COUNT(*) FROM Posts WHERE UserId=@U) WHERE UserId=@U";
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@U", userId);
                cmd.ExecuteNonQuery();
            }
        }

        private static List<PostImageDto> GetPostImages(SqlConnection conn, int postId)
        {
            const string sql = "SELECT ImageUrl FROM PostImages WHERE PostId=@P ORDER BY DisplayOrder";
            var list = new List<PostImageDto>();
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@P", postId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new PostImageDto
                        {
                            Url = r.IsDBNull(0) ? null : r.GetString(0),
                            Emoji = "🍽️",
                            ColorClass = "img-warm"
                        });
                    }
                }
            }
            return list;
        }

        private static void FillInteractionState(SqlConnection conn, int postId, int userId, PostDto post)
        {
            if (userId <= 0) return;

            const string likeSql = "SELECT COUNT(1) FROM Likes WHERE PostId=@P AND UserId=@U";
            using (var cmd = new SqlCommand(likeSql, conn))
            {
                cmd.Parameters.AddWithValue("@P", postId);
                cmd.Parameters.AddWithValue("@U", userId);
                post.IsLiked = (int)cmd.ExecuteScalar() > 0;
            }

            const string saveSql = "SELECT COUNT(1) FROM Saves WHERE PostId=@P AND UserId=@U";
            using (var cmd = new SqlCommand(saveSql, conn))
            {
                cmd.Parameters.AddWithValue("@P", postId);
                cmd.Parameters.AddWithValue("@U", userId);
                post.IsSaved = (int)cmd.ExecuteScalar() > 0;
            }
        }

        private static PostDto MapPostDto(SqlDataReader r)
        {
            var postType = r.GetString(r.GetOrdinal("PostType"));
            var recipeDataRaw = r.IsDBNull(r.GetOrdinal("RecipeData")) ? null : r.GetString(r.GetOrdinal("RecipeData"));
            var locName = r.IsDBNull(r.GetOrdinal("LocationName")) ? null : r.GetString(r.GetOrdinal("LocationName"));
            var locAddr = r.IsDBNull(r.GetOrdinal("LocationAddress")) ? null : r.GetString(r.GetOrdinal("LocationAddress"));

            PostRecipeDto recipe = null;
            if (postType == "recipe" && !string.IsNullOrEmpty(recipeDataRaw))
            {
                try { recipe = JsonConvert.DeserializeObject<PostRecipeDto>(recipeDataRaw); }
                catch { /* ignore */ }
            }

            return new PostDto
            {
                Id = r.GetInt32(r.GetOrdinal("PostId")).ToString(),
                Type = postType,
                Title = r.IsDBNull(r.GetOrdinal("Title")) ? null : r.GetString(r.GetOrdinal("Title")),
                Content = r.IsDBNull(r.GetOrdinal("Content")) ? null : r.GetString(r.GetOrdinal("Content")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
                LikesCount = r.GetInt32(r.GetOrdinal("LikesCount")),
                CommentsCount = r.GetInt32(r.GetOrdinal("CommentsCount")),
                SavesCount = r.GetInt32(r.GetOrdinal("SavesCount")),
                Author = new PostAuthorDto
                {
                    UserId = r.GetInt32(r.GetOrdinal("UserId")),
                    Name = r.GetString(r.GetOrdinal("FullName")),
                    Username = r.GetString(r.GetOrdinal("Username")),
                    AvatarUrl = r.IsDBNull(r.GetOrdinal("AvatarUrl")) ? null : r.GetString(r.GetOrdinal("AvatarUrl"))
                },
                Location = string.IsNullOrEmpty(locName) ? null : new PostLocationDto { Name = locName, Address = locAddr },
                Recipe = recipe,
                Images = new List<PostImageDto>()
            };
        }

        private static User MapUser(SqlDataReader r)
        {
            return new User
            {
                UserId = r.GetInt32(r.GetOrdinal("UserId")),
                Username = r.GetString(r.GetOrdinal("Username")),
                PasswordHash = r.GetString(r.GetOrdinal("PasswordHash")),
                Email = r.GetString(r.GetOrdinal("Email")),
                FullName = r.GetString(r.GetOrdinal("FullName")),
                AvatarUrl = r.IsDBNull(r.GetOrdinal("AvatarUrl")) ? null : r.GetString(r.GetOrdinal("AvatarUrl")),
                Bio = r.IsDBNull(r.GetOrdinal("Bio")) ? null : r.GetString(r.GetOrdinal("Bio")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
                Role = r.IsDBNull(r.GetOrdinal("Role")) ? "user" : r.GetString(r.GetOrdinal("Role"))
            };
        }

        private static Restaurant MapRestaurant(SqlDataReader r)
        {
            return new Restaurant
            {
                RestaurantId = r.GetInt32(r.GetOrdinal("RestaurantId")),
                Name = r.GetString(r.GetOrdinal("Name")),
                Address = r.IsDBNull(r.GetOrdinal("Address")) ? null : r.GetString(r.GetOrdinal("Address")),
                City = r.IsDBNull(r.GetOrdinal("City")) ? null : r.GetString(r.GetOrdinal("City")),
                District = r.IsDBNull(r.GetOrdinal("District")) ? null : r.GetString(r.GetOrdinal("District")),
                Latitude = r.IsDBNull(r.GetOrdinal("Latitude")) ? (double?)null : r.GetDouble(r.GetOrdinal("Latitude")),
                Longitude = r.IsDBNull(r.GetOrdinal("Longitude")) ? (double?)null : r.GetDouble(r.GetOrdinal("Longitude")),
                Phone = r.IsDBNull(r.GetOrdinal("Phone")) ? null : r.GetString(r.GetOrdinal("Phone")),
                OpeningHours = r.IsDBNull(r.GetOrdinal("OpeningHours")) ? null : r.GetString(r.GetOrdinal("OpeningHours")),
                PriceRange = r.IsDBNull(r.GetOrdinal("PriceRange")) ? null : r.GetString(r.GetOrdinal("PriceRange")),
                AvgRating = r.GetDouble(r.GetOrdinal("AvgRating")),
                ReviewCount = r.GetInt32(r.GetOrdinal("ReviewCount")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
            };
        }
        public static void UpdateUserProfile(int userId, string fullName, string bio, string location)
        {
            using (var conn = GetConnection())
            {
                const string sql = @"
            UPDATE Users SET
                FullName  = @F,
                Bio       = @B,
                Location  = @L,
                UpdatedAt = GETUTCDATE()
            WHERE UserId = @U";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@F", (object)fullName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@B", (object)bio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@L", (object)location ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@U", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}