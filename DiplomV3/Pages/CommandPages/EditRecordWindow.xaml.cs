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
    public partial class EditRecordWindow : Page
    {
        private AdminPage parentPage;
        private string tableName;
        private int recordId;
        private string tableNameForUserAccess; // Для таблицы user_table_access
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
        private string primaryKeyColumn;
        private List<string> autoIncrementColumns;
        private bool isUserTableAccess;

        // Конструктор для обычных таблиц
        public EditRecordWindow(AdminPage parent, string table, int id)
        {
            InitializeComponent();
            parentPage = parent;
            tableName = table;
            recordId = id;
            isUserTableAccess = false;
            Loaded += EditRecordWindow_Loaded;
        }

        // Конструктор для таблицы user_table_access
        public EditRecordWindow(AdminPage parent, string table, int userId, string tableNameForAccess)
        {
            InitializeComponent();
            parentPage = parent;
            tableName = table;
            recordId = userId;
            tableNameForUserAccess = tableNameForAccess;
            isUserTableAccess = true;
            Loaded += EditRecordWindow_Loaded;
        }

        private void EditRecordWindow_Loaded(object sender, RoutedEventArgs e)
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
                LoadRecordData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации страницы: {ex.Message}");
            }
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
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                tables.Add(reader.GetString(0));
                            }
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

        private void LoadRecordData()
        {
            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    string query;
                    MySqlCommand cmd;

                    if (isUserTableAccess)
                    {
                        query = $"SELECT * FROM `{tableName}` WHERE `user_id` = @userId AND `table_name` = @tableName";
                        cmd = new MySqlCommand(query, conn);
                        cmd.Parameters.AddWithValue("@userId", recordId);
                        cmd.Parameters.AddWithValue("@tableName", tableNameForUserAccess);
                    }
                    else
                    {
                        query = $"SELECT * FROM `{tableName}` WHERE `{primaryKeyColumn}` = @id";
                        cmd = new MySqlCommand(query, conn);
                        cmd.Parameters.AddWithValue("@id", recordId);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            foreach (var control in DynamicFieldsPanel.Children)
                            {
                                if (control is TextBox txtBox && txtBox.Tag is string key1)
                                {
                                    if (reader[key1] != DBNull.Value)
                                    {
                                        txtBox.Text = reader[key1].ToString();
                                    }
                                }
                                else if (control is ComboBox comboBox && comboBox.Tag is string key2)
                                {
                                    if (key2 == "table_name" && isUserTableAccess)
                                    {
                                        comboBox.SelectedItem = reader[key2].ToString();
                                    }
                                    else if (reader[key2] != DBNull.Value)
                                    {
                                        int value = Convert.ToInt32(reader[key2]);
                                        comboBox.SelectedValue = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
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

                if (values.Count == 0) return;

                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string updateQuery;
                    MySqlCommand cmd;

                    if (isUserTableAccess)
                    {
                        updateQuery = $"UPDATE `{tableName}` SET {string.Join(",", values.Keys.Select(k => $"{k} = @{k}"))} " +
                                     $"WHERE `user_id` = @userId AND `table_name` = @tableName";

                        cmd = new MySqlCommand(updateQuery, conn);
                        cmd.Parameters.AddWithValue("@userId", recordId);
                        cmd.Parameters.AddWithValue("@tableName", tableNameForUserAccess);
                    }
                    else
                    {
                        updateQuery = $"UPDATE `{tableName}` SET {string.Join(",", values.Keys.Select(k => $"{k} = @{k}"))} " +
                                     $"WHERE `{primaryKeyColumn}` = @id";

                        cmd = new MySqlCommand(updateQuery, conn);
                        cmd.Parameters.AddWithValue("@id", recordId);
                    }

                    foreach (var kv in values)
                        cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);

                    cmd.ExecuteNonQuery();

                    MessageBox.Show("Данные успешно обновлены!");
                    parentPage.CloseSlideMenu();
                    parentPage.LoadTableData(tableName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}");
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
                Text = $"Редактирование записи в таблице: {tableName}",
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
                    Style = (Style)FindResource("ModernComboBox"),
                    IsEnabled = false // Делаем поле только для чтения при редактировании
                };
                comboBox.ItemsSource = GetAvailableTables();
                comboBox.SelectedItem = tableNameForUserAccess;

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

            // Специальная обработка для таблицы user_table_access
            if (tableName.Equals("user_table_access", StringComparison.OrdinalIgnoreCase))
            {
                if (columnName.Equals("user_id", StringComparison.OrdinalIgnoreCase))
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
                else if (columnName.Equals("table_name", StringComparison.OrdinalIgnoreCase))
                {
                    var comboBox = new ComboBox
                    {
                        Tag = columnName,
                        Style = (Style)FindResource("ModernComboBox"),
                        IsEnabled = !isUserTableAccess // Разрешаем редактирование только при добавлении
                    };
                    comboBox.ItemsSource = GetAvailableTables();
                    input = comboBox;
                }
            }
            // Остальная логика для других таблиц
            else if (columnName.EndsWith("_id") || columnName.Equals("user_id", StringComparison.OrdinalIgnoreCase))
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
            else if (columnName.Equals("table_name", StringComparison.OrdinalIgnoreCase))
            {
                // Поле для выбора таблицы
                var comboBox = new ComboBox
                {
                    Tag = columnName,
                    Style = (Style)FindResource("ModernComboBox"),
                    IsEnabled = !isUserTableAccess // Разрешаем редактирование только при добавлении
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

        private string GetColumnType(string tableName, string columnName)
        {
            try
            {
                using (var conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        $"SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
                        $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName",
                        conn);
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    cmd.Parameters.AddWithValue("@columnName", columnName);
                    return cmd.ExecuteScalar()?.ToString().ToLower() ?? "varchar";
                }
            }
            catch
            {
                return "varchar"; // Значение по умолчанию
            }
        }

       

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            parentPage.CloseSlideMenu();
        }
    }
}