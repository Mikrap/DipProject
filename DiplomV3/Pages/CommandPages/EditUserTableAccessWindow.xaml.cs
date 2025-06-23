using System;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using DiplomV3.Properties;

namespace DiplomV3.Pages.CommandPages
{
    public partial class EditUserTableAccessWindow : Page
    {
        private readonly AdminPage _adminPage;
        private readonly int _userId;
        private readonly string _currentTableName;

        public EditUserTableAccessWindow(AdminPage adminPage, int userId, string tableName)
        {
            InitializeComponent();
            _adminPage = adminPage;
            _userId = userId;
            _currentTableName = tableName;

            // Устанавливаем значения в поля
            UserIdTextBox.Text = userId.ToString();
            CurrentTableTextBox.Text = tableName;

            LoadAvailableTables();
        }

        private void LoadAvailableTables()
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
                            string tableName = reader["table_name"].ToString();
                            NewTableComboBox.Items.Add(tableName);

                            // Выбираем текущую таблицу, если она есть в списке
                            if (tableName == _currentTableName)
                            {
                                NewTableComboBox.SelectedItem = tableName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки таблиц: " + ex.Message);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (NewTableComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите новую таблицу!");
                return;
            }

            string newTableName = NewTableComboBox.SelectedItem.ToString();

            if (newTableName == _currentTableName)
            {
                MessageBox.Show("Новая таблица совпадает с текущей!");
                return;
            }

            var result = MessageBox.Show($"Заменить доступ для пользователя {_userId} с таблицы '{_currentTableName}' на '{newTableName}'?",
                                        "Подтверждение", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Удаляем старую запись
                    string deleteQuery = "DELETE FROM user_table_access WHERE user_id = @userId AND table_name = @currentTableName";
                    MySqlCommand deleteCmd = new MySqlCommand(deleteQuery, conn);
                    deleteCmd.Parameters.AddWithValue("@userId", _userId);
                    deleteCmd.Parameters.AddWithValue("@currentTableName", _currentTableName);
                    deleteCmd.ExecuteNonQuery();

                    // Добавляем новую запись
                    string insertQuery = "INSERT INTO user_table_access (user_id, table_name) VALUES (@userId, @newTableName)";
                    MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@userId", _userId);
                    insertCmd.Parameters.AddWithValue("@newTableName", newTableName);
                    insertCmd.ExecuteNonQuery();
                }

                MessageBox.Show("Доступ успешно изменен!");
                _adminPage.ReturnToUserList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка изменения доступа: " + ex.Message);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить этот доступ?", "Подтверждение", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = "DELETE FROM user_table_access WHERE user_id = @userId AND table_name = @tableName";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@userId", _userId);
                    cmd.Parameters.AddWithValue("@tableName", _currentTableName);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Доступ успешно удален!");
                _adminPage.ReturnToUserList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления доступа: " + ex.Message);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _adminPage.ReturnToUserList();
        }
    }
}