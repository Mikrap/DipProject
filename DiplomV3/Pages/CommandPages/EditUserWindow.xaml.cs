using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using DiplomV3.Properties;

namespace DiplomV3.Pages.CommandPages
{
    public partial class EditUserWindow : Page
    {
        private AdminPage parentPage;
        private string selectedTable;
        private int userId;
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
        private ObservableCollection<UserTableAccess> availableTables = new ObservableCollection<UserTableAccess>();

        public class UserTableAccess
        {
            public string TableName { get; set; }
            public bool IsSelected { get; set; }
        }

        public EditUserWindow(AdminPage parent, int userId)
        {
            InitializeComponent();
            parentPage = parent;
            this.userId = userId;
            selectedTable = parentPage.GetSelectedTable();
            Loaded += EditUserWindow_Loaded;
        }

        private void EditUserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (userId == 0)
            {
                MessageBox.Show("Не выбран элемент для редактирования.");
                return;
            }

            LoadUserData(userId);
            GenerateFields(selectedTable);
            LoadAvailableTables();
        }

        private void LoadUserData(int userId)
        {
            using (var conn = new MySqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    string query = $"SELECT * FROM `{selectedTable}` WHERE `id` = @id";
                    var cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@id", userId);

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        foreach (var control in DynamicFieldsPanel.Children)
                        {
                            if (control is TextBox txtBox && txtBox.Tag != null)
                            {
                                var columnName = txtBox.Tag.ToString();
                                if (reader[columnName] != DBNull.Value)
                                {
                                    txtBox.Text = reader[columnName].ToString();
                                }
                            }
                            else if (control is PasswordBox pwdBox && pwdBox.Tag != null)
                            {
                                var columnName = pwdBox.Tag.ToString();
                                if (reader[columnName] != DBNull.Value)
                                {
                                    pwdBox.Password = reader[columnName].ToString();
                                }
                            }
                            else if (control is DatePicker datePicker && datePicker.Tag != null)
                            {
                                var columnName = datePicker.Tag.ToString();
                                if (reader[columnName] != DBNull.Value)
                                {
                                    datePicker.SelectedDate = Convert.ToDateTime(reader[columnName]);
                                }
                            }
                            else if (control is CheckBox checkBox && checkBox.Tag != null)
                            {
                                var columnName = checkBox.Tag.ToString();
                                checkBox.IsChecked = reader[columnName] != DBNull.Value && Convert.ToInt32(reader[columnName]) == 1;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Пользователь не найден.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при загрузке данных: " + ex.Message);
                }
            }
        }

        private void LoadAvailableTables()
        {
            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Получаем все таблицы в БД
                    var allTables = new List<string>();
                    var cmd = new MySqlCommand("SHOW TABLES", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allTables.Add(reader.GetString(0));
                        }
                    }

                    // Получаем таблицы, доступные пользователю
                    var userTables = new List<string>();
                    cmd = new MySqlCommand("SELECT table_name FROM user_table_access WHERE user_id = @userId", conn);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            userTables.Add(reader.GetString(0));
                        }
                    }

                    // Заполняем список для отображения
                    availableTables.Clear();
                    foreach (var table in allTables)
                    {
                        availableTables.Add(new UserTableAccess
                        {
                            TableName = table,
                            IsSelected = userTables.Contains(table)
                        });
                    }

                    TablesListBox.ItemsSource = availableTables;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц: {ex.Message}");
            }
        }

        private void GenerateFields(string tableName)
        {
            var columns = GetTableSchema(tableName);
            DynamicFieldsPanel.Children.Clear();

            foreach (var column in columns)
            {
                if (column.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var label = new TextBlock
                {
                    Text = column.ColumnName,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Control input = CreateInputField(column.DataType, column.ColumnName);
                if (input != null)
                {
                    DynamicFieldsPanel.Children.Add(label);
                    DynamicFieldsPanel.Children.Add(input);
                }
            }
        }

        private Control CreateInputField(string type, string columnName)
        {
            Control input = null;
            if (type.Contains("int"))
            {
                input = new TextBox { Tag = columnName };
                input.PreviewTextInput += (s, e) => { e.Handled = !int.TryParse(e.Text, out _); };
            }
            else if (type.Contains("date"))
            {
                input = new DatePicker { Tag = columnName };
            }
            else if (type.Contains("bool"))
            {
                input = new CheckBox { Tag = columnName, Content = "Да / Нет" };
            }
            else if (type.Contains("password"))
            {
                input = new PasswordBox { Tag = columnName };
            }
            else
            {
                input = new TextBox { Tag = columnName };
            }

            if (input != null)
            {
                input.Height = 30;
                input.Margin = new Thickness(0, 5, 0, 0);
                input.Background = (Brush)Application.Current.Resources["InputBackgroundColor"];
                input.Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"];
                input.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];
            }

            return input;
        }

        private List<(string ColumnName, string DataType)> GetTableSchema(string tableName)
        {
            var columns = new List<(string, string)>();
            using (var conn = new MySqlConnection(connString))
            {
                conn.Open();
                var cmd = new MySqlCommand($"DESCRIBE `{tableName}`", conn);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string name = reader["Field"].ToString();
                    string type = reader["Type"].ToString().ToLower();
                    columns.Add((name, type));
                }
            }
            return columns;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем основные данные пользователя
            SaveUserData();

            // Сохраняем доступ к таблицам
            SaveTableAccess();

            parentPage.ReturnToUserList();
        }

        private void SaveUserData()
        {
            var values = new Dictionary<string, object>();

            foreach (var child in DynamicFieldsPanel.Children)
            {
                if (child is TextBox tb && tb.Tag is string key1)
                    values[key1] = tb.Text;
                else if (child is PasswordBox pb && pb.Tag is string key2)
                    values[key2] = pb.Password;
                else if (child is DatePicker dp && dp.Tag is string key3)
                    values[key3] = dp.SelectedDate?.ToString("yyyy-MM-dd");
                else if (child is CheckBox cb && cb.Tag is string key4)
                    values[key4] = cb.IsChecked == true ? 1 : 0;
            }

            if (values.Count == 0) return;

            string updateQuery = $"UPDATE `{selectedTable}` SET {string.Join(",", values.Keys.Where(k => k != "id").Select(k => $"{k} = @{k}"))} WHERE id = @id";

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand(updateQuery, conn);

                    foreach (var kv in values.Where(k => k.Key != "id"))
                        cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при обновлении данных пользователя: " + ex.Message);
                }
            }
        }

        private void SaveTableAccess()
        {
            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Удаляем все текущие записи для пользователя
                    var cmd = new MySqlCommand("DELETE FROM user_table_access WHERE user_id = @userId", conn);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.ExecuteNonQuery();

                    // Добавляем выбранные таблицы
                    foreach (var table in availableTables.Where(t => t.IsSelected))
                    {
                        cmd = new MySqlCommand("INSERT INTO user_table_access (user_id, table_name) VALUES (@userId, @tableName)", conn);
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@tableName", table.TableName);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения доступа к таблицам: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            parentPage.ReturnToUserList();
        }
    }
}