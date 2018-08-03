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

namespace WatcherProject
{
    /// <summary>
    /// Логика взаимодействия для ModalGetNameWindow.xaml
    /// </summary>
    public partial class ModalGetNameWindow : Window
    {
        public ModalGetNameWindow(string text)
        {
            InitializeComponent();
            TextLabel.Content = text;
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        public string ItemName
        {
            get { return NameTextBox.Text; }
        }
    }
}
