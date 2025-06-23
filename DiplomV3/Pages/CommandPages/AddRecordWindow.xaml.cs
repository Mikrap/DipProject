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
    public partial class AddRecordWindow : Page
    {
        private AdminPage parentPage;
        private string tableName;
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
        private bool isUserTableAccess;
        private string primaryKeyColumn;
        private List<string> autoIncrementColumns;

        // Конструктор для обычных таблиц
        public AddRecordWindow(AdminPage parent, string table)
        {
            InitializeComponent();
            parentPage = parent;
            tableName = table;
            isUserTableAccess = false;
            Loaded += AddRecordWindow_Loaded;
        }

        // Конструктор для таблицы user_table_access
        public AddRecordWindow(AdminPage parent, string table, bool isUserAccess)
        {
            InitializeComponent();
            parentPage = parent;
            tableName = table;
            isUserTableAccess = isUserAccess;
            Loaded += AddRecordWindow_Loaded;
        }

        private void AddRecordWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    primaryKeyColumn = GetPrimaryKeyColumn(conn, tableName);
                    autoIncrementColumns = GetAutoIncrementColumns(conn, tableName);
                }

                var columns = GetTableSchema(tableName);
                GenerateFields(columns);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации страницы: {ex.Message}");
            }
        }

        private List<(string ColumnName, string DataType, bool IsNullable)> GetTableSchema(string tableName)
        {
            var columns = new List<(string, string, bool)>();
            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    var cmd = new MySqlCommand($"DESCRIBE `{tableName}`", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении структуры таблицы: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return columns;
        }

        private void GenerateFields(List<(string ColumnName, string DataType, bool IsNullable)> columns)
        {
            DynamicFieldsPanel.Children.Clear();

            // Добавляем заголовок с именем таблицы
            var tableTitle = new TextBlock
            {
                Text = $"Добавление записи в таблицу: {tableName}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                Margin = new Thickness(0, 0, 0, 10),
                TextAlignment = TextAlignment.Center
            };
            DynamicFieldsPanel.Children.Add(tableTitle);

            foreach (var (col, type, isNullable) in columns)
            {
                if (col.Equals(primaryKeyColumn, StringComparison.OrdinalIgnoreCase) ||
                    autoIncrementColumns.Contains(col))
                    continue;

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

            // Добавляем поле для table_name, если его нет в колонках, но это таблица user_table_access
            if (isUserTableAccess && !columns.Any(c => c.ColumnName.Equals("table_name", StringComparison.OrdinalIgnoreCase)))
            {
                var label = new TextBlock
                {
                    Text = "Имя таблицы",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["PrimaryForegroundColor"],
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var comboBox = new ComboBox
                {
                    Tag = "table_name",
                    Style = (Style)FindResource("ModernComboBox")
                };
                comboBox.ItemsSource = GetAvailableTables();

                DynamicFieldsPanel.Children.Add(label);
                DynamicFieldsPanel.Children.Add(comboBox);
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
                            "user_id" => "ID пользователя",
                            "table_name" => "Имя таблицы",
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

            // Специальная обработка для внешних ключей (включая user_id)
            if (columnName.EndsWith("_id") || columnName.Equals("user_id", StringComparison.OrdinalIgnoreCase))
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
            else if (columnName.Equals("table_name", StringComparison.OrdinalIgnoreCase) && isUserTableAccess)
            {
                // Специальное поле для выбора таблицы в user_table_access
                var comboBox = new ComboBox
                {
                    Tag = columnName,
                    Style = (Style)FindResource("ModernComboBox")
                };
                comboBox.ItemsSource = GetAvailableTables();
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
                // Специальная обработка для поля user_id
                if (columnName.Equals("user_id", StringComparison.OrdinalIgnoreCase))
                {
                    using (var conn = new MySqlConnection(connString))
                    {
                        conn.Open();
                        string query = "SELECT Id, Username FROM Users";
                        using (var cmd = new MySqlCommand(query, conn))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    data.Add(reader.GetInt32("Id"), reader.GetString("Username"));
                                }
                            }
                        }
                    }
                    return data;
                }

                // Остальная логика для других полей
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

        private List<string> GetAvailableTables()
        {
            var tables = new List<string>();

            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = "SHOW TABLES";
                    var cmd = new MySqlCommand(query, conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tables.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении списка таблиц: {ex.Message}");
            }

            return tables;
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

        private string GetPrimaryKeyColumn(MySqlConnection conn, string tableName)
        {
            try
            {
                if (tableName.Equals("inv_writeoffs_10", StringComparison.OrdinalIgnoreCase) ||
                    tableName.Equals("writeoffs", StringComparison.OrdinalIgnoreCase))
                {
                    return "writeoff_id";
                }

                string query = $@"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @tableName
                    AND COLUMN_KEY = 'PRI'
                    LIMIT 1";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "id"; // Возвращаем "id" по умолчанию, если не нашли PK
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при определении первичного ключа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return "id"; // Значение по умолчанию
            }
        }

        private List<string> GetAutoIncrementColumns(MySqlConnection conn, string tableName)
        {
            var autoColumns = new List<string>();
            string query = $"SHOW COLUMNS FROM `{tableName}` WHERE Extra LIKE '%auto_increment%'";
            var cmd = new MySqlCommand(query, conn);
            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                autoColumns.Add(reader["Field"].ToString());
            }
            reader.Close();

            return autoColumns;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
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
                    else if (child is ComboBox cb && cb.Tag is string key2)
                    {
                        if (isUserTableAccess && key2 == "table_name")
                        {
                            values[key2] = cb.SelectedItem?.ToString();
                        }
                        else if (cb.SelectedValue != null)
                        {
                            values[key2] = cb.SelectedValue;
                        }
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

                if (values.Count == 0) return;

                // Проверка обязательных полей для user_table_access
                if (isUserTableAccess)
                {
                    if (!values.ContainsKey("user_id") || values["user_id"] == null)
                    {
                        MessageBox.Show("Необходимо выбрать пользователя!");
                        return;
                    }

                    if (!values.ContainsKey("table_name") || string.IsNullOrEmpty(values["table_name"]?.ToString()))
                    {
                        MessageBox.Show("Необходимо выбрать таблицу!");
                        return;
                    }
                }

                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string insertQuery = $"INSERT INTO `{tableName}` ({string.Join(",", values.Keys)}) " +
                                       $"VALUES ({string.Join(",", values.Keys.Select(k => $"@{k}"))})";

                    MySqlCommand cmd = new MySqlCommand(insertQuery, conn);
                    foreach (var kv in values)
                        cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);

                    cmd.ExecuteNonQuery();

                    MessageBox.Show("Запись успешно добавлена!");
                    parentPage.CloseSlideMenu();
                    parentPage.LoadTableData(tableName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении данных: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            parentPage.CloseSlideMenu();
        }
    }
}