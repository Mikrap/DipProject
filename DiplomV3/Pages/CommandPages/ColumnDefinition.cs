using MySql.Data.MySqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DiplomV3.Properties;

namespace DiplomV3.Pages.CommandPages
{
    public class TableColumnDefinition : INotifyPropertyChanged
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
                UpdateAvailableColumns();
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

        public ObservableCollection<string> DataTypes { get; set; } = new();
        public ObservableCollection<string> AvailableTables { get; set; } = new();
        public ObservableCollection<string> AvailableColumns { get; set; } = new();

        private void UpdateAvailableColumns()
        {
            if (string.IsNullOrEmpty(ForeignTable))
            {
                AvailableColumns.Clear();
                return;
            }

            try
            {
                var connectionString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
                using var connection = new MySqlConnection(connectionString);
                connection.Open();

                var cmd = new MySqlCommand($"SHOW COLUMNS FROM `{ForeignTable}`;", connection);
                using var reader = cmd.ExecuteReader();

                AvailableColumns.Clear();
                while (reader.Read())
                {
                    AvailableColumns.Add(reader["Field"].ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке столбцов: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}