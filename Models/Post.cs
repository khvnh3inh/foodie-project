using System;
using System.Collections.Generic;

namespace FoodieProject.Models
{
    // ─── Entity: Post ─────────────────────────────
    public class Post
    {
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public string PostType { get; set; }       // recipe | review | photo | video | location
        public string LocationName { get; set; }
        public string LocationAddress { get; set; }
        public string RecipeData { get; set; }      // JSON string
        public int? RestaurantId { get; set; }
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int SavesCount { get; set; }
        public int ViewsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ─── Entity: PostImage ────────────────────────
    public class PostImage
    {
        public int ImageId { get; set; }
        public int PostId { get; set; }
        public string ImageUrl { get; set; }
        public string Emoji { get; set; }
        public string ColorClass { get; set; }
        public int DisplayOrder { get; set; }
    }

    // ─── DTO: Trả về cho frontend (khớp M4 JS) ───
    public class PostDto
    {
        public string Id { get; set; }              // PostId dạng string cho JS
        public string Type { get; set; }            // recipe | review | photo ...
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        // Tác giả
        public PostAuthorDto Author { get; set; }

        // Hình ảnh
        public List<PostImageDto> Images { get; set; }

        // Vị trí (nếu có)
        public PostLocationDto Location { get; set; }

        // Recipe (nếu type == recipe)
        public PostRecipeDto Recipe { get; set; }

        // Review (nếu type == review)
        public PostReviewDto Review { get; set; }

        // Counts
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int SavesCount { get; set; }

        // Trạng thái của user hiện tại
        public bool IsLiked { get; set; }
        public bool IsSaved { get; set; }
    }

    public class PostAuthorDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string AvatarUrl { get; set; }
    }

    public class PostImageDto
    {
        public string Url { get; set; }
        public string Emoji { get; set; }
        public string ColorClass { get; set; }
    }

    public class PostLocationDto
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public class PostRecipeDto
    {
        public List<string> Ingredients { get; set; }
        public List<string> Steps { get; set; }
    }

    public class PostReviewDto
    {
        public double Rating { get; set; }
        public Dictionary<string, double> Categories { get; set; }
    }

    // ─── Request: Tạo bài viết mới ───────────────
    public class CreatePostRequest
    {
        public string Content { get; set; }
        public string Title { get; set; }
        public string PostType { get; set; }
        public string LocationName { get; set; }
        public string LocationAddress { get; set; }
        public string RecipeData { get; set; }      // JSON string
        public int? RestaurantId { get; set; }
    }

    // ─── DTO: Comment ─────────────────────────────
    public class CommentDto
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikesCount { get; set; }
        public PostAuthorDto Author { get; set; }
    }
    public class AddImageRequest
    {
        public string ImageUrl { get; set; }
        public int DisplayOrder { get; set; } = 0;
    }
    public class CreateCommentRequest
    {
        public string Content { get; set; }
    }
    public class UploadImageRequest
    {
        public string Base64 { get; set; }
        public string MimeType { get; set; }
        public string FileName { get; set; }
        public int DisplayOrder { get; set; } = 0;
    }
    public class UpdatePostRequest
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string RecipeData { get; set; }
        public string LocationName { get; set; }
        public string LocationAddress { get; set; }
    }
    public class RepostRequest
    {
        public string Comment { get; set; }
    }
}


