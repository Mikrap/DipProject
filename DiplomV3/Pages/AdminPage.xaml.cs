using DiplomV3.Pages.CommandPages;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiplomV3.Properties;

namespace DiplomV3.Pages
{
    public partial class AdminPage : Page
    {
        internal string selectedTableName;

        private void LoadSettings()
        {
            String Server = Settings.Default.DbServerAddress;
            String Login = Settings.Default.DbLogin;
            String DBPassword = Settings.Default.DbPassword;
        }

        public void SetSelectedTable(string tableName)
        {
            selectedTableName = tableName;
        }

        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

        public AdminPage()
        {
            InitializeComponent();
            ApplyTheme();
            LoadSettings();
            LoadTableNames();

            // Изначально скрываем элементы управления
            HideTableControls();
        }

        private void HideTableControls()
        {
            BtnAddUser.Visibility = Visibility.Collapsed;
            BtnEditUser.Visibility = Visibility.Collapsed;
            BtnDeleteUser.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowTableControls()
        {
            BtnAddUser.Visibility = Visibility.Visible;
            BtnEditUser.Visibility = Visibility.Visible;
            BtnDeleteUser.Visibility = Visibility.Visible;
            SearchPanel.Visibility = Visibility.Visible;
        }

        internal void LoadTableNames()
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
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке таблиц: " + ex.Message);
            }
        }

        private void TableIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tableName)
            {
                SetSelectedTable(tableName);
                TableNameTextBlock.Text = $"Таблица: {tableName}";

                TableIconsPanel.Visibility = Visibility.Collapsed;
                UserDataGrid.Visibility = Visibility.Visible;

                // Показываем элементы управления для таблицы
                ShowTableControls();

                LoadTableData(tableName);
            }
        }
        public string GetSelectedTable()
        {
            return selectedTableName;
        }
        private void LoadTableData(string tableName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = $"SELECT * FROM `{tableName}`";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    // Здесь обращаемся к UserDataGrid, чтобы загрузить данные
                    UserDataGrid.ItemsSource = dt.DefaultView;
                    TableNameTextBlock.Text = $"Таблица: {tableName}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке данных: " + ex.Message);
            }
        }
        private void BtnDeleteTable_Click(object sender, RoutedEventArgs e)
        {
            string selectedTable = GetSelectedTable();

            if (string.IsNullOrEmpty(selectedTable))
            {
                MessageBox.Show("Сначала выберите таблицу для удаления.");
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить таблицу \"{selectedTable}\"? Это действие необратимо.",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

                    using (var conn = new MySql.Data.MySqlClient.MySqlConnection(connString))
                    {
                        conn.Open();
                        var cmd = new MySql.Data.MySqlClient.MySqlCommand($"DROP TABLE `{selectedTable}`", conn);
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show($"Таблица \"{selectedTable}\" успешно удалена.");
                    LoadTableNames(); // метод, который обновляет список таблиц и панель иконок
                    MessageBox.Show($"Таблица \"{selectedTable}\" успешно удалена.");
                    LoadTableNames(); // метод, который обновляет список таблиц и панель иконок

                    // Сброс состояния
                    selectedTableName = null;
                    UserDataGrid.Visibility = Visibility.Collapsed;
                    TableIconsPanel.Visibility = Visibility.Visible;
                    TableNameTextBlock.Text = "Выберите таблицу:";
                    HideTableControls();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при удалении таблицы: " + ex.Message);
                }
            }
        }
        private void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            OpenSlideMenu(new AddUserWindow(this));
        }

        private void BtnEditUser_Click(object sender, RoutedEventArgs e)
        {
            if (UserDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите пользователя для редактирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var row = (System.Data.DataRowView)UserDataGrid.SelectedItem;
            int userId = Convert.ToInt32(row["id"]);
            

            OpenSlideMenu(new EditUserWindow(this, userId));
        }

        private void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (UserDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите запись для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var row = (DataRowView)UserDataGrid.SelectedItem;
            int id = Convert.ToInt32(row["id"]);

            if (!row.Row.Table.Columns.Contains("id"))
            {
                MessageBox.Show("Таблица не содержит столбца 'id'.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedTable = TableNameTextBlock.Text.Replace("Таблица: ", "");
            using (MySqlConnection conn = new MySqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    string query = $"DELETE FROM `{selectedTable}` WHERE id = @id";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();

                    MessageBox.Show("Запись удалена.");
                    LoadTableData(selectedTable);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка удаления: " + ex.Message);
                }
            }
        }

        private void EditRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DataRowView row)
            {
                if (!row.Row.Table.Columns.Contains("id"))
                {
                    MessageBox.Show("Столбец 'id' не найден.");
                    return;
                }

                int userId = Convert.ToInt32(row["id"]);


                OpenSlideMenu(new EditUserWindow(this, userId));
            }
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DataRowView row)
            {
                if (!row.Row.Table.Columns.Contains("id"))
                {
                    MessageBox.Show("Столбец 'id' не найден.");
                    return;
                }

                int id = Convert.ToInt32(row["id"]);
                string selectedTable = GetSelectedTable();

                var result = MessageBox.Show("Вы уверены, что хотите удалить эту запись?", "Подтверждение", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes) return;

                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connString))
                    {
                        conn.Open();
                        string query = $"DELETE FROM `{selectedTable}` WHERE id = @id";
                        MySqlCommand cmd = new MySqlCommand(query, conn);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }

                    LoadTableData(selectedTable);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка удаления: " + ex.Message);
                }
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {

            // Получаем название таблицы из TableNameTextBlock
            string selectedTable = TableNameTextBlock.Text.Replace("Таблица: ", "");


            if (string.IsNullOrEmpty(selectedTableName))
            {
                // Таблица не выбрана — обновляем список таблиц
                LoadTableNames(); // метод, который наполняет TableIconsPanel
            }
            else
            {
                // Таблица выбрана — обновляем данные таблицы
                LoadTableData(selectedTableName); // метод, который обновляет DataGrid
            }
        }
        public void OpenSlideMenu(Page page)
        {
            ContentFrame.Content = page;
            SlidePanel.Width = 400; // Открываем панель
        }

        public void CloseSlideMenu()
        {
            SlidePanel.Width = 0; // Закрываем панель
        }

        public void ReturnToUserList()
        {
            CloseSlideMenu(); // Закрыть слайд-меню

            // Получаем выбранную таблицу
            string selectedTable = TableNameTextBlock.Text.Replace("Таблица: ", "");

            if (!string.IsNullOrEmpty(selectedTable))
            {
                LoadTableData(selectedTable); // Загружаем данные для выбранной таблицы
            }
        }

        private void GoToMainPage(object sender, RoutedEventArgs e)
        {
            UserDataGrid.Visibility = Visibility.Collapsed;
            TableIconsPanel.Visibility = Visibility.Visible;
            TableNameTextBlock.Text = "Выберите таблицу:";

            // Скрываем элементы управления при возврате к списку таблиц
            HideTableControls();
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

        // Метод для загрузки темы
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

                // Сохраняем тему в настройках
                Properties.Settings.Default.CurrentTheme = themeKey;
                Properties.Settings.Default.Save();

                // Обновляем иконку на кнопке
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

        // Загружаем тему при старте
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

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string search = TxtSearch.Text.Trim();
            string table = TableNameTextBlock.Text.Replace("Таблица: ", ""); // Получаем название таблицы из текста

            if (string.IsNullOrWhiteSpace(table))
            {
                MessageBox.Show("Выберите таблицу перед поиском.");
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = $"SELECT * FROM `{table}` WHERE UserName LIKE @search OR Role LIKE @search";
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");

                    MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    UserDataGrid.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка поиска: " + ex.Message);
            }
        }
        private void Exit(object sender, RoutedEventArgs e)
        {
            // Получаем текущее окно (MainWindowMenu)
            var mainWindowMenu = Application.Current.Windows[0]; // Получаем первое окно в списке (MainWindowMenu)

            if (mainWindowMenu != null)
            {
                mainWindowMenu.Close(); // Закрываем текущее окно (MainWindowMenu)
            }

            // Открытие нового окна MainWindow (страница авторизации)
            MainWindow mainWindow = new MainWindow(); // Создаем новое окно
            mainWindow.Show(); // Показываем окно MainWindow
        }

        private void AddNewRow_Click(object sender, RoutedEventArgs e)
        {
            // Пример: показать панель справа для добавления пользователя
            OpenSlideMenu(new AddUserWindow(this));
        }

        private void BtnCreateTable_Click(object sender, RoutedEventArgs e)
        {
            OpenSlideMenu(new CreateTablePage(this));

        }
    }
}
