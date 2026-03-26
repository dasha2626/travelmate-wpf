using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TravelMate.Models;

namespace TravelMate.Services.Providers
{
    // Provider, który odczytuje loty z pliku JSON
    public class JsonFlightProvider : IFlightProvider
    {
        private readonly string _filePath;

        public JsonFlightProvider(string filePath = null)
        {
            // Jeśli nie podano ścieżki, używamy domyślnej
            _filePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loty.json");
        }

        // Wczytywanie wszystkich lotów asynchronicznie
        public async Task<List<Flight>> LoadFlightsAsync()
        {
            if (!File.Exists(_filePath))
                return new List<Flight>();

            FileStream stream = null;
            StreamReader reader = null;
            try
            {
                stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new StreamReader(stream);
                string json = await reader.ReadToEndAsync();

                var flights = JsonSerializer.Deserialize<List<Flight>>(json);
                return flights ?? new List<Flight>();
            }
            finally
            {
                if (reader != null) reader.Dispose();
                if (stream != null) stream.Dispose();
            }
        }

        // Wyszukiwanie lotów z filtrami
        public async Task<List<Flight>> SearchFlightsAsync(string from, string to, DateTime? date)
        {
            var flights = await LoadFlightsAsync();

            var filtered = flights.Where(f =>
                (string.IsNullOrEmpty(from) || f.From.Equals(from, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(to) || f.To.Equals(to, StringComparison.OrdinalIgnoreCase)) &&
                (!date.HasValue || DateTime.ParseExact(f.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture) == date.Value)
            ).ToList();

            return filtered;
        }
    }
}