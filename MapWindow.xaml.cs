using FrankFurter;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;
using MigraDoc.DocumentObjectModel.Internals;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
 using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using TravelMate.Models;
using TravelMate.Services.Providers;
using static GMap.NET.Entity.OpenStreetMapRouteEntity;






namespace TravelMate
{

    public partial class MapWindow : Window
    {

        private double sredniaOcenaLiczbowa = 0;

        private int userId;
        public int UserId => userId;

        private List<GMapRoute> temporaryRoutes = new List<GMapRoute>();
        private Dictionary<string, double> rates = new Dictionary<string, double>();
        private string targetCurrency = "PLN";


        public List<GMapMarker> TemporaryMarkers { get; private set; } = new List<GMapMarker>();


        public bool IsAddingPoint { get; set; } = false;
        public AddVisitPointWindow CurrentAddVisitPointWindow { get; set; }

        public AddAccommodationWindow CurrentAddAccommodationWindow { get; set; }


        public bool IsAddingRoute { get; set; } = false;
        public TripDetailsWindow CurrentTripDetailsWindow { get; set; }
        public AddTripWindow CurrentAddTripWindow { get; set; }
        public AddRouteWindow CurrentAddRouteWindow { get; set; }

        private PointLatLng? routeStart = null;
        private GMapRoute currentRoute = null;
        private string loginUzytkownika = "-";
        public bool IsSelectingHome { get; set; } = false;

        public event Action<string, string, string, double, double> AddressSelected;
        public event Action<long, string, string, string, double, double> TripLocationChanged;

        private GMapMarker domMarker;

        public bool isChangingTripLocation = false;
        public long? tripIdToChange;
        public double latitudeTemp;
        public double longitudeTemp;

        private List<GMapPolygon> countryPolygons = new List<GMapPolygon>();
        private int currentYear;
        private int currentMonth;
        private DateTime? selectedCalendarDate = null;
        public TabControl MainTabControl => this.tabControl;
        public event PropertyChangedEventHandler PropertyChanged;


        private Dictionary<string, Brush> airlineColors = new Dictionary<string, Brush>
{
    { "LOT", Brushes.Red },
    { "Ryanair", Brushes.Blue },
    { "WizzAir", Brushes.Purple },
    { "EasyJet", Brushes.Orange }
};

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public void DisableTripLocationChangeMode()
        {
            isChangingTripLocation = false;
            tripIdToChange = -1;
            Cursor = Cursors.Arrow;
        }

        public MapWindow(int id)
        {
            InitializeComponent();
            dpOd.SelectedDateChanged += DpOd_SelectedDateChanged;
            dpDo.SelectedDateChanged += DpDo_SelectedDateChanged;
            userId = id;
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT login FROM uzytkownicy WHERE id_uzytkownika = @id";
                cmd.Parameters.AddWithValue("@id", userId);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    loginUzytkownika = result.ToString();
            }
            Loaded += async (s, e) =>
            {
                await LoadRatesAsync();
                OdswiezStatystyki();
            };
            InitializeMap();
            InitializeSearchMap();
            LoadTrips();

            TripLocationChanged += (tripId, kraj, miasto, adres, lat, lng) =>
            {
                OpenExistingTrip(tripId);
            };
            LoadTripsForCalendar();
            var now = DateTime.Now;
            PopulateCalendar(now.Year, now.Month);

            var today = DateTime.Now;
            currentYear = today.Year;
            currentMonth = today.Month;

            FillYearCombo();
            PopulateCalendar(currentYear, currentMonth);


            FillYearCombo();
            PopulateCalendar(currentYear, currentMonth);
            calendarGrid.Loaded += (s, e) =>
            {
                HighlightToday();
            };
            this.DataContext = this;


        }

        public MapWindow(int id, GMapControl sharedMap)
        {
            InitializeComponent();
            userId = id;
            MainMap = sharedMap;
            IsSelectingHome = true;
        }
        private void CalendarDay_MouseEnter(object sender, MouseEventArgs e)
        {
            ((Border)sender).Background = new SolidColorBrush(Color.FromRgb(230, 240, 255));
        }

        private void CalendarDay_MouseLeave(object sender, MouseEventArgs e)
        {
            ((Border)sender).Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
        }

        private void InitializeMap()
        {
            MainMap.MapProvider = GMapProviders.GoogleMap;
            MainMap.MinZoom = 2;
            MainMap.MaxZoom = 18;
            MainMap.Zoom = 6;
            MainMap.ShowCenter = false;
            MainMap.Position = new PointLatLng(52.2297, 21.0122);

            MainMap.MouseLeftButtonDown += MainMap_MouseLeftButtonDown;
        }

        private void SetCurrentLocation()
        {
            GeoCoordinateWatcher watcher = new GeoCoordinateWatcher();

            watcher.PositionChanged += (s, e) =>
            {
                var coord = e.Position.Location;

                if (!coord.IsUnknown)
                {
                    MainMap.Position = new PointLatLng(coord.Latitude, coord.Longitude);
                    MainMap.Zoom = 13;
                    watcher.Stop();
                }
            };

            watcher.Start();
        }

        private async void MainMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickedElement = e.OriginalSource as FrameworkElement;

            if (clickedElement != null && clickedElement.DataContext is GMapMarker)
                return;

            var point = e.GetPosition(MainMap);
            var latLng = MainMap.FromLocalToLatLng((int)point.X, (int)point.Y);

            var (kraj, miasto, adres) = await GetAddressPartsAsync(latLng.Lat, latLng.Lng);
            string fullAddress = await GetFullAddressAsync(latLng.Lat, latLng.Lng);



            if (CurrentAddAccommodationWindow != null && CurrentAddAccommodationWindow.IsSelectingMapPoint)
            {
                CurrentAddAccommodationWindow.SetPointFromMap(latLng.Lat, latLng.Lng, fullAddress);
                return;
            }

            if (CurrentTripDetailsWindow != null && CurrentTripDetailsWindow.isSelectingAccommodation)
            {
                CurrentTripDetailsWindow.SetAccommodationPointFromMap(latLng.Lat, latLng.Lng, fullAddress);
                return;
            }



            if (IsAddingPoint)
            {
                if (CurrentAddVisitPointWindow != null)
                {
                    CurrentAddVisitPointWindow.SetPointFromMap(latLng.Lat, latLng.Lng, fullAddress);
                    return;
                }


                if (CurrentTripDetailsWindow != null && CurrentTripDetailsWindow.isSelectingVisitPoint)
                {
                    CurrentTripDetailsWindow.SetVisitPointFromMap(latLng.Lat, latLng.Lng, fullAddress);
                    CurrentTripDetailsWindow.isSelectingVisitPoint = false;
                    IsAddingPoint = false;
                    return;
                }



            }



            if (IsAddingRoute)
            {
                if (CurrentAddRouteWindow != null)
                {
                    CurrentAddRouteWindow.SetPointFromMap(latLng.Lat, latLng.Lng, fullAddress);
                    return;
                }

                if (CurrentTripDetailsWindow != null)
                {
                    CurrentTripDetailsWindow.SetPointFromMap(latLng.Lat, latLng.Lng, fullAddress);
                    return;
                }
            }

            if (isChangingTripLocation)
            {
                latitudeTemp = latLng.Lat;
                longitudeTemp = latLng.Lng;

                if (tripIdToChange.HasValue && tripIdToChange > 0)
                {
                    using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new SQLiteCommand(
                            @"SELECT tytul, miejsce, data_od, data_do, ocena, opis
                  FROM podroze 
                  WHERE id_podrozy=@id", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", tripIdToChange);

                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string tytul = reader["tytul"].ToString();
                                    string miejsce = reader["miejsce"].ToString();
                                    DateTime? dataOd = reader["data_od"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["data_od"]);
                                    DateTime? dataDo = reader["data_do"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["data_do"]);

                                    var editWindow = new AddTripWindow(latLng.Lat, latLng.Lng, userId, tytul, tripIdToChange)
                                    {
                                        Owner = this
                                    };

                                    editWindow.txtKraj.Text = kraj;
                                    editWindow.txtMiasto.Text = miasto;
                                    editWindow.txtAdres.Text = adres;

                                    editWindow.dpStart.SelectedDate = dataOd;
                                    editWindow.dpEnd.SelectedDate = dataDo;
                                    int ocena = reader["ocena"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ocena"]);
                                    string opis = reader["opis"] == DBNull.Value ? "" : reader["opis"].ToString();


                                    if (ocena >= 1 && ocena <= 5)
                                    {

                                        editWindow.cbOcena.SelectedIndex = ocena - 1;
                                    }
                                    else
                                    {
                                        editWindow.cbOcena.SelectedIndex = -1;
                                    }

                                    editWindow.txtOpis.Text = opis;


                                    editWindow.TripAdded += () =>
                                    {
                                        LoadTripMarkersOnly();
                                        DisableTripLocationChangeMode();
                                        LoadTripsForCalendar();
                                        PopulateCalendar(currentYear, currentMonth);
                                    };

                                    editWindow.Closed += (s, e2) =>
                                    {
                                        DisableTripLocationChangeMode();
                                    };

                                    editWindow.ShowDialog();
                                }
                            }
                        }
                    }
                }
                else
                {
                    var addTripWindow = new AddTripWindow(latLng.Lat, latLng.Lng, userId, fromCalendarEmptyDay: true)
                    {
                        Owner = this
                    };

                    addTripWindow.dpStart.SelectedDate = selectedCalendarDate;
                    addTripWindow.dpEnd.SelectedDate = selectedCalendarDate;

                    addTripWindow.txtKraj.Text = kraj;
                    addTripWindow.txtMiasto.Text = miasto;
                    addTripWindow.txtAdres.Text = adres;
                    tabControl.SelectedIndex = 0;
                    addTripWindow.TripAdded += () =>
                    {
                        LoadTripMarkersOnly();
                        DisableTripLocationChangeMode();
                        LoadTripsForCalendar();
                        PopulateCalendar(currentYear, currentMonth);
                        tabControl.SelectedIndex = 1;
                    };

                    addTripWindow.ShowDialog();
                }


                return;
            }


            if (IsSelectingHome)
            {
                Cursor = Cursors.Cross;

                AddressSelected?.Invoke(kraj, miasto, adres, latLng.Lat, latLng.Lng);

                if (domMarker != null)
                {
                    MainMap.Markers.Remove(domMarker);
                    domMarker = null;
                }

                domMarker = new GMapMarker(new PointLatLng(latLng.Lat, latLng.Lng))
                {
                    Shape = new Image
                    {
                        Width = 32,
                        Height = 32,
                        Source = new BitmapImage(
                    new Uri("pack://application:,,,/images/house.png", UriKind.Absolute)
                ),
                        ToolTip = "Dom"
                    },
                    Offset = new Point(-16, -16),
                    ZIndex = int.MaxValue
                };

                MainMap.Markers.Add(domMarker);

                MessageBox.Show("Dom został ustawiony pomyślnie ✅", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                IsSelectingHome = false;
                Cursor = Cursors.Arrow;

                return;
            }


            if (!IsAddingRoute && !isChangingTripLocation && !IsSelectingHome)
            {
                var addTripWindow = new AddTripWindow(latLng.Lat, latLng.Lng, userId);

                CurrentAddTripWindow = addTripWindow;

                addTripWindow.txtKraj.Text = kraj;
                addTripWindow.txtMiasto.Text = miasto;
                addTripWindow.txtAdres.Text = adres;

                addTripWindow.TripAdded += () =>
                {
                    var tripMarkers = MainMap.Markers.Where(m => m != domMarker).ToList();

                    foreach (var marker in tripMarkers)
                        MainMap.Markers.Remove(marker);

                    LoadTripMarkersOnly();
                    LoadTripsForCalendar();
                    PopulateCalendar(currentYear, currentMonth);
                };

                addTripWindow.ShowDialog();
            }
        }

        private void CalendarDay_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            dynamic dayObj = border.DataContext;
            if (dayObj == null) return;

            int day;
            if (!int.TryParse(dayObj.DayNumber.ToString(), out day))
            {
                return;
            }
            selectedCalendarDate = new DateTime(currentYear, currentMonth, day);

            var addTrip = new AddTripWindow(userId, fromCalendarEmptyDay: true)
            {
                Owner = this
            };

            addTrip.dpStart.SelectedDate = selectedCalendarDate;
            addTrip.dpEnd.SelectedDate = selectedCalendarDate;

            addTrip.TripAdded += () =>
            {
                LoadTripMarkersOnly();
                LoadTripsForCalendar();
                PopulateCalendar(currentYear, currentMonth);
            };

            addTrip.ShowDialog();
        }


        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            int year = DateTime.Today.Year;
            int month = DateTime.Today.Month;

            cmbYear.SelectedItem = year;
            PopulateCalendar(year, month);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                HighlightToday();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void FillYearCombo()
        {
            cmbYear.Items.Clear();

            for (int year = 2000; year <= 2050; year++)
                cmbYear.Items.Add(year);

            cmbYear.SelectedItem = currentYear;
        }
        private void cmbYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbYear.SelectedItem == null) return;

            currentYear = (int)cmbYear.SelectedItem;
            PopulateCalendar(currentYear, currentMonth);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                HighlightToday();
            }), System.Windows.Threading.DispatcherPriority.Loaded);

        }
        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            currentMonth--;

            if (currentMonth < 1)
            {
                currentMonth = 12;
                currentYear--;
                cmbYear.SelectedItem = currentYear;
            }

            PopulateCalendar(currentYear, currentMonth);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                HighlightToday();
            }), System.Windows.Threading.DispatcherPriority.Loaded);

        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            currentMonth++;

            if (currentMonth > 12)
            {
                currentMonth = 1;
                currentYear++;
                cmbYear.SelectedItem = currentYear;
            }

            PopulateCalendar(currentYear, currentMonth);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                HighlightToday();
            }), System.Windows.Threading.DispatcherPriority.Loaded);

        }

        private void BtnRestoreAddTrip_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentAddTripWindow != null)
            {
                CurrentAddTripWindow.Show();
                CurrentAddTripWindow.Activate();
                btnRestoreAddTrip.Visibility = Visibility.Collapsed;
            }
        }
        public void NotifyAddTripWindowClosed()
        {
            btnRestoreAddTrip.Visibility = Visibility.Collapsed;
            CurrentAddTripWindow = null;
        }

        public async Task<string> GetFullAddressAsync(double lat, double lng)
        {
            string url =
                $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&lon={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                $"&format=json&addressdetails=1&accept-language=pl";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent",
                    "TravelMateApp/1.0 (dashtayn69@gmail.com)");

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return "Nieznany adres (API odrzuciło zapytanie)";

                string json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);

                if (data["display_name"] != null)
                    return data["display_name"].ToString();

                return "Nieznany adres";
            }
        }



        public bool LoadRoutesOnMap(long tripId)
        {
            if (MainMap == null) return false;


            var oldRoutes = MainMap.Markers.OfType<GMapRoute>().ToList();
            foreach (var r in oldRoutes)
                MainMap.Markers.Remove(r);
            temporaryRoutes.Clear();

            bool hasRoutes = false;
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(@"
            SELECT pd1.szerokosc AS startLat, pd1.dlugosc AS startLng,
                   pd2.szerokosc AS endLat, pd2.dlugosc AS endLng
            FROM dojazdy d
            JOIN punkty_dojazdow pd1 ON d.id_punktu_start = pd1.id_punktu
            JOIN punkty_dojazdow pd2 ON d.id_punktu_koniec = pd2.id_punktu
            WHERE d.id_podrozy = @tripId
        ", conn))
                {
                    cmd.Parameters.AddWithValue("@tripId", tripId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            hasRoutes = true;

                            double startLat = Convert.ToDouble(reader["startLat"]);
                            double startLng = Convert.ToDouble(reader["startLng"]);
                            double endLat = Convert.ToDouble(reader["endLat"]);
                            double endLng = Convert.ToDouble(reader["endLng"]);

                            var routePoints = new List<PointLatLng>
                    {
                        new PointLatLng(startLat, startLng),
                        new PointLatLng(endLat, endLng)
                    };

                            var gmapRoute = new GMapRoute(routePoints)
                            {
                                Shape = new Path
                                {
                                    Stroke = Brushes.Blue,
                                    StrokeThickness = 3
                                }
                            };

                            MainMap.Markers.Add(gmapRoute);
                            temporaryRoutes.Add(gmapRoute);
                        }
                    }
                }
            }

            return hasRoutes;
        }


        public void ClearTemporaryRoutes()
        {
            foreach (var route in temporaryRoutes)
                MainMap.Markers.Remove(route);

            temporaryRoutes.Clear();
        }
        public void ClearTemporaryMarkers()
        {
            foreach (var marker in TemporaryMarkers)
            {
                MainMap.Markers.Remove(marker);
            }
            TemporaryMarkers.Clear();
        }
        public void LoadTrips()
        {
            try
            {

                string homeAddress = "";
                double homeLat = 0;
                double homeLng = 0;

                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SQLiteCommand(
                    @"SELECT dom_adres, dom_szerokosc, dom_dlugosc, dom_kraj, dom_miasto FROM uzytkownicy WHERE id_uzytkownika=@userId",
                    conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            homeLat = reader["dom_szerokosc"] == DBNull.Value ? 0 : Convert.ToDouble(reader["dom_szerokosc"]);
                            homeLng = reader["dom_dlugosc"] == DBNull.Value ? 0 : Convert.ToDouble(reader["dom_dlugosc"]);

                            homeAddress = $"{reader["dom_kraj"]}, {reader["dom_miasto"]}, {reader["dom_adres"]}";
                        }
                    }
                }

                var trips = new List<(long id, string nazwa, double szer, double dlug, string typ)>();



                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SQLiteCommand(
                    @"SELECT p.id_podrozy, pm.nazwa, pm.szerokosc, pm.dlugosc, pm.typ
      FROM podroze p
      JOIN punkty_mapy pm ON p.id_podrozy = pm.id_podrozy
      WHERE p.id_uzytkownika=@userId",
                    conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trips.Add((
                                (long)reader["id_podrozy"],
                                reader["nazwa"].ToString(),
                                reader["szerokosc"] == DBNull.Value ? 0 : Convert.ToDouble(reader["szerokosc"]),
                                reader["dlugosc"] == DBNull.Value ? 0 : Convert.ToDouble(reader["dlugosc"]),

                                reader["typ"].ToString()
                            ));
                        }
                    }
                }



                var tripMarkers = MainMap.Markers.Where(m => m != domMarker).ToList();
                foreach (var m in tripMarkers)
                    MainMap.Markers.Remove(m);


                if (domMarker == null &&
     !string.IsNullOrEmpty(homeAddress) &&
     homeLat != 0 &&
     homeLng != 0)
                {
                    var image = new Image
                    {
                        Width = 28,
                        Height = 28,
                        Source = new BitmapImage(
                            new Uri("pack://application:,,,/images/house.png", UriKind.Absolute)
                        ),
                        ToolTip = "Twój dom"
                    };

                    domMarker = new GMapMarker(new PointLatLng(homeLat, homeLng))
                    {
                        Shape = image,
                        Offset = new Point(-14, -28),
                        ZIndex = int.MaxValue
                    };

                    MainMap.Markers.Add(domMarker);
                }

                else if (domMarker != null)
                {
                    domMarker.Position = new PointLatLng(homeLat, homeLng);
                }


                foreach (var t in trips)
                {
                    string iconUri = t.typ == "odwiedzone"
                        ? "pack://application:,,,/images/location.png"
                        : "pack://application:,,,/images/pin.png";

                    var pinImage = new Image
                    {
                        Width = 28,
                        Height = 28,
                        Source = new BitmapImage(new Uri(iconUri, UriKind.Absolute)),
                        ToolTip = t.nazwa,
                        Tag = t.id
                    };

                    var marker = new GMapMarker(new PointLatLng(t.szer, t.dlug))
                    {
                        Shape = pinImage,
                        Offset = new Point(-14, -28)
                    };

                    marker.Shape.MouseLeftButtonUp += (s, e2) =>
                    {
                        e2.Handled = true;
                        OpenExistingTrip(t.id);
                    };

                    MainMap.Markers.Add(marker);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania podróży: {ex.Message}\n{ex.StackTrace}");
            }

            UpdateVisitedCountriesCount();
        }

        public void LoadTripMarkersOnly()
        {
            try
            {
                var trips = new List<(long id, string nazwa, double szer, double dlug, string typ, int ocena, string opis)>();
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SQLiteCommand(
                @"SELECT p.id_podrozy, pm.nazwa, pm.szerokosc, pm.dlugosc, pm.typ, p.ocena, p.opis
  FROM podroze p
  JOIN punkty_mapy pm ON p.id_podrozy = pm.id_podrozy
  WHERE p.id_uzytkownika=@userId",
                conn))
                {
                    conn.Open();
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trips.Add((
                                (long)reader["id_podrozy"],
                                reader["nazwa"].ToString(),
                                reader["szerokosc"] == DBNull.Value ? 0 : Convert.ToDouble(reader["szerokosc"]),
                                reader["dlugosc"] == DBNull.Value ? 0 : Convert.ToDouble(reader["dlugosc"]),

                                reader["typ"].ToString(),
                                reader["ocena"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ocena"]),
                                reader["opis"] == DBNull.Value ? "" : reader["opis"].ToString()
                            ));
                        }
                    }
                }

                foreach (var t in trips)
                {
                    string iconUri = t.typ == "odwiedzone"
                        ? "pack://application:,,,/images/location.png"
                        : "pack://application:,,,/images/pin.png";

                    var pinImage = new Image
                    {
                        Width = 28,
                        Height = 28,
                        Source = new BitmapImage(new Uri(iconUri, UriKind.Absolute)),
                        ToolTip = t.nazwa,
                        Tag = t.id
                    };

                    var marker = new GMapMarker(new PointLatLng(t.szer, t.dlug))
                    {
                        Shape = pinImage,
                        Offset = new Point(-14, -28)
                    };

                    marker.Shape.MouseLeftButtonUp += (s, e2) =>
                    {
                        e2.Handled = true;
                        OpenExistingTrip(t.id);
                    };

                    MainMap.Markers.Add(marker);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania podróży: {ex.Message}\n{ex.StackTrace}");
            }

            UpdateVisitedCountriesCount();
        }

        private async Task<(string kraj, string miasto, string adres)> GetAddressPartsAsync(double lat, double lng)
        {
            if (double.IsNaN(lat) || double.IsNaN(lng))
                return ("", "", "");

            string url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}&format=json&accept-language=pl";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "TravelMateApp/1.0 (daria@gmail.com)");

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return ("", "", "");

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                var address = json["address"];
                if (address == null)
                    return ("", "", "");

                string kraj = address["country"]?.ToString() ?? "";
                string miasto = address["city"]?.ToString() ?? address["town"]?.ToString() ?? address["village"]?.ToString() ?? "";
                string adres = address["road"]?.ToString() ?? "";

                return (kraj, miasto, adres);
            }
        }

        public void EnableTripLocationChangeMode(long? tripId = null)
        {
            isChangingTripLocation = true;
            tripIdToChange = tripId;


            Cursor = Cursors.Cross;

            MessageBox.Show("Kliknij na mapie nowe miejsce dla tej podróży 🗺️",
                "Tryb zmiany lokalizacji", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void AddMarker(double lat, double lng, string nazwa, Brush kolor, long idPodrozy)
        {
            var marker = new GMapMarker(new PointLatLng(lat, lng))
            {
                Shape = new Ellipse
                {
                    Width = 16,
                    Height = 16,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Fill = kolor,
                    ToolTip = nazwa,
                    Tag = idPodrozy
                }
            };

            marker.Shape.MouseLeftButtonUp += (s, e) =>
            {
                long tripId = (long)((Ellipse)s).Tag;
                try
                {
                    using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

                    {
                        conn.Open();
                        string query = @"SELECT p.id_podrozy, p.tytul, p.miejsce, p.data_od, p.data_do, p.ocena, p.opis
                                 FROM podroze p
                                 JOIN punkty_mapy pm ON p.id_podrozy = pm.id_podrozy
                                 WHERE p.id_podrozy = @id";

                        using (var cmd = new SQLiteCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", tripId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string tytul = reader["tytul"].ToString();
                                    string miejsce = reader["miejsce"].ToString();
                                    DateTime? dataOd = reader["data_od"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["data_od"]);
                                    DateTime? dataDo = reader["data_do"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["data_do"]);
                                    int ocena = reader["ocena"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ocena"]);
                                    string opis = reader["opis"] == DBNull.Value ? "" : reader["opis"].ToString();
                                    var addTrip = new AddTripWindow(lat, lng, userId, tytul, tripId);
                                    addTrip.TripAdded += () =>
                                    {
                                        MainMap.Markers.Clear();
                                        LoadTrips();
                                        LoadTripsForCalendar();
                                        PopulateCalendar(currentYear, currentMonth);
                                    };

                                    addTrip.txtTytul.Text = tytul;
                                    addTrip.txtMiasto.Text = miejsce;
                                    addTrip.dpStart.SelectedDate = dataOd;
                                    addTrip.dpEnd.SelectedDate = dataDo;

                                    addTrip.ShowDialog();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd przy otwieraniu podróży: {ex.Message}");
                }
            };

            MainMap.Markers.Add(marker);
        }

        private void BtnKonto_Click(object sender, RoutedEventArgs e)
        {
            var oknoKonta = new AccountWindow(userId, MainMap)
            {
                Owner = this
            };
            oknoKonta.ShowDialog();
        }

        public void RemoveMarkersForTrip(long tripId)
        {
            var markersToRemove = MainMap.Markers
                .Where(m => m != domMarker && (m.Shape is FrameworkElement fe) && fe.Tag is long id && id == tripId)
                .ToList();

            foreach (var m in markersToRemove)
                MainMap.Markers.Remove(m);
        }

        private void OpenExistingTrip(long idPodrozy)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

                {
                    conn.Open();
                    string query = @"SELECT p.tytul, p.miejsce, p.data_od, p.data_do, p.ocena, p.opis, pm.szerokosc, pm.dlugosc
                 FROM podroze p
                 JOIN punkty_mapy pm ON p.id_podrozy = pm.id_podrozy
                 WHERE p.id_podrozy = @id";


                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", idPodrozy);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string tytul = reader["tytul"].ToString();
                                string miejsce = reader["miejsce"].ToString();
                                DateTime? dataOd = reader["data_od"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["data_od"]);
                                DateTime? dataDo = reader["data_do"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["data_do"]);
                                double szer = Convert.ToDouble(reader["szerokosc"]);
                                double dlug = Convert.ToDouble(reader["dlugosc"]);
                                double? ocena = reader["ocena"] == DBNull.Value ? null : (double?)Convert.ToDouble(reader["ocena"]);
                                string opis = reader["opis"]?.ToString() ?? "";

                                var editWindow = new AddTripWindow(szer, dlug, userId, tytul, idPodrozy);
                                editWindow.TripAdded += () =>
                                {
                                    var tripMarkers = MainMap.Markers.Where(m => m != domMarker).ToList();
                                    foreach (var marker in tripMarkers)
                                        MainMap.Markers.Remove(marker);
                                    LoadTripMarkersOnly();
                                    LoadTripsForCalendar();
                                    PopulateCalendar(currentYear, currentMonth);
                                };

                                editWindow.txtTytul.Text = tytul;
                                editWindow.txtMiasto.Text = miejsce;
                                editWindow.dpStart.SelectedDate = dataOd;
                                editWindow.dpEnd.SelectedDate = dataDo;

                                editWindow.txtOpis.Text = opis;


                                if (ocena.HasValue && ocena.Value >= 1 && ocena.Value <= 5)
                                {
                                    editWindow.cbOcena.SelectedIndex = (int)ocena.Value - 1;
                                }
                                else
                                {
                                    editWindow.cbOcena.SelectedIndex = -1;
                                }


                                editWindow.Owner = this;
                                editWindow.ShowDialog();
                            }

                            else
                            {
                                MessageBox.Show("Nie znaleziono podróży o podanym ID.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania podróży: {ex.Message}");
            }
        }
        private List<(long id, string tytul, DateTime start, DateTime end, int ocena, string opis)> tripsCalendar = new List<(long, string, DateTime, DateTime, int, string)>();


        private void LoadTripsForCalendar()
        {
            tripsCalendar.Clear();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

            {
                conn.Open();
                string query = @"SELECT id_podrozy, tytul, data_od, data_do, ocena, opis FROM podroze WHERE id_uzytkownika=@userId";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["data_od"] != DBNull.Value && reader["data_do"] != DBNull.Value)
                            {
                                DateTime start = Convert.ToDateTime(reader["data_od"]);
                                DateTime end = Convert.ToDateTime(reader["data_do"]);
                                int ocena = reader["ocena"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ocena"]);
                                string opis = reader["opis"] == DBNull.Value ? "" : reader["opis"].ToString();

                                tripsCalendar.Add(((long)reader["id_podrozy"], reader["tytul"].ToString(), start, end, ocena, opis));
                            }
                        }

                    }
                }
            }
        }

        private void PopulateCalendar(int year, int month)
        {
            currentYear = year;
            currentMonth = month;

            txtMonthYear.Text = new DateTime(year, month, 1)
                .ToString("MMMM yyyy", new CultureInfo("pl-PL"));

            calendarGrid.Items.Clear();

            var firstDay = new DateTime(year, month, 1);
            int daysInMonth = DateTime.DaysInMonth(year, month);


            int startOffset = ((int)firstDay.DayOfWeek + 6) % 7;



            for (int i = 0; i < startOffset; i++)
            {
                calendarGrid.Items.Add(new
                {
                    DayNumber = "",
                    Trips = new List<object>()
                });
            }


            for (int day = 1; day <= daysInMonth; day++)
            {
                var currentDate = new DateTime(year, month, day);

                var tripsOnDay = tripsCalendar
                    .Where(t => currentDate >= t.start && currentDate <= t.end)
                    .Select(t => new { Name = t.tytul, TripId = t.id })
                    .ToList();

                calendarGrid.Items.Add(new
                {
                    DayNumber = day.ToString(),
                    Trips = tripsOnDay
                });
            }
        }

        private void HighlightToday()
        {
            var today = DateTime.Today;

            foreach (var item in calendarGrid.Items)
            {
                dynamic dayObj = item;


                if (!int.TryParse(dayObj.DayNumber?.ToString(), out int dayNumber))
                    continue;

                if (dayNumber != today.Day ||
                    currentMonth != today.Month ||
                    currentYear != today.Year)
                    continue;

                var container = calendarGrid.ItemContainerGenerator
                    .ContainerFromItem(item) as FrameworkElement;
                if (container == null) continue;

                var border = FindChild<Border>(container);
                if (border == null) continue;

                border.Background = new SolidColorBrush(Color.FromRgb(255, 200, 200));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 100, 100));
                border.BorderThickness = new Thickness(2);

                border.MouseEnter -= CalendarDay_MouseEnter;
                border.MouseLeave -= CalendarDay_MouseLeave;

                var stackPanel = FindChild<StackPanel>(border);
                if (stackPanel != null &&
                    !stackPanel.Children.OfType<TextBlock>().Any(tb => tb.Text == "Dzisiaj"))
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = "Dzisiaj",
                        FontSize = 10,
                        Foreground = Brushes.Black,
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }


                break;
            }
        }

        public static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T tChild)
                    return tChild;

                var result = FindChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }


        private void TripButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag is long tripId)
            {
                OpenExistingTrip(tripId);
            }
        }




        private void UpdateVisitedCountriesCount()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

                {
                    conn.Open();

                    string query = @"
                SELECT COUNT(DISTINCT p.kraj) AS liczba_krajow
                FROM podroze p
                JOIN punkty_mapy pm ON p.id_podrozy = pm.id_podrozy
                WHERE p.id_uzytkownika = @userId AND pm.typ = 'odwiedzone';";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = cmd.ExecuteScalar();
                        int liczbaOdwiedzonych = result != null ? Convert.ToInt32(result) : 0;

                        int liczbaWszystkich = 195;

                        double procent = (double)liczbaOdwiedzonych / liczbaWszystkich * 100;

                        var txt = FindName("txtZwiedzoneKraje") as TextBlock;
                        if (txt != null)
                            txt.Text = $"Zwiedzono: {liczbaOdwiedzonych}/{liczbaWszystkich} 🌍 ({procent:0.##}%)";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania krajów: {ex.Message}");
            }
        }


        private async void HighlightVisitedCountries()
        {
            try
            {

                List<string> countries = new List<string>();
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

                {
                    conn.Open();
                    string query = @"SELECT DISTINCT p.kraj
                             FROM podroze p
                             JOIN punkty_mapy pm ON p.id_podrozy = pm.id_podrozy
                             WHERE p.id_uzytkownika=@userId AND pm.typ='odwiedzone';";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                countries.Add(reader["kraj"].ToString());
                        }
                    }
                }

                foreach (var country in countries)
                {
                    var boundaryPoints = await GetCountryBoundaryPoints(country);

                    if (boundaryPoints.Count > 0)
                    {

                        var polygon = new GMapPolygon(boundaryPoints)
                        {
                            Shape = new System.Windows.Shapes.Polygon
                            {
                                Stroke = Brushes.Red,
                                StrokeThickness = 2,
                                Fill = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0))
                            }
                        };

                        countryPolygons.Add(polygon);


                        MainMap.Markers.Add(polygon);
                        MainMap.RegenerateShape(polygon);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas podświetlania krajów: {ex.Message}");
            }
        }

        private bool highlightActive = false;

        private void BtnHighlightCountries_Click(object sender, RoutedEventArgs e)
        {
            if (!highlightActive)
            {
                HighlightVisitedCountries();
                btnHighlightCountries.Content = "Ukryj kraje";
                highlightActive = true;
            }
            else
            {
                RemoveCountryHighlights();
                btnHighlightCountries.Content = "Pokaż kraje";
                highlightActive = false;
            }
        }


        private async Task<List<PointLatLng>> GetCountryBoundaryPoints(string country)
        {
            List<PointLatLng> points = new List<PointLatLng>();

            try
            {
                string url = $"https://nominatim.openstreetmap.org/search.php?country={Uri.EscapeDataString(country)}&polygon_geojson=1&format=json";

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "TravelMateApp/1.0");

                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode) return points;

                    var json = JArray.Parse(await response.Content.ReadAsStringAsync());
                    if (json.Count == 0) return points;

                    var geojson = json[0]["geojson"];
                    if (geojson == null) return points;

                    if (geojson["type"].ToString() == "Polygon")
                    {
                        foreach (var coord in geojson["coordinates"][0])
                        {
                            double lng = coord[0].ToObject<double>();
                            double lat = coord[1].ToObject<double>();
                            points.Add(new PointLatLng(lat, lng));
                        }
                    }
                    else if (geojson["type"].ToString() == "MultiPolygon")
                    {
                        foreach (var poly in geojson["coordinates"])
                        {
                            foreach (var coord in poly[0])
                            {
                                double lng = coord[0].ToObject<double>();
                                double lat = coord[1].ToObject<double>();
                                points.Add(new PointLatLng(lat, lng));
                            }
                        }
                    }
                }
            }
            catch { }

            return points;
        }

        private void RemoveCountryHighlights()
        {
            foreach (var poly in countryPolygons)
            {
                MainMap.Markers.Remove(poly);
            }
            countryPolygons.Clear();
        }





        private List<Podroz> PobierzPodroze(DateTime? od = null, DateTime? do_ = null)
        {
            var podroze = new List<Podroz>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM podroze WHERE id_uzytkownika = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);

                if (od.HasValue && do_.HasValue)
                {
                    cmd.CommandText += " AND date(data_do) >= date(@od) AND date(data_od) <= date(@do)";
                    cmd.Parameters.AddWithValue("@od", od.Value.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@do", do_.Value.ToString("yyyy-MM-dd"));
                }
                else if (od.HasValue)
                {
                    cmd.CommandText += " AND date(data_do) >= date(@od)";
                    cmd.Parameters.AddWithValue("@od", od.Value.ToString("yyyy-MM-dd"));
                }
                else if (do_.HasValue)
                {
                    cmd.CommandText += " AND date(data_od) <= date(@do)";
                    cmd.Parameters.AddWithValue("@do", do_.Value.ToString("yyyy-MM-dd"));
                }

                using (var reader = cmd.ExecuteReader())
                {


                    while (reader.Read())
                    {
                        var podroz = new Podroz
                        {
                            id_podrozy = Convert.ToInt32(reader["id_podrozy"]),
                            tytul = reader["tytul"]?.ToString() ?? "",
                            tytulPodstawowy = reader["tytul"]?.ToString() ?? "",
                            miejsce = reader["miejsce"]?.ToString() ?? "",
                            miasto = reader["miasto"]?.ToString() ?? "",
                            kraj = reader["kraj"]?.ToString() ?? "",
                            data_od = DateTime.Parse(reader["data_od"].ToString()),
                            data_do = DateTime.Parse(reader["data_do"].ToString())
                        };


                        int id = podroz.id_podrozy;
                        double? domLat = null;
                        double? domLon = null;

                        using (var cmdUser = conn.CreateCommand())
                        {
                            cmdUser.CommandText = "SELECT dom_szerokosc, dom_dlugosc FROM uzytkownicy WHERE id_uzytkownika=@id";
                            cmdUser.Parameters.AddWithValue("@id", reader["id_uzytkownika"]);
                            using (var r = cmdUser.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    domLat = r["dom_szerokosc"] != DBNull.Value
                                   ? Convert.ToDouble(r["dom_szerokosc"])
                                  : (double?)null;

                                    domLon = r["dom_dlugosc"] != DBNull.Value
                                        ? Convert.ToDouble(r["dom_dlugosc"])
                                        : (double?)null;

                                }
                            }
                        }

                        using (var cmdPunkt = conn.CreateCommand())
                        {
                            cmdPunkt.CommandText = @"
        SELECT szerokosc, dlugosc
        FROM punkty_mapy
        WHERE id_podrozy=@id
        ORDER BY id_punktu ASC
        LIMIT 1
    ";
                            cmdPunkt.Parameters.AddWithValue("@id", podroz.id_podrozy);
                            using (var r = cmdPunkt.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    double latPunkt = Convert.ToDouble(r["szerokosc"]);
                                    double lonPunkt = Convert.ToDouble(r["dlugosc"]);
                                    if (domLat.HasValue && domLon.HasValue)
                                    {
                                        podroz.OdlegloscOdDomu = ObliczOdleglosc(
                                            domLat.Value,
                                            domLon.Value,
                                            latPunkt,
                                            lonPunkt
                                        );
                                    }
                                    else
                                    {
                                        podroz.OdlegloscOdDomu = -1;
                                    }

                                }
                            }
                        }

                        double totalCost = 0;

                        using (var cmdDojazdy = conn.CreateCommand())
                        {
                            cmdDojazdy.CommandText = "SELECT cena, waluta FROM dojazdy WHERE id_podrozy=@id";
                            cmdDojazdy.Parameters.AddWithValue("@id", id);
                            using (var r = cmdDojazdy.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    double val = r["cena"] != DBNull.Value ? Convert.ToDouble(r["cena"]) : 0;
                                    string cur = r["waluta"] != DBNull.Value ? r["waluta"].ToString() : "PLN";
                                    totalCost += ConvertToTarget(val, cur, "PLN", rates);
                                }
                            }
                        }

                        using (var cmdNoclegi = conn.CreateCommand())
                        {
                            cmdNoclegi.CommandText = "SELECT cena, waluta FROM noclegi WHERE id_podrozy=@id";
                            cmdNoclegi.Parameters.AddWithValue("@id", id);
                            using (var r = cmdNoclegi.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    double val = r["cena"] != DBNull.Value ? Convert.ToDouble(r["cena"]) : 0;
                                    string cur = r["waluta"] != DBNull.Value ? r["waluta"].ToString() : "PLN";
                                    totalCost += ConvertToTarget(val, cur, "PLN", rates);
                                }
                            }
                        }


                        using (var cmdZwiedzanie = conn.CreateCommand())
                        {
                            cmdZwiedzanie.CommandText = "SELECT cena, waluta FROM zwiedzanie WHERE id_podrozy=@id";
                            cmdZwiedzanie.Parameters.AddWithValue("@id", id);
                            using (var r = cmdZwiedzanie.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    double val = r["cena"] != DBNull.Value ? Convert.ToDouble(r["cena"]) : 0;
                                    string cur = r["waluta"] != DBNull.Value ? r["waluta"].ToString() : "PLN";
                                    totalCost += ConvertToTarget(val, cur, "PLN", rates);
                                }
                            }
                        }

                        podroz.koszt = totalCost;

                        double totalKilometers = 0;

                        using (var cmdDojazdyKm = conn.CreateCommand())
                        {
                            cmdDojazdyKm.CommandText = @"
        SELECT ps.szerokosc AS latStart, ps.dlugosc AS lonStart,
               pk.szerokosc AS latEnd, pk.dlugosc AS lonEnd
        FROM dojazdy d
        JOIN punkty_dojazdow ps ON d.id_punktu_start = ps.id_punktu
        JOIN punkty_dojazdow pk ON d.id_punktu_koniec = pk.id_punktu
        WHERE d.id_podrozy = @id
    ";
                            cmdDojazdyKm.Parameters.AddWithValue("@id", id);

                            using (var r = cmdDojazdyKm.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    double latStart = Convert.ToDouble(r["latStart"]);
                                    double lonStart = Convert.ToDouble(r["lonStart"]);
                                    double latEnd = Convert.ToDouble(r["latEnd"]);
                                    double lonEnd = Convert.ToDouble(r["lonEnd"]);

                                    double odleglosc = ObliczOdleglosc(latStart, lonStart, latEnd, lonEnd);


                                    totalKilometers += odleglosc;
                                }
                            }
                        }

                        podroz.laczneKilometry = totalKilometers;

                        using (var cmdTransport = conn.CreateCommand())
                        {
                            cmdTransport.CommandText = @"
                        SELECT 
                            SUM(CASE WHEN srodek_transportu='Samolot' THEN 1 ELSE 0 END) AS loty,
                            SUM(CASE WHEN srodek_transportu='Pociąg' THEN 1 ELSE 0 END) AS pociagi
                        FROM dojazdy
                        WHERE id_podrozy=@id
                    ";
                            cmdTransport.Parameters.AddWithValue("@id", id);
                            using (var r = cmdTransport.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    podroz.iloscLotow = r["loty"] != DBNull.Value ? Convert.ToInt32(r["loty"]) : 0;
                                    podroz.iloscPociagow = r["pociagi"] != DBNull.Value ? Convert.ToInt32(r["pociagi"]) : 0;
                                }
                            }
                        }


                        using (var cmdNoclegi = conn.CreateCommand())
                        {
                            cmdNoclegi.CommandText = "SELECT SUM(julianday(data_do) - julianday(data_od) + 1) FROM noclegi WHERE id_podrozy=@id";
                            cmdNoclegi.Parameters.AddWithValue("@id", id);
                            var noclegi = cmdNoclegi.ExecuteScalar();
                            podroz.iloscNoclegow = noclegi != DBNull.Value ? Convert.ToInt32(noclegi) : 0;
                        }


                        int ocena = reader["ocena"] != DBNull.Value ? Convert.ToInt32(reader["ocena"]) : -1;

                        if (ocena == -1)
                        {
                            podroz.ocenaLiczbowa = null;
                            podroz.sredniaOcena = "☆☆☆☆☆";
                        }
                        else
                        {
                            podroz.ocenaLiczbowa = ocena;
                            podroz.sredniaOcena = new string('★', ocena) + new string('☆', 5 - ocena);
                        }



                        podroze.Add(podroz);
                    }
                }
            }

            return podroze;
        }
        private void DpOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpOd.SelectedDate.HasValue)
            {
                dpDo.DisplayDateStart = dpOd.SelectedDate.Value;
                if (dpDo.SelectedDate < dpOd.SelectedDate)
                    dpDo.SelectedDate = dpOd.SelectedDate;
            }
            else
            {
                dpDo.DisplayDateStart = null;
            }
        }

        private void DpDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDo.SelectedDate.HasValue)
            {
                dpOd.DisplayDateEnd = dpDo.SelectedDate.Value;
                if (dpOd.SelectedDate > dpDo.SelectedDate)
                    dpOd.SelectedDate = dpDo.SelectedDate;
            }
            else
            {
                dpOd.DisplayDateEnd = null;
            }
        }




        private void AktualizujStatystyki(List<Podroz> podroze)
        {
            if (podroze.Count == 0)
            {
                txtLacznieWydane.Text = "0 PLN";
                txtLacznieNocy.Text = "0";
                txtLacznieLotow.Text = "0";
                txtLaczniePociagow.Text = "0";
                txtSredniKoszt.Text = "0 PLN";
                txtLacznieDni.Text = "0";
                txtNajdrozsza.Text = "-";
                txtNajtansza.Text = "-";
                txtKraje.Text = "0";
                txtMiasta.Text = "0";
                txtSredniaOcena.Text = "★☆☆☆☆";
                txtTopKategoria.Text = "-";
                txtLacznieKm.Text = podroze.Sum(p => p.laczneKilometry).ToString("N0") + " km";

                return;
            }

            txtLacznieWydane.Text = $"{podroze.Sum(p => p.koszt):F0} PLN";
            txtLacznieNocy.Text = podroze.Sum(p => p.iloscNoclegow).ToString();
            txtLacznieLotow.Text = podroze.Sum(p => p.iloscLotow).ToString();
            txtLaczniePociagow.Text = podroze.Sum(p => p.iloscPociagow).ToString();
            txtSredniKoszt.Text = $"{podroze.Average(p => p.koszt):F0} PLN";
            txtLacznieDni.Text = podroze.Sum(p => (p.data_do - p.data_od).Days + 1).ToString();
            txtLacznieKm.Text = podroze.Sum(p => p.laczneKilometry).ToString("N0") + " km";
            var najdrozsza = podroze.OrderByDescending(p => p.koszt).First();
            var najtansza = podroze.OrderBy(p => p.koszt).First();
            txtNajdrozsza.Text = $"{najdrozsza.miasto} - {najdrozsza.tytul} ({najdrozsza.koszt:F0} PLN)";
            txtNajtansza.Text = $"{najtansza.miasto} - {najtansza.tytul} ({najtansza.koszt:F0} PLN)";


            string ExtractCountry(string fullAddress)
            {
                if (string.IsNullOrEmpty(fullAddress)) return null;
                var parts = fullAddress.Split(',');
                return parts.Length > 0 ? parts[parts.Length - 1].Trim() : null;
            }

            var miasta = new HashSet<string>();
            var kraje = new HashSet<string>();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

            {
                conn.Open();

                foreach (var p in podroze)
                {

                    if (!string.IsNullOrEmpty(p.miasto)) miasta.Add(p.miasto);
                    if (!string.IsNullOrEmpty(p.kraj)) kraje.Add(p.kraj);


                    var cmdDojazdy = conn.CreateCommand();
                    cmdDojazdy.CommandText = @"
            SELECT DISTINCT pd.nazwa AS pelnyAdres
            FROM dojazdy d
            JOIN punkty_dojazdow pd ON d.id_punktu_koniec = pd.id_punktu
            WHERE d.id_podrozy = @id
        ";
                    cmdDojazdy.Parameters.AddWithValue("@id", p.id_podrozy);
                    using (var r = cmdDojazdy.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string pelnyAdres = r["pelnyAdres"]?.ToString();
                            if (!string.IsNullOrEmpty(pelnyAdres))
                            {
                                miasta.Add(pelnyAdres);
                                var kraj = ExtractCountry(pelnyAdres);
                                if (!string.IsNullOrEmpty(kraj)) kraje.Add(kraj);
                            }
                        }
                    }


                    var cmdNoclegi = conn.CreateCommand();
                    cmdNoclegi.CommandText = @"
            SELECT DISTINCT pn.nazwa AS pelnyAdres
            FROM noclegi n
            JOIN punkty_noclegowe pn ON n.id_punktu = pn.id_punktu
            WHERE n.id_podrozy = @id
        ";
                    cmdNoclegi.Parameters.AddWithValue("@id", p.id_podrozy);
                    using (var r = cmdNoclegi.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string pelnyAdres = r["pelnyAdres"]?.ToString();
                            if (!string.IsNullOrEmpty(pelnyAdres))
                            {
                                miasta.Add(pelnyAdres);
                                var kraj = ExtractCountry(pelnyAdres);
                                if (!string.IsNullOrEmpty(kraj)) kraje.Add(kraj);
                            }
                        }
                    }


                    var cmdZwiedzanie = conn.CreateCommand();
                    cmdZwiedzanie.CommandText = @"
            SELECT DISTINCT p.nazwa AS pelnyAdres
            FROM zwiedzanie z
            JOIN punkty_zwiedzania p ON z.id_punktu = p.id_punktu
            WHERE z.id_podrozy=@id
        ";
                    cmdZwiedzanie.Parameters.AddWithValue("@id", p.id_podrozy);
                    using (var r = cmdZwiedzanie.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string pelnyAdres = r["pelnyAdres"]?.ToString();
                            if (!string.IsNullOrEmpty(pelnyAdres))
                            {
                                miasta.Add(pelnyAdres);
                                var kraj = ExtractCountry(pelnyAdres);
                                if (!string.IsNullOrEmpty(kraj)) kraje.Add(kraj);
                            }
                        }
                    }
                }
            }

            txtMiasta.Text = miasta.Count.ToString();
            txtKraje.Text = kraje.Count.ToString();


            var ocenione = podroze.Where(p => p.ocenaLiczbowa.HasValue).ToList();

            if (ocenione.Count == 0)
            {
                txtSredniaOcena.Text = "☆☆☆☆☆";
                sredniaOcenaLiczbowa = 0;
            }
            else
            {
                double avg = ocenione.Average(p => p.ocenaLiczbowa.Value);
                sredniaOcenaLiczbowa = avg;
                int rounded = (int)Math.Round(avg);
                txtSredniaOcena.Text = new string('★', rounded) + new string('☆', 5 - rounded);
            }



            var podrozeIds = podroze.Select(p => p.id_podrozy).ToList();
            if (podrozeIds.Count > 0)
            {
                var idsParam = string.Join(",", podrozeIds);

                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))

                {
                    conn.Open();

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = $@"
            SELECT k.nazwa, COUNT(*) AS cnt
            FROM zwiedzanie z
            JOIN punkty_zwiedzania p ON z.id_punktu = p.id_punktu
            JOIN kategorie k ON p.id_kategorii = k.id_kategorii
            WHERE z.id_podrozy IN ({idsParam})
            GROUP BY k.nazwa
            ORDER BY cnt DESC
            LIMIT 1
        ";
                    var result = cmd.ExecuteScalar();
                    txtTopKategoria.Text = result != DBNull.Value && result != null ? result.ToString() : "-";
                }
            }
            else
            {
                txtTopKategoria.Text = "-";
            }


        }

        public async Task LoadRatesAsync()
        {
            rates = await GetExchangeRatesAsync("PLN");
        }

        public async Task<Dictionary<string, double>> GetExchangeRatesAsync(string baseCurrency)
        {
            using (var client = new HttpClient())
            {
                string url = $"https://api.frankfurter.app/latest?from={baseCurrency}";
                var json = await client.GetStringAsync(url);
                var data = System.Text.Json.JsonSerializer.Deserialize<FrankfurterResponse>(json);

                if (!data.rates.ContainsKey(baseCurrency))
                    data.rates[baseCurrency] = 1;

                return data.rates;
            }
        }

        public double ConvertToTarget(double amount, string fromCurrency, string targetCurrency, Dictionary<string, double> rates)
        {
            if (fromCurrency == targetCurrency) return Math.Round(amount, 2);

            double fromRate = rates.ContainsKey(fromCurrency) ? rates[fromCurrency] : 1;
            double toRate = rates.ContainsKey(targetCurrency) ? rates[targetCurrency] : 1;

            double converted = amount / fromRate * toRate;
            return Math.Round(converted, 2);
        }

        public class FrankfurterResponse
        {
            public string @base { get; set; }
            public Dictionary<string, double> rates { get; set; }
        }
        private void EksportujPdf_Click(object sender, RoutedEventArgs e)
        {




            if (!(icPodroze.ItemsSource is List<Podroz> lista) || lista.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu!");
                return;
            }


            var od = dpOd.SelectedDate;
            var do_ = dpDo.SelectedDate;
            var item = cbSortowanie.SelectedItem as ComboBoxItem;
            var sortowanie = item?.ToolTip?.ToString() ?? "-";


            var sciezka = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "RaportPodrozy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf"
            );

            var listaDoPdf = lista
    .Select(p => new Podroz
    {
        id_podrozy = p.id_podrozy,
        tytul = p.tytul,
        miejsce = p.miejsce,
        miejsceEmoji = p.miejsceEmoji,
        data_od = p.data_od,
        data_do = p.data_do,
        laczneKilometry = p.laczneKilometry,
        OdlegloscOdDomu = p.OdlegloscOdDomu,
        koszt = p.koszt,
        iloscEtapow = p.iloscEtapow,
        iloscNoclegow = p.iloscNoclegow,
        sredniaOcena = p.sredniaOcena,
        tytulPodstawowy = p.tytulPodstawowy,
        miasto = p.miasto,
        kraj = p.kraj,
        ocenaLiczbowa = p.ocenaLiczbowa,
        iloscLotow = p.iloscLotow,
        iloscPociagow = p.iloscPociagow
    })
    .ToList();

            PdfExporter.EksportujPodroze(
                sciezka,
                lista,
                od,
                do_,
                sortowanie,
                txtLacznieWydane.Text,
                txtLacznieNocy.Text,
                txtLacznieLotow.Text,
                txtLaczniePociagow.Text,
                txtSredniKoszt.Text,
                txtLacznieDni.Text,
                txtLacznieKm.Text,
                txtNajdrozsza.Text,
                txtNajtansza.Text,
                txtKraje.Text,
                txtMiasta.Text,
               sredniaOcenaLiczbowa.ToString("F1"),
                txtTopKategoria.Text,
                 loginUzytkownika
            );
        }


        private async void Filtruj_Click(object sender, RoutedEventArgs e)
        {
            await LoadRatesAsync();

            var od = dpOd.SelectedDate;
            var doData = dpDo.SelectedDate;

            var lista = PobierzPodroze(od, doData)
                        .OrderBy(p => p.data_od)
                        .ToList();

            icPodroze.ItemsSource = lista;
            AktualizujStatystyki(lista);
        }

        private void OdswiezStatystyki()
        {
            var lista = PobierzPodroze().OrderBy(p => p.data_od).ToList();
            icPodroze.ItemsSource = lista;
            AktualizujStatystyki(lista);
        }

        private void Resetuj_Click(object sender, RoutedEventArgs e)
        {

            dpOd.SelectedDate = null;
            dpDo.SelectedDate = null;
            dpOd.DisplayDateEnd = null;
            dpDo.DisplayDateStart = null;

            cbSortowanie.SelectedIndex = -1;

            OdswiezStatystyki();
        }
        public static double ObliczOdleglosc(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private void Szczegoly_Click(object sender, RoutedEventArgs e)
        {
            var podroz = (Podroz)((Button)sender).DataContext;
            var okno = new DetailsWindow(podroz.id_podrozy, loginUzytkownika);
            okno.ShowDialog();
        }

        private void CbSortowanie_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (icPodroze.ItemsSource == null) return;

            var podroze = ((List<Podroz>)icPodroze.ItemsSource).ToList();


            foreach (var p in podroze)
                p.tytul = p.tytulPodstawowy;

            if (cbSortowanie.SelectedItem is ComboBoxItem selected)
            {
                string choice = selected.Content.ToString();

                switch (choice)
                {
                    case "Czas ⬆":
                        podroze = podroze.OrderBy(p => (p.data_do - p.data_od).Days + 1).ToList();
                        foreach (var p in podroze)
                            p.tytul = $"{p.tytul} ({(p.data_do - p.data_od).Days + 1} dni)";
                        break;

                    case "Czas ⬇":
                        podroze = podroze.OrderByDescending(p => (p.data_do - p.data_od).Days + 1).ToList();
                        foreach (var p in podroze)
                            p.tytul = $"{p.tytul} ({(p.data_do - p.data_od).Days + 1} dni)";
                        break;

                    case "Cena ⬆":
                        podroze = podroze.OrderBy(p => p.koszt).ToList();
                        foreach (var p in podroze)
                            p.tytul = $"{p.tytul} ({p.koszt:F0} zł)";
                        break;

                    case "Cena ⬇":
                        podroze = podroze.OrderByDescending(p => p.koszt).ToList();
                        foreach (var p in podroze)
                            p.tytul = $"{p.tytul} ({p.koszt:F0} zł)";
                        break;

                    case "Odległość ⬆":
                        podroze = podroze
                            .Where(p => p.OdlegloscOdDomu >= 0)
                            .OrderBy(p => p.OdlegloscOdDomu)
                            .ToList();
                        foreach (var p in podroze)
                        {
                            if (p.OdlegloscOdDomu >= 0)
                                p.tytul = $"{p.tytulPodstawowy} ({p.OdlegloscOdDomu:F1} km od domu)";
                            else
                                p.tytul = $"{p.tytulPodstawowy} (brak ustawionego domu)";
                        }
                        break;


                    case "Odległość ⬇":
                        podroze = podroze
                            .Where(p => p.OdlegloscOdDomu >= 0)
                            .OrderByDescending(p => p.OdlegloscOdDomu)
                            .ToList();

                        foreach (var p in podroze)
                        {
                            if (p.OdlegloscOdDomu >= 0)
                                p.tytul = $"{p.tytulPodstawowy} ({p.OdlegloscOdDomu:F1} km od domu)";
                            else
                                p.tytul = $"{p.tytulPodstawowy} (brak ustawionego domu)";
                        }
                        break;


                    case "Data ⬆":
                        podroze = podroze.OrderBy(p => p.data_od).ToList();
                        break;

                    case "Data ⬇":
                        podroze = podroze.OrderByDescending(p => p.data_od).ToList();
                        break;
                }


                icPodroze.ItemsSource = podroze;
            }
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

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

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
                    .Where(l => l.Contains("–"))
                    .Select(l =>
                    {
                        var parts = l.Split(new[] { '–' }, 2);

                        return new Rekomendacja
                        {
                            Tytul = parts[0]
                                .Trim()
                                .TrimStart('1', '2', '3', '4', '5', '.', ' '),
                            Opis = parts.Length > 1 ? parts[1].Trim() : ""
                        };
                    })
                    .ToList();
            }
        }




        private decimal _budzetDecimal;
        public decimal BudzetDecimal
        {
            get => _budzetDecimal;
            set
            {
                _budzetDecimal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BudzetString));
            }
        }

        public string BudzetString
        {
            get => BudzetDecimal == 0
                ? ""
                : BudzetDecimal.ToString("0.00", CultureInfo.InvariantCulture);

            set
            {
                if (decimal.TryParse(
                        value.Replace(",", "."),
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                {
                    BudzetDecimal = parsed;
                }
            }
        }


        private async void GenerujRekomendacje_Click(object sender, RoutedEventArgs e)
        {
            var aktywnosci = new List<string>();
            if (CboxNarty.IsChecked == true) aktywnosci.Add("narty");
            if (CboxMuzea.IsChecked == true) aktywnosci.Add("muzea");
            if (CboxArchitektura.IsChecked == true) aktywnosci.Add("architektura");
            if (CboxPlaza.IsChecked == true) aktywnosci.Add("plaża");
            if (CboxGory.IsChecked == true) aktywnosci.Add("góry");
            if (CboxPrzyroda.IsChecked == true) aktywnosci.Add("przyroda");
            if (CboxKluby.IsChecked == true) aktywnosci.Add("kluby i imprezy");
            if (CboxSport.IsChecked == true) aktywnosci.Add("sport");


            if (!string.IsNullOrWhiteSpace(InnaAktywnoscTextBox.Text))
            {
                aktywnosci.Add(InnaAktywnoscTextBox.Text.Trim());
            }


            var budzet = BudzetDecimal > 0
     ? BudzetDecimal.ToString("0.00", CultureInfo.InvariantCulture)
     : "dowolny";



            var lokalizacja = LokalizacjaTextBox.Text;
            var dataOd = DataOdPicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "dowolna";
            var dataDo = DataDoPicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "dowolna";

            string prompt =
                "Na podstawie preferencji użytkownika zaproponuj DOKŁADNIE 5 kierunków podróży.\n\n" +
                "Zwróć odpowiedź WYŁĄCZNIE w poniższym formacie (bez wstępów, komentarzy i podsumowań):\n\n" +
                "1. Nazwa kierunku – krótki opis (2–3 zdania)\n" +
                "2. Nazwa kierunku – krótki opis\n" +
                "3. Nazwa kierunku – krótki opis\n" +
                "4. Nazwa kierunku – krótki opis\n" +
                "5. Nazwa kierunku – krótki opis\n\n" +
                "Preferencje:\n" +
                "- Aktywności: " + string.Join(", ", aktywnosci) + "\n" +
                "- Budżet: " + budzet + " PLN\n" +
                "- Lokalizacja: " + lokalizacja + "\n" +
                "- Daty: od " + dataOd + " do " + dataDo;


            var rekomendacje = await WywolajGemini(prompt);
            RekomendacjeListBox.ItemsSource = rekomendacje;
        }
        private void BudzetTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = (TextBox)sender;


            if (char.IsDigit(e.Text, 0))
                return;


            if (e.Text == "." && !tb.Text.Contains("."))
                return;

            e.Handled = true;
        }


        private void BudzetTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!decimal.TryParse(text.Replace(",", "."), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void DataOdPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataOdPicker.SelectedDate.HasValue)
            {

                DataDoPicker.DisplayDateStart = DataOdPicker.SelectedDate;


                if (DataDoPicker.SelectedDate < DataOdPicker.SelectedDate)
                {
                    DataDoPicker.SelectedDate = DataOdPicker.SelectedDate;
                }
            }
            else
            {

                DataDoPicker.DisplayDateStart = null;
            }
        }


        /// new
        /// 

        private List<Flight> _filteredFlights = new List<Flight>();
        private int _currentFlightIndex = 0;
        private void InitializeSearchMap()
        {
            // Provider mapy
            SearchMap.MapProvider = GoogleMapProvider.Instance;
            GMaps.Instance.Mode = AccessMode.ServerAndCache; // online + cache

            // Centrum mapy – Polska
            SearchMap.Position = new PointLatLng(52.2297, 21.0122); // Warszawa
            SearchMap.MinZoom = 2;
            SearchMap.MaxZoom = 18;
            SearchMap.Zoom = 6;

            // Opcje interakcji
            SearchMap.CanDragMap = true;
            SearchMap.MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter;
            SearchMap.ShowCenter = false; // ukrywa krzyżyk w środku
        }

        private async void DisplayFlightsOnMap(List<Flight> flights)
        {
            SearchMap.Markers.Clear();

            foreach (var flight in flights)
            {
                // marker startowy
                var startMarker = new GMapMarker(new PointLatLng(flight.Lat, flight.Lng))
                {
                    Shape = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1,
                        Fill = airlineColors.ContainsKey(flight.Airline) ? airlineColors[flight.Airline] : Brushes.Gray
                    },
                    Offset = new Point(-6, -6),
                    ZIndex = int.MaxValue,
                    Tag = $"{flight.From} → {flight.To} ({flight.Date}) - {flight.Airline} - {flight.Price} PLN"
                };
                ToolTipService.SetToolTip(startMarker.Shape, startMarker.Tag.ToString());
                SearchMap.Markers.Add(startMarker);

                // marker końcowy
                var destCoords = await GetCoordinatesAsync(flight.To);
                if (destCoords.HasValue)
                {
                    var endMarker = new GMapMarker(destCoords.Value)
                    {
                        Shape = new Ellipse
                        {
                            Width = 10,
                            Height = 10,
                            Stroke = Brushes.Black,
                            StrokeThickness = 1,
                            Fill = Brushes.White
                        },
                        Offset = new Point(-5, -5)
                    };
                    SearchMap.Markers.Add(endMarker);

                    // trasa
                    var points = new List<PointLatLng> { new PointLatLng(flight.Lat, flight.Lng), destCoords.Value };
                    var route = new GMapRoute(points)
                    {
                        Shape = new System.Windows.Shapes.Path
                        {
                            Stroke = airlineColors.ContainsKey(flight.Airline) ? airlineColors[flight.Airline] : Brushes.Gray,
                            StrokeThickness = 2
                        }
                    };
                    var routeMarker = new GMapMarker(points[0]) { Shape = route.Shape };
                    SearchMap.Markers.Add(routeMarker);
                }
            }

            if (flights.Count > 0)
                SearchMap.Position = new PointLatLng(flights[0].Lat, flights[0].Lng);
        }
        private async void BtnSzukajPodroz_Click(object sender, RoutedEventArgs e)
        {
            string startCity = txtStartCity.Text.Trim();
            string endCity = txtEndCity.Text.Trim();
            DateTime? date = dpStartDate.SelectedDate;

            // 1️⃣ Provider pattern
            var providers = new List<IFlightProvider>
            {
                new JsonFlightProvider(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loty_lot.json")),
                new JsonFlightProvider(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loty_ryanair.json")),
                new JsonFlightProvider(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loty_wizzair.json")),
                new JsonFlightProvider(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loty_easyjet.json"))
            };
            var metasearch = new FlightMetasearchService(providers);

            var flights = await metasearch.SearchAsync(startCity, endCity, date);

            // 2️⃣ Symulacja ceny i czasu (do rankingu)
            FlightUtils.AssignFakePriceAndDuration(flights);

            // 3️⃣ Ranking lotów
            FlightUtils.CalculateScore(flights);

            _filteredFlights = flights
    .OrderByDescending(f => f.Score)
    .ToList();

            _currentFlightIndex = 0;

            ShowCurrentFlight();
        }

        private void ShowCurrentFlight()
        {
            if (_filteredFlights.Count == 0)
                return;

            var flight = _filteredFlights[_currentFlightIndex];

            DisplayFlightsOnMap(new List<Flight> { flight });
        }
        // Synchronizowana wersja wczytywania lotów
        private List<Flight> LoadFlights()
        {
            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loty.json");
            if (!System.IO.File.Exists(filePath))
                return new List<Flight>();

            string json = System.IO.File.ReadAllText(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<List<Flight>>(json);
        }
        // Minimalny test mapy (do wywołania np. w Loaded)
        private void TestMapMarkers()
        {
            var testMarker = new GMapMarker(new PointLatLng(52.2297, 21.0122))
            {
                Shape = new Ellipse { Width = 20, Height = 20, Fill = Brushes.Red },
                Offset = new Point(-10, -10)
            };
            SearchMap.Markers.Add(testMarker);
            Debug.WriteLine("Dodano testowy marker na mapie.");
        }


        private void NextFlight_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredFlights.Count == 0)
                return;

            _currentFlightIndex++;

            if (_currentFlightIndex >= _filteredFlights.Count)
                _currentFlightIndex = 0;

            ShowCurrentFlight();
        }
        private void PrevFlight_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredFlights.Count == 0)
                return;

            _currentFlightIndex--;

            if (_currentFlightIndex < 0)
                _currentFlightIndex = _filteredFlights.Count - 1;

            ShowCurrentFlight();
        }
        /// <summary>
        /// / Przykładowa metoda do pobrania współrzędnych miasta
        //
        public async Task<PointLatLng?> GetCoordinatesAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return null;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://nominatim.openstreetmap.org/search?city={Uri.EscapeDataString(city)}&format=json&limit=1&accept-language=pl";
                    client.DefaultRequestHeaders.Add("User-Agent", "TravelMateApp/1.0 (daria@travelmate.pl)");

                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                        return null;

                    var jsonArray = JArray.Parse(await response.Content.ReadAsStringAsync());
                    if (jsonArray.Count == 0)
                        return null;

                    var firstResult = jsonArray[0];
                    double lat = double.Parse(firstResult["lat"].ToString(), CultureInfo.InvariantCulture);
                    double lng = double.Parse(firstResult["lon"].ToString(), CultureInfo.InvariantCulture);

                    return new PointLatLng(lat, lng);
                }
            }
            catch
            {
                return null;
            }
        }


        public class POI
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
            public string Address { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; }
        }

        private async Task<POI> GetPOIDetailsAsync(string xid)
        {
            string apiKey = ConfigurationManager.AppSettings["OpenTripMapApiKey"];
            string url = $"https://api.opentripmap.com/0.1/en/places/xid/{xid}?apikey={apiKey}";

            using (HttpClient client = new HttpClient())
            {
                var json = await client.GetStringAsync(url);
                var data = JObject.Parse(json);

                return new POI
                {
                    Name = data["name"]?.ToString() ?? "(bez nazwy)",
                    Type = data["kinds"]?.ToString(),
                    Lat = data["point"]?["lat"]?.ToObject<double>() ?? 0,
                    Lng = data["point"]?["lon"]?.ToObject<double>() ?? 0,
                    Address = data["address"]?["city"]?.ToString() ?? "",
                    Description = data["wikipedia_extracts"]?["text"]?.ToString() ?? "",
                    ImageUrl = data["preview"]?["source"]?.ToString() ?? ""
                };
            }
        }
        private async Task<List<POI>> GetPOIsAsync(string city, string type)
        {
            var pois = new List<POI>();

            try
            {
                var coords = await GetCoordinatesAsync(city);
                if (coords == null)
                {
                    Debug.WriteLine($"[POI] Nie znaleziono współrzędnych dla miasta: {city}");
                    return pois;
                }

                double lat = coords.Value.Lat;
                double lon = coords.Value.Lng;
                string apiKey = ConfigurationManager.AppSettings["OpenTripMapApiKey"];
                string apiType = type;
                String url = $"https://api.opentripmap.com/0.1/en/places/radius?" +
             $"radius=20000&lon={lon.ToString(CultureInfo.InvariantCulture)}&lat={lat.ToString(CultureInfo.InvariantCulture)}&kinds={type}&format=json&apikey={apiKey}";


                Debug.WriteLine($"[POI] URL: {url}");
                Debug.WriteLine($"[POI] Lat={lat}, Lon={lon}, Type={apiType}");
                using (HttpClient client = new HttpClient())
                {
                    var json = await client.GetStringAsync(url);
                    var data = JArray.Parse(json);

                    foreach (var item in data)
                    {
                        string xid = item["xid"]?.ToString();
                        if (!string.IsNullOrEmpty(xid))
                        {
                            var poiDetail = await GetPOIDetailsAsync(xid);
                            pois.Add(poiDetail);
                        }
                    }
                }

                Debug.WriteLine($"[POI] Liczba pobranych punktów: {pois.Count}");
                foreach (var p in pois)
                {
                    Debug.WriteLine($"[POI] {p.Name} ({p.Lat}, {p.Lng})");
                }
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"[POI] Błąd: {ex.Message}");
            }

            return pois;
        }

        private List<POI> _poiResults = new List<POI>();
        private int _currentPOIIndex = 0;

        private void DisplayPOIOnMap(POI poi)
        {
            Debug.WriteLine($"[DisplayPOI] Dodaję marker: {poi.Name} ({poi.Lat}, {poi.Lng})");

            if (poi.Lat == 0 && poi.Lng == 0)
            {
                Debug.WriteLine("[DisplayPOI] Ostrzeżenie: współrzędne są zerowe!");
                return;
            }

            var marker = new GMapMarker(new PointLatLng(poi.Lat, poi.Lng))
            {
                Shape = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Fill = Brushes.Green
                },
                Offset = new Point(-6, -6),
                ZIndex = int.MaxValue,
                Tag = poi // przechowujemy cały obiekt POI w Tag
            };

            // Tworzymy tooltip z opisem i zdjęciem (jeśli są)
            var toolTipPanel = new StackPanel { Width = 200 };

            // Nazwa
            toolTipPanel.Children.Add(new TextBlock
            {
                Text = poi.Name,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });

            // Opis
            if (!string.IsNullOrEmpty(poi.Description))
            {
                toolTipPanel.Children.Add(new TextBlock
                {
                    Text = poi.Description,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 5)
                });
            }

            // Zdjęcie
            if (!string.IsNullOrEmpty(poi.ImageUrl))
            {
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(poi.ImageUrl)),
                    Height = 100,
                    Stretch = Stretch.UniformToFill
                };
                toolTipPanel.Children.Add(image);
            }

            var tooltip = new ToolTip
            {
                Content = toolTipPanel,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
            };

            ToolTipService.SetToolTip(marker.Shape, tooltip);

            SearchMap.Markers.Add(marker);
        }
        private async void BtnSzukajPOI_Click(object sender, RoutedEventArgs e)
        {
            string city = txtCityPOI.Text.Trim();
            string type = ((ComboBoxItem)cbPOIType.SelectedItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(type)) return;

            // Pobranie POI
            _poiResults = await GetPOIsAsync(city, type);

            if (_poiResults.Count == 0)
            {
                Debug.WriteLine("[POI] Nie znaleziono punktów w tym mieście.");
                return;
            }

            // Dodanie wszystkich POI na mapę
            foreach (var poi in _poiResults)
            {
                DisplayPOIOnMap(poi);
            }

            // Ustawienie widoku mapy na pierwszym POI
            if (_poiResults.Count > 0)
            {
                var firstPOI = _poiResults[0];
                SearchMap.Position = new PointLatLng(firstPOI.Lat, firstPOI.Lng);
                SearchMap.Zoom = 12; // ustaw większy zoom, żeby marker był widoczny

                // 🔹 Testowy marker w tym samym miejscu
                var testMarker = new GMapMarker(new PointLatLng(firstPOI.Lat, firstPOI.Lng))
                {
                    Shape = new Ellipse { Width = 20, Height = 20, Fill = Brushes.Red }
                };
                SearchMap.Markers.Add(testMarker);
                Debug.WriteLine("[POI] Dodano testowy marker");
            }

            // Opcjonalnie: aktualizacja licznika POI
            _currentPOIIndex = 0;
            txtPOICounter.Text = $"{_poiResults.Count} punktów na mapie";
        }
        private void BtnPrevPOI_Click(object sender, RoutedEventArgs e)
        {
            if (_poiResults.Count == 0) return;
            _currentPOIIndex = (_currentPOIIndex - 1 + _poiResults.Count) % _poiResults.Count;
            DisplayPOIOnMap(_poiResults[_currentPOIIndex]);
            txtPOICounter.Text = $"{_currentPOIIndex + 1} / {_poiResults.Count}";
        }

        private void BtnNextPOI_Click(object sender, RoutedEventArgs e)
        {
            if (_poiResults.Count == 0) return;
            _currentPOIIndex = (_currentPOIIndex + 1) % _poiResults.Count;
            DisplayPOIOnMap(_poiResults[_currentPOIIndex]);
            txtPOICounter.Text = $"{_currentPOIIndex + 1} / {_poiResults.Count}";
        }

        private List<Airport> _cachedAirports = null;


        // 🔹 Obsługa przycisku
        private async void BtnLoadAirports_Click(object sender, RoutedEventArgs e)
        {
            await LoadAirports();
        }

        // 🔹 Ładowanie lotnisk i dodawanie markerów
        private async Task LoadAirports()
        {
            var airports = await GetAirports();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var airport in airports)
                {
                    Debug.WriteLine($"{airport.name} ({airport.iata_code}): {airport.latitude}, {airport.longitude}");

                    var marker = new GMapMarker(new PointLatLng(airport.latitude, airport.longitude))
                    {
                        Shape = new Label
                        {
                            Content = "✈ " + airport.iata_code,
                            FontSize = 12
                        }
                    };

                    SearchMap.Markers.Add(marker);
                }
            });
        }

        // 🔹 Klasa lotniska
        public class Airport
        {
            public string name { get; set; }
            public string iata_code { get; set; }
            public double latitude { get; set; }
            public double longitude { get; set; }
        }

        // 🔹 Klasy do deserializacji API
        public class AirportAttributes
        {
            public string name { get; set; }
            public string code { get; set; }
            public string type { get; set; }
            public double latitude { get; set; }
            public double longitude { get; set; }
            public string iata_code { get; set; }
        }

        public class AirportData
        {
            public string id { get; set; }
            public string type { get; set; }
            public AirportAttributes attributes { get; set; }
        }

        public class AirportApiResponse
        {
            public List<AirportData> data { get; set; }
        }

        // 🔹 Pobieranie lotnisk z airportsapi.com (tylko duże lotniska)
        public async Task<List<Airport>> GetAirports()

        {
            string[] europeanCountries = new[] { "PL", "DE", "FR", "IT", "ES", "NL", "BE", "CH", "AT", "SE", "NO", "FI", "UA" };
            if (_cachedAirports != null) return _cachedAirports;

             _cachedAirports = new List<Airport>();

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);

                foreach (var country in europeanCountries)
                {
                    string url = $"https://airportsapi.com/api/countries/{country}/airports?filter[type]=large_airport&page[size]=1000";

                    string response;
                    try
                    {
                        response = await client.GetStringAsync(url);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Błąd pobierania API dla kraju {country}: " + ex.Message);
                        continue; // przejdź do następnego kraju
                    }

                    AirportApiResponse apiResponse;
                    try
                    {
                        apiResponse = JsonConvert.DeserializeObject<AirportApiResponse>(response);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Błąd deserializacji JSON dla kraju {country}: " + ex.Message);
                        continue;
                    }

                    if (apiResponse?.data == null || apiResponse.data.Count == 0) continue;

                    var airports = apiResponse.data
                        .Select(a => new Airport
                        {
                            name = a.attributes.name,
                            iata_code = a.attributes.iata_code,
                            latitude = a.attributes.latitude,
                            longitude = a.attributes.longitude
                        })
                        .Where(a => a.latitude != 0 && a.longitude != 0)
                        .ToList();

                    _cachedAirports.AddRange(airports);
                }
            }

            return _cachedAirports;
        }
    }

    public class Podroz
    {
        public int id_podrozy { get; set; }
        public string tytul { get; set; }
        public string miejsce { get; set; }
        public string miejsceEmoji { get; set; } = "🌍";
        public DateTime data_od { get; set; }
        public DateTime data_do { get; set; }
        public double laczneKilometry { get; set; } = 0;
        public double OdlegloscOdDomu { get; set; } = 0;
        public double koszt { get; set; }
        public int iloscEtapow { get; set; }
        public int iloscNoclegow { get; set; }
        public string sredniaOcena { get; set; } = "★★★★☆";
        public string tytulPodstawowy { get; set; }
        public string miasto { get; set; } = "";
        public string kraj { get; set; } = "";
        public int? ocenaLiczbowa { get; set; }
        public string pelnyAdres
        => $"{kraj}, {miasto}, {miejsce}";

        public int iloscLotow { get; set; }
        public int iloscPociagow { get; set; }
    }

    public class Rekomendacja
    {
        public string Tytul { get; set; }
        public string Opis { get; set; }
    }

}