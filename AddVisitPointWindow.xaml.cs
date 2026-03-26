using GMap.NET.WindowsPresentation;
using GMap.NET;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Data.SQLite;
using System.Linq;
using System.Windows.Input;

namespace TravelMate
{
    public partial class AddVisitPointWindow : Window
    {
        private long tripId;
        private int userId;


        public MapWindow MapOwner { get; set; }
        private bool isSelectingMapPoint = false;
        private GMapMarker currentMarker;
        private DateTime tripStartDate;
        private DateTime tripEndDate;
        public TripDetailsWindow CurrentTripDetailsWindow { get; set; }

        public class CategoryItem
        {
            public int Id { get; set; }
            public string Icon { get; set; }
        }



        public double pointLat, pointLng;
        public string fullAddress;

        public AddVisitPointWindow(long tripId, int userId, DateTime tripStart, DateTime tripEnd)
        {
            InitializeComponent();
            this.tripId = tripId;
            this.userId = userId;
            this.tripStartDate = tripStart;
            this.tripEndDate = tripEnd;

            LoadCategories();

            var editableTextBox = (TextBox)cmbCategory.Template.FindName("PART_EditableTextBox", cmbCategory);
            if (editableTextBox != null)
            {
                editableTextBox.TextChanged += EditableTextBox_TextChanged;
            }

            dpVisitDate.DisplayDateStart = tripStartDate.AddDays(-10);
            dpVisitDate.DisplayDateEnd = tripEndDate.AddDays(10);
        }




 
        private void BtnSetPointOnMap_Click(object sender, RoutedEventArgs e)
        {
            if (MapOwner != null)
            {
                isSelectingMapPoint = true;
                MapOwner.IsAddingPoint = true;
                MapOwner.CurrentAddVisitPointWindow = this;

                this.Hide();
                MapOwner.Focus();

                MessageBox.Show("Kliknij na mapie punkt, który chcesz ustawić jako miejsce zwiedzania.");
            }
        }

        public void SetPointFromMap(double lat, double lng, string address)
        {

          

            if (!isSelectingMapPoint) return;

            pointLat = lat;
            pointLng = lng;
            fullAddress = address;

            txtPlaceName.Text = fullAddress;


           
            string iconPath = "pack://application:,,,/Images/pinezka.png";

            if (cmbCategory.SelectedItem is ComboBoxItem selected &&
                selected.Tag is CategoryItem cat)
            {
             
                iconPath = $"pack://application:,,,/Images/{cat.Icon}";
            }




            if (MapOwner != null)
            {
                currentMarker = new GMapMarker(new PointLatLng(lat, lng))
                {
                    Shape = new Image
                    {
                        Width = 32,
                        Height = 32,
                        Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute)),
                        ToolTip = fullAddress
                    },
                    Offset = new Point(-16, -32)
                };

                MapOwner.MainMap.Markers.Add(currentMarker);
                MapOwner.TemporaryMarkers.Add(currentMarker);



                MapOwner.IsAddingPoint = false;
                MapOwner.CurrentAddVisitPointWindow = null;
            }

            isSelectingMapPoint = false;

            this.Show();
            MessageBox.Show("Punkt zwiedzania ustawiony na mapie!");
        }
        private bool ValidateVisitDate(DateTime visitDate)
        {
            DateTime minDate = tripStartDate.AddDays(-10);
            DateTime maxDate = tripEndDate.AddDays(10);

            if (visitDate < minDate || visitDate > maxDate)
            {
                MessageBox.Show($"Data punktu zwiedzania musi być w przedziale {minDate:dd.MM.yyyy} – {maxDate:dd.MM.yyyy}");
                return false;
            }

            return true;
        }

        private void cmbCategory_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (currentMarker == null) return;

            string typedText = cmbCategory.Text.Trim();
            ComboBoxItem matchedItem = null;

            foreach (ComboBoxItem item in cmbCategory.Items)
            {
                if (string.Equals(item.Content.ToString(), typedText, StringComparison.OrdinalIgnoreCase))
                {
                    matchedItem = item;
                    break;
                }
            }

            string iconPath;

            if (matchedItem != null && matchedItem.Tag is CategoryItem cat)
            {
                
                iconPath = $"pack://application:,,,/Images/{cat.Icon}";
            }
            else
            {
                
                iconPath = "pack://application:,,,/Images/pin.png";
                cmbCategory.SelectedItem = null;
            }

            if (currentMarker.Shape is Image img)
                img.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
        }

        private void EditableTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (currentMarker == null) return;

            string typedText = cmbCategory.Text.Trim();
            string iconPath = "pack://application:,,,/Images/pin.png"; 

            foreach (ComboBoxItem item in cmbCategory.Items)
            {
                if (string.Equals(item.Content.ToString(), typedText, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Tag is CategoryItem cat)
                    {
                        
                        iconPath = $"pack://application:,,,/Images/{cat.Icon}";
                    }
                    break;
                }
            }

            if (currentMarker.Shape is Image img)
                img.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
        }



        private void cmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCategory.IsDropDownOpen == false && cmbCategory.IsEditable)
                return;

            if (currentMarker == null) return;

            if (cmbCategory.SelectedItem is ComboBoxItem selected &&
                selected.Tag is CategoryItem cat)
            {
                
                string iconPath = $"pack://application:,,,/Images/{cat.Icon}";

                if (currentMarker.Shape is Image img)
                    img.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            }
        }

        private void BtnSaveVisitPoint_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrEmpty(txtPlaceName.Text) || pointLat == 0 || pointLng == 0)
            {
                MessageBox.Show("Ustaw lokalizację na mapie przed zapisaniem!");
                return;
            }
            if (!dpVisitDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę punktu zwiedzania.");
                return;
            }

            DateTime visitDate = dpVisitDate.SelectedDate.Value;

            if (!ValidateVisitDate(visitDate))
                return;

            SaveVisitPointToDatabase();


            CurrentTripDetailsWindow?.LoadExpenses();


     
            if (currentMarker != null && MapOwner != null)
            {
                MapOwner.MainMap.Markers.Remove(currentMarker);
                currentMarker = null;
            }
            this.Close();
        }


        private void LoadCategories()
        {
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand("SELECT id_kategorii, nazwa, ikona FROM kategorie ORDER BY nazwa", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cmbCategory.Items.Add(new ComboBoxItem
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



        private void SaveVisitPointToDatabase()
        {
            object walutaValue = (cmbCurrency.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PLN";
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                int? categoryId = null;
                string typedCategory = cmbCategory.Text.Trim();

 
                using (var cmdCheck = new SQLiteCommand("SELECT id_kategorii FROM kategorie WHERE nazwa=@nazwa", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@nazwa", typedCategory);
                    var result = cmdCheck.ExecuteScalar();
                    if (result != null)
                    {
                        categoryId = Convert.ToInt32(result);
                    }
                }

                if (!categoryId.HasValue && !string.IsNullOrWhiteSpace(typedCategory))
                {
                    using (var cmdInsertCat = new SQLiteCommand(
                               "INSERT INTO kategorie (nazwa, ikona, czy_domyslna) VALUES (@nazwa, @ikona, 0); SELECT last_insert_rowid();", conn))
                    {
                        cmdInsertCat.Parameters.AddWithValue("@nazwa", typedCategory);
                        cmdInsertCat.Parameters.AddWithValue("@ikona", "default.png"); 
                        categoryId = Convert.ToInt32(cmdInsertCat.ExecuteScalar());
                    }
                }

                long newPointId;
                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO punkty_zwiedzania (nazwa, szerokosc, dlugosc, typ, id_kategorii) 
              VALUES (@nazwa, @lat, @lng, @typ, @idKat); 
              SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@nazwa", txtPlaceName.Text.Trim());
                    cmd.Parameters.AddWithValue("@lat", pointLat);
                    cmd.Parameters.AddWithValue("@lng", pointLng);
                    cmd.Parameters.AddWithValue("@typ", typedCategory);
                    cmd.Parameters.AddWithValue("@idKat", categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);

                    newPointId = (long)cmd.ExecuteScalar();
                }

                using (var cmd2 = new SQLiteCommand(
                    @"INSERT INTO zwiedzanie (id_podrozy, id_punktu, data, cena, waluta, ocena, uwagi)
              VALUES (@trip, @punkt, @data, @cena, @waluta, @ocena, @uwagi)", conn))
                {
                    object dataValue = dpVisitDate.SelectedDate.HasValue
                        ? (object)dpVisitDate.SelectedDate.Value.ToString("yyyy-MM-dd")
                        : DBNull.Value;

                    object cenaValue = string.IsNullOrWhiteSpace(txtTicketPrice.Text)
                        ? (object)DBNull.Value
                        : (object)txtTicketPrice.Text.Trim();

                    int? ocenaValue = cbVisitRating.SelectedIndex >= 0
      ? cbVisitRating.SelectedIndex + 1
      : (int?)null;


                    cmd2.Parameters.AddWithValue("@trip", tripId);
                    cmd2.Parameters.AddWithValue("@punkt", newPointId);
                    cmd2.Parameters.AddWithValue("@data", dataValue);
                    cmd2.Parameters.AddWithValue("@cena", string.IsNullOrWhiteSpace(txtTicketPrice.Text)
                     ? DBNull.Value
                    : (object)txtTicketPrice.Text.Trim());
                    cmd2.Parameters.AddWithValue("@waluta", walutaValue);
                    cmd2.Parameters.AddWithValue("@ocena",
     ocenaValue.HasValue ? (object)ocenaValue.Value : DBNull.Value);

                    cmd2.Parameters.AddWithValue("@uwagi", txtNotes.Text.Trim());

                    cmd2.ExecuteNonQuery();
                }
            }

            MessageBox.Show("Punkt zwiedzania zapisany pomyślnie.");
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (currentMarker != null && MapOwner != null)
            {
                MapOwner.MainMap.Markers.Remove(currentMarker);
                MapOwner.TemporaryMarkers.Remove(currentMarker);
                currentMarker = null;
            }

            if (MapOwner != null)
                MapOwner.CurrentAddVisitPointWindow = null;
        }


    }

}
