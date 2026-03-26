using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TravelMate.Models;

namespace TravelMate.Services.Providers
{
    public class FlightMetasearchService
    {
        private readonly List<IFlightProvider> _providers;

        public FlightMetasearchService(List<IFlightProvider> providers)
        {
            _providers = providers;
        }

        public async Task<List<Flight>> SearchAsync(string from, string to, DateTime? date)
        {
            var allFlights = new List<Flight>();
            foreach (var provider in _providers)
            {
                var flights = await provider.SearchFlightsAsync(from, to, date);
                allFlights.AddRange(flights);
            }
            return allFlights;
        }
    }
}
