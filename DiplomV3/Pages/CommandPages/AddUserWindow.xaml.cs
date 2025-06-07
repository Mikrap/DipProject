using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiplomV3.Properties;

namespace DiplomV3.Pages.CommandPages
{
    public partial class AddUserWindow : Page
    {
        private AdminPage parentPage;
        private string selectedTable;
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

        public AddUserWindow(AdminPage parent)
        {
            InitializeComponent();
            parentPage = parent;
            selectedTable = parentPage.GetSelectedTable();
            Loaded += AddUserWindow_Loaded;
        }

        private void AddUserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedTable))
            {
                MessageBox.Show("Выберите таблицу перед добавлением данных.");
                return;
            }

            var columns = GetTableSchema(selectedTable);
            GenerateFields(columns);
        }

        private List<string> GetAutoIncrementColumns(string tableName)
        {
            var autoColumns = new List<string>();

            using (var conn = new MySqlConnection(connString))
            {
                conn.Open();
                string query = $"SHOW COLUMNS FROM `{tableName}` WHERE Extra LIKE '%auto_increment%'";
                var cmd = new MySqlCommand(query, conn);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    autoColumns.Add(reader["Field"].ToString());
                }
            }

            return autoColumns;
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

        private void GenerateFields(List<(string ColumnName, string DataType)> columns)
        {
            var autoCols = GetAutoIncrementColumns(selectedTable);
            DynamicFieldsPanel.Children.Clear();

            if (columns.Count == 0)
            {
                MessageBox.Show("Нет доступных столбцов для добавления.");
                return;
            }

            foreach (var (col, type) in columns)
            {
                if (autoCols.Contains(col)) continue;

                var label = new TextBlock
                {
                    Text = col,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Control input = CreateInputField(type, col);
                if (input == null)
                    continue;

                DynamicFieldsPanel.Children.Add(label);
                DynamicFieldsPanel.Children.Add(input);
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

            input.Height = 30;
            input.Margin = new Thickness(0, 5, 0, 0);
            input.Background = (Brush)Application.Current.Resources["InputBackgroundColor"];
            input.Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"];
            input.BorderBrush = (Brush)Application.Current.Resources["BorderColor"];

            return input;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
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

            if (values.Count == 0)
            {
                MessageBox.Show("Нет данных для добавления.");
                return;
            }

            string columns = string.Join(",", values.Keys);
            string parameters = string.Join(",", values.Keys.Select(k => "@" + k));

            string insertQuery = $"INSERT INTO `{selectedTable}` ({columns}) VALUES ({parameters})";

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand(insertQuery, conn);
                    foreach (var kv in values)
                        cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Данные успешно добавлены!");
                    parentPage.ReturnToUserList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при добавлении: " + ex.Message);
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            parentPage.ReturnToUserList();
        }
    }
}
