using System;
using System.Text.Json.Serialization;

namespace TravelMate.Models
{
    public class Flight
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Date { get; set; }  // np. "2026-03-10"
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double LatTo { get; set; }
        public double LngTo { get; set; }
        public string Airline { get; set; }

        // Pola do rankingu (nie są serializowane do JSON)
        [JsonIgnore]
        public double Price { get; set; }

        [JsonIgnore]
        public double DurationHours { get; set; }

        [JsonIgnore]
        public double Score { get; set; }
    }
}