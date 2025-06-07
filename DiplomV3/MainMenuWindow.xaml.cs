using DiplomV3.Pages;
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
using System.Windows.Shapes;


namespace DiplomV3
{
    /// <summary>
    /// Логика взаимодействия для MainMenuWindow.xaml
    /// </summary>
    public partial class MainMenuWindow : Window
    {
        private int currentUserId;
        private string currentUserRole;

        public MainMenuWindow(int userId, string role)
        {
            InitializeComponent();
            if (role == "Admin")
            {
                FrameContent.Content = new AdminPage();
            }
            else
            {
                FrameContent.Content = new UserPage(userId, role);
            }
        }
    }
}
