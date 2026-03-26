using System;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace TravelMate
{
    public partial class AddTripWindow : Window
    {
 
        private int userId;
        private double latitude;
        private double longitude;
        private string tripName;
        private long? tripId = null;
        private bool isEditing = false;
        private long? existingTripId = null;

        public event Action TripAdded;
        private bool isFromCalendarEmptyDay = false;
        public bool EnableLocationSelection { get; set; } = false;
        public event Action<double, double> LocationSelected;

        private void BtnSetLocation_Click(object sender, RoutedEventArgs e)
        {
            if (EnableLocationSelection)
            {
                LocationSelected?.Invoke(latitude, longitude);

            }
        }


        public AddTripWindow(double lat = 0, double lng = 0, int idUzytkownika = 0, string tytul = "", long? idPodrozy = null)
        {
            InitializeComponent();
            this.Closed += (s, e) =>
            {
                if (Owner is MapWindow mapWindow)
                {
                    mapWindow.ClearTemporaryMarkers();
                    mapWindow.ClearTemporaryRoutes();
                }
            };
            latitude = lat;
            longitude = lng;
            userId = idUzytkownika;
            tripName = tytul;
            existingTripId = idPodrozy;

            if (idPodrozy.HasValue)
            {
                existingTripId = idPodrozy;
                isEditing = true; 
                latitude = 0;      
                longitude = 0;
            }
            else
            {
                isEditing = false; 
            }

            if (!string.IsNullOrEmpty(tripName))
                txtTytul.Text = tripName;

            if (lat != 0 && lng != 0)
                _ = SetPlaceNameAsync(lat, lng);
            
        }
        public AddTripWindow(int userId, bool fromCalendarEmptyDay = false)
        {
            InitializeComponent();
            this.userId = userId;
            this.isFromCalendarEmptyDay = fromCalendarEmptyDay;
            


            if (isFromCalendarEmptyDay)
            {
                BtnChangeLocation.Content = "Ustaw lokalizację";
            }
            else
            {
                BtnChangeLocation.Content = "Zmień lokalizację";
            }
        }
        public AddTripWindow(double lat, double lng, int userId, bool fromCalendarEmptyDay)
    : this(lat, lng, userId)
        {
            this.isFromCalendarEmptyDay = fromCalendarEmptyDay;
            if (fromCalendarEmptyDay)
                BtnChangeLocation.Content = "Ustaw lokalizację";
            

        }

        public AddTripWindow(int userId, string connectionString, string tytul, string kraj, string miasto, string adres, DateTime start, DateTime end, int ocena, string opis) 
        {
            InitializeComponent();
            this.userId = userId;
            txtTytul.Text = tytul;
            txtKraj.Text = kraj;
            txtMiasto.Text = miasto;
            txtAdres.Text = adres;
            dpStart.SelectedDate = start;
            dpEnd.SelectedDate = end;
            cbOcena.SelectedIndex = ocena - 1;
            txtOpis.Text = opis ?? "";
            SetRatingVisibility();

        }
        private bool IsTripCompleted(DateTime start, DateTime end)
        {
            DateTime today = DateTime.Today;
            return end.Date <= today; 
        }
        private void SetRatingVisibility()
        {
            bool completed = false;

 
            if (dpStart.SelectedDate.HasValue && dpEnd.SelectedDate.HasValue)
            {
                completed = dpEnd.SelectedDate.Value.Date <= DateTime.Today;
            }


            cbOcena.IsEnabled = completed;
            cbOcena.Visibility = completed ? Visibility.Visible : Visibility.Collapsed;

            foreach (var child in ((Grid)cbOcena.Parent).Children)
            {
                if (child is TextBlock tb && tb.Text.Contains("Ocena"))
                {
                    tb.Visibility = completed ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            if (!completed)
            {
                cbOcena.SelectedIndex = -1;  
                txtOpis.Text = "";            
            }
        }


        private bool IsOverlappingTrip(DateTime start, DateTime end, long? excludeTripId = null)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT COUNT(*) 
                FROM podroze 
                WHERE id_uzytkownika=@userId 
                  AND (@start <= data_do AND @end >= data_od)";

                    if (excludeTripId.HasValue)
                        query += " AND id_podrozy<>@excludeId";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@start", start);
                        cmd.Parameters.AddWithValue("@end", end);
                        if (excludeTripId.HasValue)
                            cmd.Parameters.AddWithValue("@excludeId", excludeTripId.Value);

                        long count = (long)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch
            {
                return false; 
            }
        }



        private void SaveTrip_Click(object sender, RoutedEventArgs e)
        {
            
            if (string.IsNullOrWhiteSpace(txtTytul.Text))
            {
                MessageBox.Show("Hej! 😎 Nadaj swojej wycieczce jakąś nazwę – inaczej nikt nie będzie wiedział, gdzie jedziesz!");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtKraj.Text) || string.IsNullOrWhiteSpace(txtMiasto.Text) || string.IsNullOrWhiteSpace(txtAdres.Text))
            {
                MessageBox.Show("Proszę uzupełnić kraj, miasto i adres podróży.");
                return;
            }

            DateTime? dataOd = dpStart.SelectedDate;
            DateTime? dataDo = dpEnd.SelectedDate;

            if (!dataOd.HasValue || !dataDo.HasValue)
            {
                MessageBox.Show("Proszę wybrać daty rozpoczęcia i zakończenia podróży.");
                return;
            }

            if (dataOd.Value.Year < 1930 || dataOd.Value.Year > 2100)
            {
                MessageBox.Show("Data rozpoczęcia musi być pomiędzy 1930 a 2100 rokiem.");
                return;
            }

            if (dataDo.Value.Year < 1930 || dataDo.Value.Year > 2100)
            {
                MessageBox.Show("Data zakończenia musi być pomiędzy 1930 a 2100 rokiem.");
                return;
            }

            if (dataDo < dataOd)
            {
                MessageBox.Show("Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.");
                return;
            }

            string typWycieczki = (dataDo.Value.Date < DateTime.Now.Date) ? "odwiedzone" : "planowane";

            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    if (isEditing && existingTripId.HasValue)
                    {
                        bool hasOverlap = IsOverlappingTrip(dataOd.Value, dataDo.Value, isEditing ? existingTripId : null);
                        if (hasOverlap)
                        {
                            var result = MessageBox.Show(
                                "Uwaga! Wybrany przedział dat zachodzi na inną podróż. Czy chcesz kontynuować?",
                                "Kolizja dat",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning
                            );

                            if (result == MessageBoxResult.No)
                                return; 
                        }




                        int ocena = cbOcena.SelectedIndex >= 0 ? cbOcena.SelectedIndex + 1 : 0;

                      
                        if (!IsTripCompleted(dpStart.SelectedDate.Value, dpEnd.SelectedDate.Value))
                        {
                            ocena = 0; 
                        }

                      
                        


                        string updateTrip = @"
                        UPDATE podroze 
                        SET tytul=@tytul,
                        kraj=@kraj,
                        miasto=@miasto,
                        miejsce=@adres,
                        data_od=@dataOd,
                        data_do=@dataDo,
                        ocena=@ocena,
                        opis=@opis
                        WHERE id_podrozy=@id";

                        using (var cmd = new SQLiteCommand(updateTrip, conn))
                        {
                            cmd.Parameters.AddWithValue("@tytul", txtTytul.Text);
                            cmd.Parameters.AddWithValue("@kraj", txtKraj.Text);
                            cmd.Parameters.AddWithValue("@miasto", txtMiasto.Text);
                            cmd.Parameters.AddWithValue("@adres", txtAdres.Text);
                            cmd.Parameters.AddWithValue("@dataOd", dataOd.Value);
                            cmd.Parameters.AddWithValue("@dataDo", dataDo.Value);
                            cmd.Parameters.AddWithValue("@id", existingTripId.Value);
                            cmd.Parameters.AddWithValue("@ocena", ocena == 0 ? (object)DBNull.Value : ocena);
                            cmd.Parameters.AddWithValue("@opis", string.IsNullOrWhiteSpace(txtOpis.Text)
                                ? (object)DBNull.Value
                                : txtOpis.Text);

                            cmd.ExecuteNonQuery();
                        }

                        
                        string updatePoint = @"UPDATE punkty_mapy SET nazwa=@nazwa, typ=@typ WHERE id_podrozy=@id";
                        using (var cmd = new SQLiteCommand(updatePoint, conn))
                        {
                            cmd.Parameters.AddWithValue("@nazwa", txtTytul.Text);
                            cmd.Parameters.AddWithValue("@typ", typWycieczki);
                            cmd.Parameters.AddWithValue("@id", existingTripId.Value);
                            cmd.ExecuteNonQuery();
                        }

                        
                        if (Owner is MapWindow mapa && mapa.latitudeTemp != 0 && mapa.longitudeTemp != 0)
                        {
                            string updateLocation = @"UPDATE punkty_mapy SET szerokosc=@lat, dlugosc=@lng, nazwa=@nazwa WHERE id_podrozy=@id";
                            using (var cmd = new SQLiteCommand(updateLocation, conn))
                            {
                                cmd.Parameters.AddWithValue("@lat", mapa.latitudeTemp);
                                cmd.Parameters.AddWithValue("@lng", mapa.longitudeTemp);
                                cmd.Parameters.AddWithValue("@nazwa", txtTytul.Text);
                                cmd.Parameters.AddWithValue("@id", existingTripId.Value);
                                cmd.ExecuteNonQuery();
                            }


                            mapa.RemoveMarkersForTrip(existingTripId.Value);
                            mapa.LoadTripMarkersOnly();
                            mapa.latitudeTemp = 0;
                            mapa.longitudeTemp = 0;
                            mapa.isChangingTripLocation = false;
                            mapa.tripIdToChange = -1;
                        }

                        MessageBox.Show("Dane podróży zostały zaktualizowane!");
                        if (isFromCalendarEmptyDay)
                        {
                            BtnChangeLocation.Content = "Zmień lokalizację";
                            isFromCalendarEmptyDay = false;
                        }

                    }
                    else
                    {
                        bool hasOverlap = IsOverlappingTrip(dataOd.Value, dataDo.Value, isEditing ? existingTripId : null);

                        if (hasOverlap)
                        {
                            var result = MessageBox.Show(
                                "Uwaga! Wybrany przedział dat zachodzi na inną podróż. Czy chcesz kontynuować?",
                                "Kolizja dat",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning
                            );

                            if (result == MessageBoxResult.No)
                                return; 
                        }

                        string insertTrip = @"
    INSERT INTO podroze (tytul, kraj, miasto, miejsce, data_od, data_do, id_uzytkownika, ocena, opis) 
    VALUES (@tytul, @kraj, @miasto, @adres, @dataOd, @dataDo, @userId, @ocena, @opis)";

                        using (var cmd = new SQLiteCommand(insertTrip, conn))
                        {
                            cmd.Parameters.AddWithValue("@tytul", txtTytul.Text);
                            cmd.Parameters.AddWithValue("@kraj", txtKraj.Text);
                            cmd.Parameters.AddWithValue("@miasto", txtMiasto.Text);
                            cmd.Parameters.AddWithValue("@adres", txtAdres.Text);
                            cmd.Parameters.AddWithValue("@dataOd", dataOd.Value);
                            cmd.Parameters.AddWithValue("@dataDo", dataDo.Value);
                            cmd.Parameters.AddWithValue("@userId", userId);
                            int ocena = cbOcena.SelectedIndex >= 0 ? cbOcena.SelectedIndex + 1 : 0;
                            cmd.Parameters.AddWithValue("@ocena", ocena == 0 ? (object)DBNull.Value : ocena);
                            cmd.Parameters.AddWithValue("@opis", string.IsNullOrWhiteSpace(txtOpis.Text)
                                ? (object)DBNull.Value
                                : txtOpis.Text);

                            cmd.ExecuteNonQuery();
                        }

                        long newTripId;
                        using (var cmd = new SQLiteCommand("SELECT last_insert_rowid()", conn))
                        {
                            newTripId = (long)cmd.ExecuteScalar();
                        }

                        if (latitude != 0 && longitude != 0)
                        {
                            string insertPoint = @"
                                INSERT INTO punkty_mapy (id_podrozy, nazwa, szerokosc, dlugosc, typ) 
                                VALUES (@idPodrozy, @nazwa, @lat, @lng, @typ)";
                            using (var cmd = new SQLiteCommand(insertPoint, conn))
                            {
                                cmd.Parameters.AddWithValue("@idPodrozy", newTripId);
                                cmd.Parameters.AddWithValue("@nazwa", txtTytul.Text);
                                cmd.Parameters.AddWithValue("@lat", latitude);
                                cmd.Parameters.AddWithValue("@lng", longitude);
                                cmd.Parameters.AddWithValue("@typ", typWycieczki);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show("Podróż została dodana!");
                    }

                    TripAdded?.Invoke();

                    if (Owner is MapWindow map)  
                    {
                        map.DisableTripLocationChangeMode();

                        if (isFromCalendarEmptyDay)
                        {
                            map.tabControl.SelectedIndex = 1; 
                            isFromCalendarEmptyDay = false;
                        }
                    }


                    this.Close();

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisu podróży: {ex.Message}");
            }

            if (Owner is MapWindow mapWindow)
                mapWindow.DisableTripLocationChangeMode();
        }

        private void BtnChangeLocation_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MapWindow mapa)
            {
                mapa.tabControl.SelectedIndex = 0;
                long? id = existingTripId ?? tripId;

                
                mapa.EnableTripLocationChangeMode(id);

                
                if (isFromCalendarEmptyDay)
                {
                    isFromCalendarEmptyDay = false;
                    BtnChangeLocation.Content = "Zmień lokalizację";
                }

             
                this.Hide();
            }
        }
        private void dpEnd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            SetRatingVisibility();
        }

        private void dpStart_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpStart.SelectedDate.HasValue)
            {
                dpEnd.DisplayDateStart = dpStart.SelectedDate.Value;
                if (dpEnd.SelectedDate.HasValue && dpEnd.SelectedDate < dpStart.SelectedDate)
                {
                    MessageBox.Show("⏰ Ups! Data zakończenia nie może być przed rozpoczęciem!");
                    dpEnd.SelectedDate = null;
                }
            }
            SetRatingVisibility();
        }

        private void BtnShowRoute_Click(object sender, RoutedEventArgs e)
        {
            long idPodrozy = existingTripId ?? tripId ?? 0;

            if (idPodrozy == 0)
            {
                MessageBox.Show("Najpierw zapisz podróż, aby zobaczyć trasę.");
                return;
            }

            if (Owner is MapWindow mapWindow)
            {
                this.Hide();
                mapWindow.tabControl.SelectedIndex = 0;
                mapWindow.btnRestoreAddTrip.Visibility = Visibility.Visible;
                mapWindow.CurrentAddTripWindow = this;

                mapWindow.ClearTemporaryRoutes();
                mapWindow.ClearTemporaryMarkers();

                bool hasRoutes = mapWindow.LoadRoutesOnMap(idPodrozy);

               
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    string query = @"
                SELECT p.szerokosc, p.dlugosc, k.ikona, z.uwagi
                FROM zwiedzanie z
                INNER JOIN punkty_zwiedzania p ON z.id_punktu = p.id_punktu
                LEFT JOIN kategorie k ON p.id_kategorii = k.id_kategorii
                WHERE z.id_podrozy = @idPodrozy";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idPodrozy", idPodrozy);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                double lat = Convert.ToDouble(reader["szerokosc"]);
                                double lng = Convert.ToDouble(reader["dlugosc"]);
                                string icon = reader["ikona"]?.ToString() ?? "pin.png";
                                if (string.IsNullOrEmpty(icon))
                                    icon = "default.png";
                                string tooltip = reader["uwagi"]?.ToString() ?? "";

                                
                                string iconUri = $"pack://application:,,,/Images/{icon}";

                                var marker = new GMap.NET.WindowsPresentation.GMapMarker(new GMap.NET.PointLatLng(lat, lng))
                                {
                                    Shape = new System.Windows.Controls.Image
                                    {
                                        Width = 32,
                                        Height = 32,
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconUri, UriKind.Absolute)),
                                        ToolTip = tooltip
                                    },
                                    Offset = new System.Windows.Point(-16, -32)
                                };

                                mapWindow.MainMap.Markers.Add(marker);
                                mapWindow.TemporaryMarkers.Add(marker);
                            }
                        }
                    }
                }

                
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    string queryNoclegi = @"
                SELECT p.szerokosc, p.dlugosc, kn.ikona, n.uwagi, n.data_od, n.data_do, p.nazwa
                FROM noclegi n
                INNER JOIN punkty_noclegowe p ON n.id_punktu = p.id_punktu
                LEFT JOIN kategorie_noclegow kn ON p.id_kategorii = kn.id_kategorii
                WHERE n.id_podrozy = @idPodrozy";

                    using (var cmd = new SQLiteCommand(queryNoclegi, conn))
                    {
                        cmd.Parameters.AddWithValue("@idPodrozy", idPodrozy);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["szerokosc"] == DBNull.Value || reader["dlugosc"] == DBNull.Value)
                                    continue;

                                double lat = Convert.ToDouble(reader["szerokosc"]);
                                double lng = Convert.ToDouble(reader["dlugosc"]);
                                string icon = reader["ikona"]?.ToString();
                                string uwagi = reader["uwagi"]?.ToString();
                                string nazwa = reader["nazwa"]?.ToString();
                                string dataOd = reader["data_od"]?.ToString();
                                string dataDo = reader["data_do"]?.ToString();

                                if (string.IsNullOrEmpty(icon))
                                    icon = "default1.png";

                                string tooltip = nazwa;
                                if (!string.IsNullOrWhiteSpace(dataOd) || !string.IsNullOrWhiteSpace(dataDo))
                                {
                                    tooltip += "\n";
                                    try
                                    {
                                        DateTime od = DateTime.Parse(dataOd);
                                        DateTime do_ = DateTime.Parse(dataDo);
                                        tooltip += $"({od:dd.MM.yyyy} → {do_:dd.MM.yyyy})";
                                    }
                                    catch
                                    {
                                        tooltip += $"({dataOd} → {dataDo})";
                                    }
                                }
                                if (!string.IsNullOrWhiteSpace(uwagi))
                                    tooltip += $"\n{uwagi}";

                              
                                string iconUri = $"pack://application:,,,/Images/{icon}";

                                var marker = new GMap.NET.WindowsPresentation.GMapMarker(new GMap.NET.PointLatLng(lat, lng))
                                {
                                    Shape = new System.Windows.Controls.Image
                                    {
                                        Width = 32,
                                        Height = 32,
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconUri, UriKind.Absolute)),
                                        ToolTip = tooltip
                                    },
                                    Offset = new System.Windows.Point(-16, -32)
                                };

                                mapWindow.MainMap.Markers.Add(marker);
                                mapWindow.TemporaryMarkers.Add(marker);
                            }
                        }
                    }
                }

                if (hasRoutes)
                    MessageBox.Show("Trasa, punkty zwiedzania i noclegi zostały wyświetlone na mapie!");
                else
                    MessageBox.Show("Nie ma tras dojazdów, ale punkty zwiedzania i noclegi zostały wyświetlone.");
            }
        }


        protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
            base.OnMouseDown(e);
        }

        private async Task SetPlaceNameAsync(double lat, double lng)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}&format=json&accept-language=pl";
                    client.DefaultRequestHeaders.Add("User-Agent", "TravelMateApp/1.0 (daria@travelmate.pl)");

                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        txtAdres.Text = $"({lat:F4}, {lng:F4})";
                        return;
                    }

                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var address = json["address"];
                    txtKraj.Text = address?["country"]?.ToString() ?? "";
                    txtMiasto.Text = address?["city"]?.ToString() ?? address?["town"]?.ToString() ?? address?["village"]?.ToString() ?? "";

                    string ulica = address?["road"]?.ToString() ?? "";
                    string numer = address?["house_number"]?.ToString() ?? "";
                    string kod = address?["postcode"]?.ToString() ?? "";
                    txtAdres.Text = $"{ulica} {numer}, {kod}".Trim().Trim(',');
                }
            }
            catch
            {
                txtAdres.Text = $"({lat:F4}, {lng:F4})";
            }
        }

        private void DeleteTrip_Click(object sender, RoutedEventArgs e)
        {
            if (!existingTripId.HasValue)
            {
                MessageBox.Show("Nie można usunąć, ponieważ ta podróż nie istnieje w bazie.");
                return;
            }

            var confirm = MessageBox.Show("Czy na pewno chcesz usunąć tę podróż?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.No) return;

            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    string deletePoints = "DELETE FROM punkty_mapy WHERE id_podrozy=@id";
                    using (var cmd = new SQLiteCommand(deletePoints, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", existingTripId.Value);
                        cmd.ExecuteNonQuery();
                    }

                    string deleteTrip = "DELETE FROM podroze WHERE id_podrozy=@id";
                    using (var cmd = new SQLiteCommand(deleteTrip, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", existingTripId.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Podróż została usunięta.");
                TripAdded?.Invoke();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania: {ex.Message}");
            }
        }
        private void BtnDetails_Click(object sender, RoutedEventArgs e)
        {
            long idPodrozy = existingTripId ?? tripId ?? 0;

            if (idPodrozy == 0)
            {
                MessageBox.Show("Najpierw zapisz podróż, aby zobaczyć szczegóły.");
                return;
            }

            int currentUserId = (this.Owner as MapWindow).UserId;
            var detailsWindow = new TripDetailsWindow(idPodrozy, currentUserId);

            detailsWindow.Owner = this.Owner;

            var map = this.Owner as MapWindow;
            if (map != null)
                map.ClearTemporaryRoutes();
            map.ClearTemporaryMarkers();
            map.CurrentAddTripWindow = this;

            this.Hide();

            detailsWindow.Show();
        }



        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (Owner is MapWindow mapWindow)
            {
                mapWindow.ClearTemporaryRoutes();
                mapWindow.NotifyAddTripWindowClosed();
            }
        }


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MapWindow mapWindow)
            {
                mapWindow.DisableTripLocationChangeMode();
                mapWindow.ClearTemporaryRoutes();
            }

            this.Close();
        }


     
    }
}
