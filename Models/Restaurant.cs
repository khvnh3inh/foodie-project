using System;

namespace FoodieProject.Models
{
    public class Restaurant
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Phone { get; set; }
        public string OpeningHours { get; set; }
        public string PriceRange { get; set; }
        public double AvgRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
