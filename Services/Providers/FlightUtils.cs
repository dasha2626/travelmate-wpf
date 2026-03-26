using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TravelMate.Models;

namespace TravelMate.Services.Providers
{
    public static class FlightUtils
    {
        private static Random rnd = new Random();

        public static void AssignFakePriceAndDuration(List<Flight> flights)
        {
            foreach (var f in flights)
            {
                f.Price = rnd.Next(100, 1000);
                f.DurationHours = rnd.Next(1, 5);
            }
        }

        public static void CalculateScore(List<Flight> flights)
        {
            if (flights.Count == 0) return;

            double maxPrice = flights.Max(f => f.Price);
            double minPrice = flights.Min(f => f.Price);
            double maxDuration = flights.Max(f => f.DurationHours);
            double minDuration = flights.Min(f => f.DurationHours);

            foreach (var f in flights)
            {
                double priceScore = 1 - (f.Price - minPrice) / (maxPrice - minPrice + 0.01);
                double durationScore = 1 - (f.DurationHours - minDuration) / (maxDuration - minDuration + 0.01);

                f.Score = 0.7 * priceScore + 0.3 * durationScore;
            }
        }
    }
}
