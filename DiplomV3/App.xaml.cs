using System;
using System.Windows;
using DiplomV3.Properties; // Добавляем, чтобы использовать настройки

namespace DiplomV3
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Загружаем текущую тему из настроек
            string theme = Settings.Default.CurrentTheme;
            ApplyTheme(theme);
        }

        public static void ApplyTheme(string themeKey)
        {
            if (string.IsNullOrEmpty(themeKey))
                themeKey = "LightTheme"; // Тема по умолчанию

            var themeDictionary = new ResourceDictionary
            {
                Source = new Uri($"/Themes/{themeKey}.xaml", UriKind.Relative)
            };

            // Очищаем старые ресурсы и добавляем новую тему
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(themeDictionary);
            // Сохраняем выбор темы
            Settings.Default.CurrentTheme = themeKey;
            Settings.Default.Save();


        }
    }
}
