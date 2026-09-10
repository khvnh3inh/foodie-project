using System;

namespace FoodieProject.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int UserId { get; set; }
        public int RestaurantId { get; set; }
        public double RatingTaste { get; set; }
        public double RatingPrice { get; set; }
        public double RatingSpace { get; set; }
        public double RatingService { get; set; }
        public double RatingOverall { get; set; }
        public string Content { get; set; }
        public DateTime? VisitedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReviewRequest
    {
        public int RestaurantId { get; set; }
        public double RatingTaste { get; set; }
        public double RatingPrice { get; set; }
        public double RatingSpace { get; set; }
        public double RatingService { get; set; }
        public string Content { get; set; }
    }
    public class ReviewSubmitRequest
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string PriceRange { get; set; }
        public double RatingTaste { get; set; } = 5;
        public double RatingPrice { get; set; } = 5;
        public double RatingSpace { get; set; } = 5;
        public double RatingService { get; set; } = 5;
        public string Content { get; set; }
    }
}
