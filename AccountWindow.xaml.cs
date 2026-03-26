using GMap.NET.WindowsPresentation;
using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace TravelMate
{
    public partial class AccountWindow : Window
    {
        private int userId;
    
        private GMapControl sharedMap;


        public AccountWindow(int userId, GMapControl map)
        {
            InitializeComponent();
            this.userId = userId;
            this.sharedMap = map;
            WczytajDaneUzytkownika();
        }

        private void WczytajDaneUzytkownika()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT login, email, dom_kraj, dom_miasto, dom_adres FROM uzytkownicy WHERE id_uzytkownika=@id";

                        cmd.Parameters.AddWithValue("@id", userId);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                txtLogin.Text = r["login"].ToString();
                                txtEmail.Text = r["email"]?.ToString() ?? "";
                                if (r["dom_kraj"] != DBNull.Value)
                                {
                                    string kraj = r["dom_kraj"].ToString();
                                    string miasto = r["dom_miasto"].ToString();
                                    string adres = r["dom_adres"].ToString();
                                    txtDom.Text = $"{kraj}, {miasto}, {adres}";
                                }

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            string nowyLogin = txtLogin.Text.Trim();
            string nowyEmail = txtEmail.Text.Trim();
            string aktualneHaslo = pwdCurrent.Password.Trim();
            string noweHaslo = pwdNew.Password.Trim();

           
            if (string.IsNullOrWhiteSpace(nowyLogin) || nowyLogin.Length < 3)
            {
                MessageBox.Show("Login musi mieć co najmniej 3 znaki.", "Błąd danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(nowyEmail))
            {
                if (!Regex.IsMatch(nowyEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    MessageBox.Show("Niepoprawny format adresu e-mail.", "Błąd danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            bool zmianaHasla = !string.IsNullOrEmpty(noweHaslo);
            if (zmianaHasla && string.IsNullOrEmpty(aktualneHaslo))
            {
                MessageBox.Show("Aby zmienić hasło, podaj aktualne hasło.", "Błąd danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                   
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = "SELECT COUNT(*) FROM uzytkownicy WHERE login=@login AND id_uzytkownika<>@id";
                        chk.Parameters.AddWithValue("@login", nowyLogin);
                        chk.Parameters.AddWithValue("@id", userId);
                        long count = (long)chk.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Ten login jest już zajęty. Wybierz inny.", "Błąd danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    
                    if (zmianaHasla)
                    {
                        using (var verify = conn.CreateCommand())
                        {
                            verify.CommandText = "SELECT haslo FROM uzytkownicy WHERE id_uzytkownika=@id";
                            verify.Parameters.AddWithValue("@id", userId);
                            object obj = verify.ExecuteScalar();
                            string zapisaneHaslo = obj?.ToString() ?? "";

                            string podaneHaslo = HaszujHaslo(aktualneHaslo);
                            if (!string.Equals(zapisaneHaslo, podaneHaslo, StringComparison.OrdinalIgnoreCase))
                            {
                                MessageBox.Show("Aktualne hasło jest niepoprawne.", "Błąd uwierzytelnienia", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }

                    
                    using (var upd = conn.CreateCommand())
                    {
                        if (zmianaHasla)
                        {
                            string noweHasloHash = HaszujHaslo(noweHaslo);
                            upd.CommandText = "UPDATE uzytkownicy SET login=@login, email=@mail, haslo=@haslo WHERE id_uzytkownika=@id";
                            upd.Parameters.AddWithValue("@haslo", noweHasloHash);
                        }
                        else
                        {
                            upd.CommandText = "UPDATE uzytkownicy SET login=@login, email=@mail WHERE id_uzytkownika=@id";
                        }

                        upd.Parameters.AddWithValue("@login", nowyLogin);
                        upd.Parameters.AddWithValue("@mail", nowyEmail);
                        upd.Parameters.AddWithValue("@id", userId);
                        upd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Dane konta zostały zaktualizowane pomyślnie.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas zapisu danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnWyloguj_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Czy na pewno chcesz się wylogować?",
                                "Wylogowanie",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
            
                App.Current.Properties["UserId"] = null;
                App.Current.Properties["Login"] = null;
                App.Current.Properties["Email"] = null;

                
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();

                
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != mainWindow)
                        window.Close();
                }
            }
        }

        private string HaszujHaslo(string haslo)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(haslo);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private void BtnPodajDom_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MapWindow mapa)
            {
                mapa.IsSelectingHome = true;
                mapa.Cursor = Cursors.Cross;

                mapa.AddressSelected += (kraj, miasto, adres, szer, dlug) =>
                {
                    txtDom.Text = $"{kraj}, {miasto}, {adres}";
                    ZapiszDomWBazie(kraj, miasto, adres, szer, dlug);
                };

                this.Close(); 
                mapa.Show();  
            }
        }


        private void ZapiszDomWBazie(string kraj, string miasto, string adres, double szer, double dlug)
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"UPDATE uzytkownicy 
                                    SET dom_kraj=@kraj, dom_miasto=@miasto, dom_adres=@adres, 
                                        dom_szerokosc=@szer, dom_dlugosc=@dlug 
                                    WHERE id_uzytkownika=@id";

                        cmd.Parameters.AddWithValue("@kraj", kraj);
                        cmd.Parameters.AddWithValue("@miasto", miasto);
                        cmd.Parameters.AddWithValue("@adres", adres);
                        cmd.Parameters.AddWithValue("@szer", szer);
                        cmd.Parameters.AddWithValue("@dlug", dlug);
                        cmd.Parameters.AddWithValue("@id", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu lokalizacji domu: {ex.Message}");
            }
        }


    }
}
