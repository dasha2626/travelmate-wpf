using System;
using System.Data.SQLite;
using System.Device.Location;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Data.SqlClient;
using System.Net;

namespace TravelMate
{
    public partial class AddRouteWindow : Window
    {
        private long tripId;
        public double startLat, startLng, endLat, endLng;
        public string startAddress, endAddress;
        public long startPointId, endPointId;
        private DateTime tripStartDate;
        private DateTime tripEndDate;
        private int userId = -1;
        private bool startPrefilledFromHome = false;

        public TripDetailsWindow DetailsOwner { get; set; }
        public bool isSelectingStartPoint = false;
        public bool isSelectingEndPoint = false;
        public MapWindow MapOwner { get; set; }

        public AddRouteWindow(long tripId, DateTime start, DateTime end)
        {
            InitializeComponent();
            this.tripId = tripId;
            tripStartDate = start;
            tripEndDate = end;

            dpDeparture.DisplayDateStart = tripStartDate.AddDays(-10);
            dpDeparture.DisplayDateEnd = tripEndDate.AddDays(10);

            dpArrival.DisplayDateStart = tripStartDate.AddDays(-10);
            dpArrival.DisplayDateEnd = tripEndDate.AddDays(10);

            this.Closed += AddRouteWindow_Closed;
        }
        public AddRouteWindow(long tripId, int userId, DateTime start, DateTime end) : this(tripId, start, end)
        {
            this.userId = userId;

            Loaded += async (s, e) =>
            {
                await LoadStartAddressLogicAsync();
                await LoadDefaultEndAddressAsync();
            };
        }

        public AddRouteWindow(long tripId)
        {
            InitializeComponent();
            this.tripId = tripId;
            tripStartDate = DateTime.Now;
            tripEndDate = DateTime.Now.AddDays(1);
        }


        private void AddRouteWindow_Closed(object sender, EventArgs e)
        {
            if (DetailsOwner != null)
            {
                DetailsOwner.Show();   
                DetailsOwner.Activate(); 
            }

            if (MapOwner != null)
            {
                MapOwner.CurrentAddRouteWindow = null;
                MapOwner.IsAddingRoute = false;
            }
        }
        private void BtnSetStartPoint_Click(object sender, RoutedEventArgs e)
        {
            if (MapOwner != null)
            {
                MapOwner.IsAddingRoute = true;
                MapOwner.CurrentAddRouteWindow = this;

                isSelectingStartPoint = true;
                isSelectingEndPoint = false;

                this.Hide();
                MapOwner.Focus();
            }
        }


        private async Task LoadDefaultEndAddressAsync()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    string miejsce = "", miasto = "", kraj = "";

                    using (var cmd = new SQLiteCommand(
                        "SELECT miejsce, miasto, kraj FROM podroze WHERE id_podrozy = @trip", conn))
                    {
                        cmd.Parameters.AddWithValue("@trip", tripId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                miejsce = reader["miejsce"]?.ToString();
                                miasto = reader["miasto"]?.ToString();
                                kraj = reader["kraj"]?.ToString();
                            }
                        }
                    }


                    endAddress = $"{miejsce}, {miasto}, {kraj}".Trim().Trim(',');
                    txtToAddressFull.Text = endAddress;

                    // 🔽 GEOKODOWANIE
                    var geo = await GeocodeAddressAsync(endAddress);
                    endLat = geo.lat;
                    endLng = geo.lng;




                    using (var cmd2 = new SQLiteCommand(
                        "SELECT szerokosc, dlugosc FROM punkty_mapy WHERE id_podrozy = @trip AND typ='planowane' ORDER BY id_punktu LIMIT 1",
                        conn))
                    {
                        cmd2.Parameters.AddWithValue("@trip", tripId);

                        using (var reader2 = await cmd2.ExecuteReaderAsync())
                        {
                            if (await reader2.ReadAsync())
                            {
                                endLat = Convert.ToDouble(reader2["szerokosc"]);
                                endLng = Convert.ToDouble(reader2["dlugosc"]);
                                return;
                            }
                        }
                    }

                 
                   
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadDefaultEndAddressAsync error: " + ex.Message);
            }
        }



        private async Task LoadDefaultStartIfNoRoutesAsync()
        {
            try
            {
                if (userId <= 0) return;

                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    
                    using (var cmdCount = new SQLiteCommand("SELECT COUNT(*) FROM dojazdy WHERE id_podrozy = @podroz", conn))
                    {
                        cmdCount.Parameters.AddWithValue("@podroz", tripId);
                        long count = (long)(await cmdCount.ExecuteScalarAsync());
                        if (count > 0) return; 
                    }

                   
                    using (var cmdUser = new SQLiteCommand("SELECT dom_adres, dom_szerokosc, dom_dlugosc, dom_miasto, dom_kraj FROM uzytkownicy WHERE id_uzytkownika = @id", conn))
                    {
                        cmdUser.Parameters.AddWithValue("@id", userId);

                        using (var reader = await cmdUser.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var domAdres = reader["dom_adres"] == DBNull.Value ? null : reader["dom_adres"].ToString();
                                var domMiasto = reader["dom_miasto"] == DBNull.Value ? null : reader["dom_miasto"].ToString();
                                var domKraj = reader["dom_kraj"] == DBNull.Value ? null : reader["dom_kraj"].ToString();
                                var domLat = reader["dom_szerokosc"] == DBNull.Value ? 0.0 : Convert.ToDouble(reader["dom_szerokosc"]);
                                var domLng = reader["dom_dlugosc"] == DBNull.Value ? 0.0 : Convert.ToDouble(reader["dom_dlugosc"]);

                                if (!string.IsNullOrEmpty(domAdres) && domLat != 0 && domLng != 0)
                                {
                                    
                                    string fullHomeAddress = $"{domAdres}, {domMiasto}, {domKraj}".Trim().Trim(',');

                                    startAddress = fullHomeAddress;
                                    startLat = domLat;
                                    startLng = domLng;

                                    txtFromAddressFull.Text = fullHomeAddress;

                                    startPrefilledFromHome = true;
                                }

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                
                Console.WriteLine("LoadDefaultStartIfNoRoutesAsync error: " + ex.Message);
            }
        }

        private async Task LoadStartAddressLogicAsync()
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                long count = 0;

                using (var cmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM dojazdy WHERE id_podrozy = @p", conn))
                {
                    cmd.Parameters.AddWithValue("@p", tripId);
                    count = (long)(await cmd.ExecuteScalarAsync());
                }

                if (count == 0)
                {
                    await LoadDefaultStartIfNoRoutesAsync();
                }
                else
                {
                    await LoadLastRouteEndPointAsync();
                }
            }
        }

       



        private async Task LoadLastRouteEndPointAsync()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SQLiteCommand(
                        @"SELECT d.id_punktu_koniec, p.nazwa, p.szerokosc, p.dlugosc
                  FROM dojazdy d
                  JOIN punkty_dojazdow p ON p.id_punktu = d.id_punktu_koniec
                  WHERE d.id_podrozy = @trip
                  ORDER BY d.kolejnosc DESC
                  LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@trip", tripId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                long lastEndPointId = Convert.ToInt64(reader["id_punktu_koniec"]);
                                string nazwa = reader["nazwa"].ToString();
                                double lat = Convert.ToDouble(reader["szerokosc"]);
                                double lng = Convert.ToDouble(reader["dlugosc"]);

                                startAddress = nazwa;
                                txtFromAddressFull.Text = nazwa;

                                startLat = lat;
                                startLng = lng;

                                startPointId = lastEndPointId;
                                ;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadLastRouteEndPointAsync error: " + ex.Message);
            }
        }






        private void dpDeparture_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDeparture.SelectedDate.HasValue)
            {
                dpArrival.DisplayDateStart = dpDeparture.SelectedDate.Value;

                if (dpArrival.SelectedDate.HasValue && dpArrival.SelectedDate < dpDeparture.SelectedDate)
                {
                    MessageBox.Show("Data przyjazdu nie może być wcześniej niż data wyjazdu.");
                    dpArrival.SelectedDate = null;
                }
            }
        }

        private void BtnSetEndPoint_Click(object sender, RoutedEventArgs e)
        {
            if (MapOwner != null)
            {
                if (startLat == 0 && startLng == 0)
                {
                    MessageBox.Show("Najpierw ustaw punkt startowy!");
                    return;
                }

                isSelectingStartPoint = false;
                isSelectingEndPoint = true;

                MapOwner.IsAddingRoute = true;
                MapOwner.CurrentAddRouteWindow = this;

                this.Hide();
                MapOwner.Focus();

                MessageBox.Show("Kliknij na mapie punkt końcowy trasy 🚗");
            }
        }

        public void SetRouteAddress(string fullAddress, double lat, double lng)
        {
            if (isSelectingStartPoint)
            {
                startAddress = fullAddress;
                startLat = lat;
                startLng = lng;
                txtFromAddressFull.Text = fullAddress;
                isSelectingStartPoint = false;
            }
            else if (isSelectingEndPoint)
            {
                endAddress = fullAddress;
                endLat = lat;
                endLng = lng;
                txtToAddressFull.Text = fullAddress;
                isSelectingEndPoint = false;
            }

            this.Show();
        }

        public void SaveRoutePoints()
        {
           

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                if (startPointId == 0
                  && !string.IsNullOrEmpty(startAddress)
                  && startLat != 0 && startLng != 0)

                {
                    using (var cmd = new SQLiteCommand("INSERT INTO punkty_dojazdow (nazwa, szerokosc, dlugosc) VALUES (@n, @lat, @lng); SELECT last_insert_rowid();", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", startAddress);
                        cmd.Parameters.AddWithValue("@lat", startLat);
                        cmd.Parameters.AddWithValue("@lng", startLng);
                        startPointId = (long)cmd.ExecuteScalar();
                    }
                }

                if (endPointId == 0 && !string.IsNullOrEmpty(endAddress) && endLat != 0 && endLng != 0)
                {
                    using (var cmd = new SQLiteCommand("INSERT INTO punkty_dojazdow (nazwa, szerokosc, dlugosc) VALUES (@n, @lat, @lng); SELECT last_insert_rowid();", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", endAddress);
                        cmd.Parameters.AddWithValue("@lat", endLat);
                        cmd.Parameters.AddWithValue("@lng", endLng);
                        endPointId = (long)cmd.ExecuteScalar();
                    }
                }
            }
        }


        public async void SetPointFromMap(double lat, double lng, string fullAddress)
        {
            if (isSelectingStartPoint)
            {
                startLat = lat;
                startLng = lng;
                startAddress = fullAddress;
                txtFromAddressFull.Text = fullAddress;

                startPointId = await SaveSinglePoint(fullAddress, lat, lng);
                isSelectingStartPoint = false;

                MapOwner.IsAddingRoute = false;
                MapOwner.CurrentAddRouteWindow = null;

                MessageBox.Show("Punkt startowy ustawiony!");
                this.Show();
                return;
            }

            if (isSelectingEndPoint)
            {
                endLat = lat;
                endLng = lng;
                endAddress = fullAddress;
                txtToAddressFull.Text = fullAddress;

                endPointId = await SaveSinglePoint(fullAddress, lat, lng);
                isSelectingEndPoint = false;

                DrawRouteOnMap();

                MapOwner.IsAddingRoute = false;
                MapOwner.CurrentAddRouteWindow = null;

                MessageBox.Show("Punkt końcowy ustawiony i trasa wyznaczona!");
                this.Show();
            }
        }

        private async Task<long> SaveSinglePoint(string address, double lat, double lng)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SQLiteCommand("INSERT INTO punkty_dojazdow (nazwa, szerokosc, dlugosc) VALUES (@n, @lat, @lng); SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@n", address);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);
                    return (long)(await cmd.ExecuteScalarAsync());
                }
            }
        }
        private void DrawRouteOnMap()
        {
            if (MapOwner == null) return;

            var points = new List<PointLatLng>
    {
        new PointLatLng(startLat, startLng),
        new PointLatLng(endLat, endLng)
    };

            var route = new GMapRoute(points)
            {
                Shape = new Path
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 3
                }
            };

            MapOwner.MainMap.Markers.Add(route);

        }



        private bool TryParseTime(string input, out TimeSpan result)
        {
            return TimeSpan.TryParseExact(input, @"hh\:mm", null, out result);
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


        private async Task InsertRouteAsync()
        {
            string transport = (cmbTransport.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string waluta = (cmbCurrency.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PLN";


            if (!TryParseTime(txtDepartureTime.Text, out TimeSpan depTime))
            {
                MessageBox.Show("Nieprawidłowa godzina wyjazdu (użyj HH:MM).");
                return;
            }

            if (!TryParseTime(txtArrivalTime.Text, out TimeSpan arrTime))
            {
                MessageBox.Show("Nieprawidłowa godzina przyjazdu (użyj HH:MM).");
                return;
            }

            if (!dpDeparture.SelectedDate.HasValue || !dpArrival.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz obie daty.");
                return;
            }

            DateTime departureDateTime = dpDeparture.SelectedDate.Value.Add(depTime);
            DateTime arrivalDateTime = dpArrival.SelectedDate.Value.Add(arrTime);

            string departureString = departureDateTime.ToString("yyyy-MM-dd HH:mm");
            string arrivalString = arrivalDateTime.ToString("yyyy-MM-dd HH:mm");

            string przewoznik = txtCarrier.Text.Trim();
            string numerRejsu = txtRouteNumber.Text.Trim();

            double.TryParse(txtPrice.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double cena);

            string uwagi = txtNotes.Text.Trim();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();

                long kolejnyEtap = 1;

                using (var cmdOrder = new SQLiteCommand("SELECT IFNULL(MAX(kolejnosc), 0) + 1 FROM dojazdy WHERE id_podrozy = @podroz", conn))
                {
                    cmdOrder.Parameters.AddWithValue("@podroz", tripId);
                    kolejnyEtap = (long)cmdOrder.ExecuteScalar();
                }

                using (var cmd = new SQLiteCommand(@"INSERT INTO dojazdy (id_podrozy, id_punktu_start, id_punktu_koniec, srodek_transportu, przewoznik, numer_rejsu, data_wyjazdu, data_przyjazdu, cena, waluta, uwagi, kolejnosc) VALUES (@podroz, @start, @koniec, @transport, @przewoznik, @numer, @wyjazd, @przyjazd, @cena, @waluta, @uwagi, @kolejnosc)", conn))
                {
                    cmd.Parameters.AddWithValue("@podroz", tripId);
                    cmd.Parameters.AddWithValue("@start", startPointId);
                    cmd.Parameters.AddWithValue("@koniec", endPointId);
                    cmd.Parameters.AddWithValue("@transport", transport);
                    cmd.Parameters.AddWithValue("@przewoznik", przewoznik);
                    cmd.Parameters.AddWithValue("@numer", numerRejsu);
                    cmd.Parameters.AddWithValue("@wyjazd", departureString);
                    cmd.Parameters.AddWithValue("@przyjazd", arrivalString);
                    cmd.Parameters.AddWithValue("@cena", cena);
                    cmd.Parameters.AddWithValue("@waluta", waluta);
                    cmd.Parameters.AddWithValue("@uwagi", uwagi);
                    cmd.Parameters.AddWithValue("@kolejnosc", kolejnyEtap);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
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
        private async Task<(double lat, double lng)> GeocodeAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return (0, 0);

            try
            {
                string url =
                    $"https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(address)}" +
                    $"&format=json&limit=1";

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add(
                        "User-Agent", "TravelMateApp/1.0");

                    var response = await client.GetStringAsync(url);
                    var json = JArray.Parse(response);

                    if (!json.Any())
                        return (0, 0);

                    double lat = double.Parse(
                        json[0]["lat"].ToString(),
                        System.Globalization.CultureInfo.InvariantCulture);

                    double lng = double.Parse(
                        json[0]["lon"].ToString(),
                        System.Globalization.CultureInfo.InvariantCulture);

                    return (lat, lng);
                }
            }
            catch
            {
                return (0, 0);
            }
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

        private void txtPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox tb = sender as TextBox;
            char c = e.Text[0];

            // Pozwalamy tylko cyfry i kropkę
            if (!char.IsDigit(c) && c != '.')
            {
                e.Handled = true;
                return;
            }

            // Pozwalamy tylko jedną kropkę
            if (c == '.' && tb.Text.Contains('.'))
            {
                e.Handled = true;
                return;
            }

            // Sprawdzamy, jaki będzie tekst po wstawieniu nowego znaku
            string newText = tb.Text.Substring(0, tb.SelectionStart) + c + tb.Text.Substring(tb.SelectionStart);

            string[] parts = newText.Split('.');

            // Jeżeli przed kropką są dokładnie dwie cyfry, pierwsza nie może być 0
            if (parts[0].Length == 2 && parts[0][0] == '0')
            {
                e.Handled = true;
                return;
            }

            // Po kropce max 2 cyfry
            if (parts.Length == 2 && parts[1].Length > 2)
            {
                e.Handled = true;
                return;
            }

            e.Handled = false;
        }

        private void txtPrice_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Blokujemy spację
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void txtPrice_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            tb.SelectionStart = 0;
            tb.SelectionLength = tb.Text.Length;
        }



        private async void BtnSaveRoute_Click(object sender, RoutedEventArgs e)
        {
           

            if (string.IsNullOrEmpty(startAddress) || string.IsNullOrEmpty(endAddress))
            {
                MessageBox.Show("Ustaw oba punkty na mapie przed zapisaniem!");
                return;
            }

            if (!dpDeparture.SelectedDate.HasValue || !dpArrival.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz daty.");
                return;
            }

            if (!TryParseTime(txtDepartureTime.Text, out TimeSpan depTime))
            {
                MessageBox.Show("Niepoprawna godzina wyjazdu (format HH:MM)");
                return;
            }

            if (!TryParseTime(txtArrivalTime.Text, out TimeSpan arrTime))
            {
                MessageBox.Show("Niepoprawna godzina przyjazdu (format HH:MM)");
                return;
            }

            DateTime departure = dpDeparture.SelectedDate.Value.Add(depTime);
            DateTime arrival = dpArrival.SelectedDate.Value.Add(arrTime);

            if (!ValidateRouteDateTimes(departure, arrival))
                return;
            if (!string.IsNullOrEmpty(startAddress) && (startLat == 0 || startLng == 0))
            {
                var geo = await GeocodeAddressAsync(startAddress);
                startLat = geo.lat;
                startLng = geo.lng;
            }

            if (!string.IsNullOrEmpty(endAddress) && (endLat == 0 || endLng == 0))
            {
                var geo = await GeocodeAddressAsync(endAddress);
                endLat = geo.lat;
                endLng = geo.lng;
            }
            SaveRoutePoints();

            await InsertRouteAsync();

            MessageBox.Show("Dojazd zapisany pomyślnie.");
           
            MapOwner?.ClearTemporaryRoutes();

            MapOwner?.LoadRoutesOnMap(tripId);
            MapOwner?.ClearTemporaryRoutes();

            DetailsOwner?.LoadRoutes();
            DetailsOwner?.Show();
            DetailsOwner?.LoadExpenses();

            this.Hide();
        }


    }
}
