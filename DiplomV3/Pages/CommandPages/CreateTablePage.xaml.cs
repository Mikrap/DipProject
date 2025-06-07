using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using DiplomV3.Properties;

namespace DiplomV3.Pages.CommandPages
{
    public partial class CreateTablePage : Page
    {
        public ObservableCollection<TableColumn> Columns { get; set; } = new();
        public ObservableCollection<string> DataTypes { get; set; } = new();
        public ObservableCollection<string> AllTables { get; set; } = new();

        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";

        public CreateTablePage(AdminPage adminPage)
        {
            InitializeComponent();
            DataContext = this;

            LoadDataTypes();
            LoadTablesFromDatabase();
        }


        private void LoadDataTypes()
        {
            DataTypes = new ObservableCollection<string>
            {
                "INT", "VARCHAR(255)", "TEXT", "DATE", "DATETIME", "FLOAT", "DOUBLE"
            };
        }

        private void LoadTablesFromDatabase()
        {
            try
            {
                using var connection = new MySqlConnection(connString);
                connection.Open();

                var getTablesCmd = new MySqlCommand("SHOW TABLES;", connection);
                using var reader = getTablesCmd.ExecuteReader();
                AllTables.Clear();
                TablesTreeView.Items.Clear();

                while (reader.Read())
                {
                    string tableName = reader.GetString(0);
                    AllTables.Add(tableName);

                    var tableItem = new TreeViewItem
                    {
                        Header = tableName,
                        Tag = "Table",
                        FontWeight = FontWeights.SemiBold
                    };
                    TablesTreeView.Items.Add(tableItem);
                }

                connection.Close();

                foreach (TreeViewItem tableItem in TablesTreeView.Items)
                {
                    using var conn = new MySqlConnection(connString);
                    conn.Open();
                    string tableName = tableItem.Header.ToString();
                    var getColumnsCmd = new MySqlCommand($"SHOW COLUMNS FROM `{tableName}`;", conn);
                    using var colReader = getColumnsCmd.ExecuteReader();

                    while (colReader.Read())
                    {
                        string columnName = colReader["Field"].ToString();
                        string columnType = colReader["Type"].ToString();

                        tableItem.Items.Add(new TreeViewItem
                        {
                            Header = $"{columnName} ({columnType})",
                            Tag = "Column"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке таблиц: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            Columns.Add(new TableColumn
            {
                DataTypes = DataTypes,
                AvailableTables = AllTables
            });
        }

        private void TablesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ColumnsDataGrid.SelectedItem is TableColumn selectedColumn && e.NewValue is TreeViewItem selectedItem)
            {
                if (selectedItem.Tag?.ToString() == "Table")
                {
                    selectedColumn.ForeignTable = selectedItem.Header.ToString();
                }
                else if (selectedItem.Tag?.ToString() == "Column" && selectedItem.Parent is TreeViewItem parentItem)
                {
                    string columnFullText = selectedItem.Header.ToString();
                    string columnName = columnFullText.Split(' ')[0];

                    selectedColumn.ForeignTable = parentItem.Header.ToString();
                    selectedColumn.ForeignColumn = columnName;
                }
            }
        }

        private void CreateTableButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TableNameTextBox.Text))
            {
                MessageBox.Show("Введите имя таблицы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Columns.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один столбец!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var connection = new MySqlConnection(connString);
                connection.Open();

                // Сначала создаем таблицу без внешних ключей
                string sql = $"CREATE TABLE IF NOT EXISTS `{TableNameTextBox.Text}` (";

                foreach (var column in Columns)
                {
                    sql += $"`{column.Name}` {column.DataType}";

                    if (column.IsNotNull) sql += " NOT NULL";
                    if (column.IsAutoIncrement) sql += " AUTO_INCREMENT";
                    if (column.IsPrimaryKey) sql += " PRIMARY KEY";

                    sql += ", ";
                }

                // Удаляем последнюю запятую
                sql = sql.TrimEnd(',', ' ') + ") ENGINE=InnoDB;";

                var cmd = new MySqlCommand(sql, connection);
                cmd.ExecuteNonQuery();

                // Затем добавляем внешние ключи отдельными запросами
                foreach (var column in Columns)
                {
                    if (!string.IsNullOrEmpty(column.ForeignTable) && !string.IsNullOrEmpty(column.ForeignColumn))
                    {
                        string fkSql = $"ALTER TABLE `{TableNameTextBox.Text}` " +
                                      $"ADD CONSTRAINT `fk_{TableNameTextBox.Text}_{column.Name}` " +
                                      $"FOREIGN KEY (`{column.Name}`) " +
                                      $"REFERENCES `{column.ForeignTable}` (`{column.ForeignColumn}`) " +
                                      $"ON DELETE RESTRICT ON UPDATE CASCADE;";

                        try
                        {
                            cmd = new MySqlCommand(fkSql, connection);
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception fkEx)
                        {
                            MessageBox.Show($"Ошибка при создании внешнего ключа: {fkEx.Message}",
                                          "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }

                MessageBox.Show($"Таблица {TableNameTextBox.Text} успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем список таблиц
                TablesTreeView.Items.Clear();
                LoadTablesFromDatabase();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании таблицы: {ex.Message}\n\nПроверьте:\n" +
                               "1. Корректность имен таблиц и столбцов\n" +
                               "2. Совпадение типов данных в связанных столбцах\n" +
                               "3. Существование таблицы, на которую ссылается внешний ключ",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TableNameTextBox.Text = string.Empty;
            Columns.Clear();
        }
    }

    public class TableColumn : INotifyPropertyChanged
    {
        private string _name;
        private string _dataType;
        private bool _isPrimaryKey;
        private bool _isNotNull;
        private bool _isAutoIncrement;
        private string _foreignTable;
        private string _foreignColumn;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); }
        }

        public bool IsPrimaryKey
        {
            get => _isPrimaryKey;
            set { _isPrimaryKey = value; OnPropertyChanged(); }
        }

        public bool IsNotNull
        {
            get => _isNotNull;
            set { _isNotNull = value; OnPropertyChanged(); }
        }

        public bool IsAutoIncrement
        {
            get => _isAutoIncrement;
            set { _isAutoIncrement = value; OnPropertyChanged(); }
        }

        public string ForeignTable
        {
            get => _foreignTable;
            set
            {
                if (_foreignTable == value) return;
                _foreignTable = value;
                OnPropertyChanged();
            }
        }

        public string ForeignColumn
        {
            get => _foreignColumn;
            set
            {
                if (_foreignColumn == value) return;
                _foreignColumn = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> DataTypes { get; set; }
        public ObservableCollection<string> AvailableTables { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}