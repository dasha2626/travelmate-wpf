using System;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
namespace TravelMate
{

    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }


        private string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string haslo = txtHaslo.Password.Trim();

            bool valid = true;
            ResetFieldStyles();

            
            if (string.IsNullOrEmpty(login))
            {
                valid = false;
                SetFieldInvalid(txtLogin, "Login nie może być pusty.");
            }

            
            if (string.IsNullOrEmpty(haslo))
            {
                valid = false;
                SetFieldInvalid(txtHaslo, "Hasło nie może być puste.");
            }

            if (!valid)
            {
                MessageBox.Show("Niektóre pola są niepoprawne.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var selectCmd = conn.CreateCommand();
                    string hashedPassword = HashPassword(haslo);

                    selectCmd.CommandText = "SELECT COUNT(*) FROM uzytkownicy WHERE Login=@login AND Haslo=@haslo";
                    selectCmd.Parameters.AddWithValue("@login", login);
                    selectCmd.Parameters.AddWithValue("@haslo", hashedPassword);


                    int count = Convert.ToInt32(selectCmd.ExecuteScalar());

                    if (count > 0)
                    {
                        
                        var idCmd = conn.CreateCommand();
                        idCmd.CommandText = "SELECT id_uzytkownika FROM uzytkownicy WHERE Login=@login";
                        idCmd.Parameters.AddWithValue("@login", login);
                        int userId = Convert.ToInt32(idCmd.ExecuteScalar());

                        
                        MapWindow mapWindow = new MapWindow(userId);
                        mapWindow.Show();
                        this.Close();
                    }

                    else
                    {
                        MessageBox.Show("Niepoprawny login lub hasło.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas logowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToRegister_Click(object sender, RoutedEventArgs e)
        {
            MainWindow registerWindow = new MainWindow();
            registerWindow.Show();
            this.Close(); 
        }

        private void ResetFieldStyles()
        {
            txtLogin.BorderBrush = Brushes.Gray;
            txtHaslo.BorderBrush = Brushes.Gray;
            txtLogin.ToolTip = null;
            txtHaslo.ToolTip = null;
        }

        private void SetFieldInvalid(Control field, string message)
        {
            field.BorderBrush = Brushes.Red;
            field.ToolTip = message;
        }
    }
}
