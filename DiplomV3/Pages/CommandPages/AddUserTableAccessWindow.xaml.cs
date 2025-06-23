using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using DiplomV3.Properties;

namespace DiplomV3.Pages.CommandPages
{
    public partial class AddUserTableAccessWindow : Page
    {
        private readonly AdminPage _adminPage;

        public AddUserTableAccessWindow(AdminPage adminPage)
        {
            InitializeComponent();
            _adminPage = adminPage;
            LoadTables();
        }

        private void LoadTables()
        {
            try
            {
                string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = "SELECT table_name FROM system_tables WHERE is_system = 0";
                    MySqlCommand cmd = new MySqlCommand(query, conn);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TableNameComboBox.Items.Add(reader["table_name"].ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки таблиц: " + ex.Message);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UserIdTextBox.Text) ||
                string.IsNullOrWhiteSpace(TableNameComboBox.Text))
            {
                MessageBox.Show("Заполните все поля!");
                return;
            }

            if (!int.TryParse(UserIdTextBox.Text, out int userId))
            {
                MessageBox.Show("ID пользователя должен быть числом!");
                return;
            }

            try
            {
                string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = "INSERT INTO user_table_access (user_id, table_name) VALUES (@userId, @tableName)";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@tableName", TableNameComboBox.Text);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Доступ успешно добавлен!");
                _adminPage.ReturnToUserList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка добавления доступа: " + ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _adminPage.ReturnToUserList();
        }
    }
}