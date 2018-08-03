using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
using Emgu.CV;
using Emgu.CV.Structure;

namespace WatcherProject
{
    public partial class FaceItem : UserControl
    {
        private MainWindow MainWindow;

        public FaceItem(MainWindow MW, int faceIndex)
        {
            InitializeComponent();
            MainWindow = MW;

            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri(Environment.CurrentDirectory + MainWindow.PeopleData.Face[faceIndex], UriKind.RelativeOrAbsolute);
            bi.EndInit();
            FaceImage.Source = bi;

            int nameIndex = MainWindow.PeopleData.NameId[faceIndex];
            FaceName.Content = nameIndex + "." + MainWindow.PeopleData.Name[nameIndex - 1];
            FaceName.ToolTip = FaceName.Content;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            int faceId = MainWindow.FaceItemsPlace.Children.IndexOf(this);
            int nameId = MainWindow.PeopleData.NameId[faceId];
            
            if (File.Exists(Environment.CurrentDirectory + MainWindow.PeopleData.Face[faceId]))
                File.Delete(Environment.CurrentDirectory + MainWindow.PeopleData.Face[faceId]); 

            MainWindow.PeopleData.NameId.RemoveAt(faceId);
            MainWindow.PeopleData.Face.RemoveAt(faceId);
            if (MainWindow.PeopleData.NameId.IndexOf(nameId) == -1)
            {
                MainWindow.PeopleData.Name[nameId - 1] = null;
                MainWindow.PeopleData.DeletedNamesId.Add(nameId - 1);
            }
            while (MainWindow.PeopleData.Name.Count() != 0 && MainWindow.PeopleData.Name.Last() == null)
            {
                int last = MainWindow.PeopleData.Name.Count() - 1;
                MainWindow.PeopleData.DeletedNamesId.Remove(last);
                MainWindow.PeopleData.Name.RemoveAt(last);
            }
            Watcher.WriteJson(MainWindow.PeopleData, ConfigurationManager.AppSettings["JsonData"]);
            MainWindow.TrainFaceRecognizer();
            MainWindow.FaceItemsPlace.Children.Remove(this);
        }
    }
}
