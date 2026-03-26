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

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            EnableWAL();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string haslo = txtHaslo.Password.Trim();
            string email = txtEmail.Text.Trim();

            bool valid = true;

            ResetFieldStyles();

            
            if (string.IsNullOrEmpty(login))
            {
                valid = false;
                SetFieldInvalid(txtLogin, "Login nie może być pusty.");
            }
            else if (login.Length < 3)
            {
                valid = false;
                SetFieldInvalid(txtLogin, "Login musi mieć co najmniej 3 znaki.");
            }

           
            if (string.IsNullOrEmpty(haslo))
            {
                valid = false;
                SetFieldInvalid(txtHaslo, "Hasło nie może być puste.");
            }
            else if (haslo.Length < 6)
            {
                valid = false;
                SetFieldInvalid(txtHaslo, "Hasło musi mieć co najmniej 6 znaków.");
            }

            
            if (!string.IsNullOrEmpty(email))
            {
                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    valid = false;
                    SetFieldInvalid(txtEmail, "Nieprawidłowy format email.");
                }
            }

            if (!valid)
            {
                MessageBox.Show("Niektóre pola są niepoprawne. Sprawdź podpowiedzi przy polach.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;




                using (var connection = new SQLiteConnection(connectionString))

                {
                    connection.Open();

                    var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                    INSERT INTO uzytkownicy (Login, Haslo, Email)
                    VALUES (@login, @haslo, @email)";
                    insertCmd.Parameters.AddWithValue("@login", login);
                    string hashedPassword = HashPassword(haslo);
                    insertCmd.Parameters.AddWithValue("@haslo", hashedPassword);

                    insertCmd.Parameters.AddWithValue("@email", email);


                    insertCmd.ExecuteNonQuery();
                }

                MessageBox.Show($"Użytkownik z loginem '{login}' został zarejestrowany!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisu do bazy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            
           
            this.Close();


            txtLogin.Clear();
            txtHaslo.Clear();
            txtEmail.Clear();
        }

        private void GoToLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close(); 
        }


        private void ResetFieldStyles()
        {
            txtLogin.BorderBrush = Brushes.Gray;
            txtHaslo.BorderBrush = Brushes.Gray;
            txtEmail.BorderBrush = Brushes.Gray;
            txtLogin.ToolTip = null;
            txtHaslo.ToolTip = null;
            txtEmail.ToolTip = null;
        }

        private void SetFieldInvalid(System.Windows.Controls.Control field, string message)
        {
            field.BorderBrush = Brushes.Red;
            field.ToolTip = message;
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


        private void SaveUserToDatabase(string login, string haslo, string email)
        {

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
                INSERT INTO uzytkownicy (login, haslo, email)
                VALUES (@login, @haslo, @email)";

                insertCmd.Parameters.AddWithValue("@login", login);
                insertCmd.Parameters.AddWithValue("@haslo", haslo);
                insertCmd.Parameters.AddWithValue("@email", email);


                insertCmd.ExecuteNonQuery();
            }
        }

        private void EnableWAL()
        {
            try
            {
                using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("PRAGMA journal_mode=WAL;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy ustawianiu WAL: {ex.Message}");
            }
        }


    }
}
   
