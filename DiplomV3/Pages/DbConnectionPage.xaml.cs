using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DiplomV3.Properties;

namespace DiplomV3.Pages
{
    /// <summary>
    /// Логика взаимодействия для DbConnectionPage.xaml
    /// </summary>
    public partial class DbConnectionPage : Page
    {
        public DbConnectionPage()
        {
            InitializeComponent();
            LoadSettings();
        }


        private void LoadSettings()
        {
            // Загружаем сохранённые настройки в поля
            TxtServer.Text = Settings.Default.DbServerAddress;
            TxtDbLogin.Text = Settings.Default.DbLogin;
            TxtDbPassword.Password = Settings.Default.DbPassword;
        }
        private void SaveSettings()
        {
            // Сохраняем настройки при подключении
            Settings.Default.DbServerAddress = TxtServer.Text;
            Settings.Default.DbLogin = TxtDbLogin.Text;
            Settings.Default.DbPassword = TxtDbPassword.Password;
            Settings.Default.Save();
        }

        private void ConnectToDb_Click(object sender, RoutedEventArgs e)
        {
            // Загружаем строку подключения из настроек
            string connString = $"Сервер: {Settings.Default.DbServerAddress} Логин: {Settings.Default.DbLogin} пароль: {Settings.Default.DbPassword}";

            MessageBox.Show($"Строка подключения: {connString}");

            SaveSettings();
            MessageBox.Show("Подключение успешно!");
            // Перезапуск приложения
            Application.Current.Shutdown();  // Закрытие текущего приложения

            // Запуск нового экземпляра приложения
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
        }


        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаемся к главному окну
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                // Показываем панель авторизации
                mainWindow.LoginPanel.Visibility = Visibility.Visible;

                // Скрываем страницу подключения к БД
                mainWindow.MainFrame.Visibility = Visibility.Collapsed;
            }
        }
    }
}
