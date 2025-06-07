using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using DiplomV3.Properties;
using Excel = Microsoft.Office.Interop.Excel;
using Word = Microsoft.Office.Interop.Word;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DiplomV3.Pages
{
    public partial class TaxReportsPage : Page
    {
        private string connString = $"Server={Settings.Default.DbServerAddress};Database=DataBase;Uid={Settings.Default.DbLogin};Pwd={Settings.Default.DbPassword};";
        private int currentUserId = 10; // ID текущего пользователя (можно получить из настроек или передать в конструктор)

        public TaxReportsPage()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
            EndDatePicker.SelectedDate = DateTime.Now;
            LoadReportData();
        }

        private void DateRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate > EndDatePicker.SelectedDate)
            {
                MessageBox.Show("Дата начала не может быть позже даты окончания", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LoadReportData();
        }
        private DataRow GetCompanyInfo()
        {
            DataTable companyInfo = new DataTable();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string query = "SELECT * FROM company_info WHERE user_id = @userId";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", currentUserId);
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            adapter.Fill(companyInfo);
                        }
                    }
                }

                return companyInfo.Rows.Count > 0 ? companyInfo.Rows[0] : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных компании: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private void LoadReportData()
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
                return;

            DateTime startDate = StartDatePicker.SelectedDate.Value;
            DateTime endDate = EndDatePicker.SelectedDate.Value;

            try
            {
                string reportType = ((ComboBoxItem)ReportTypeCombo.SelectedItem).Content.ToString();
                DataTable reportData = new DataTable();

                switch (reportType)
                {
                    case "КУДиР (УСН)":
                        reportData = GenerateKUDiRReport(startDate, endDate);
                        break;
                    case "Расчёт НДС":
                        reportData = GenerateVATReport(startDate, endDate);
                        break;
                    case "Декларация УСН":
                        reportData = GenerateUSNReport(startDate, endDate);
                        break;
                    case "Декларация ЕНВД":
                        reportData = GenerateENVDReport(startDate, endDate);
                        break;
                }

                ReportDataGrid.ItemsSource = reportData.DefaultView;
                UpdateSummaryText(reportData, reportType);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataTable GenerateKUDiRReport(DateTime startDate, DateTime endDate)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Дата", typeof(string));
            table.Columns.Add("Документ", typeof(string));
            table.Columns.Add("Контрагент", typeof(string));
            table.Columns.Add("Содержание операции", typeof(string));
            table.Columns.Add("Доходы", typeof(decimal));
            table.Columns.Add("Расходы", typeof(decimal));

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Доходы от продаж
                    string incomeQuery = $@"
                    SELECT 
                        DATE_FORMAT(r.receipt_date, '%d.%m.%Y') AS date,
                        r.document_number AS doc,
                        r.supplier AS counterparty,
                        'Поступление товара' AS operation,
                        SUM(r.quantity * r.price) AS income,
                        0 AS expense
                    FROM inv_receipts_{currentUserId} r
                    WHERE r.receipt_date BETWEEN @startDate AND @endDate
                    GROUP BY r.receipt_date, r.document_number, r.supplier";

                    using (MySqlCommand cmd = new MySqlCommand(incomeQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);

                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            DataTable incomeData = new DataTable();
                            adapter.Fill(incomeData);
                            foreach (DataRow row in incomeData.Rows)
                            {
                                table.Rows.Add(
                                    row["date"],
                                    row["doc"],
                                    row["counterparty"],
                                    row["operation"],
                                    row["income"],
                                    row["expense"]);
                            }
                        }
                    }

                    // Расходы на закупку товаров
                    string expenseQuery = $@"
                    SELECT 
                        DATE_FORMAT(w.writeoff_date, '%d.%m.%Y') AS date,
                        w.document_number AS doc,
                        'Списание товара' AS operation,
                        w.reason AS counterparty,
                        0 AS income,
                        SUM(w.quantity * 
                            (SELECT r.price FROM inv_receipts_{currentUserId} r 
                             WHERE r.product_id = w.product_id 
                             ORDER BY r.receipt_date DESC LIMIT 1)) AS expense
                    FROM inv_writeoffs_{currentUserId} w
                    WHERE w.writeoff_date BETWEEN @startDate AND @endDate
                    GROUP BY w.writeoff_date, w.document_number, w.reason";

                    using (MySqlCommand cmd = new MySqlCommand(expenseQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);

                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            DataTable expenseData = new DataTable();
                            adapter.Fill(expenseData);
                            foreach (DataRow row in expenseData.Rows)
                            {
                                table.Rows.Add(
                                    row["date"],
                                    row["doc"],
                                    row["counterparty"],
                                    row["operation"],
                                    row["income"],
                                    row["expense"]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования КУДиР: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return table;
        }

        private DataTable GenerateVATReport(DateTime startDate, DateTime endDate)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Период", typeof(string));
            table.Columns.Add("Налоговая база", typeof(decimal));
            table.Columns.Add("Ставка НДС", typeof(string));
            table.Columns.Add("Сумма НДС", typeof(decimal));
            table.Columns.Add("Вычеты", typeof(decimal));
            table.Columns.Add("К уплате", typeof(decimal));

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Расчет НДС с продаж (исходящий НДС)
                    string salesQuery = $@"
                    SELECT 
                        DATE_FORMAT(r.receipt_date, '%m.%Y') AS period,
                        SUM(r.quantity * r.price) AS taxBase,
                        '20%' AS rate,
                        SUM(r.quantity * r.price) * 0.2 AS vatAmount
                    FROM inv_receipts_{currentUserId} r
                    WHERE r.receipt_date BETWEEN @startDate AND @endDate
                    GROUP BY DATE_FORMAT(r.receipt_date, '%m.%Y')";

                    DataTable salesData = new DataTable();
                    using (MySqlCommand cmd = new MySqlCommand(salesQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            adapter.Fill(salesData);
                        }
                    }

                    // Расчет НДС к вычету (входящий НДС)
                    string purchasesQuery = $@"
                    SELECT 
                        DATE_FORMAT(w.writeoff_date, '%m.%Y') AS period,
                        SUM(w.quantity * 
                            (SELECT r.price FROM inv_receipts_{currentUserId} r 
                             WHERE r.product_id = w.product_id 
                             ORDER BY r.receipt_date DESC LIMIT 1)) * 0.2 AS vatDeduction
                    FROM inv_writeoffs_{currentUserId} w
                    WHERE w.writeoff_date BETWEEN @startDate AND @endDate
                    GROUP BY DATE_FORMAT(w.writeoff_date, '%m.%Y')";

                    DataTable purchasesData = new DataTable();
                    using (MySqlCommand cmd = new MySqlCommand(purchasesQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            adapter.Fill(purchasesData);
                        }
                    }

                    // Объединение данных
                    var periods = salesData.AsEnumerable()
                        .Select(r => r["period"].ToString())
                        .Union(purchasesData.AsEnumerable().Select(r => r["period"].ToString()))
                        .Distinct()
                        .OrderBy(p => p);

                    foreach (var period in periods)
                    {
                        decimal taxBase = salesData.AsEnumerable()
                            .Where(r => r["period"].ToString() == period)
                            .Sum(r => Convert.ToDecimal(r["taxBase"]));

                        decimal vatAmount = salesData.AsEnumerable()
                            .Where(r => r["period"].ToString() == period)
                            .Sum(r => Convert.ToDecimal(r["vatAmount"]));

                        decimal vatDeduction = purchasesData.AsEnumerable()
                            .Where(r => r["period"].ToString() == period)
                            .Sum(r => Convert.ToDecimal(r["vatDeduction"]));

                        decimal toPay = vatAmount - vatDeduction;

                        table.Rows.Add(
                            period,
                            taxBase,
                            "20%",
                            vatAmount,
                            vatDeduction,
                            toPay > 0 ? toPay : 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования расчета НДС: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return table;
        }

        private DataTable GenerateUSNReport(DateTime startDate, DateTime endDate)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Показатель", typeof(string));
            table.Columns.Add("Сумма", typeof(decimal));
            table.Columns.Add("Примечание", typeof(string));

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Доходы
                    decimal income = 0;
                    string incomeQuery = $@"
                    SELECT SUM(r.quantity * r.price) AS total
                    FROM inv_receipts_{currentUserId} r
                    WHERE r.receipt_date BETWEEN @startDate AND @endDate";

                    using (MySqlCommand cmd = new MySqlCommand(incomeQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);
                        income = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    // Расходы
                    decimal expenses = 0;
                    string expensesQuery = $@"
                    SELECT SUM(w.quantity * 
                        (SELECT r.price FROM inv_receipts_{currentUserId} r 
                         WHERE r.product_id = w.product_id 
                         ORDER BY r.receipt_date DESC LIMIT 1)) AS total
                    FROM inv_writeoffs_{currentUserId} w
                    WHERE w.writeoff_date BETWEEN @startDate AND @endDate";

                    using (MySqlCommand cmd = new MySqlCommand(expensesQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);
                        expenses = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    // Заполнение таблицы
                    table.Rows.Add("Доходы всего", income, "Поступления товаров");
                    table.Rows.Add("Расходы всего", expenses, "Списания товаров");
                    table.Rows.Add("Налоговая база", income - expenses, "Доходы минус расходы");
                    table.Rows.Add("Ставка налога", 15, "15% для УСН 'Доходы минус расходы'");
                    table.Rows.Add("Сумма налога", (income - expenses) * 0.15m, "К уплате");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования декларации УСН: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return table;
        }

        private DataTable GenerateENVDReport(DateTime startDate, DateTime endDate)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Показатель", typeof(string));
            table.Columns.Add("Значение", typeof(string));

            try
            {
                // Примерные данные для ЕНВД (так как в БД нет специфичных данных для ЕНВД)
                table.Rows.Add("Вид деятельности", "Розничная торговля");
                table.Rows.Add("Базовая доходность (руб/мес)", "1800");
                table.Rows.Add("Физический показатель", "Площадь торгового зала (кв.м)");
                table.Rows.Add("Количество кв.м", "50");
                table.Rows.Add("К1 (коэффициент-дефлятор)", "1.915");
                table.Rows.Add("К2 (корректирующий коэффициент)", "0.8");
                table.Rows.Add("Налоговая ставка", "15%");

                // Расчет вмененного дохода
                decimal baseIncome = 1800m;
                decimal area = 50m;
                decimal k1 = 1.915m;
                decimal k2 = 0.8m;
                int months = (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1;

                decimal vmenennyDohod = baseIncome * area * k1 * k2 * months;
                decimal tax = vmenennyDohod * 0.15m;

                table.Rows.Add("Вмененный доход за период", vmenennyDohod.ToString("N2"));
                table.Rows.Add("Сумма налога к уплате", tax.ToString("N2"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования декларации ЕНВД: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return table;
        }

        private void UpdateSummaryText(DataTable data, string reportType)
        {
            string summary = $"Отчет: {reportType}\n";
            summary += $"Период: {StartDatePicker.SelectedDate.Value.ToShortDateString()} - {EndDatePicker.SelectedDate.Value.ToShortDateString()}\n";

            switch (reportType)
            {
                case "КУДиР (УСН)":
                    decimal income = data.AsEnumerable().Sum(r => Convert.ToDecimal(r["Доходы"]));
                    decimal expenses = data.AsEnumerable().Sum(r => Convert.ToDecimal(r["Расходы"]));
                    summary += $"Доходы: {income:N2} руб.\n";
                    summary += $"Расходы: {expenses:N2} руб.\n";
                    summary += $"Финансовый результат: {income - expenses:N2} руб.";
                    break;

                case "Расчёт НДС":
                    decimal vatToPay = data.AsEnumerable().Sum(r => Convert.ToDecimal(r["К уплате"]));
                    summary += $"НДС к уплате: {vatToPay:N2} руб.";
                    break;

                case "Декларация УСН":
                    decimal taxBase = Convert.ToDecimal(data.Rows[2]["Сумма"]);
                    decimal taxAmount = Convert.ToDecimal(data.Rows[4]["Сумма"]);
                    summary += $"Налоговая база: {taxBase:N2} руб.\n";
                    summary += $"Налог к уплате: {taxAmount:N2} руб.";
                    break;

                case "Декларация ЕНВД":
                    string taxAmountENVD = data.Rows[data.Rows.Count - 1]["Значение"].ToString();
                    summary += $"Налог к уплате: {taxAmountENVD} руб.";
                    break;
            }

            SummaryText.Text = summary;
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            LoadReportData();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (!(ReportDataGrid.ItemsSource is DataView dataView) || dataView.Table.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Excel.Application();
                excelApp.Visible = true;
                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Sheets[1];

                // Получаем информацию о компании
                DataRow companyInfo = GetCompanyInfo();

                // Добавляем шапку с названием компании
                string companyName = companyInfo != null ? companyInfo["company_name"].ToString() : "ООО 'Ваша Компания'";
                worksheet.Cells[1, 1] = companyName;
                worksheet.Cells[1, 1].Font.Bold = true;
                worksheet.Cells[1, 1].Font.Size = 14;
                worksheet.Range["A1", "F1"].Merge();

                // Добавляем юридическую информацию, если есть
                if (companyInfo != null)
                {
                    worksheet.Cells[2, 1] = $"ИНН: {companyInfo["inn"]} / КПП: {companyInfo["kpp"]}";
                    worksheet.Range["A2", "F2"].Merge();

                    worksheet.Cells[3, 1] = $"Юридический адрес: {companyInfo["legal_address"]}";
                    worksheet.Range["A3", "F3"].Merge();
                }

                // Добавляем название отчета
                string reportType = ((ComboBoxItem)ReportTypeCombo.SelectedItem).Content.ToString();
                int currentRow = companyInfo != null ? 5 : 2;
                worksheet.Cells[currentRow, 1] = $"Отчет: {reportType}";
                worksheet.Cells[currentRow, 1].Font.Bold = true;
                worksheet.Range[$"A{currentRow}", $"F{currentRow}"].Merge();
                currentRow++;

                // Добавляем период
                worksheet.Cells[currentRow, 1] = $"Период: {StartDatePicker.SelectedDate.Value.ToShortDateString()} - {EndDatePicker.SelectedDate.Value.ToShortDateString()}";
                worksheet.Range[$"A{currentRow}", $"F{currentRow}"].Merge();
                currentRow += 2; // Пропускаем строку перед таблицей

                // Остальной код остается без изменений...
                // [существующий код формирования таблицы и сводки]

                // Модифицируем блок подписей
                int lastRow = dataView.Table.Rows.Count + currentRow + 7;

                if (companyInfo != null)
                {
                    worksheet.Cells[lastRow, 1] = $"Директор: _________________ /{companyInfo["ceo_name"]}/";
                }
                else
                {
                    worksheet.Cells[lastRow, 1] = "Директор: _________________ /Иванов И.И./";
                }

                worksheet.Cells[lastRow + 1, 1] = "Дата: _________________";
                worksheet.Cells[lastRow + 2, 1] = "М.П.";

                // Сохранение файла
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"{companyName} - Налоговый отчет {DateTime.Now:yyyy-MM-dd}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    workbook.SaveAs(saveFileDialog.FileName);
                    MessageBox.Show("Отчет успешно экспортирован в Excel", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта в Excel: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Освобождение ресурсов
                if (workbook != null) Marshal.ReleaseComObject(workbook);
                if (worksheet != null) Marshal.ReleaseComObject(worksheet);
                if (excelApp != null)
                {
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
                }
            }
        }
        private void ExportToWord_Click(object sender, RoutedEventArgs e)
        {
            if (!(ReportDataGrid.ItemsSource is DataView dataView) || dataView.Table.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Word.Application wordApp = null;
            Word.Document doc = null;

            try
            {
                wordApp = new Word.Application();
                wordApp.Visible = true;
                doc = wordApp.Documents.Add();

                // Получаем информацию о компании
                DataRow companyInfo = GetCompanyInfo();

                // Добавляем шапку документа
                Word.Paragraph header = doc.Paragraphs.Add();
                header.Range.Text = companyInfo != null ? companyInfo["company_name"].ToString() : "ООО 'Ваша Компания'";
                header.Range.Font.Bold = 1;
                header.Range.Font.Size = 14;
                header.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                header.Range.InsertParagraphAfter();

                // Добавляем юридическую информацию, если есть
                if (companyInfo != null)
                {
                    Word.Paragraph legalInfo = doc.Paragraphs.Add();
                    legalInfo.Range.Text = $"ИНН: {companyInfo["inn"]} / КПП: {companyInfo["kpp"]} / ОГРН: {companyInfo["ogrn"]}";
                    legalInfo.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    legalInfo.Range.InsertParagraphAfter();

                    Word.Paragraph address = doc.Paragraphs.Add();
                    address.Range.Text = $"Юридический адрес: {companyInfo["legal_address"]}";
                    address.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    address.Range.InsertParagraphAfter();
                }

                // Название отчета
                string reportType = ((ComboBoxItem)ReportTypeCombo.SelectedItem).Content.ToString();
                Word.Paragraph title = doc.Paragraphs.Add();
                title.Range.Text = $"Отчет: {reportType}";
                title.Range.Font.Bold = 1;
                title.Range.Font.Size = 12;
                title.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                title.Range.InsertParagraphAfter();

                // Период отчета
                Word.Paragraph period = doc.Paragraphs.Add();
                period.Range.Text = $"Период: {StartDatePicker.SelectedDate.Value.ToShortDateString()} - {EndDatePicker.SelectedDate.Value.ToShortDateString()}";
                period.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                period.Range.InsertParagraphAfter();

                // Пустая строка
                Word.Paragraph emptyLine = doc.Paragraphs.Add();
                emptyLine.Range.Text = "";
                emptyLine.Range.InsertParagraphAfter();

                // Создаем таблицу для данных
                Word.Table table = doc.Tables.Add(
                    doc.Paragraphs.Add().Range,
                    dataView.Table.Rows.Count + 1, // +1 для заголовков
                    dataView.Table.Columns.Count);

                // Заполняем заголовки таблицы
                for (int i = 0; i < dataView.Table.Columns.Count; i++)
                {
                    table.Cell(1, i + 1).Range.Text = dataView.Table.Columns[i].ColumnName;
                    table.Cell(1, i + 1).Range.Font.Bold = 1;
                }

                // Заполняем данные таблицы
                for (int i = 0; i < dataView.Table.Rows.Count; i++)
                {
                    for (int j = 0; j < dataView.Table.Columns.Count; j++)
                    {
                        table.Cell(i + 2, j + 1).Range.Text = dataView.Table.Rows[i][j].ToString();
                    }
                }

                // Форматирование таблицы
                table.Range.ParagraphFormat.SpaceAfter = 0;
                table.Rows[1].Range.Font.Bold = 1;
                table.Rows[1].Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                table.Borders.Enable = 1;
                table.AutoFitBehavior(Word.WdAutoFitBehavior.wdAutoFitWindow);

                // Добавляем сводку
                Word.Paragraph summary = doc.Paragraphs.Add();
                summary.Range.Text = SummaryText.Text;
                summary.Range.InsertParagraphAfter();

                // Добавляем место для подписей
                Word.Paragraph sign1 = doc.Paragraphs.Add();
                sign1.Range.Text = companyInfo != null ?
                    $"Директор: _________________ /{companyInfo["ceo_name"]}/" :
                    "Директор: _________________ /Иванов И.И./";
                sign1.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphRight;
                sign1.Range.InsertParagraphAfter();

                Word.Paragraph sign2 = doc.Paragraphs.Add();
                sign2.Range.Text = "Дата: _________________";
                sign2.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphRight;
                sign2.Range.InsertParagraphAfter();

                Word.Paragraph sign3 = doc.Paragraphs.Add();
                sign3.Range.Text = "М.П.";
                sign3.Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphRight;
                sign3.Range.InsertParagraphAfter();

                // Добавляем банковские реквизиты, если есть
                if (companyInfo != null && !string.IsNullOrEmpty(companyInfo["bank_name"].ToString()))
                {
                    Word.Paragraph bankHeader = doc.Paragraphs.Add();
                    bankHeader.Range.Text = "Банковские реквизиты:";
                    bankHeader.Range.Font.Bold = 1;
                    bankHeader.Range.InsertParagraphAfter();

                    Word.Paragraph bankDetails = doc.Paragraphs.Add();
                    bankDetails.Range.Text = $"{companyInfo["bank_name"]}\n" +
                                            $"БИК: {companyInfo["bik"]}\n" +
                                            $"Р/с: {companyInfo["payment_account"]}\n" +
                                            $"Корр. счет: {companyInfo["correspondent_account"]}";
                    bankDetails.Range.InsertParagraphAfter();
                }

                // Сохранение файла
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Word Documents|*.docx",
                    FileName = companyInfo != null ?
                        $"{companyInfo["company_name"]} - Налоговый отчет {DateTime.Now:yyyy-MM-dd}" :
                        $"Налоговый отчет {DateTime.Now:yyyy-MM-dd}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    doc.SaveAs2(saveFileDialog.FileName);
                    MessageBox.Show("Отчет успешно экспортирован в Word", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта в Word: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Освобождение ресурсов
                if (doc != null) Marshal.ReleaseComObject(doc);
                if (wordApp != null)
                {
                    wordApp.Quit();
                    Marshal.ReleaseComObject(wordApp);
                }
            }
        }
    }
}