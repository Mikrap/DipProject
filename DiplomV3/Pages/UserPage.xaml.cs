using DiplomV3.Pages.CommandPages;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DiplomV3.Properties;
using System.Windows.Data;
using DiplomV3.Pages;

namespace DiplomV3.Pages
{
    public partial class UserPage : Page
    {
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
        private int currentUserId;
        private string currentUserRole;
        private string selectedTableName;

        public UserPage(int userId, string userRole)
        {
            InitializeComponent();
            currentUserId = userId;
            currentUserRole = userRole;
            ApplyTheme();
            BtnAnalytics.Visibility = CheckAnalyticsAccess() ? Visibility.Visible : Visibility.Collapsed;

            if (currentUserRole == "Admin")
            {
                LoadAllTables();
            }
            else
            {
                LoadAvailableTables();
            }
        }

        private void HideTableControls()
        {
            TableControlsPanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowTableControls()
        {
            TableControlsPanel.Visibility = Visibility.Visible;
            SearchPanel.Visibility = Visibility.Visible;
        }

        private void LoadAvailableTables()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = @"SELECT uta.table_name 
                                     FROM user_table_access uta
                                     JOIN users u ON uta.user_id = u.Id
                                     WHERE u.Id = @userId";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@userId", currentUserId);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        TableIconsPanel.Children.Clear();
                        while (reader.Read())
                        {
                            string tableName = reader.GetString(0);
                            CreateTableButton(tableName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAllTables()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = "SHOW TABLES";
                    MySqlCommand cmd = new MySqlCommand(query, conn);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        TableIconsPanel.Children.Clear();
                        while (reader.Read())
                        {
                            string tableName = reader.GetString(0);
                            CreateTableButton(tableName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateTableButton(string tableName)
        {
            var btn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "📄",
                            FontSize = 24,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = tableName,
                            TextAlignment = TextAlignment.Center,
                            FontSize = 12,
                            Margin = new Thickness(0, 5, 0, 0)
                        }
                    }
                },
                Width = 100,
                Height = 80,
                Margin = new Thickness(10),
                Tag = tableName,
                Style = (Style)FindResource("GitHubButtonStyle")
            };

            btn.Click += TableIcon_Click;
            TableIconsPanel.Children.Add(btn);
        }

        private Dictionary<string, (string RefTable, string RefColumn)> GetForeignKeys(string tableName)
        {
            var foreignKeys = new Dictionary<string, (string, string)>();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = @"SELECT 
                            COLUMN_NAME, 
                            REFERENCED_TABLE_NAME, 
                            REFERENCED_COLUMN_NAME 
                        FROM 
                            INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                        WHERE 
                            TABLE_SCHEMA = DATABASE() AND 
                            TABLE_NAME = @tableName AND 
                            REFERENCED_TABLE_NAME IS NOT NULL";

                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@tableName", tableName);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string column = reader.GetString(0);
                            string refTable = reader.GetString(1);
                            string refColumn = reader.GetString(2);
                            foreignKeys.Add(column, (refTable, refColumn));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения информации о внешних ключах: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return foreignKeys;
        }

        private void LoadTableData(string tableName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Получаем первичный ключ таблицы
                    string primaryKey = GetPrimaryKeyColumn(conn, tableName);
                    if (string.IsNullOrEmpty(primaryKey))
                    {
                        MessageBox.Show($"Не удалось определить первичный ключ для таблицы {tableName}", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Получаем метаданные о таблице и ее столбцах
                    DataTable tableMetadata = GetTableMetadata(conn, tableName);

                    // Получаем данные из основной таблицы
                    DataTable mainData = GetMainTableData(conn, tableName, primaryKey);

                    // Обрабатываем связи и добавляем связанные данные
                    ProcessTableRelationships(conn, tableName, mainData, tableMetadata, primaryKey);

                    // Настраиваем и отображаем DataGrid
                    ConfigureAndDisplayDataGrid(mainData, tableMetadata, primaryKey);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetPrimaryKeyColumn(MySqlConnection conn, string tableName)
        {
            // Специальная обработка для таблицы списаний
            if (tableName.Equals("inv_writeoffs_10", StringComparison.OrdinalIgnoreCase) ||
                tableName.Equals("writeoffs", StringComparison.OrdinalIgnoreCase))
            {
                return "writeoff_id";
            }

            string query = $@"
        SELECT COLUMN_NAME
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
        AND TABLE_NAME = '{tableName}'
        AND COLUMN_KEY = 'PRI'
        LIMIT 1";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                object result = cmd.ExecuteScalar();
                return result?.ToString();
            }
        }

        private DataTable GetTableMetadata(MySqlConnection conn, string tableName)
        {
            DataTable metadata = new DataTable();

            string query = @"
                SELECT 
                    c.column_name, 
                    c.column_type,
                    c.is_primary,
                    c.is_foreign,
                    c.foreign_table,
                    c.foreign_column,
                    c.display_name,
                    c.is_visible,
                    c.display_order
                FROM 
                    table_columns c
                JOIN 
                    system_tables t ON c.table_id = t.id
                WHERE 
                    t.table_name = @tableName
                ORDER BY 
                    c.display_order";

            MySqlCommand cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);

            MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
            adapter.Fill(metadata);

            return metadata;
        }

        private DataTable GetMainTableData(MySqlConnection conn, string tableName, string primaryKey)
        {
            string query = $"SELECT * FROM `{tableName}` ORDER BY `{primaryKey}`";
            MySqlCommand cmd = new MySqlCommand(query, conn);

            DataTable dt = new DataTable();
            MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
            adapter.Fill(dt);

            return dt;
        }

        private void ProcessTableRelationships(MySqlConnection conn, string tableName,
                                      DataTable mainData, DataTable tableMetadata, string primaryKey)
        {
            var foreignKeys = tableMetadata.AsEnumerable()
                .Where(r => r.Field<bool>("is_foreign"))
                .OrderBy(r => r.Field<int>("display_order"));

            foreach (DataRow fkRow in foreignKeys)
            {
                string columnName = fkRow["column_name"].ToString();
                string foreignTable = fkRow["foreign_table"].ToString();
                string foreignColumn = fkRow["foreign_column"].ToString();
                string displayName = fkRow["display_name"].ToString();

                string foreignPrimaryKey = GetPrimaryKeyColumn(conn, foreignTable);
                string displayColumn = GetDisplayColumnForTable(conn, foreignTable);

                DataTable foreignData = new DataTable();
                string foreignQuery = $"SELECT `{foreignColumn}`, `{displayColumn}` FROM `{foreignTable}`";
                MySqlDataAdapter foreignAdapter = new MySqlDataAdapter(foreignQuery, conn);
                foreignAdapter.Fill(foreignData);

                string displayColumnName = $"{columnName}_{displayName}";
                if (!mainData.Columns.Contains(displayColumnName))
                {
                    mainData.Columns.Add(displayColumnName, typeof(string));
                }

                foreach (DataRow row in mainData.Rows)
                {
                    if (row[columnName] != DBNull.Value)
                    {
                        object fkValue = row[columnName];
                        DataRow[] matchedRows = foreignData.Select($"{foreignColumn} = '{fkValue}'");

                        row[displayColumnName] = matchedRows.Length > 0 ? matchedRows[0][displayColumn] : "N/A";
                    }
                    else
                    {
                        row[displayColumnName] = "NULL";
                    }
                }
            }
        }

        private void ConfigureAndDisplayDataGrid(DataTable data, DataTable metadata, string primaryKey)
        {
            ProductsDataGrid.AutoGenerateColumns = false;
            ProductsDataGrid.Columns.Clear();

            // Добавляем кнопки действий только если есть первичный ключ
            if (!string.IsNullOrEmpty(primaryKey))
            {
                AddActionButtonsColumns(primaryKey);
            }

            var visibleColumns = metadata.AsEnumerable()
                .Where(r => r.Field<bool>("is_visible"))
                .OrderBy(r => r.Field<int>("display_order"));

            foreach (DataRow column in visibleColumns)
            {
                string columnName = column["column_name"].ToString();
                string displayName = column["display_name"].ToString();
                bool isForeign = column.Field<bool>("is_foreign");

                if (isForeign)
                {
                    string displayColumnName = $"{columnName}_{column["display_name"]}";
                    if (data.Columns.Contains(displayColumnName))
                    {
                        AddDataGridColumn(displayName, displayColumnName);
                    }
                }
                else if (data.Columns.Contains(columnName))
                {
                    AddDataGridColumn(displayName, columnName);
                }
            }

            ProductsDataGrid.ItemsSource = data.DefaultView;
            TableNameTextBlock.Text = $"Таблица: {selectedTableName}";
            ProductsDataGrid.Visibility = Visibility.Visible;
            TablesScrollViewer.Visibility = Visibility.Collapsed;
            ShowTableControls();
        }

        private void AddDataGridColumn(string header, string bindingPath)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"[{bindingPath}]"),
                Width = DataGridLength.Auto
            };

            column.CellStyle = new Style(typeof(DataGridCell))
            {
                Setters =
                {
                    new Setter(Control.BackgroundProperty, FindResource("SecondaryBackgroundColor")),
                    new Setter(Control.ForegroundProperty, FindResource("PrimaryForegroundColor")),
                    new Setter(Control.BorderBrushProperty, FindResource("BorderColor")),
                    new Setter(Control.BorderThicknessProperty, new Thickness(0,0,1,1)),
                    new Setter(Control.PaddingProperty, new Thickness(5)),
                    new Setter(Control.FontSizeProperty, 12.0)
                }
            };

            ProductsDataGrid.Columns.Add(column);
        }

        private string GetDisplayColumnForTable(MySqlConnection conn, string tableName)
        {
            string[] preferredColumns = { "name", "title", "description", "product_name", "item_name" };

            try
            {
                string query = $"SHOW COLUMNS FROM `{tableName}`";
                MySqlCommand cmd = new MySqlCommand(query, conn);
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader.GetString(0);
                        if (preferredColumns.Contains(columnName))
                        {
                            return columnName;
                        }
                    }
                }

                query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND DATA_TYPE IN ('varchar', 'char', 'text') LIMIT 1";
                cmd = new MySqlCommand(query, conn);
                object result = cmd.ExecuteScalar();

                return result?.ToString() ?? "id";
            }
            catch
            {
                return "id";
            }
        }

        private void AddActionButtonsColumns(string primaryKey)
        {
            // Кнопка "Добавить" в заголовке
            var addButton = new Button
            {
                Content = "➕",
                ToolTip = "Добавить запись",
                Style = (Style)FindResource("GitHubButtonStyle"),
                Width = 35,
                Height = 35,
                FontSize = 10
            };
            addButton.Click += AddNewRow_Click;

            var addColumn = new DataGridTemplateColumn
            {
                Header = addButton,
                Width = 70,
                CellTemplate = new DataTemplate(typeof(ContentControl))
            };
            ProductsDataGrid.Columns.Add(addColumn);

            // Кнопка редактирования
            var editColumn = new DataGridTemplateColumn
            {
                Header = "✏",
                Width = 50,
                CellTemplate = CreateButtonTemplate("✏", "Редактировать", (s, e) => EditRow_Click(s, e, primaryKey))
            };
            ProductsDataGrid.Columns.Add(editColumn);

            // Кнопка удаления
            var deleteColumn = new DataGridTemplateColumn
            {
                Header = "🗑",
                Width = 50,
                CellTemplate = CreateButtonTemplate("🗑", "Удалить",
                        (s, e) => DeleteRow_Click(s, e, primaryKey))
            };
            ProductsDataGrid.Columns.Add(deleteColumn);
        }

        private DataTemplate CreateButtonTemplate(string content, string tooltip, RoutedEventHandler clickHandler)
        {
            var factory = new FrameworkElementFactory(typeof(Button));
            factory.SetValue(Button.ContentProperty, content);
            factory.SetValue(Button.ToolTipProperty, tooltip);
            factory.SetValue(Button.StyleProperty, FindResource("GitHubButtonStyle"));
            factory.SetValue(Button.TagProperty, new Binding());
            factory.AddHandler(Button.ClickEvent, clickHandler);

            return new DataTemplate { VisualTree = factory };
        }

        private void BtnAnalytics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool hasAccess = CheckAnalyticsAccess();

                if (hasAccess)
                {
                    AnalyticsPage analyticsPage = new AnalyticsPage(currentUserId, currentUserRole);
                    NavigationService.Navigate(analyticsPage);
                }
                else
                {
                    MessageBox.Show("У вас нет прав доступа к аналитике", "Ошибка доступа",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке доступа: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckAnalyticsAccess()
        {
            if (currentUserRole == "Admin")
                return true;

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();
                string query = @"SELECT COUNT(*) FROM user_table_access 
                        WHERE user_id = @userId AND (table_name = 'analytics' OR table_name = 'all')";
                MySqlCommand cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@userId", currentUserId);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private void TableIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tableName)
            {
                selectedTableName = tableName;
                LoadTableData(tableName);
            }
        }

        private void BtnBackToTables_Click(object sender, RoutedEventArgs e)
        {
            ProductsDataGrid.Visibility = Visibility.Collapsed;
            TablesScrollViewer.Visibility = Visibility.Visible;
            TableNameTextBlock.Text = "Выберите таблицу:";
            HideTableControls();
            CloseSlideMenu();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedTableName))
            {
                MessageBox.Show("Сначала выберите таблицу", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenSlideMenu(new AddRecordPage(this, selectedTableName));
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для редактирования", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataRowView row = (DataRowView)ProductsDataGrid.SelectedItem;
            string primaryKey = GetPrimaryKeyColumn(new MySqlConnection(connString), selectedTableName);

            if (!row.Row.Table.Columns.Contains(primaryKey))
            {
                MessageBox.Show($"Таблица не содержит столбца '{primaryKey}'", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int recordId = Convert.ToInt32(row[primaryKey]);
            OpenSlideMenu(new EditRecordPage(this, selectedTableName, recordId));
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для удаления", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataRowView row = (DataRowView)ProductsDataGrid.SelectedItem;

            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                conn.Open();
                string primaryKey = GetPrimaryKeyColumn(conn, selectedTableName);

                if (string.IsNullOrEmpty(primaryKey) || !row.Row.Table.Columns.Contains(primaryKey))
                {
                    MessageBox.Show($"Не удалось определить первичный ключ для таблицы '{selectedTableName}'", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int recordId = Convert.ToInt32(row[primaryKey]);

                if (MessageBox.Show("Вы уверены, что хотите удалить эту запись?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        string query = $"DELETE FROM `{selectedTableName}` WHERE `{primaryKey}` = @id";
                        MySqlCommand cmd = new MySqlCommand(query, conn);
                        cmd.Parameters.AddWithValue("@id", recordId);
                        cmd.ExecuteNonQuery();

                        LoadTableData(selectedTableName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void EditRow_Click(object sender, RoutedEventArgs e, string primaryKey)
        {
            if (sender is Button btn && btn.Tag is DataRowView row)
            {
                if (!row.Row.Table.Columns.Contains(primaryKey))
                {
                    MessageBox.Show($"Столбец '{primaryKey}' не найден", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int recordId = Convert.ToInt32(row[primaryKey]);
                OpenSlideMenu(new EditRecordPage(this, selectedTableName, recordId));
            }
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e, string primaryKey)
        {
            if (sender is Button btn && btn.Tag is DataRowView row)
            {
                if (string.IsNullOrEmpty(primaryKey) || !row.Row.Table.Columns.Contains(primaryKey))
                {
                    MessageBox.Show($"Не удалось определить первичный ключ для таблицы '{selectedTableName}'", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int recordId = Convert.ToInt32(row[primaryKey]);

                if (MessageBox.Show("Вы уверены, что хотите удалить эту запись?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (MySqlConnection conn = new MySqlConnection(connString))
                        {
                            conn.Open();
                            string query = $"DELETE FROM `{selectedTableName}` WHERE `{primaryKey}` = @id";
                            MySqlCommand cmd = new MySqlCommand(query, conn);
                            cmd.Parameters.AddWithValue("@id", recordId);
                            cmd.ExecuteNonQuery();
                        }

                        LoadTableData(selectedTableName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void AddNewRow_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedTableName))
            {
                MessageBox.Show("Сначала выберите таблицу", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenSlideMenu(new AddRecordPage(this, selectedTableName));
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(selectedTableName))
            {
                LoadTableData(selectedTableName);
            }
            else
            {
                if (currentUserRole == "Admin")
                {
                    LoadAllTables();
                }
                else
                {
                    LoadAvailableTables();
                }
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string searchText = TxtProductSearch.Text.Trim();
            if (string.IsNullOrEmpty(selectedTableName))
            {
                MessageBox.Show("Сначала выберите таблицу для поиска", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(searchText))
            {
                LoadTableData(selectedTableName);
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    var foreignKeys = GetForeignKeys(selectedTableName);
                    string columnsQuery = $"SHOW COLUMNS FROM `{selectedTableName}`";
                    MySqlCommand columnsCmd = new MySqlCommand(columnsQuery, conn);
                    var columnsReader = columnsCmd.ExecuteReader();

                    List<string> columns = new List<string>();
                    while (columnsReader.Read())
                    {
                        columns.Add(columnsReader.GetString(0));
                    }
                    columnsReader.Close();

                    foreach (var fk in foreignKeys)
                    {
                        columns.Add($"{fk.Key}_display");
                    }

                    string whereClause = string.Join(" OR ", columns.Select(c => $"{c} LIKE @search"));
                    string query = $"SELECT * FROM `{selectedTableName}` WHERE {whereClause}";

                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@search", $"%{searchText}%");

                    MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    ProductsDataGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TaxReportsPage reportsPage = new TaxReportsPage();
                NavigationService.Navigate(reportsPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка перехода на страницу отчетов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public void OpenSlideMenu(Page page)
        {
            ContentFrame.Content = page;
            SlidePanel.Width = 400;
        }

        public void CloseSlideMenu()
        {
            SlidePanel.Width = 0;
        }

        public void ReturnToTable()
        {
            CloseSlideMenu();
            LoadTableData(selectedTableName);
        }

        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            string currentTheme = Properties.Settings.Default.CurrentTheme;

            if (currentTheme == "DarkTheme")
            {
                SetTheme("LightTheme");
            }
            else
            {
                SetTheme("DarkTheme");
            }
        }

        private void SetTheme(string themeKey)
        {
            try
            {
                Application.Current.Resources.MergedDictionaries.Clear();

                var theme = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/DiplomV3;component/Themes/{themeKey}.xaml", UriKind.Absolute)
                };
                Application.Current.Resources.MergedDictionaries.Add(theme);

                Properties.Settings.Default.CurrentTheme = themeKey;
                Properties.Settings.Default.Save();

                if (ThemeIcon != null)
                {
                    ThemeIcon.Text = themeKey == "DarkTheme" ? "🌙" : "🌞";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка смены темы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyTheme()
        {
            string savedTheme = Properties.Settings.Default.CurrentTheme;
            if (!string.IsNullOrEmpty(savedTheme))
            {
                SetTheme(savedTheme);
            }
            else
            {
                SetTheme("LightTheme");
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            var mainWindowMenu = Application.Current.Windows[0];
            if (mainWindowMenu != null)
            {
                mainWindowMenu.Close();
            }

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}