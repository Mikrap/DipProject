using System;
using System.Windows;
using System.Windows.Media;
using MySql.Data.MySqlClient;
using DiplomV3.Pages;
using DiplomV3.Properties;

namespace DiplomV3
{
    public partial class MainWindow : Window
    {
        private string Server = Settings.Default.DbServerAddress;
        private string Login = Settings.Default.DbLogin;
        private string DBPassword = Settings.Default.DbPassword;

        public MainWindow()
        {
            InitializeComponent();
            CheckDatabaseConnection();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Обновляем поля при загрузке
            Server = Settings.Default.DbServerAddress;
            Login = Settings.Default.DbLogin;
            DBPassword = Settings.Default.DbPassword;
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text;
            string password = TxtPassword.Password;

            var userData = GetUserData(username, password);

            if (userData != null)
            {
                MainMenuWindow mainMenu = new MainMenuWindow(userData.Value.userId, userData.Value.role);
                mainMenu.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверный логин или пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (int userId, string role)? GetUserData(string username, string password)
        {
            string connString = $"Server={Server};Database=DataBase;Uid={Login};Pwd={DBPassword};";

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    string query = "SELECT Id, role FROM users WHERE username=@user AND password=@pass";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", password);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = reader.GetInt32("Id");
                            string role = reader.GetString("role");
                            return (userId, role);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка подключения: " + ex.Message);
                }
            }
            return null;
        }

        private void CheckDatabaseConnection()
        {
            string connString = $"Server={Server};Database=DataBase;Uid={Login};Pwd={DBPassword};";

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    ConnectionIndicator.Fill = Brushes.Green;
                }
                catch
                {
                    ConnectionIndicator.Fill = Brushes.Red;
                }
            }
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            MainFrame.Visibility = Visibility.Visible;
            MainFrame.Navigate(new Pages.DbConnectionPage());
        }
    }
}