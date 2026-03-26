using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TravelMate.Models;

namespace TravelMate.Services.Providers
{
    public interface IFlightProvider
    {
        Task<List<Flight>> SearchFlightsAsync(string from, string to, DateTime? date);
    }
}
