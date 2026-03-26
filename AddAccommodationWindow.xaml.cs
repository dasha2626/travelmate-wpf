using System;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GMap.NET;
using GMap.NET.WindowsPresentation; 


namespace TravelMate
{
    public partial class AddAccommodationWindow : Window
    {
        private long tripId;
        private int userId;
     
        public bool IsSelectingMapPoint { get; set; } = false;

        public MapWindow MapOwner { get; set; }

        private double latitude;
        private double longitude;
        public GMapMarker CurrentMarker { get; set; }

        public string fullAddress;
        private DateTime tripStartDate;
        private DateTime tripEndDate;
        public AddAccommodationWindow(long tripId, int userId, DateTime tripStart, DateTime tripEnd)
        {
            InitializeComponent();
            this.tripId = tripId;
            this.userId = userId;
            this.tripStartDate = tripStart;  
            this.tripEndDate = tripEnd;
            LoadAccommodationCategories();

            dpCheckInDate.DisplayDateStart = tripStartDate.AddDays(-10);
            dpCheckInDate.DisplayDateEnd = tripEndDate.AddDays(10);

            dpCheckOutDate.DisplayDateStart = tripStartDate.AddDays(-10);
            dpCheckOutDate.DisplayDateEnd = tripEndDate.AddDays(10);
            dpCheckInDate.SelectedDateChanged += DpCheckInDate_SelectedDateChanged;
            dpCheckOutDate.SelectedDateChanged += DpCheckOutDate_SelectedDateChanged;


        }
        public class CategoryItem
        {
            public int Id { get; set; }
            public string Icon { get; set; }
        }


        private void BtnSetAccommodationAddressOnMap_Click(object sender, RoutedEventArgs e)
        {
            if (MapOwner != null)
            {
                IsSelectingMapPoint = true;
                MapOwner.CurrentAddAccommodationWindow = this;

                this.Hide();
                MapOwner.Focus();

                MessageBox.Show("Kliknij na mapie punkt, który chcesz ustawić jako adres noclegu.");
            }
        }

        public void SetPointFromMap(double lat, double lng, string address)
        {
            if (!IsSelectingMapPoint) return;

            latitude = lat;
            longitude = lng;
            fullAddress = address;
            txtAddress.Text = fullAddress;

            
            string iconUri = "pack://application:,,,/images/nocleg.png";

            if (cmbAccommodationType.SelectedItem is ComboBoxItem selected &&
                selected.Tag is CategoryItem cat)
            {
               
                iconUri = $"pack://application:,,,/images/{cat.Icon}";
            }

            if (MapOwner != null)
            {
                CurrentMarker = new GMapMarker(new PointLatLng(lat, lng))
                {
                    Shape = new Image
                    {
                        Width = 32,
                        Height = 32,
                        Source = new BitmapImage(new Uri(iconUri, UriKind.Absolute)),
                        ToolTip = fullAddress
                    },
                    Offset = new Point(-16, -32)
                };

                MapOwner.MainMap.Markers.Add(CurrentMarker);
                MapOwner.TemporaryMarkers.Add(CurrentMarker);

                MapOwner.CurrentAddAccommodationWindow = null;
            }

            IsSelectingMapPoint = false;

            this.Show();
            this.Activate();
            MessageBox.Show("Adres noclegu ustawiony na mapie!");
        }

        private void LoadAccommodationCategories()
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT id_kategorii, nazwa, ikona FROM kategorie_noclegow ORDER BY nazwa", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cmbAccommodationType.Items.Add(new ComboBoxItem
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

   
            var editableTextBox = (TextBox)cmbAccommodationType.Template.FindName("PART_EditableTextBox", cmbAccommodationType);
            if (editableTextBox != null)
                editableTextBox.TextChanged += EditableAccommodationTextBox_TextChanged;
        }
        private void EditableAccommodationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CurrentMarker == null) return;

            string typedText = cmbAccommodationType.Text.Trim();
           
            string iconUri = "pack://application:,,,/Images/nocleg.png";

            foreach (ComboBoxItem item in cmbAccommodationType.Items)
            {
                if (string.Equals(item.Content.ToString(), typedText, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Tag is CategoryItem cat)
                    {
                      
                        iconUri = $"pack://application:,,,/Images/{cat.Icon}";
                    }
                    break;
                }
            }

            if (CurrentMarker.Shape is Image img)
            {
                img.Source = new BitmapImage(new Uri(iconUri, UriKind.Absolute));
            }
        }

        private bool ValidateAccommodationDates(DateTime checkIn, DateTime checkOut)
        {
            DateTime minDate = tripStartDate.AddDays(-10);
            DateTime maxDate = tripEndDate.AddDays(10);

            if (checkIn < minDate || checkIn > maxDate)
            {
                MessageBox.Show($"Data zameldowania musi być w zakresie {minDate:dd.MM.yyyy} – {maxDate:dd.MM.yyyy}");
                return false;
            }

            if (checkOut < minDate || checkOut > maxDate)
            {
                MessageBox.Show($"Data wymeldowania musi być w zakresie {minDate:dd.MM.yyyy} – {maxDate:dd.MM.yyyy}");
                return false;
            }

            if (checkIn >= checkOut)
            {
                MessageBox.Show("Data zameldowania musi być wcześniejsza niż data wymeldowania.");
                return false;
            }

            return true;
        }



        public void SetAddressFromMap(string address)
        {
            if (!IsSelectingMapPoint) return;

            fullAddress = address;
            txtAddress.Text = fullAddress;

            IsSelectingMapPoint = false;

            if (MapOwner != null)
                MapOwner.CurrentAddAccommodationWindow = null;

            this.Show();
            this.Activate();

            MessageBox.Show("Адрес noclegu ustawiony на mapie!");
        }

        private void BtnSaveAccommodation_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAddress.Text))
            {
                MessageBox.Show("Ustaw adres noclegu na mapie.");
                return;
            }
            if (!dpCheckInDate.SelectedDate.HasValue || !dpCheckOutDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz daty zameldowania i wymeldowania.");
                return;
            }

            DateTime checkIn = dpCheckInDate.SelectedDate.Value;
            DateTime checkOut = dpCheckOutDate.SelectedDate.Value;

   
            if (!ValidateAccommodationDates(checkIn, checkOut))
                return;
            if (!IsAccommodationDateAvailable(checkIn))
            {
                MessageBox.Show("Już masz nocleg w tym dniu. Wybierz inną datę zameldowania.");
                return;
            }
          


            SaveAccommodationToDatabase();
            if (MapOwner?.CurrentTripDetailsWindow != null)
            {
                MapOwner.CurrentTripDetailsWindow.LoadExpenses();
            }
            this.Close();
        }

        private void txtRating_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
    
            e.Handled = !"12345".Contains(e.Text);
        }
        private void DpCheckInDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpCheckInDate.SelectedDate.HasValue)
            {
      
                dpCheckOutDate.DisplayDateStart = dpCheckInDate.SelectedDate.Value.AddDays(1);

                if (dpCheckOutDate.SelectedDate.HasValue &&
                    dpCheckOutDate.SelectedDate.Value <= dpCheckInDate.SelectedDate.Value)
                {
                    dpCheckOutDate.SelectedDate = null;
                }
            }
        }
        private void DpCheckOutDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpCheckOutDate.SelectedDate.HasValue)
            {
         
                dpCheckInDate.DisplayDateEnd = dpCheckOutDate.SelectedDate.Value.AddDays(-1);


                if (dpCheckInDate.SelectedDate.HasValue &&
                    dpCheckInDate.SelectedDate.Value >= dpCheckOutDate.SelectedDate.Value)
                {
                    dpCheckInDate.SelectedDate = null;
                }
            }
        }
        private bool IsAccommodationDateAvailable(DateTime checkIn)
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(@"
            SELECT COUNT(*) 
            FROM noclegi
            WHERE id_podrozy = @trip
              AND @checkIn >= data_od
              AND @checkIn < data_do", conn)) 
                {
                    cmd.Parameters.AddWithValue("@trip", tripId);
                    cmd.Parameters.AddWithValue("@checkIn", checkIn.ToString("yyyy-MM-dd"));

                    long count = (long)cmd.ExecuteScalar();
                    return count == 0;
                }
            }
        }







        private void SaveAccommodationToDatabase()
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                string selectedCurrency = (cmbCurrency.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "PLN";

                int? categoryId = null;
                string typedCategory = cmbAccommodationType.Text.Trim();
               
     
                using (var cmdCheck = new SQLiteCommand("SELECT id_kategorii FROM kategorie_noclegow WHERE nazwa=@nazwa", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@nazwa", typedCategory);
                    var result = cmdCheck.ExecuteScalar();
                    if (result != null)
                        categoryId = Convert.ToInt32(result);
                }

 
                if (!categoryId.HasValue && !string.IsNullOrWhiteSpace(typedCategory))
                {
                    using (var cmdInsert = new SQLiteCommand(
                        "INSERT INTO kategorie_noclegow (nazwa, ikona, czy_domyslna) VALUES (@nazwa, @ikona, 0); SELECT last_insert_rowid();", conn))
                    {
                        cmdInsert.Parameters.AddWithValue("@nazwa", typedCategory);
                        cmdInsert.Parameters.AddWithValue("@ikona", "default.png"); 
                        categoryId = Convert.ToInt32(cmdInsert.ExecuteScalar());
                    }
                }


                long newPointId;
                using (var cmd = new SQLiteCommand(
      @"INSERT INTO punkty_noclegowe (nazwa, opis, szerokosc, dlugosc, id_kategorii, adres) 
      VALUES (@nazwa, @opis, @szerokosc, @dlugosc, @id_kategorii, @adres); 
      SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@nazwa",
                    string.IsNullOrWhiteSpace(txtAccommodationName.Text)
                    ? (object)DBNull.Value
                    : txtAccommodationName.Text.Trim());

                    
                    cmd.Parameters.AddWithValue("@opis", txtNotes.Text.Trim());
                    cmd.Parameters.AddWithValue("@szerokosc", latitude);
                    cmd.Parameters.AddWithValue("@dlugosc", longitude);
                    cmd.Parameters.AddWithValue("@id_kategorii", categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@adres", fullAddress ?? "");

                    newPointId = (long)cmd.ExecuteScalar();
                }




                using (var cmd2 = new SQLiteCommand(
     @"INSERT INTO noclegi (id_podrozy, id_punktu, data_od, data_do, cena, waluta, ocena, uwagi)
      VALUES (@trip, @punkt, @dataOd, @dataDo, @cena, @waluta, @ocena, @uwagi)", conn))
                {
                    cmd2.Parameters.AddWithValue("@trip", tripId);
                    cmd2.Parameters.AddWithValue("@punkt", newPointId);
                    cmd2.Parameters.AddWithValue("@dataOd", dpCheckInDate.SelectedDate.Value.ToString("yyyy-MM-dd"));
                    cmd2.Parameters.AddWithValue("@dataDo", dpCheckOutDate.SelectedDate.Value.ToString("yyyy-MM-dd"));
                    cmd2.Parameters.AddWithValue("@cena", string.IsNullOrWhiteSpace(txtPrice.Text) ? DBNull.Value : (object)txtPrice.Text.Trim());
                    cmd2.Parameters.AddWithValue("@waluta", selectedCurrency);
                    string selectedRating = (cmbRating.SelectedItem as ComboBoxItem)?.Content.ToString();
                    cmd2.Parameters.AddWithValue("@ocena", string.IsNullOrWhiteSpace(selectedRating) ? DBNull.Value : (object)selectedRating);
                    cmd2.Parameters.AddWithValue("@uwagi", txtNotes.Text.Trim());

                    cmd2.ExecuteNonQuery();
                }

            }

            MessageBox.Show("Nocleg zapisany pomyślnie.");
        }
        private void cmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (cmbCurrency.SelectedItem is ComboBoxItem selected)
            {
                string selectedCurrency = selected.Content.ToString();

                txtPrice.ToolTip = $"Cena w {selectedCurrency}";
            }
        }
        private void txtPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox tb = sender as TextBox;
            char c = e.Text[0];

          
            if (!char.IsDigit(c) && c != '.')
            {
                e.Handled = true;
                return;
            }

         
            if (c == '.' && tb.Text.Contains('.'))
            {
                e.Handled = true;
                return;
            }

           
            string newText = tb.Text.Substring(0, tb.SelectionStart) + c + tb.Text.Substring(tb.SelectionStart);

            string[] parts = newText.Split('.');

            
            if (parts[0].Length == 2 && parts[0][0] == '0')
            {
                e.Handled = true;
                return;
            }

        
            if (parts.Length == 2 && parts[1].Length > 2)
            {
                e.Handled = true;
                return;
            }

            e.Handled = false;
        }

        private void txtPrice_PreviewKeyDown(object sender, KeyEventArgs e)
        {
           
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



        private void cmbAccommodationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentMarker == null) return;

       
            string iconUri = "pack://application:,,,/Images/nocleg.png";

            if (cmbAccommodationType.SelectedItem is ComboBoxItem selected && selected.Tag is CategoryItem cat)
            {
                
                iconUri = $"pack://application:,,,/Images/{cat.Icon}";
            }

            if (CurrentMarker.Shape is Image img)
            {
                img.Source = new BitmapImage(new Uri(iconUri, UriKind.Absolute));
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (CurrentMarker != null && MapOwner != null)
            {
                MapOwner.MainMap.Markers.Remove(CurrentMarker);
                MapOwner.TemporaryMarkers.Remove(CurrentMarker);
                CurrentMarker = null;
            }


            if (MapOwner != null)
                MapOwner.CurrentAddAccommodationWindow = null;
        }


    }
}
