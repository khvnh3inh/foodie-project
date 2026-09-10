using System;

namespace FoodieProject.Models
{
    public class Follow
    {
        public int FollowerId { get; set; }
        public int FollowingId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FollowResponse
    {
        public bool IsFollowing { get; set; }
        public string Message { get; set; }
    }
}
