using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using LiveCharts;
using LiveCharts.Wpf;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Device.Location;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static TravelMate.AddVisitPointWindow;
using System.Configuration;

namespace TravelMate
{
    public partial class TripDetailsWindow : Window
    {
        private long tripId;
        private int userId;
        private GMapMarker currentMarker;
        public SeriesCollection DetailedSeries { get; set; } = new SeriesCollection();

        private DateTime tripStartDate;
        private DateTime tripEndDate;

        public RouteDisplay editingRoute = null;

        private long startPointId = 0;
        private long endPointId = 0;
        private string selectedCurrency = "PLN"; 
        private Dictionary<string, double> rates = new Dictionary<string, double>();

        public bool isSelectingStartPoint = false;
        public bool isSelectingEndPoint = false;
        public VisitPointDisplay editingVisitPoint = null;
        public bool isSelectingVisitPoint = false;


        public bool isSelectingAccommodation = false;
        public AccommodationDisplay editingAccommodation;

        public MapWindow MapOwner { get; set; }

        public TripDetailsWindow(long idPodrozy, int userId)
        {
            InitializeComponent();
            this.Closed += TripDetailsWindow_Closed;

            tripId = idPodrozy;
            this.userId = userId;
           
           

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT tytul FROM podroze WHERE id_podrozy=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        this.Title = $"Szczegóły podróży: {result.ToString()}";
                    }
                }
            }

            LoadTripDates();
            LoadRoutes();
            LoadVisitPoints();
            LoadAccommodations();
            LoadExpenses();
            LoadDetailedExpenses();

        }

        // ogolne
        private void TripDetailsWindow_Closed(object sender, EventArgs e)
        {
            MapOwner?.ClearTemporaryRoutes();

            if (this.Owner != null)
            {
                this.Owner.Activate();
            }
        }


        private void UpdateAddressTextBox(string textBoxName, string address)
        {
            if (lstRoutes.ItemsSource is IEnumerable<RouteDisplay> routes)
            {
                foreach (var item in lstRoutes.Items)
                {
                    var container = lstRoutes.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container == null) continue;

                    var expander = container.FindAncestor<Expander>();
                    if (expander == null) continue;

                    var txtBox = expander.FindName(textBoxName) as TextBox;
                    if (txtBox != null && item == editingRoute)
                    {
                        txtBox.Text = address;
                        break;
                    }
                }
            }
        }


        private void LoadTripDates()
        {
           

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT data_od, data_do FROM podroze WHERE id_podrozy=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            tripStartDate = reader.GetDateTime(0);
                            tripEndDate = reader.GetDateTime(1);
                        }
                    }
                }
            }
        }



        private void UpdateVisitAddressTextBox(string address)
        {
            foreach (var item in lstVisits.Items)
            {
                var container = lstVisits.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (item == editingVisitPoint)
                {
                    var txt = container.FindName("txtVisitAddress") as TextBox;
                    if (txt != null) txt.Text = address;
                    break;
                }
            }
        }

        // koniec ogolnych 

        //generuj 
        public class Podroz
        {
            public int Id { get; set; }
            public string Tytul { get; set; }
            public string Miejsce { get; set; }
            public string DataOd { get; set; }
            public string DataDo { get; set; }
        }

        public class Nocleg
        {
            public string Nazwa { get; set; }
            public string Adres { get; set; }
            public string DataOd { get; set; }
            public string DataDo { get; set; }
        }

        public class Zwiedzanie
        {
            public string Nazwa { get; set; }
            public string Data { get; set; } // null jeśli brak daty
            public string Uwagi { get; set; }
        }

        public class Rekomendacja
        {
            public string Tytul { get; set; }
            public string Opis { get; set; }
        }


        private async Task<List<Rekomendacja>> WywolajGemini(string prompt)
        {
            var apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
            var url = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={apiKey}";

            using (var client = new HttpClient())
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(jsonResponse, "Błąd Gemini");
                    return new List<Rekomendacja>();
                }

                var doc = JsonDocument.Parse(jsonResponse);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l =>
                    {
                        var parts = l.Split(new[] { '–' }, 2);
                        return new Rekomendacja
                        {
                            Tytul = parts[0].Trim(),
                            Opis = parts.Length > 1 ? parts[1].Trim() : ""
                        };
                    })
                    .ToList();
            }
        }
        private async Task<List<Rekomendacja>> GenerujHarmonogramAI(Podroz podroz, List<Nocleg> noclegi, List<Zwiedzanie> zwiedzania)
        {
            string prompt = "Na podstawie poniższych danych wygeneruj harmonogram podróży dzienny z godzinami aktywności.\n" +
                            "Uwzględnij daty przyjazdów, noclegów i zaplanowane punkty zwiedzania.\n" +
                            "Jeżeli brak danych, zasugeruj miejsca w pobliżu noclegów.\n\n";

            prompt += $"Podróż: {podroz.Tytul} ({podroz.Miejsce}), od {podroz.DataOd} do {podroz.DataDo}\n\n";

            prompt += "Noclegi:\n";
            foreach (var n in noclegi)
                prompt += $"- {n.DataOd} – {n.DataDo}: {n.Nazwa}, {n.Adres}\n";

            prompt += "\nPunkty zwiedzania:\n";
            foreach (var z in zwiedzania)
                prompt += $"- {(string.IsNullOrEmpty(z.Data) ? "dowolny dzień" : z.Data)}: {z.Nazwa} {z.Uwagi}\n";

            prompt += "\nFormat odpowiedzi:\n" +
                      "Dzień 1 (data):\n09:00–10:00 – aktywność\n10:30–12:00 – aktywność\n…\n" +
                      "Dzień 2 (data):\n…";

            return await WywolajGemini(prompt);
        }
        private async void BtnGenerujPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Wyłącz przycisk, żeby użytkownik nie kliknął wielokrotnie
                (sender as Button).IsEnabled = false;

                // Pobierz dane z bazy
                var podroz = PobierzPodroz((int)tripId);
                var noclegi = PobierzNoclegi((int)tripId);
                var zwiedzanie = PobierzZwiedzanie((int)tripId);

                if (podroz == null)
                {
                    MessageBox.Show("Nie znaleziono podróży!");
                    return;
                }

                // Wywołanie AI w tle
                var rekomendacje = await Task.Run(() => GenerujHarmonogramAI(podroz, noclegi, zwiedzanie));

                // Wyświetlenie w ListBox
                HarmonogramListBox.ItemsSource = rekomendacje;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas generowania planu: " + ex.Message);
            }
            finally
            {
                // Włącz przycisk z powrotem
                (sender as Button).IsEnabled = true;
            }
        }
        private Podroz PobierzPodroz(int id)
        {
            using (SQLiteConnection conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id_podrozy, tytul, miejsce, data_od, data_do FROM podroze WHERE id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", id);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Podroz
                            {
                                Id = reader.GetInt32(0),
                                Tytul = reader.GetString(1),
                                Miejsce = reader.GetString(2),
                                DataOd = reader.GetString(3),
                                DataDo = reader.GetString(4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        private List<Nocleg> PobierzNoclegi(int idPodrozy)
        {
            var list = new List<Nocleg>();
            using (SQLiteConnection conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT p.nazwa, p.adres, n.data_od, n.data_do " +
                                      "FROM noclegi n " +
                                      "JOIN punkty_noclegowe p ON n.id_punktu = p.id_punktu " +
                                      "WHERE n.id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", idPodrozy);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Nocleg
                            {
                                Nazwa = reader.GetString(0),
                                Adres = reader.GetString(1),
                                DataOd = reader.GetString(2),
                                DataDo = reader.GetString(3)
                            });
                        }
                    }
                }
            }
            return list;
        }

        private List<Zwiedzanie> PobierzZwiedzanie(int idPodrozy)
        {
            var list = new List<Zwiedzanie>();
            using (SQLiteConnection conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT z.id_punktu, p.nazwa, z.data, z.uwagi " +
                                      "FROM zwiedzanie z " +
                                      "JOIN punkty_zwiedzania p ON z.id_punktu=p.id_punktu " +
                                      "WHERE z.id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", idPodrozy);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Zwiedzanie
                            {
                                Nazwa = reader.GetString(1),
                                Data = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Uwagi = reader.IsDBNull(3) ? "" : reader.GetString(3)
                            });
                        }
                    }
                }
            }
            return list;
        }

        //    DOJAZDY //
        public void SetPointFromMap(double lat, double lng, string fullAddress)
        {
            if (editingRoute == null) return;

            if (isSelectingStartPoint)
            {
                editingRoute.StartPointId = SavePoint(fullAddress, lat, lng);
                editingRoute.StartAddress = fullAddress; 
                isSelectingStartPoint = false;

                MessageBox.Show("Punkt startowy ustawiony!");
                this.Show();
                this.Activate();
            }
            else if (isSelectingEndPoint)
            {
                editingRoute.EndPointId = SavePoint(fullAddress, lat, lng);
                editingRoute.EndAddress = fullAddress; 
                isSelectingEndPoint = false;

                MessageBox.Show("Punkt końcowy ustawiony!");
                this.Show();
                this.Activate();
            }


        }

        private void BtnEditRoute_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var expander = btn.FindAncestor<Expander>();
            if (expander == null) return;

            var view = expander.FindName("viewMode") as FrameworkElement;
            var edit = expander.FindName("editMode") as FrameworkElement;

            if (view != null) view.Visibility = Visibility.Collapsed;
            if (edit != null)
            {
                edit.Visibility = Visibility.Visible;

                var dpDeparture = expander.FindName("dpDeparture") as DatePicker;
                var dpArrival = expander.FindName("dpArrival") as DatePicker;

                if (dpDeparture != null)
                {
                    dpDeparture.DisplayDateStart = tripStartDate.AddDays(-10);
                    dpDeparture.DisplayDateEnd = tripEndDate.AddDays(10);
                }

                if (dpArrival != null)
                {
                    dpArrival.DisplayDateStart = tripStartDate.AddDays(-10);
                    dpArrival.DisplayDateEnd = tripEndDate.AddDays(10);
                }

                var route = btn.DataContext as RouteDisplay;

                if (route != null)
                {
                    var txtDepartureTime = expander.FindName("txtDepartureTime") as TextBox;
                    var txtArrivalTime = expander.FindName("txtArrivalTime") as TextBox;

                    if (txtDepartureTime != null && route.DataWyjazduDate.HasValue)
                        txtDepartureTime.Text = route.DataWyjazduDate.Value.ToString("HH:mm");

                    if (txtArrivalTime != null && route.DataPrzyjazduDate.HasValue)
                        txtArrivalTime.Text = route.DataPrzyjazduDate.Value.ToString("HH:mm");

                    var cmbTransport = expander.FindName("cmbTransport") as ComboBox;

                    if (cmbTransport != null)
                    {
                        foreach (ComboBoxItem item in cmbTransport.Items)
                        {
                            if ((string)item.Content == route.Transport)
                            {
                                cmbTransport.SelectedItem = item;
                                break;
                            }
                        }
                    }

                    var txtCarrier = expander.FindName("txtCarrier") as TextBox;
                    if (txtCarrier != null) txtCarrier.Text = route.Przewoznik;

                    var txtRouteNumber = expander.FindName("txtRouteNumber") as TextBox;
                    if (txtRouteNumber != null) txtRouteNumber.Text = route.NumerRejsu;

                    var txtPrice = expander.FindName("txtPrice") as TextBox;
                    if (txtPrice != null) txtPrice.Text = route.CenaString;

                    var txtNotes = expander.FindName("txtNotes") as TextBox;
                    if (txtNotes != null) txtNotes.Text = route.Uwagi;
                }
            }
        }



        private async void BtnSaveEditedRoute_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var route = btn.DataContext as RouteDisplay;

            var expander = btn.FindAncestor<Expander>();
            if (expander == null) return;

            var dpDeparture = expander.FindName("dpDeparture") as DatePicker;
            var dpArrival = expander.FindName("dpArrival") as DatePicker;
            var txtDepartureTime = expander.FindName("txtDepartureTime") as TextBox;
            var txtArrivalTime = expander.FindName("txtArrivalTime") as TextBox;

            var cmbCurrency = expander.FindName("cmbCurrency") as ComboBox;

            if (cmbCurrency != null)
                route.Waluta = (cmbCurrency.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PLN";


            if (dpDeparture == null || dpArrival == null || txtDepartureTime == null || txtArrivalTime == null)
                return;

            if (!dpDeparture.SelectedDate.HasValue || !dpArrival.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz obie daty.");
                return;
            }

            if (!TryParseTime(txtDepartureTime.Text, out TimeSpan depTime))
            {
                MessageBox.Show("Niepoprawna godzina wyjazdu (format HH:MM).");
                return;
            }

            if (!TryParseTime(txtArrivalTime.Text, out TimeSpan arrTime))
            {
                MessageBox.Show("Niepoprawna godzina przyjazdu (format HH:MM).");
                return;
            }

            DateTime departure = dpDeparture.SelectedDate.Value.Add(depTime);
            DateTime arrival = dpArrival.SelectedDate.Value.Add(arrTime);

            if (!ValidateRouteDateTimes(departure, arrival))
                return;

            route.DataWyjazdu = departure.ToString("yyyy-MM-dd HH:mm");
            route.DataPrzyjazdu = arrival.ToString("yyyy-MM-dd HH:mm");

            await UpdateRouteAsync(route);

            MessageBox.Show("Zapisano zmiany.");

            LoadRoutes();
            LoadExpenses(); 
            LoadDetailedExpenses();


        }



        private async Task UpdateRouteAsync(RouteDisplay r)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SQLiteCommand(@"
                    UPDATE dojazdy SET 
                        id_punktu_start = @s,
                        id_punktu_koniec = @k,
                        srodek_transportu = @t,
                        przewoznik = @p,
                        numer_rejsu = @nr,
                        data_wyjazdu = @dw,
                        data_przyjazdu = @dp,
                        cena = @c,
                        waluta = @w,
                        uwagi = @u
                    WHERE id_dojazdu = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", r.IdDojazdu);
                    cmd.Parameters.AddWithValue("@s", r.StartPointId > 0 ? r.StartPointId : r.IdStart);
                    cmd.Parameters.AddWithValue("@k", r.EndPointId > 0 ? r.EndPointId : r.IdKoniec);
                    cmd.Parameters.AddWithValue("@t", r.Transport);
                    cmd.Parameters.AddWithValue("@p", r.Przewoznik);
                    cmd.Parameters.AddWithValue("@nr", r.NumerRejsu);
                    cmd.Parameters.AddWithValue("@dw", r.DataWyjazdu);
                    cmd.Parameters.AddWithValue("@dp", r.DataPrzyjazdu);
                    cmd.Parameters.AddWithValue("@c", r.CenaDecimal);   

                    cmd.Parameters.AddWithValue("@w", r.Waluta);

                    cmd.Parameters.AddWithValue("@u", r.Uwagi);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        private void BtnEditStartPoint_Click(object sender, RoutedEventArgs e)
        {
            var route = (sender as FrameworkElement)?.DataContext as RouteDisplay;
            if (route == null) return;

            if (Owner is MapWindow map)
            {
                map.IsAddingRoute = true;
                map.CurrentTripDetailsWindow = this;

                isSelectingStartPoint = true;
                isSelectingEndPoint = false;

                editingRoute = route;

                this.Hide();
                MapOwner?.Activate();

                MessageBox.Show("Kliknij nowy punkt startowy na mapie 🚗", "Edytuj dojazd");
            }
        }



        private void BtnEditEndPoint_Click(object sender, RoutedEventArgs e)
        {
            var route = (sender as FrameworkElement)?.DataContext as RouteDisplay;
            if (route == null) return;

            if (Owner is MapWindow map)
            {
             

                map.IsAddingRoute = true;
                map.CurrentTripDetailsWindow = this;

                isSelectingStartPoint = false;
                isSelectingEndPoint = true;

                editingRoute = route;

                this.Hide();
                MapOwner?.Activate();

                MessageBox.Show("Kliknij nowy punkt końcowy na mapie 🚗", "Edytuj dojazd");
            }
        }



        private bool ValidateRouteDateTimes(DateTime departure, DateTime arrival)
        {
            DateTime min = tripStartDate.AddDays(-10);
            DateTime max = tripEndDate.AddDays(10).Date.AddHours(23).AddMinutes(59);

            if (departure > arrival)
            {
                MessageBox.Show("Wyjazd nie może być później niż przyjazd.");
                return false;
            }

            if (departure < min || departure > max)
            {
                MessageBox.Show($"Wyjazd musi być w przedziale {min:dd.MM.yyyy HH:mm} – {max:dd.MM.yyyy HH:mm}");
                return false;
            }

            if (arrival < min || arrival > max)
            {
                MessageBox.Show($"Przyjazd musi być w przedziale {min:dd.MM.yyyy HH:mm} – {max:dd.MM.yyyy HH:mm}");
                return false;
            }

            return true;
        }

        private void dpDeparture_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            var dpDeparture = sender as DatePicker;
            if (dpDeparture == null) return;

            var expander = dpDeparture.FindAncestor<Expander>();
            var dpArrival = expander?.FindName("dpArrival") as DatePicker;

            if (dpDeparture.SelectedDate.HasValue && dpArrival != null)
            {
                dpArrival.DisplayDateStart = dpDeparture.SelectedDate.Value;

                if (dpArrival.SelectedDate.HasValue &&
                    dpArrival.SelectedDate < dpDeparture.SelectedDate)
                {
                    dpArrival.SelectedDate = dpDeparture.SelectedDate;
                }
            }
        }

        private void dpArrival_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            var dpArrival = sender as DatePicker;
            if (dpArrival == null) return;

            var expander = dpArrival.FindAncestor<Expander>();
            var dpDeparture = expander?.FindName("dpDeparture") as DatePicker;

            if (dpArrival.SelectedDate.HasValue && dpDeparture != null)
            {
                dpDeparture.DisplayDateEnd = dpArrival.SelectedDate.Value;

                if (dpDeparture.SelectedDate.HasValue &&
                    dpDeparture.SelectedDate > dpArrival.SelectedDate)
                {
                    dpDeparture.SelectedDate = dpArrival.SelectedDate;
                }
            }
        }


        private async void BtnDeleteRoute_Click(object sender, RoutedEventArgs e)
        {
            var route = (sender as FrameworkElement).DataContext as RouteDisplay;

            var result = MessageBox.Show(
                "Czy na pewno chcesz usunąć ten dojazd?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                await DeleteRouteAsync(route);
                LoadRoutes();
                LoadExpenses(); 
                LoadDetailedExpenses();


            }
        }

        private async Task DeleteRouteAsync(RouteDisplay r)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SQLiteCommand(
                    "DELETE FROM dojazdy WHERE id_dojazdu=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", r.IdDojazdu);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SQLiteCommand(@"
                    WITH numbered AS (
                        SELECT id_dojazdu,
                               ROW_NUMBER() OVER (ORDER BY kolejnosc) AS new_order
                        FROM dojazdy
                        WHERE id_podrozy=@trip
                    )
                    UPDATE dojazdy
                    SET kolejnosc = (
                        SELECT new_order FROM numbered
                        WHERE numbered.id_dojazdu = dojazdy.id_dojazdu
                    )
                    WHERE id_podrozy=@trip;", conn))
                {
                    cmd.Parameters.AddWithValue("@trip", tripId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private long SavePoint(string address, double lat, double lng)
        {
            long pointId = 0;

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"
            INSERT INTO punkty_dojazdow (nazwa, szerokosc, dlugosc)
            VALUES (@name, @lat, @lng);
            SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@name", address);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);

                    pointId = (long)cmd.ExecuteScalar();
                }
            }

            return pointId;
        }



        private void BtnOpenAddRouteWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MapWindow mapa)
            {
                if (mapa.CurrentAddRouteWindow != null &&
                    mapa.CurrentAddRouteWindow.IsVisible)
                {
                    mapa.CurrentAddRouteWindow.Activate();
                }
                else
                {
                    var addRouteWindow = new AddRouteWindow(tripId, userId, tripStartDate, tripEndDate);

                    addRouteWindow.Owner = mapa;
                    addRouteWindow.MapOwner = mapa;
                    addRouteWindow.DetailsOwner = this;

                    addRouteWindow.Closed += (s, _) =>
                    {
                        mapa.CurrentAddRouteWindow = null;
                        mapa.Activate();
                    };

                    mapa.CurrentAddRouteWindow = addRouteWindow;
                    addRouteWindow.Show();
                }

                this.Hide();
                MapOwner?.Activate();
            }
        }


        private bool TryParseTime(string input, out TimeSpan result)
        {
            return TimeSpan.TryParseExact(input, @"hh\:mm",
                CultureInfo.InvariantCulture, out result);
        }



        public void LoadRoutes()
        {
            var routes = new List<RouteDisplay>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                string query = @"
                    SELECT d.id_dojazdu,
                           d.id_punktu_start,
                           d.id_punktu_koniec,
                           d.kolejnosc,
                           ps.nazwa AS Start,
                           pk.nazwa AS End,
                           d.srodek_transportu,
                           d.przewoznik,
                           d.numer_rejsu,
                           d.data_wyjazdu,
                           d.data_przyjazdu,
                           d.cena,
                          d.waluta,
                           d.uwagi
                    FROM dojazdy d
                    JOIN punkty_dojazdow ps ON ps.id_punktu = d.id_punktu_start
                    JOIN punkty_dojazdow pk ON pk.id_punktu = d.id_punktu_koniec
                    WHERE d.id_podrozy=@tripId
                    ORDER BY d.kolejnosc";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@tripId", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var route = new RouteDisplay
                            {
                                IdDojazdu = Convert.ToInt64(reader[0]),
                                IdStart = Convert.ToInt64(reader[1]),
                                IdKoniec = Convert.ToInt64(reader[2]),
                                Kolejnosc = Convert.ToInt64(reader[3]),
                                Naglowek = $"{Convert.ToInt64(reader[3])}. {reader.GetString(4)} → {reader.GetString(5)}",
                                Transport = reader.GetString(6),
                                Przewoznik = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                NumerRejsu = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                DataWyjazdu = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                DataPrzyjazdu = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                StartAddress = reader.GetString(4),
                                EndAddress = reader.GetString(5),
                                CenaDecimal = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader[11]),
                                Waluta = reader.IsDBNull(12) ? "PLN" : reader.GetString(12),
                                Uwagi = reader.IsDBNull(13) ? "" : reader.GetString(13)

                            };


                            routes.Add(route);
                        }
                    }
                }
            }

            lstRoutes.ItemsSource = routes;

        }

        private void txtTime_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox tb = sender as TextBox;
            int pos = tb.SelectionStart;

            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
                return;
            }

            
            if (pos == 2)
            {
                tb.SelectionStart = 3;
                pos = 3;
            }

            char c = e.Text[0];

            
            switch (pos)
            {
                case 0: 
                    if (c != '0' && c != '1' && c != '2')
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
                case 1: 
                    if (tb.Text.Length > 0 && tb.Text[0] == '2' && (c < '0' || c > '3'))
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
                case 3: 
                    if (c < '0' || c > '5')
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
                case 4: 
                    break; 
            }


            
            if (tb.Text.Length < 5)
            {
                tb.Text = tb.Text.PadRight(5, '0');
            }

            
            if (pos < 0 || pos > 4)
            {
                e.Handled = true;
                return;
            }

            
            char[] textChars = tb.Text.ToCharArray();
            textChars[pos] = c;
            tb.Text = new string(textChars);

            
            tb.SelectionStart = pos + 1;
            e.Handled = true;

        }


        private void txtTime_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            TextBox tb = sender as TextBox;
            int pos = tb.SelectionStart;

            
            if ((e.Key == Key.Back && pos == 3) || (e.Key == Key.Back && pos == 0))
            {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Delete && pos == 2)
            {
                e.Handled = true;
                return;
            }

            
            if (e.Key == Key.Back && pos > 0)
            {
                if (pos == 3) pos = 2; 
                char[] textChars = tb.Text.ToCharArray();
                textChars[pos - 1] = '0';
                tb.Text = new string(textChars);
                tb.SelectionStart = pos - 1;
                e.Handled = true;
            }

            
            if (e.Key == Key.Delete && pos < tb.Text.Length)
            {
                if (pos == 2) pos = 3; 
                char[] textChars = tb.Text.ToCharArray();
                textChars[pos] = '0';
                tb.Text = new string(textChars);
                tb.SelectionStart = pos;
                e.Handled = true;
            }
        }

        private void txtTime_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            tb.SelectionStart = 0;
        }

        // KONIEC DOJAZDY //


        //  PUNKTY ZWIEDZANIA   // 

        public void LoadVisitPoints()
        {
 
            var visits = new List<VisitPointDisplay>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                string query = @"
    SELECT z.id_zwiedzania,
           z.id_punktu,
           z.data,
           z.cena,
           z.waluta,
           z.ocena,
           z.uwagi,
           p.nazwa AS Adres,
           p.id_kategorii AS id_kategorii,
          k.nazwa AS Kategoria
    FROM zwiedzanie z
    JOIN punkty_zwiedzania p ON p.id_punktu = z.id_punktu
    LEFT JOIN kategorie k ON k.id_kategorii = p.id_kategorii
    WHERE z.id_podrozy = @tripId
    ORDER BY z.data";


                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@tripId", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var vp = new VisitPointDisplay
                            {
                                IdZwiedzania = Convert.ToInt64(reader["id_zwiedzania"]),
                                IdPunktu = Convert.ToInt64(reader["id_punktu"]),
                                Adres = reader["Adres"].ToString(),
                                IdKategorii = reader.IsDBNull(reader.GetOrdinal("id_kategorii")) ? 0 : Convert.ToInt32(reader["id_kategorii"]),
                                Kategoria = reader.IsDBNull(reader.GetOrdinal("Kategoria")) ? "" : reader["Kategoria"].ToString(),


                                DataZwiedzaniaDate = reader.IsDBNull(reader.GetOrdinal("data"))
                                ? (DateTime?)null
                                : Convert.ToDateTime(reader["data"]),
                                CenaValue = reader.IsDBNull(reader.GetOrdinal("cena")) ? 0 : Convert.ToDecimal(reader["cena"]),
                                Ocena = reader.IsDBNull(reader.GetOrdinal("ocena")) ? "" : reader["ocena"].ToString(),
                                Waluta = reader.IsDBNull(reader.GetOrdinal("waluta")) ? "PLN" : reader["waluta"].ToString(),
                                Uwagi = reader.IsDBNull(reader.GetOrdinal("uwagi")) ? "" : reader["uwagi"].ToString()
                            };


                            visits.Add(vp);
                        }
                    }
                }
            }

            lstVisits.ItemsSource = visits;

        }


        private bool ValidateVisitDateTime(DateTime visitDate)
        {
            DateTime min = tripStartDate.Date.AddDays(-10);
            DateTime max = tripEndDate.Date.AddDays(10);

            DateTime visit = visitDate.Date;

            if (visit < min || visit > max)
            {
                MessageBox.Show($"Data zwiedzania musi być w przedziale {min:dd.MM.yyyy} – {max:dd.MM.yyyy}");
                return false;
            }

            return true;
        }


        private void dpVisitDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            var dp = sender as DatePicker;
            if (dp?.SelectedDate == null) return;

            DateTime selected = dp.SelectedDate.Value.Date;

            DateTime min = tripStartDate.Date.AddDays(-10);
            DateTime max = tripEndDate.Date.AddDays(10);

            if (selected < min)
            {
                MessageBox.Show($"Data nie może być wcześniejsza niż {min:dd.MM.yyyy}");
                dp.SelectedDate = min;
            }
            else if (selected > max)
            {
                MessageBox.Show($"Data nie może być późniejsza niż {max:dd.MM.yyyy}");
                dp.SelectedDate = max;
            }
        }

        private void BtnOpenAddVisitPointWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MapWindow mapa)
            {
                if (mapa.CurrentAddVisitPointWindow != null &&
                    mapa.CurrentAddVisitPointWindow.IsVisible)
                {
                    mapa.CurrentAddVisitPointWindow.Activate();
                }
                else
                {
                    var addVisitWindow = new AddVisitPointWindow(tripId, userId, tripStartDate, tripEndDate)
                    {
                        Owner = mapa,
                        MapOwner = mapa,
                        CurrentTripDetailsWindow = this
                    };

                    addVisitWindow.Closed += (s, _) =>
                    {
                        mapa.CurrentAddVisitPointWindow = null;
                        mapa.Activate(); 
                        this.Show();    
                        LoadVisitPoints();
                        LoadExpenses();
                        LoadDetailedExpenses();
                    };

                    mapa.CurrentAddVisitPointWindow = addVisitWindow;
                    addVisitWindow.Show();

                    this.Hide(); 
                }
            }
        }


        private void BtnPickVisitPointFromMap_Click(object sender, RoutedEventArgs e)
        {
            var visit = (sender as FrameworkElement)?.DataContext as VisitPointDisplay;
            if (visit == null) return;

            if (Owner is MapWindow map)
            {
                
                map.IsAddingPoint = true;
                map.CurrentTripDetailsWindow = this;
                isSelectingVisitPoint = true;
                editingVisitPoint = visit;

                this.Hide();
                MapOwner?.Activate();

                MessageBox.Show("Kliknij na mapie, aby ustawić nowy punkt zwiedzania.", "Wybierz punkt");
            }
        }



        private long SaveVisitPoint(string address, double lat, double lng)
        {
            long pointId = 0;

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"
            INSERT INTO punkty_zwiedzania (nazwa, szerokosc, dlugosc, id_kategorii)
            VALUES (@name, @lat, @lng, NULL);
            SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@name", address);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);

                    pointId = (long)cmd.ExecuteScalar();
                }
            }

            return pointId;
        }


        private void BtnEditVisitPoint_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var expander = btn.FindAncestor<Expander>();
            if (expander == null) return;

            var view = expander.FindName("viewMode") as FrameworkElement;
            var edit = expander.FindName("editMode") as FrameworkElement;

            if (view != null) view.Visibility = Visibility.Collapsed;
            if (edit != null) edit.Visibility = Visibility.Visible;

            var visit = btn.DataContext as VisitPointDisplay;

            if (visit != null)
            {
                var dpDate = expander.FindName("dpDate") as DatePicker;
                if (dpDate != null)
                {
                    
                    dpDate.DisplayDateStart = tripStartDate.Date.AddDays(-10);
                    dpDate.DisplayDateEnd = tripEndDate.Date.AddDays(10);

                    if (visit.DataZwiedzaniaDate.HasValue)
                        dpDate.SelectedDate = visit.DataZwiedzaniaDate.Value.Date;
                }

                
                var txtAddress = expander.FindName("txtEditAddress") as TextBox;
                if (txtAddress != null) txtAddress.Text = visit.Adres;

                var txtPrice = expander.FindName("txtEditPrice") as TextBox;
                if (txtPrice != null)
                {
                    visit.CenaString = txtPrice.Text; 
                }

                var txtRating = expander.FindName("txtEditRating") as TextBox;
                if (txtRating != null) txtRating.Text = visit.Ocena;

                var txtNotes = expander.FindName("txtEditNotes") as TextBox;
                if (txtNotes != null) txtNotes.Text = visit.Uwagi;

                
                var cmbCategory = expander.FindName("cmbEditCategory") as ComboBox;
                if (cmbCategory != null)
                {
                    foreach (ComboBoxItem item in cmbCategory.Items)
                    {
                        if ((item.Tag as CategoryItem)?.Id == visit.IdKategorii)
                        {
                            cmbCategory.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }


        private void txtEditRating_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;


            if (!"12345".Contains(e.Text))
            {
                e.Handled = true;
                return;
            }

            if (tb.Text.Length >= 1)
            {
                e.Handled = true;
                return;
            }
        }
        private async void BtnSaveEditedVisitPoint_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var visit = btn.DataContext as VisitPointDisplay;
            if (visit == null) return;

            var expander = btn.FindAncestor<Expander>();
            if (expander == null) return;

       
            var dpDate = expander.FindName("dpDate") as DatePicker;
            var txtAddress = expander.FindName("txtEditAddress") as TextBox;
            var txtPrice = expander.FindName("txtEditPrice") as TextBox;
            var cmbCurrency = expander.FindName("cmbEditCurrency") as ComboBox;
            var txtRating = expander.FindName("txtEditRating") as TextBox;
            var txtNotes = expander.FindName("txtEditNotes") as TextBox;
            var cmbCategory = expander.FindName("cmbEditCategory") as ComboBox;

           
            if (dpDate == null || !dpDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę zwiedzania.");
                return;
            }

            DateTime selectedDate = dpDate.SelectedDate.Value;

           
            if (!ValidateVisitDateTime(selectedDate))
                return;

        

            visit.DataZwiedzaniaDate = selectedDate;

            if (txtAddress != null) visit.Adres = txtAddress.Text;
            if (txtPrice != null) visit.CenaString = txtPrice.Text;
            if (txtRating != null) visit.Ocena = txtRating.Text;
            if (txtNotes != null) visit.Uwagi = txtNotes.Text;
            if (cmbCurrency != null && cmbCurrency.SelectedItem is ComboBoxItem selectedCurrency)
            {
                visit.Waluta = selectedCurrency.Content.ToString();
            }

           
            if (cmbCategory != null && cmbCategory.SelectedItem is ComboBoxItem selected &&
                selected.Tag is CategoryItem cat)
            {
                visit.Kategoria = selected.Content.ToString();
                await UpdateVisitPointCategoryAsync(visit.IdPunktu, cat.Id);
            }

        
            await UpdateVisitPointAsync(visit);

            MessageBox.Show("Zapisano zmiany.");
            LoadVisitPoints();
            LoadExpenses();
            LoadDetailedExpenses();

        }


        private async Task UpdateVisitPointAsync(VisitPointDisplay v)
        {

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SQLiteCommand(@"
           UPDATE zwiedzanie
    SET id_punktu=@idPunktu,
        data=@data,
        uwagi=@uwagi,
        cena=@cena,
        waluta=@waluta,
        ocena=@ocena
    WHERE id_zwiedzania=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", v.IdZwiedzania);
                    cmd.Parameters.AddWithValue("@idPunktu", v.IdPunktu);
                    cmd.Parameters.AddWithValue("@data", v.DataZwiedzaniaDate?.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@uwagi", v.Uwagi);
                    cmd.Parameters.AddWithValue("@cena", v.CenaValue);
                    cmd.Parameters.AddWithValue("@waluta", v.Waluta);
                    cmd.Parameters.AddWithValue("@ocena", v.Ocena);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }




        private async Task UpdateVisitPointCategoryAsync(long pointId, int categoryId)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SQLiteCommand(
                    "UPDATE punkty_zwiedzania SET id_kategorii=@cat WHERE id_punktu=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", pointId);
                    cmd.Parameters.AddWithValue("@cat", categoryId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }


        public void SetVisitPointFromMap(double lat, double lng, string fullAddress)
        {
            if (editingVisitPoint == null) return;

         
            long newPointId = SaveVisitPoint(fullAddress, lat, lng);

       
            editingVisitPoint.IdPunktu = newPointId;
            editingVisitPoint.Adres = fullAddress; 

            isSelectingVisitPoint = false;
            UpdateVisitAddressTextBox(editingVisitPoint.Adres);
            MessageBox.Show("Punkt zwiedzania zaktualizowany!");

            this.Show();
            this.Activate();

        }


        private async void BtnDeleteVisitPoint_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var visit = btn.DataContext as VisitPointDisplay;
            if (visit == null) return;

            var result = MessageBox.Show(
                "Czy na pewno chcesz usunąć ten punkt zwiedzania?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                await DeleteVisitPointAsync(visit);
                LoadVisitPoints();
                LoadExpenses();
                LoadDetailedExpenses();

            }
        }


        private async Task DeleteVisitPointAsync(VisitPointDisplay v)
        {

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                using (var tran = conn.BeginTransaction())
                {
                    
                    int? categoryId = null;
                    using (var cmdGetCat = new SQLiteCommand(
                        "SELECT id_kategorii FROM punkty_zwiedzania WHERE id_punktu=@id", conn, tran))
                    {
                        cmdGetCat.Parameters.AddWithValue("@id", v.IdPunktu);
                        var result = await cmdGetCat.ExecuteScalarAsync();
                        if (result != DBNull.Value && result != null)
                            categoryId = Convert.ToInt32(result);
                    }

                   
                    using (var cmdDelVisit = new SQLiteCommand(
                        "DELETE FROM zwiedzanie WHERE id_zwiedzania=@id", conn, tran))
                    {
                        cmdDelVisit.Parameters.AddWithValue("@id", v.IdZwiedzania);
                        await cmdDelVisit.ExecuteNonQueryAsync();
                    }

                   
                    using (var cmdDelPoint = new SQLiteCommand(
                        "DELETE FROM punkty_zwiedzania WHERE id_punktu=@id", conn, tran))
                    {
                        cmdDelPoint.Parameters.AddWithValue("@id", v.IdPunktu);
                        await cmdDelPoint.ExecuteNonQueryAsync();
                    }

                   
                    if (categoryId.HasValue)
                    {
                        using (var cmdCheck = new SQLiteCommand(
                            @"SELECT COUNT(*) FROM punkty_zwiedzania 
                      WHERE id_kategorii=@catId", conn, tran))
                        {
                            cmdCheck.Parameters.AddWithValue("@catId", categoryId.Value);
                            var count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                            if (count == 0)
                            {
                                using (var cmdDelCat = new SQLiteCommand(
                                    "DELETE FROM kategorie WHERE id_kategorii=@catId AND czy_domyslna=0", conn, tran))
                                {
                                    cmdDelCat.Parameters.AddWithValue("@catId", categoryId.Value);
                                    await cmdDelCat.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                    tran.Commit();
                }
            }
        }



        private void cmbEditCategory_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;

            combo.Items.Clear();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT id_kategorii, nazwa, ikona FROM kategorie ORDER BY nazwa", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        combo.Items.Add(new ComboBoxItem
                        {
                            Content = reader["nazwa"].ToString(),
                            Tag = new CategoryItem
                            {
                                Id = Convert.ToInt32(reader["id_kategorii"]),
                                Icon = reader["ikona"].ToString()
                            }
                        });
                    }
                }
            }
        }

        private void cmbEditCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo.SelectedItem is ComboBoxItem selected &&
                selected.Tag is CategoryItem cat && currentMarker != null)
            {
                
                string iconPath = $"pack://application:,,,/Images/{cat.Icon}";

                if (currentMarker.Shape is Image img)
                    img.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            }
        }


        // KONIEC PUNKTY ZWIEDZANIA 



        // NOCLEGI 


        public void LoadAccommodations()
        {
            var accommodations = new List<AccommodationDisplay>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                string query = @"
         SELECT 
    n.id_noclegu,
    n.id_punktu,
    n.data_od,
    n.data_do,
    n.cena,
    n.waluta,
    n.ocena,
    n.uwagi,
    p.nazwa AS Nazwa,
    p.adres AS Adres,
    p.id_kategorii AS IdKategorii,      -- ✔️ DODANE
    k.nazwa AS Kategoria
FROM noclegi n
JOIN punkty_noclegowe p ON p.id_punktu = n.id_punktu
LEFT JOIN kategorie_noclegow k ON k.id_kategorii = p.id_kategorii
WHERE n.id_podrozy=@tripId
ORDER BY n.data_od
"
;

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@tripId", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            accommodations.Add(new AccommodationDisplay
                            {
                                IdNoclegu = Convert.ToInt64(reader["id_noclegu"]),
                                IdPunktu = Convert.ToInt64(reader["id_punktu"]),
                                Nazwa = reader["Nazwa"].ToString(),
                                Kategoria = reader.IsDBNull(reader.GetOrdinal("Kategoria")) ? "" : reader["Kategoria"].ToString(),
                                IdKategorii = reader.IsDBNull(reader.GetOrdinal("IdKategorii"))
    ? 0
    : Convert.ToInt32(reader["IdKategorii"]),

                                Adres = reader.IsDBNull(reader.GetOrdinal("adres"))
    ? reader["Nazwa"].ToString() 
    : reader["adres"].ToString(),

                                DataOd = reader.IsDBNull(reader.GetOrdinal("data_od")) ? "" : Convert.ToDateTime(reader["data_od"]).ToString("yyyy-MM-dd"),
                                DataDo = reader.IsDBNull(reader.GetOrdinal("data_do")) ? "" : Convert.ToDateTime(reader["data_do"]).ToString("yyyy-MM-dd"),
                                DataOdDate = reader.IsDBNull(reader.GetOrdinal("data_od")) ? (DateTime?)null : Convert.ToDateTime(reader["data_od"]),
                                DataDoDate = reader.IsDBNull(reader.GetOrdinal("data_do")) ? (DateTime?)null : Convert.ToDateTime(reader["data_do"]),
                                Cena = reader.IsDBNull(reader.GetOrdinal("cena")) ? 0 : Convert.ToDecimal(reader["cena"]),
                                Waluta = reader.IsDBNull(reader.GetOrdinal("waluta")) ? "PLN" : reader["waluta"].ToString(),
                            Ocena = reader.IsDBNull(reader.GetOrdinal("ocena")) ? 0 : Convert.ToInt32(reader["ocena"]),
                                Uwagi = reader.IsDBNull(reader.GetOrdinal("uwagi")) ? "" : reader["uwagi"].ToString()
                            });

                        }
                    }
                }
            }

            lstAccommodations.ItemsSource = accommodations;
        }

        private void BtnOpenAddAccommodationWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MapWindow mapa)
            {
                var addWindow = new AddAccommodationWindow(tripId, userId, tripStartDate, tripEndDate)
                {
                    Owner = mapa,
                    MapOwner = mapa
                };

               
                addWindow.Closed += (s, _) =>
                {
                    mapa.CurrentAddAccommodationWindow = null;
                    mapa.Activate(); 
                    this.Show();    
                    this.LoadAccommodations(); 
                    this.LoadExpenses();
                };

               
                mapa.CurrentAddAccommodationWindow = addWindow;

                addWindow.Show();
                this.Hide(); 
            }
        }

        private void BtnEditAccommodation_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var expander = btn.FindAncestor<Expander>();
            if (expander == null) return;

            var view = expander.FindName("viewMode") as FrameworkElement;
            var edit = expander.FindName("editMode") as FrameworkElement;

            if (view != null) view.Visibility = Visibility.Collapsed;
            if (edit != null) edit.Visibility = Visibility.Visible;

            var acc = btn.DataContext as AccommodationDisplay;
            if (acc != null)
            {
                var dpFrom = expander.FindName("dpFrom") as DatePicker;
                var dpTo = expander.FindName("dpTo") as DatePicker;
                var txtAddress = expander.FindName("txtEditAddress") as TextBox;
                var txtPrice = expander.FindName("txtEditPrice") as TextBox;
                var cmbCurrency = expander.FindName("cmbEditCurrency") as ComboBox;
                var txtRating = expander.FindName("txtEditRating") as TextBox;
                var txtNotes = expander.FindName("txtEditNotes") as TextBox;
                var cmbCategory = expander.FindName("cmbEditCategory") as ComboBox;
                if (dpFrom != null)
                {
                    dpFrom.DisplayDateStart = tripStartDate.Date.AddDays(-10);
                    dpFrom.DisplayDateEnd = tripEndDate.Date.AddDays(10);

                   
                    dpFrom.BlackoutDates.Clear();
                    dpFrom.BlackoutDates.Add(new CalendarDateRange(DateTime.MinValue, dpFrom.DisplayDateStart.Value.AddDays(-1)));
                    dpFrom.BlackoutDates.Add(new CalendarDateRange(dpFrom.DisplayDateEnd.Value.AddDays(1), DateTime.MaxValue));

                    if (acc.DataOdDate.HasValue)
                        dpFrom.SelectedDate = acc.DataOdDate.Value;

                    dpFrom.SelectedDateChanged += dpFrom_SelectedDateChanged;
                }
                if (dpTo != null)
                {
                    
                    dpTo.DisplayDateEnd = tripEndDate.Date.AddDays(10);

                    if (dpFrom.SelectedDate.HasValue)
                    {
                        dpTo.DisplayDateStart = dpFrom.SelectedDate.Value.AddDays(1);
                    }
                    else
                    {
                        dpTo.DisplayDateStart = tripStartDate.Date.AddDays(-10);
                    }

                    dpTo.BlackoutDates.Clear();

                    if (acc.DataDoDate.HasValue)
                        dpTo.SelectedDate = acc.DataDoDate.Value;

                    dpTo.SelectedDateChanged += dpTo_SelectedDateChanged;


                    dpFrom.SelectedDateChanged += (s, e2) =>
                    {
                        if (dpFrom.SelectedDate.HasValue)
                            dpTo.DisplayDateStart = dpFrom.SelectedDate.Value.AddDays(1);
                    };
                }


                if (cmbCurrency != null)
                {
                    cmbCurrency.ItemsSource = acc.DostepneWaluty; 
                    cmbCurrency.SelectedItem = acc.Waluta;        
                }

                if (txtAddress != null) txtAddress.Text = acc.Adres;
                if (txtPrice != null) txtPrice.Text = acc.Cena.ToString("F2");


                if (txtRating != null) txtRating.Text = acc.Ocena.ToString();
                if (txtNotes != null) txtNotes.Text = acc.Uwagi;

                if (cmbCategory != null)
                {
                    foreach (ComboBoxItem item in cmbCategory.Items)
                    {
                        if ((item.Tag as CategoryItem)?.Id == acc.IdKategorii) 
                        {
                            cmbCategory.SelectedItem = item;
                            break;
                        }
                    }
                }

            }
        }
      

        private async void BtnSaveEditedAccommodation_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var acc = btn?.DataContext as AccommodationDisplay;
            if (acc == null) return;

            var expander = btn.FindAncestor<Expander>();
            if (expander == null) return;

           
            var dpFrom = expander.FindName("dpFrom") as DatePicker;
            var dpTo = expander.FindName("dpTo") as DatePicker;
            var txtAddress = expander.FindName("txtEditAddress") as TextBox;
            var txtPrice = expander.FindName("txtEditPrice") as TextBox;
            var txtRating = expander.FindName("txtEditRating") as TextBox;
            var txtNotes = expander.FindName("txtEditNotes") as TextBox;
            var txtName = expander.FindName("txtEditName") as TextBox; 
            var cmbCategory = expander.FindName("cmbEditCategory") as ComboBox;

           
            var startDate = dpFrom?.SelectedDate ?? acc.DataOdDate;
            var endDate = dpTo?.SelectedDate ?? acc.DataDoDate;

            if (!startDate.HasValue || !endDate.HasValue)
            {
                MessageBox.Show("Wybierz daty noclegu.");
                return;
            }
            if (!ValidateAccommodationDates(startDate.Value, endDate.Value))
            {
                return; 
            }

            if (!IsAccommodationDateAvailable(startDate.Value, endDate.Value, acc.IdNoclegu))
            {
                MessageBox.Show("W tym terminie masz już nocleg. Wybierz inne daty.");
                return;
            }
            
            if (txtRating != null)
            {
                string ratingText = txtRating.Text.Trim();
                if (!string.IsNullOrEmpty(ratingText))
                {
                    if (!int.TryParse(ratingText, out int ratingValue) || ratingValue < 0 || ratingValue > 5)
                    {
                        MessageBox.Show("Ocena musi być liczbą od 0 do 5 lub pozostaw puste pole.");
                        txtRating.Focus();
                        return;
                    }
                }
            }


            acc.DataOdDate = startDate.Value;
            acc.DataDoDate = endDate.Value;
            if (txtAddress != null) acc.Adres = txtAddress.Text;
            if (txtPrice != null) acc.CenaString = txtPrice.Text;
            if (txtRating != null && int.TryParse(txtRating.Text, out int rating)) acc.Ocena = rating;
            if (txtNotes != null) acc.Uwagi = txtNotes.Text;
            if (txtName != null) acc.Nazwa = txtName.Text;

   
            if (cmbCategory != null)
            {
                string kat = cmbCategory.Text.Trim();
                if (!string.IsNullOrEmpty(kat))
                {
                    int categoryId = EnsureAccommodationCategoryExists(kat);
                    acc.Kategoria = kat;
                    await UpdateAccommodationCategoryAsync(acc.IdPunktu, categoryId);


                    cmbCategory.Items.Clear();
                    cmbEditAccommodationCategory_Loaded(cmbCategory, null);


                    foreach (ComboBoxItem item in cmbCategory.Items)
                    {
                        if ((item.Tag as CategoryItem)?.Id == categoryId)
                        {
                            cmbCategory.SelectedItem = item;
                            break;
                        }
                    }
                }
            }



            await UpdateAccommodationAsync(acc);        
            await UpdateAccommodationNameAsync(acc.IdPunktu, acc.Nazwa); 
            await UpdateAccommodationAddressAsync(acc.IdPunktu, acc.Adres); 

            MessageBox.Show("Zapisano zmiany.");
            LoadAccommodations();
            LoadExpenses();
            LoadDetailedExpenses();
        }



        private async Task UpdateAccommodationNameAsync(long pointId, string newName)
        {
            
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SQLiteCommand(
                    "UPDATE punkty_noclegowe SET nazwa=@name WHERE id_punktu=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", pointId);
                    cmd.Parameters.AddWithValue("@name", newName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateAccommodationAddressAsync(long pointId, string newAddress)
        {
          
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SQLiteCommand(
                    "UPDATE punkty_noclegowe SET adres=@address WHERE id_punktu=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", pointId);
                    cmd.Parameters.AddWithValue("@address", newAddress);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }



        private async Task UpdateAccommodationCategoryAsync(long pointId, int categoryId)
        {
            
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SQLiteCommand(
                    "UPDATE punkty_noclegowe SET id_kategorii=@cat WHERE id_punktu=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", pointId);
                    cmd.Parameters.AddWithValue("@cat", categoryId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }


        private async void BtnDeleteAccommodation_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var accommodation = btn?.DataContext as AccommodationDisplay;
            if (accommodation == null) return;

            var result = MessageBox.Show(
                "Czy na pewno chcesz usunąć ten nocleg?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                await DeleteAccommodationAsync(accommodation);
                LoadAccommodations(); 
                LoadExpenses();
                LoadDetailedExpenses();
            }
        }

   
        private async Task DeleteAccommodationAsync(AccommodationDisplay a)
        {
           

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();
                using (var tran = conn.BeginTransaction())
                {
                  
                    int? categoryId = null;
                    bool isUserCategory = false;

                    using (var cmdGetCat = new SQLiteCommand(
                        @"SELECT k.id_kategorii, k.czy_domyslna 
                  FROM punkty_noclegowe p 
                  JOIN kategorie_noclegow k ON p.id_kategorii=k.id_kategorii 
                  WHERE p.id_punktu=@id", conn, tran))
                    {
                        cmdGetCat.Parameters.AddWithValue("@id", a.IdPunktu);
                        using (var reader = await cmdGetCat.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                if (reader["id_kategorii"] != DBNull.Value)
                                    categoryId = Convert.ToInt32(reader["id_kategorii"]);

                                isUserCategory = reader["czy_domyslna"] != DBNull.Value && Convert.ToInt32(reader["czy_domyslna"]) == 0;
                            }
                        }
                    }

                    using (var cmdDelAccommodation = new SQLiteCommand(
                        "DELETE FROM noclegi WHERE id_noclegu=@id", conn, tran))
                    {
                        cmdDelAccommodation.Parameters.AddWithValue("@id", a.IdNoclegu);
                        await cmdDelAccommodation.ExecuteNonQueryAsync();
                    }

           
                    using (var cmdDelPoint = new SQLiteCommand(
                        "DELETE FROM punkty_noclegowe WHERE id_punktu=@id", conn, tran))
                    {
                        cmdDelPoint.Parameters.AddWithValue("@id", a.IdPunktu);
                        await cmdDelPoint.ExecuteNonQueryAsync();
                    }

                  
                    if (categoryId.HasValue && isUserCategory)
                    {
                        using (var cmdCheck = new SQLiteCommand(
                            "SELECT COUNT(*) FROM punkty_noclegowe WHERE id_kategorii=@catId", conn, tran))
                        {
                            cmdCheck.Parameters.AddWithValue("@catId", categoryId.Value);
                            var count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());

                            if (count == 0)
                            {
                                using (var cmdDelCat = new SQLiteCommand(
                                    "DELETE FROM kategorie_noclegow WHERE id_kategorii=@catId", conn, tran))
                                {
                                    cmdDelCat.Parameters.AddWithValue("@catId", categoryId.Value);
                                    await cmdDelCat.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                    tran.Commit();
                }
            }

           
            foreach (var expander in FindVisualChildren<Expander>(this))
            {
                var cmb = expander.FindName("cmbEditCategory") as ComboBox;
                if (cmb != null)
                {
                    cmb.Items.Clear();
                    cmbEditAccommodationCategory_Loaded(cmb, null);
                }
            }
        }


  
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }



        private void BtnPickAccommodationFromMap_Click(object sender, RoutedEventArgs e)
        {
            var acc = (sender as FrameworkElement)?.DataContext as AccommodationDisplay;
            if (acc == null) return;

            if (Owner is MapWindow map)
            {
                map.IsAddingPoint = true;
                map.CurrentTripDetailsWindow = this;
                isSelectingAccommodation = true;
                editingAccommodation = acc;

                this.Hide();
                MapOwner?.Activate();

                MessageBox.Show("Kliknij na mapie, aby ustawić nowy adres noclegu.", "Wybierz punkt");
            }
        }

        public void SetAccommodationPointFromMap(double lat, double lng, string fullAddress)
        {
            if (!isSelectingAccommodation || editingAccommodation == null)
                return;

            long newPointId = SaveAccommodationPoint(fullAddress, lat, lng);

            editingAccommodation.IdPunktu = newPointId;
            editingAccommodation.Adres = fullAddress;

            isSelectingAccommodation = false;

            MessageBox.Show("Adres noclegu zaktualizowany!");

            this.Show();
            this.Activate();
        }

        private long SaveAccommodationPoint(string address, double lat, double lng)
        {
           
            long pointId = 0;
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"
            INSERT INTO punkty_noclegowe (nazwa, adres, szerokosc, dlugosc, id_kategorii)
            VALUES (@name, @address, @lat, @lng, NULL);
            SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@name", address);
                    cmd.Parameters.AddWithValue("@address", address);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);

                    pointId = (long)cmd.ExecuteScalar();
                }
            }

            return pointId;
        }

        private async Task UpdateAccommodationAsync(AccommodationDisplay a)
        {

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SQLiteCommand(@"
            UPDATE noclegi
            SET id_punktu=@idPunktu,
                data_od=@od,
                data_do=@do,
                cena=@cena,
              waluta=@waluta,
                ocena=@ocena,
                uwagi=@uwagi
            WHERE id_noclegu=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", a.IdNoclegu);
                    cmd.Parameters.AddWithValue("@idPunktu", a.IdPunktu);
                    cmd.Parameters.AddWithValue("@od", a.DataOdDate?.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@do", a.DataDoDate?.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@cena", a.Cena);
                    cmd.Parameters.AddWithValue("@waluta", a.Waluta);
                    cmd.Parameters.AddWithValue("@ocena", a.Ocena);
                    cmd.Parameters.AddWithValue("@uwagi", a.Uwagi);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

     
        private void cmbEditAccommodationCategory_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;

            combo.Items.Clear();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT id_kategorii, nazwa, ikona FROM kategorie_noclegow ORDER BY nazwa", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        combo.Items.Add(new ComboBoxItem
                        {
                            Content = reader["nazwa"].ToString(),
                            Tag = new CategoryItem
                            {
                                Id = Convert.ToInt32(reader["id_kategorii"]),
                                Icon = reader["ikona"].ToString()
                            }
                        });
                    }
                }
            }
        }

  
        private int EnsureAccommodationCategoryExists(string categoryName)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

              
                using (var check = new SQLiteCommand("SELECT id_kategorii FROM kategorie_noclegow WHERE nazwa=@name", conn))
                {
                    check.Parameters.AddWithValue("@name", categoryName);
                    var result = check.ExecuteScalar();
                    if (result != null) return Convert.ToInt32(result);
                }

              
                using (var insert = new SQLiteCommand("INSERT INTO kategorie_noclegow (nazwa, ikona, czy_domyslna) VALUES (@name, 'default.png', 0); SELECT last_insert_rowid();", conn))
                {
                    insert.Parameters.AddWithValue("@name", categoryName);
                    return Convert.ToInt32(insert.ExecuteScalar());
                }
            }
        }


        private bool ValidateAccommodationDates(DateTime startDate, DateTime endDate)
        {
            DateTime min = tripStartDate.Date.AddDays(-10);
            DateTime max = tripEndDate.Date.AddDays(10);

            if (startDate < min || endDate > max)
            {
                MessageBox.Show($"Daty noclegu muszą być w przedziale {min:dd.MM.yyyy} – {max:dd.MM.yyyy}");
                return false;
            }

            if (endDate < startDate)
            {
                MessageBox.Show("Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.");
                return false;
            }

            return true;
        }


        private void dpFrom_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            var dp = sender as DatePicker;
            if (dp?.SelectedDate == null) return;

            DateTime selected = dp.SelectedDate.Value.Date;
            DateTime min = tripStartDate.Date.AddDays(-10);
            DateTime max = tripEndDate.Date.AddDays(10);

            if (selected < min)
            {
                MessageBox.Show($"Data rozpoczęcia nie może być wcześniejsza niż {min:dd.MM.yyyy}");
                dp.SelectedDate = min;
            }
            else if (selected > max)
            {
                MessageBox.Show($"Data rozpoczęcia nie może być późniejsza niż {max:dd.MM.yyyy}");
                dp.SelectedDate = max;
            }
        }

        private void dpTo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            var dp = sender as DatePicker;
            if (dp?.SelectedDate == null) return;

            DateTime selected = dp.SelectedDate.Value.Date;
            DateTime min = tripStartDate.Date.AddDays(-10);
            DateTime max = tripEndDate.Date.AddDays(10);

            if (selected < min)
            {
                MessageBox.Show($"Data zakończenia nie może być wcześniejsza niż {min:dd.MM.yyyy}");
                dp.SelectedDate = min;
            }
            else if (selected > max)
            {
                MessageBox.Show($"Data zakończenia nie może być późniejsza niż {max:dd.MM.yyyy}");
                dp.SelectedDate = max;
            }
        }



        private bool IsAccommodationDateAvailable(DateTime checkIn, DateTime checkOut, long? excludeAccommodationId = null)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                string query = @"
            SELECT COUNT(*) 
            FROM noclegi
            WHERE id_podrozy = @trip
              AND NOT (data_do <= @checkIn OR data_od >= @checkOut)";

                if (excludeAccommodationId.HasValue)
                {
                    query += " AND id_noclegu != @excludeId";
                }

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@trip", tripId);
                    cmd.Parameters.AddWithValue("@checkIn", checkIn.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@checkOut", checkOut.ToString("yyyy-MM-dd"));

                    if (excludeAccommodationId.HasValue)
                        cmd.Parameters.AddWithValue("@excludeId", excludeAccommodationId.Value);

                    long count = (long)cmd.ExecuteScalar();
                    return count == 0;
                }
            }
        }

        // koniec NOCLEGI



        /// wydatki

        public class FrankfurterResponse
        {
            public string @base { get; set; }
            public Dictionary<string, double> rates { get; set; }
        }

        public async Task<Dictionary<string, double>> GetExchangeRatesAsync(string baseCurrency)
        {
            using (var client = new HttpClient())
            {
                string url = $"https://api.frankfurter.app/latest?from={baseCurrency}";
                var json = await client.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<FrankfurterResponse>(json);

        
                if (!data.rates.ContainsKey(baseCurrency))
                    data.rates[baseCurrency] = 1;

                return data.rates;
            }
        }




        private async Task LoadRatesAsync()
        {
            rates = await GetExchangeRatesAsync("PLN"); 

        }



        public double ConvertToTarget(double amount, string fromCurrency, string targetCurrency, Dictionary<string, double> rates)
        {
            if (fromCurrency == targetCurrency) return Math.Round(amount, 2);

            double fromRate = rates.ContainsKey(fromCurrency) ? rates[fromCurrency] : 1;
            double toRate = rates.ContainsKey(targetCurrency) ? rates[targetCurrency] : 1;

            double converted = amount / fromRate * toRate;
            return Math.Round(converted, 2); 
        }



        private CultureInfo GetCultureForCurrency(string currency)
        {
            switch (currency)
            {
                case "PLN":
                    return CultureInfo.GetCultureInfo("pl-PL");
                case "EUR":
                    return CultureInfo.GetCultureInfo("fr-FR"); 
                case "USD":
                    return CultureInfo.GetCultureInfo("en-US");
                default:
                    return CultureInfo.InvariantCulture;
            }
        }

        public void LoadExpenses()
        {
            
            double total = 0;

            Dictionary<string, double> expensesByCategory = new Dictionary<string, double>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

               
                using (var cmd = new SQLiteCommand("SELECT cena, waluta FROM dojazdy WHERE id_podrozy=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object objVal = reader["cena"];
                            double val = objVal != DBNull.Value ? Convert.ToDouble(objVal) : 0;

                            string cur = reader["waluta"] != DBNull.Value ? reader["waluta"].ToString() : "PLN";

                            double converted = ConvertToTarget(val, cur, selectedCurrency, rates);
                            total += converted;

                            if (!expensesByCategory.ContainsKey("Dojazd"))
                                expensesByCategory["Dojazd"] = 0;
                            expensesByCategory["Dojazd"] += converted;
                        }

                    }
                }


               
                using (var cmd = new SQLiteCommand("SELECT cena, waluta FROM noclegi WHERE id_podrozy=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object objVal = reader["cena"];
                            double val = objVal != DBNull.Value ? Convert.ToDouble(objVal) : 0;

                            string cur = reader["waluta"] != DBNull.Value ? reader["waluta"].ToString() : "PLN";

                            double converted = ConvertToTarget(val, cur, selectedCurrency, rates);
                            total += converted;

                            if (!expensesByCategory.ContainsKey("Nocleg"))
                                expensesByCategory["Nocleg"] = 0;
                            expensesByCategory["Nocleg"] += converted;
                        }
                    }
                }

              
                using (var cmd = new SQLiteCommand("SELECT cena, waluta FROM zwiedzanie WHERE id_podrozy=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                 
                            object objVal = reader["cena"];
                            double val = objVal != DBNull.Value ? Convert.ToDouble(objVal) : 0;

                          
                            string cur = reader["waluta"] != DBNull.Value ? reader["waluta"].ToString() : "PLN";

                           
                            double converted = ConvertToTarget(val, cur, selectedCurrency, rates);
                            total += converted;

                            if (!expensesByCategory.ContainsKey("Zwiedzanie"))
                                expensesByCategory["Zwiedzanie"] = 0;
                            expensesByCategory["Zwiedzanie"] += converted;
                        }
                    }
                }


            }

            
            var culture = GetCultureForCurrency(selectedCurrency);
            txtTotalExpenses.Text = total.ToString("C2", culture);

            spExpensesByCategory.Children.Clear();
            foreach (var kvp in expensesByCategory)
            {
                spExpensesByCategory.Children.Add(new TextBlock
                {
                    Text = $"{kvp.Key}: {kvp.Value.ToString("C2", culture)}",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }


            
            spExpensesByCategory.Children.Clear();
            foreach (var kvp in expensesByCategory)
            {
                spExpensesByCategory.Children.Add(new TextBlock
                {
                    Text = $"{kvp.Key}: {kvp.Value.ToString("C2", culture)}",

                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }

            
            var series = new SeriesCollection();
            foreach (var kvp in expensesByCategory)
            {
                series.Add(new PieSeries
                {
                    Title = $"{kvp.Key} ({selectedCurrency})",
                    Values = new ChartValues<double> { kvp.Value },
                    DataLabels = true,
                    LabelPoint = chartPoint => chartPoint.Y.ToString("C2", culture)
                });

            }

           
            pieChartExpenses.Series = series;
        }

        private async void cbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCurrency.SelectedItem is ComboBoxItem item)
            {
                selectedCurrency = item.Content.ToString();

              
                await LoadRatesAsync();

              
                LoadExpenses();
                LoadDetailedExpenses();
            }
        }



        public void LoadDetailedExpenses()
        {
            
            var culture = GetCultureForCurrency(selectedCurrency);

           
            Dictionary<string, double> sightseeing = new Dictionary<string, double>();
            Dictionary<string, double> lodging = new Dictionary<string, double>();
            Dictionary<string, double> transport = new Dictionary<string, double>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

             
                using (var cmd = new SQLiteCommand(@"
            SELECT k.nazwa, z.cena, z.waluta
            FROM zwiedzanie z
            JOIN punkty_zwiedzania p ON z.id_punktu = p.id_punktu
            JOIN kategorie k ON p.id_kategorii = k.id_kategorii
            WHERE z.id_podrozy = @id
        ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sub = reader.GetString(0);
                            double val = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader[1]);

                            string cur = reader.IsDBNull(2) ? "PLN" : reader.GetString(2);

                            double converted = ConvertToTarget(val, cur, selectedCurrency, rates);
                            if (converted > 0)
                                sightseeing[sub] = sightseeing.ContainsKey(sub) ? sightseeing[sub] + converted : converted;
                        }
                    }
                }

                
                using (var cmd = new SQLiteCommand(@"
            SELECT k.nazwa, n.cena, n.waluta
            FROM noclegi n
            JOIN punkty_noclegowe p ON n.id_punktu = p.id_punktu
            JOIN kategorie_noclegow k ON p.id_kategorii = k.id_kategorii
            WHERE n.id_podrozy = @id
        ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sub = reader.GetString(0);
                            double val = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader[1]);

                            string cur = reader.IsDBNull(2) ? "PLN" : reader.GetString(2);

                            double converted = ConvertToTarget(val, cur, selectedCurrency, rates);
                            if (converted > 0)
                                lodging[sub] = lodging.ContainsKey(sub) ? lodging[sub] + converted : converted;
                        }
                    }
                }

              
                using (var cmd = new SQLiteCommand(@"
            SELECT srodek_transportu, cena, waluta
            FROM dojazdy
            WHERE id_podrozy = @id
        ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", tripId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sub = reader.GetString(0);
                            double val = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader[1]);
                            string cur = reader.IsDBNull(2) ? "PLN" : reader.GetString(2);

                            double converted = ConvertToTarget(val, cur, selectedCurrency, rates);
                            if (converted > 0)
                                transport[sub] = transport.ContainsKey(sub) ? transport[sub] + converted : converted;
                        }
                    }
                }
            }

          

           
            var series = new SeriesCollection();

            foreach (var kvp in sightseeing)
                series.Add(new PieSeries
                {
                    Title = $"Zwiedzanie: {kvp.Key} ({selectedCurrency})",
                    Values = new ChartValues<double> { kvp.Value },
                    DataLabels = true,
                    LabelPoint = chartPoint => FormatCurrency(chartPoint.Y)
                });

            foreach (var kvp in lodging)
                series.Add(new PieSeries
                {
                    Title = $"Nocleg: {kvp.Key} ({selectedCurrency})",
                    Values = new ChartValues<double> { kvp.Value },
                    DataLabels = true,
                    LabelPoint = chartPoint => FormatCurrency(chartPoint.Y)
                });

            foreach (var kvp in transport)
                series.Add(new PieSeries
                {
                    Title = $"Transport: {kvp.Key} ({selectedCurrency})",
                    Values = new ChartValues<double> { kvp.Value },
                    DataLabels = true,
                    LabelPoint = chartPoint => FormatCurrency(chartPoint.Y)
                });

            pieChartDetailedExpenses.Series = series;
        }

        private string FormatCurrency(double amount)
        {
            var culture = GetCultureForCurrency(selectedCurrency);
            return amount.ToString("C2", culture); 
        }



        /// koniec wydatkow


        public class VisitPointDisplay : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }

            public long IdZwiedzania { get; set; }
            public long IdPunktu { get; set; }
            public string Kategoria { get; set; }

            private string adres;
            public string Adres
            {
                get => adres;
                set
                {
                    adres = value;
                    OnPropertyChanged(nameof(Adres));
                    OnPropertyChanged(nameof(Naglowek)); 
                }
            }

            private DateTime? dataZwiedzaniaDate;
            public DateTime? DataZwiedzaniaDate
            {
                get => dataZwiedzaniaDate;
                set
                {
                    dataZwiedzaniaDate = value;
                    OnPropertyChanged(nameof(DataZwiedzaniaDate));
                    OnPropertyChanged(nameof(Naglowek));
                }
            }

            public string Uwagi { get; set; }
            private decimal cena = 0;
            public decimal CenaValue
            {
                get => cena;
                set { cena = value; OnPropertyChanged(nameof(CenaValue)); OnPropertyChanged(nameof(CenaString)); }
            }

            private string waluta = "PLN";
            public string Waluta
            {
                get => waluta;
                set { waluta = value; OnPropertyChanged(nameof(Waluta)); }
            }

            public string CenaString
            {
                get => CenaValue.ToString("F2", CultureInfo.InvariantCulture);
                set
                {
                    if (decimal.TryParse(
                            value,
                            NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture,
                            out var val))
                    {
                        CenaValue = val;
                    }
                  
                    
                }
            }


            public string Ocena { get; set; }
            public int IdKategorii { get; set; }


            public string Naglowek => string.IsNullOrWhiteSpace(Kategoria)
    ? $"{Adres} ({DataZwiedzaniaDate?.ToString("dd.MM.yyyy") ?? ""})"
    : $"{Kategoria}: {Adres} ({DataZwiedzaniaDate?.ToString("dd.MM.yyyy") ?? ""})";

        }
        
        public class AccommodationDisplay : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

            public long IdNoclegu { get; set; }
            public long IdPunktu { get; set; }

            private string kategoria;
            public string Kategoria
            {
                get => kategoria;
                set { kategoria = value; OnPropertyChanged(nameof(Kategoria)); }
            }

            private string nazwa;
            public string Nazwa
            {
                get => nazwa;
                set { nazwa = value; OnPropertyChanged(nameof(Nazwa)); }
            }

            private string adres;
            public string Adres
            {
                get => adres;
                set { adres = value; OnPropertyChanged(nameof(Adres)); }
            }

            
            public string DataOd { get; set; }
            public string DataDo { get; set; }

            private decimal cena;

            public decimal Cena
            {
                get => cena;
                set
                {
                    cena = value;
                    OnPropertyChanged(nameof(Cena));
                    OnPropertyChanged(nameof(CenaString));
                }
            }

            private string waluta = "PLN";
            public string Waluta
            {
                get => waluta;
                set { waluta = value; OnPropertyChanged(nameof(Waluta)); }
            }

          
            public List<string> DostepneWaluty { get; set; } = new List<string> { "PLN", "EUR", "USD" };

          
            private DateTime? dataOdDate;
            public DateTime? DataOdDate
            {
                get
                {
                    if (dataOdDate.HasValue) return dataOdDate;
                    if (DateTime.TryParse(DataOd, out var dt))
                    {
                        dataOdDate = dt;
                        return dt;
                    }
                    return null;
                }
                set
                {
                    dataOdDate = value;
                    DataOd = value?.ToString("yyyy-MM-dd") ?? "";
                    OnPropertyChanged(nameof(DataOd));
                }
            }

            private DateTime? dataDoDate;
            public DateTime? DataDoDate
            {
                get
                {
                    if (dataDoDate.HasValue) return dataDoDate;
                    if (DateTime.TryParse(DataDo, out var dt))
                    {
                        dataDoDate = dt;
                        return dt;
                    }
                    return null;
                }
                set
                {
                    dataDoDate = value;
                    DataDo = value?.ToString("yyyy-MM-dd") ?? "";
                    OnPropertyChanged(nameof(DataDo));
                }
            }


            public string CenaString
            {
                get => Cena.ToString("F2", CultureInfo.InvariantCulture);
                set
                {
                    if (decimal.TryParse(
                            value,
                            NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture,
                            out var val))
                    {
                        Cena = val;
                    }
                 
                }
            }


            private string uwagi;
            public string Uwagi
            {
                get => uwagi;
                set { uwagi = value; OnPropertyChanged(nameof(Uwagi)); }
            }

            private int ocena;
            public int Ocena
            {
                get => ocena;
                set { ocena = value; OnPropertyChanged(nameof(Ocena)); }
            }
            

            private int idKategorii;
            public int IdKategorii
            {
                get => idKategorii;
                set { idKategorii = value; OnPropertyChanged(nameof(IdKategorii)); }
            }
            public string Naglowek
            {
                get
                {
                    string prefix;

                    if (!string.IsNullOrWhiteSpace(Nazwa) && !string.IsNullOrWhiteSpace(Kategoria))
                        prefix = $"{Nazwa} – {Kategoria}";
                    else if (!string.IsNullOrWhiteSpace(Nazwa))
                        prefix = Nazwa;
                    else if (!string.IsNullOrWhiteSpace(Kategoria))
                        prefix = Kategoria;
                    else
                        prefix = "";

                    string dataOdStr = DataOdDate?.ToString("dd.MM.yyyy") ?? "";
                    string dataDoStr = DataDoDate?.ToString("dd.MM.yyyy") ?? "";

                    string data = string.IsNullOrEmpty(dataOdStr) && string.IsNullOrEmpty(dataDoStr)
                        ? ""
                        : $"{dataOdStr} – {dataDoStr}";

                    return string.IsNullOrWhiteSpace(prefix)
                        ? $"{Adres} ({data})"
                        : $"{prefix}: {Adres} ({data})";
                }
            }



        }


        public class RouteDisplay : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
          

            public long IdDojazdu { get; set; }
            public long IdStart { get; set; }
            public long IdKoniec { get; set; }
            public long Kolejnosc { get; set; }
            public string Naglowek { get; set; }
            public string Transport { get; set; }
            public string Przewoznik { get; set; }
            public string NumerRejsu { get; set; }
            public string DataWyjazdu { get; set; }
            public string DataPrzyjazdu { get; set; }

            private DateTime? dataWyjazduDate;
            public DateTime? DataWyjazduDate
            {
                get
                {
                    if (dataWyjazduDate.HasValue) return dataWyjazduDate;
                    if (string.IsNullOrEmpty(DataWyjazdu)) return null;

                    if (DateTime.TryParseExact(DataWyjazdu,
                        "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                    {
                        dataWyjazduDate = dt;
                        return dt;
                    }

                    return null;
                }
                set
                {
                    dataWyjazduDate = value;
                    DataWyjazdu = value?.ToString("yyyy-MM-dd HH:mm") ?? "";
                    OnPropertyChanged(nameof(DataWyjazdu));
                }
            }

            private DateTime? dataPrzyjazduDate;
            public DateTime? DataPrzyjazduDate
            {
                get
                {
                    if (dataPrzyjazduDate.HasValue) return dataPrzyjazduDate;
                    if (string.IsNullOrEmpty(DataPrzyjazdu)) return null;

                    if (DateTime.TryParseExact(DataPrzyjazdu,
                        "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                    {
                        dataPrzyjazduDate = dt;
                        return dt;
                    }

                    return null;
                }
                set
                {
                    dataPrzyjazduDate = value;
                    DataPrzyjazdu = value?.ToString("yyyy-MM-dd HH:mm") ?? "";
                    OnPropertyChanged(nameof(DataPrzyjazdu));
                }
            }

            private string startAddress;
            public string StartAddress
            {
                get => startAddress;
                set
                {
                    if (startAddress != value)
                    {
                        startAddress = value;
                        OnPropertyChanged(nameof(StartAddress));
                    }
                }
            }

            private string endAddress;
            public string EndAddress
            {
                get => endAddress;
                set
                {
                    if (endAddress != value)
                    {
                        endAddress = value;
                        OnPropertyChanged(nameof(EndAddress));
                    }
                }
            }



            private decimal cenaDecimal;
            public decimal CenaDecimal
            {
                get => cenaDecimal;
                set { cenaDecimal = value; OnPropertyChanged(nameof(CenaDecimal)); OnPropertyChanged(nameof(CenaString)); }
            }

            public string CenaString
            {
                get => CenaDecimal.ToString("F2", CultureInfo.InvariantCulture);
                set
                {
                    if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        CenaDecimal = parsed;

                    OnPropertyChanged(nameof(CenaDecimal));
                }
            }

          
            private string waluta = "PLN";
            public string Waluta
            {
                get => waluta;
                set { waluta = value; OnPropertyChanged(nameof(Waluta)); }
            }

            public List<string> DostepneWaluty { get; set; } =
                new List<string> { "PLN", "EUR", "USD" };

            public string Uwagi { get; set; }
            public long StartPointId { get; set; }
            public long EndPointId { get; set; }
        }
    }
    public static class UIExtensions
    {
        public static T FindAncestor<T>(this DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T found)
                    return found;

                obj = VisualTreeHelper.GetParent(obj);
            }

            return null;
        }
    }
}
