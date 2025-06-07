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
    public partial class AddRecordPage : Page
    {
        private UserPage parentPage;
        private string tableName;
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

        public AddRecordPage(UserPage parent, string table)
        {
            InitializeComponent();
            parentPage = parent;
            tableName = table;
            Loaded += AddRecordPage_Loaded;
        }

        private void AddRecordPage_Loaded(object sender, RoutedEventArgs e)
        {
            var columns = GetTableSchema(tableName);
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

        private List<(string ColumnName, string DataType, bool IsNullable)> GetTableSchema(string tableName)
        {
            var columns = new List<(string, string, bool)>();
            using (var conn = new MySqlConnection(connString))
            {
                conn.Open();
                var cmd = new MySqlCommand($"DESCRIBE `{tableName}`", conn);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string name = reader["Field"].ToString();
                    // Пропускаем поле created_at
                    if (name.Equals("created_at", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string type = reader["Type"].ToString().ToLower();
                    bool isNullable = reader["Null"].ToString() == "YES";
                    columns.Add((name, type, isNullable));
                }
            }
            return columns;
        }

        private void GenerateFields(List<(string ColumnName, string DataType, bool IsNullable)> columns)
        {
            var autoCols = GetAutoIncrementColumns(tableName);
            DynamicFieldsPanel.Children.Clear();

            // Добавляем заголовок с именем таблицы
            var tableTitle = new TextBlock
            {
                Text = $"Добавление в таблицу: {tableName}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                Margin = new Thickness(0, 0, 0, 10),
                TextAlignment = TextAlignment.Center
            };
            DynamicFieldsPanel.Children.Add(tableTitle);

            foreach (var (col, type, isNullable) in columns)
            {
                if (autoCols.Contains(col)) continue;

                var label = new TextBlock
                {
                    Text = GetDisplayNameForColumn(col),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Control input = CreateInputField(type, col, isNullable);
                if (input == null)
                    continue;

                DynamicFieldsPanel.Children.Add(label);
                DynamicFieldsPanel.Children.Add(input);
            }
        }

        private string GetDisplayNameForColumn(string columnName)
        {
            string displayName = columnName; // Значение по умолчанию

            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    string query = @"
                        SELECT c.display_name 
                        FROM table_columns c
                        JOIN system_tables t ON c.table_id = t.id
                        WHERE t.table_name = @tableName 
                        AND c.column_name = @columnName";

                    var cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    cmd.Parameters.AddWithValue("@columnName", columnName);

                    var result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        displayName = result.ToString();
                    }
                    else
                    {
                        // Если не нашли в таблице, используем стандартные преобразования
                        displayName = columnName switch
                        {
                            "product_id" => "Товар",
                            "category_id" => "Категория",
                            "quantity" => "Количество",
                            "price" => "Цена",
                            "supplier" => "Поставщик",
                            "receipt_date" => "Дата поступления",
                            "document_number" => "Номер документа",
                            "reason" => "Причина",
                            "writeoff_date" => "Дата списания",
                            "product_name" => "Наименование товара",
                            "category_name" => "Название категории",
                            "description" => "Описание",
                            "unit" => "Единица измерения",
                            "barcode" => "Штрих-код",
                            "last_update" => "Последнее обновление",
                            _ => columnName
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении имени столбца: {ex.Message}");
                displayName = columnName;
            }

            return displayName;
        }

        private Control CreateInputField(string type, string columnName, bool isNullable)
        {
            Control input = null;

            // Специальная обработка для внешних ключей
            if (columnName.EndsWith("_id") && columnName != "writeoff_id")
            {
                var comboBox = new ComboBox
                {
                    Tag = columnName,
                    Style = (Style)FindResource("ModernComboBox")
                };
                comboBox.ItemsSource = GetReferenceData(columnName);
                comboBox.DisplayMemberPath = "Value";
                comboBox.SelectedValuePath = "Key";
                input = comboBox;
            }
            else if (type.Contains("int"))
            {
                input = new TextBox { Tag = columnName };
                input.PreviewTextInput += (s, e) => { e.Handled = !int.TryParse(e.Text, out _); };
            }
            else if (type.Contains("decimal") || type.Contains("float") || type.Contains("double"))
            {
                input = new TextBox { Tag = columnName };
                input.PreviewTextInput += (s, e) => { e.Handled = !decimal.TryParse(e.Text, out _); };
            }
            else if (type.Contains("date"))
            {
                input = new DatePicker
                {
                    Tag = columnName,
                    Style = (Style)FindResource("ModernDatePicker")
                };
            }
            else if (type.Contains("bool"))
            {
                input = new CheckBox
                {
                    Tag = columnName,
                    Content = "Да / Нет",
                    Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                    Margin = new Thickness(0, 5, 0, 0)
                };
            }
            else
            {
                input = new TextBox { Tag = columnName };
            }

            // Общие стили для всех элементов управления, кроме CheckBox
            if (!(input is CheckBox))
            {
                input.Height = 30;
                input.Margin = new Thickness(0, 5, 0, 0);
                input.SetResourceReference(Control.BackgroundProperty, "InputBackgroundColor");
                input.SetResourceReference(Control.ForegroundProperty, "PrimaryForegroundColor");
                input.SetResourceReference(Control.BorderBrushProperty, "BorderColor");
            }

            if (isNullable && input is TextBox)
            {
                ((TextBox)input).Text = "NULL";
            }

            return input;
        }

        private Dictionary<int, string> GetReferenceData(string columnName)
        {
            var data = new Dictionary<int, string>();

            try
            {
                // Первый запрос - получаем информацию о внешнем ключе
                string referenceTable = null;
                string referenceColumn = null;

                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    string query = @"
                        SELECT c.foreign_table, c.foreign_column 
                        FROM table_columns c
                        JOIN system_tables t ON c.table_id = t.id
                        WHERE t.table_name = @tableName 
                        AND c.column_name = @columnName
                        AND c.is_foreign = 1";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", tableName);
                        cmd.Parameters.AddWithValue("@columnName", columnName);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                referenceTable = reader["foreign_table"].ToString();
                                referenceColumn = reader["foreign_column"].ToString();
                            }
                        }
                    }
                }

                // Если нашли внешний ключ, получаем данные для ComboBox
                if (!string.IsNullOrEmpty(referenceTable) && !string.IsNullOrEmpty(referenceColumn))
                {
                    using (var conn = new MySqlConnection(connString))
                    {
                        conn.Open();

                        string displayColumn = GetDisplayColumnForReferenceTable(referenceTable);
                        string dataQuery = $"SELECT `{referenceColumn}`, `{displayColumn}` FROM `{referenceTable}`";

                        using (var dataCmd = new MySqlCommand(dataQuery, conn))
                        {
                            using (var dataReader = dataCmd.ExecuteReader())
                            {
                                while (dataReader.Read())
                                {
                                    data.Add(dataReader.GetInt32(0), dataReader.GetString(1));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении данных для ComboBox: {ex.Message}");
            }

            return data;
        }

        private string GetDisplayColumnForReferenceTable(string tableName)
        {
            return tableName switch
            {
                "products" => "product_name",
                "categories" => "category_name",
                _ => "id"
            };
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var values = new Dictionary<string, object>();

            foreach (var child in DynamicFieldsPanel.Children)
            {
                if (child is TextBox tb && tb.Tag is string key1)
                {
                    if (tb.Text == "NULL")
                    {
                        values[key1] = DBNull.Value;
                    }
                    else if (!string.IsNullOrEmpty(tb.Text))
                    {
                        values[key1] = tb.Text;
                    }
                }
                else if (child is ComboBox cb && cb.Tag is string key2 && cb.SelectedValue != null)
                {
                    values[key2] = cb.SelectedValue;
                }
                else if (child is DatePicker dp && dp.Tag is string key3 && dp.SelectedDate.HasValue)
                {
                    values[key3] = dp.SelectedDate?.ToString("yyyy-MM-dd");
                }
                else if (child is CheckBox chk && chk.Tag is string key4)
                {
                    values[key4] = chk.IsChecked == true ? 1 : 0;
                }
            }

            if (values.Count == 0)
            {
                MessageBox.Show("Нет данных для добавления.");
                return;
            }

            string columns = string.Join(",", values.Keys);
            string parameters = string.Join(",", values.Keys.Select(k => "@" + k));

            string insertQuery = $"INSERT INTO `{tableName}` ({columns}) VALUES ({parameters})";

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
                    parentPage.ReturnToTable();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при добавлении: " + ex.Message);
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            parentPage.ReturnToTable();
        }
    }
}