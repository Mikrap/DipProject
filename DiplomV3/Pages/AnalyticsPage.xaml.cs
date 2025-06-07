using DiplomV3.Properties;
using LiveCharts;
using LiveCharts.Wpf;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace DiplomV3.Pages
{
    public partial class AnalyticsPage : Page, INotifyPropertyChanged
    {
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
        private int currentUserId;
        private string currentUserRole;

        private SeriesCollection _combinedSeries;
        public SeriesCollection CombinedSeries
        {
            get => _combinedSeries;
            set
            {
                _combinedSeries = value;
                OnPropertyChanged();
            }
        }

        private SeriesCollection _purchasesSeries;
        public SeriesCollection PurchasesSeries
        {
            get => _purchasesSeries;
            set
            {
                _purchasesSeries = value;
                OnPropertyChanged();
            }
        }

        private SeriesCollection _topPurchasedProducts;
        public SeriesCollection TopPurchasedProducts
        {
            get => _topPurchasedProducts;
            set
            {
                _topPurchasedProducts = value;
                OnPropertyChanged();
            }
        }

        private List<TopProduct> _topProducts;
        public List<TopProduct> TopProducts
        {
            get => _topProducts;
            set
            {
                _topProducts = value;
                OnPropertyChanged();
            }
        }

        private string[] _months;
        public string[] Months
        {
            get => _months;
            set
            {
                _months = value;
                OnPropertyChanged();
            }
        }

        private SeriesCollection _writeoffsSeries;
        public SeriesCollection WriteoffsSeries
        {
            get => _writeoffsSeries;
            set
            {
                _writeoffsSeries = value;
                OnPropertyChanged();
            }
        }

        private SeriesCollection _writeoffReasonsSeries;
        public SeriesCollection WriteoffReasonsSeries
        {
            get => _writeoffReasonsSeries;
            set
            {
                _writeoffReasonsSeries = value;
                OnPropertyChanged();
            }
        }

        private string[] _writeoffMonths;
        public string[] WriteoffMonths
        {
            get => _writeoffMonths;
            set
            {
                _writeoffMonths = value;
                OnPropertyChanged();
            }
        }

        private decimal _totalWriteoffsAmount;
        public decimal TotalWriteoffsAmount
        {
            get => _totalWriteoffsAmount;
            set
            {
                _totalWriteoffsAmount = value;
                OnPropertyChanged();
            }
        }

        private decimal _totalPurchases;
        public decimal TotalPurchases
        {
            get => _totalPurchases;
            set
            {
                _totalPurchases = value;
                OnPropertyChanged();
            }
        }

        private decimal _totalWriteoffs;
        public decimal TotalWriteoffs
        {
            get => _totalWriteoffs;
            set
            {
                _totalWriteoffs = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public AnalyticsPage(int userId, string userRole = "User")
        {
            InitializeComponent();
            currentUserId = userId;
            currentUserRole = userRole;
            DataContext = this;

            // Установка начальных дат
            DateTime today = DateTime.Today;
            EndDatePicker.SelectedDate = today;
            StartDatePicker.SelectedDate = today.AddMonths(-3);
            EndDatePicker.DisplayDateEnd = today;

            LoadData();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                NavigationService.Navigate(new UserPage(currentUserId, currentUserRole));
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
                return;

            if (StartDatePicker.SelectedDate > EndDatePicker.SelectedDate)
            {
                MessageBox.Show("Дата начала не может быть позже даты окончания",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (sender == StartDatePicker)
                {
                    StartDatePicker.SelectedDate = EndDatePicker.SelectedDate.Value.AddMonths(-1);
                }
                else
                {
                    EndDatePicker.SelectedDate = StartDatePicker.SelectedDate.Value.AddMonths(1);
                }
                return;
            }

            if ((EndDatePicker.SelectedDate.Value - StartDatePicker.SelectedDate.Value).TotalDays > 365)
            {
                MessageBox.Show("Максимальный период анализа - 1 год",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.SelectedDate = StartDatePicker.SelectedDate.Value.AddYears(1);
                return;
            }

            LoadData();
        }

        private void LoadData()
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
                return;

            DateTime startDate = StartDatePicker.SelectedDate.Value;
            DateTime endDate = EndDatePicker.SelectedDate.Value;

            try
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    using (MySqlConnection conn = new MySqlConnection(connString))
                    {
                        try
                        {
                            conn.Open();

                            Dispatcher.Invoke(() =>
                            {
                                LoadCombinedData(conn, startDate, endDate);
                                LoadTopPurchasedProducts(conn, startDate, endDate);
                                LoadDetailedData(conn, startDate, endDate);
                                LoadWriteoffsAnalytics(conn, startDate, endDate);
                                LoadWriteoffsDetailedData(conn, startDate, endDate); // Добавляем вызов нового метода
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                                MessageBox.Show($"Ошибка подключения к базе данных: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
 
        private void LoadCombinedData(MySqlConnection conn, DateTime startDate, DateTime endDate)
        {
            try
            {
                string purchasesQuery = $@"
                SELECT 
                    DATE_FORMAT(r.receipt_date, '%Y-%m') AS Month,
                    SUM(r.quantity * r.price) AS TotalAmount
                FROM inv_receipts_{currentUserId} r
                WHERE r.receipt_date BETWEEN @startDate AND @endDate
                GROUP BY DATE_FORMAT(r.receipt_date, '%Y-%m')
                ORDER BY Month";

                string writeoffsQuery = $@"
                SELECT 
                    DATE_FORMAT(w.writeoff_date, '%Y-%m') AS Month,
                    SUM(w.quantity * (SELECT r.price FROM inv_receipts_{currentUserId} r 
                                      WHERE r.product_id = w.product_id 
                                      ORDER BY r.receipt_date DESC LIMIT 1)) AS TotalAmount
                FROM inv_writeoffs_{currentUserId} w
                WHERE w.writeoff_date BETWEEN @startDate AND @endDate
                GROUP BY DATE_FORMAT(w.writeoff_date, '%Y-%m')
                ORDER BY Month";

                DataTable purchasesDt = new DataTable();
                DataTable writeoffsDt = new DataTable();

                using (MySqlCommand cmd = new MySqlCommand(purchasesQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(purchasesDt);
                    }
                }

                using (MySqlCommand cmd = new MySqlCommand(writeoffsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(writeoffsDt);
                    }
                }

                var purchasesData = purchasesDt.Rows.OfType<DataRow>()
                    .Select(r => new
                    {
                        Month = r["Month"].ToString(),
                        Amount = Convert.ToDecimal(r["TotalAmount"])
                    })
                    .ToList();

                var writeoffsData = writeoffsDt.Rows.OfType<DataRow>()
                    .Select(r => new
                    {
                        Month = r["Month"].ToString(),
                        Amount = Convert.ToDecimal(r["TotalAmount"])
                    })
                    .ToList();

                var allMonths = purchasesData.Select(x => x.Month)
                    .Union(writeoffsData.Select(x => x.Month))
                    .OrderBy(x => x)
                    .ToList();

                Months = allMonths.Select(x =>
                {
                    if (DateTime.TryParseExact(x, "yyyy-MM", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime date))
                        return date.ToString("MMM yyyy", CultureInfo.CurrentCulture);
                    return x;
                }).ToArray();

                var purchasesValues = new decimal[allMonths.Count];
                var writeoffsValues = new decimal[allMonths.Count];

                for (int i = 0; i < allMonths.Count; i++)
                {
                    var month = allMonths[i];
                    var purchase = purchasesData.FirstOrDefault(x => x.Month == month);
                    var writeoff = writeoffsData.FirstOrDefault(x => x.Month == month);

                    purchasesValues[i] = purchase?.Amount ?? 0;
                    writeoffsValues[i] = writeoff?.Amount ?? 0;
                }

                TotalPurchases = purchasesValues.Sum();
                TotalWriteoffs = writeoffsValues.Sum();

                CombinedSeries = new SeriesCollection
                {
                    new ColumnSeries
                    {
                        Title = "Закупки",
                        Values = new ChartValues<decimal>(purchasesValues),
                        Fill = Brushes.SteelBlue
                    },
                    new ColumnSeries
                    {
                        Title = "Списания",
                        Values = new ChartValues<decimal>(writeoffsValues),
                        Fill = Brushes.IndianRed
                    }
                };
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка загрузки комбинированных данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void LoadTopPurchasedProducts(MySqlConnection conn, DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = $@"
                SELECT 
                    p.product_name AS ProductName,
                    SUM(r.quantity) AS TotalQuantity,
                    SUM(r.quantity * r.price) AS TotalAmount
                FROM inv_receipts_{currentUserId} r
                JOIN products p ON r.product_id = p.product_id
                WHERE r.receipt_date BETWEEN @startDate AND @endDate
                GROUP BY p.product_name
                ORDER BY TotalAmount DESC
                LIMIT 10";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);

                    DataTable dt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    decimal totalAmount = dt.Rows.OfType<DataRow>()
                        .Sum(row => Convert.ToDecimal(row["TotalAmount"]));

                    var colors = new[]
                    {
                        Brushes.SteelBlue,
                        Brushes.LightSeaGreen,
                        Brushes.IndianRed,
                        Brushes.Goldenrod,
                        Brushes.MediumPurple,
                        Brushes.Teal,
                        Brushes.DarkOrange,
                        Brushes.Crimson,
                        Brushes.DarkSlateBlue,
                        Brushes.ForestGreen
                    };

                    TopPurchasedProducts = new SeriesCollection();
                    TopProducts = new List<TopProduct>();

                    int colorIndex = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        var product = new TopProduct
                        {
                            ProductName = row["ProductName"].ToString(),
                            Quantity = Convert.ToInt32(row["TotalQuantity"]),
                            TotalAmount = Convert.ToDecimal(row["TotalAmount"])
                        };

                        TopProducts.Add(product);

                        var pieSeries = new PieSeries
                        {
                            Title = product.ProductName.Length > 15 ?
                             product.ProductName.Substring(0, 15) + "..." :
                             product.ProductName,
                            Values = new ChartValues<decimal> { product.TotalAmount },
                            DataLabels = true,
                            LabelPoint = point =>
                            {
                                decimal pointValue = (decimal)point.Y;
                                decimal total = (decimal)totalAmount;
                                return $"{(pointValue / total).ToString("P1")}";
                            },
                            Fill = colors[colorIndex % colors.Length],
                            StrokeThickness = 0
                        };

                        TopPurchasedProducts.Add(pieSeries);
                        colorIndex++;
                    }

                    Dispatcher.Invoke(() => TopProductsGrid.ItemsSource = TopProducts);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка загрузки топовых товаров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void LoadDetailedData(MySqlConnection conn, DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = $@"
                SELECT 
                    'Закупка' AS OperationType,
                    r.receipt_date AS Date,
                    p.product_name AS ProductName,
                    r.quantity AS Quantity,
                    r.price AS UnitPrice,
                    (r.quantity * r.price) AS TotalAmount,
                    r.document_number AS DocumentNumber
                FROM inv_receipts_{currentUserId} r
                JOIN products p ON r.product_id = p.product_id
                WHERE r.receipt_date BETWEEN @startDate AND @endDate
                ORDER BY Date DESC";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);

                    DataTable dt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        DetailedDataGrid.ItemsSource = dt.DefaultView;
                        DetailedDataGrid.AutoGenerateColumns = true;
                        TotalRecordsText.Text = dt.Rows.Count.ToString();
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка загрузки детализированных данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        public class WriteoffItem : INotifyPropertyChanged
        {
            private DateTime _date;
            private string _productName;
            private int _quantity;
            private string _reason;
            private string _documentNumber;
            private decimal _totalAmount;

            public DateTime Date
            {
                get => _date;
                set
                {
                    if (_date != value)
                    {
                        _date = value;
                        OnPropertyChanged();
                    }
                }
            }

            public string ProductName
            {
                get => _productName;
                set
                {
                    if (_productName != value)
                    {
                        _productName = value;
                        OnPropertyChanged();
                    }
                }
            }

            public int Quantity
            {
                get => _quantity;
                set
                {
                    if (_quantity != value)
                    {
                        _quantity = value;
                        OnPropertyChanged();
                    }
                }
            }

            public string Reason
            {
                get => _reason;
                set
                {
                    if (_reason != value)
                    {
                        _reason = value;
                        OnPropertyChanged();
                    }
                }
            }

            public string DocumentNumber
            {
                get => _documentNumber;
                set
                {
                    if (_documentNumber != value)
                    {
                        _documentNumber = value;
                        OnPropertyChanged();
                    }
                }
            }

            public decimal TotalAmount
            {
                get => _totalAmount;
                set
                {
                    if (_totalAmount != value)
                    {
                        _totalAmount = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        private void LoadWriteoffsDetailedData(MySqlConnection conn, DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = $@"
        SELECT 
            w.writeoff_date AS Date,
            p.product_name AS ProductName,
            w.quantity AS Quantity,
            w.reason AS Reason,
            w.document_number AS DocumentNumber,
            (w.quantity * (
                SELECT r.price FROM inv_receipts_{currentUserId} r
                WHERE r.product_id = w.product_id 
                ORDER BY r.receipt_date DESC 
                LIMIT 1
            )) AS TotalAmount
        FROM inv_writeoffs_{currentUserId} w
        JOIN products p ON w.product_id = p.product_id
        WHERE w.writeoff_date BETWEEN @startDate AND @endDate
        ORDER BY Date DESC";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);

                    DataTable dt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    var writeoffsList = dt.Rows.OfType<DataRow>()
                        .Select(r => new WriteoffItem
                        {
                            Date = Convert.ToDateTime(r["Date"]),
                            ProductName = r["ProductName"].ToString(),
                            Quantity = Convert.ToInt32(r["Quantity"]),
                            Reason = r["Reason"].ToString(),
                            DocumentNumber = r["DocumentNumber"].ToString(),
                            TotalAmount = Convert.ToDecimal(r["TotalAmount"])
                        })
                        .ToList();

                    Dispatcher.Invoke(() =>
                    {
                        WriteoffsDataGrid.ItemsSource = writeoffsList;
                        TotalWriteoffsRecordsText.Text = writeoffsList.Count.ToString();
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка загрузки данных списаний: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void LoadWriteoffsAnalytics(MySqlConnection conn, DateTime startDate, DateTime endDate)
        {
            try
            {
                // Загрузка данных по месяцам
                string monthlyQuery = $@"
                SELECT 
                    DATE_FORMAT(w.writeoff_date, '%Y-%m') AS Month,
                    SUM(w.quantity * (SELECT r.price FROM inv_receipts_{currentUserId} r 
                                      WHERE r.product_id = w.product_id 
                                      ORDER BY r.receipt_date DESC LIMIT 1)) AS TotalAmount
                FROM inv_writeoffs_{currentUserId} w
                WHERE w.writeoff_date BETWEEN @startDate AND @endDate
                GROUP BY DATE_FORMAT(w.writeoff_date, '%Y-%m')
                ORDER BY Month";

                using (MySqlCommand cmd = new MySqlCommand(monthlyQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);

                    DataTable monthlyDt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(monthlyDt);
                    }

                    var monthData = monthlyDt.Rows.OfType<DataRow>()
                                         .Select(r => new
                                         {
                                             Month = r["Month"].ToString(),
                                             Amount = Convert.ToDecimal(r["TotalAmount"])
                                         })
                                         .ToList();

                    WriteoffMonths = monthData.Select(x =>
                    {
                        if (DateTime.TryParseExact(x.Month, "yyyy-MM", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime date))
                            return date.ToString("MMM yyyy", CultureInfo.CurrentCulture);
                        return x.Month;
                    }).ToArray();

                    WriteoffsSeries = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Title = "Списания",
                            Values = new ChartValues<decimal>(monthData.Select(x => x.Amount)),
                            Fill = System.Windows.Media.Brushes.IndianRed
                        }
                    };
                }

                // Загрузка данных по причинам списаний
                string reasonsQuery = $@"
                SELECT 
                    w.reason AS Reason,
                    SUM(w.quantity * (SELECT r.price FROM inv_receipts_{currentUserId} r 
                                      WHERE r.product_id = w.product_id 
                                      ORDER BY r.receipt_date DESC LIMIT 1)) AS TotalAmount
                FROM inv_writeoffs_{currentUserId} w
                WHERE w.writeoff_date BETWEEN @startDate AND @endDate
                GROUP BY w.reason
                ORDER BY TotalAmount DESC";

                using (MySqlCommand cmd = new MySqlCommand(reasonsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);

                    DataTable reasonsDt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(reasonsDt);
                    }

                    var colors = new[]
                    {
                        Brushes.IndianRed,
                        Brushes.Coral,
                        Brushes.DarkSalmon,
                        Brushes.LightCoral,
                        Brushes.RosyBrown,
                        Brushes.Firebrick,
                        Brushes.DarkRed,
                        Brushes.Brown,
                        Brushes.Sienna,
                        Brushes.Maroon
                    };

                    WriteoffReasonsSeries = new SeriesCollection();
                    decimal totalAmount = reasonsDt.Rows.OfType<DataRow>()
                        .Sum(row => Convert.ToDecimal(row["TotalAmount"]));

                    TotalWriteoffsAmount = totalAmount;

                    int colorIndex = 0;
                    foreach (DataRow row in reasonsDt.Rows)
                    {
                        string reason = row["Reason"].ToString();
                        decimal amount = Convert.ToDecimal(row["TotalAmount"]);

                        WriteoffReasonsSeries.Add(new PieSeries
                        {
                            Title = reason.Length > 15 ? reason.Substring(0, 15) + "..." : reason,
                            Values = new ChartValues<decimal> { amount },
                            DataLabels = true,
                            LabelPoint = point =>
                            {
                                decimal pointValue = (decimal)point.Y;
                                decimal total = (decimal)totalAmount;
                                return $"{(pointValue / total).ToString("P1")}";
                            },
                            Fill = colors[colorIndex % colors.Length],
                            StrokeThickness = 0
                        });

                        colorIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка загрузки аналитики списаний: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void WriteoffPieChart_MouseMove(object sender, MouseEventArgs e)
        {
            var chart = (PieChart)sender;
            var mousePosition = e.GetPosition(chart);

            foreach (var series in chart.Series)
            {
                var pieSeries = series as PieSeries;
                if (pieSeries != null)
                {
                    if (mousePosition.X >= 0 && mousePosition.X <= chart.ActualWidth &&
                        mousePosition.Y >= 0 && mousePosition.Y <= chart.ActualHeight)
                    {
                        WriteoffPieChartLabel.Text = pieSeries.Title;
                        return;
                    }
                }
            }

            WriteoffPieChartLabel.Text = "Наведите на сектор диаграммы";
        }

        private void WriteoffPieChart_MouseLeave(object sender, MouseEventArgs e)
        {
            WriteoffPieChartLabel.Text = "Наведите на сектор диаграммы";
        }

        private void PieChart_MouseMove(object sender, MouseEventArgs e)
        {
            var chart = (PieChart)sender;
            var mousePosition = e.GetPosition(chart);

            foreach (var series in chart.Series)
            {
                var pieSeries = series as PieSeries;
                if (pieSeries != null)
                {
                    if (mousePosition.X >= 0 && mousePosition.X <= chart.ActualWidth &&
                        mousePosition.Y >= 0 && mousePosition.Y <= chart.ActualHeight)
                    {
                        PieChartLabel.Text = pieSeries.Title;

                        if (TopProductsGrid.ItemsSource is IEnumerable<TopProduct> topProducts)
                        {
                            var selectedProduct = topProducts.FirstOrDefault(p =>
                                p.ProductName.StartsWith(pieSeries.Title.Replace("...", "")));

                            if (selectedProduct != null)
                            {
                                TopProductsGrid.SelectedItem = selectedProduct;
                                TopProductsGrid.ScrollIntoView(selectedProduct);
                            }
                        }
                        return;
                    }
                }
            }

            PieChartLabel.Text = "Наведите на сектор диаграммы";
        }

        private void PieChart_MouseLeave(object sender, MouseEventArgs e)
        {
            PieChartLabel.Text = "Наведите на сектор диаграммы";
            TopProductsGrid.SelectedItem = null;
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TopProduct : INotifyPropertyChanged
    {
        private string _productName;
        private int _quantity;
        private decimal _totalAmount;

        public string ProductName
        {
            get => _productName;
            set
            {
                if (_productName != value)
                {
                    _productName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set
            {
                if (_totalAmount != value)
                {
                    _totalAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}