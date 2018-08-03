using System;
using System.Collections.Generic;
using System.Drawing;
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
using Emgu.CV.Face;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Newtonsoft.Json;
using System.Drawing.Imaging;
using System.IO;
using System.Configuration;
using Microsoft.Win32;
using DirectShowLib;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;

namespace WatcherProject
{
    public partial class VideoItem : UserControl
    {
        MainWindow MainWindow;
        public VideoItem(MainWindow MW, string path)
        {
            Refresh(MW, path);
        }

        public void Refresh(MainWindow MW, string path)
        {
            InitializeComponent();
            MainWindow = MW;

            VideoCapture capture = new VideoCapture(path);
            int framecount = (int)Math.Floor(capture.GetCaptureProperty(CapProp.FrameCount));
            capture.SetCaptureProperty(CapProp.PosFrames, framecount / 2);
            FileName.Content = System.IO.Path.GetFileName(path);
            VideoPreview.Source = Watcher.ToBitmapSource(capture.QueryFrame().ToImage<Bgr, Byte>());
        }

        public void VideoBorder(bool value)
        {
            CurrentVideoBorder.Visibility = value ? Visibility.Visible : Visibility.Hidden;
        }

        private void ChooseVideoButton_Click(object sender, RoutedEventArgs e)
        {
            VideoBorder(true);
            MainWindow.SetLoadVideos(MainWindow.VideoList.Children.IndexOf(this));
        }
        public bool IsActive()
        {
            return CurrentVideoBorder.Visibility == Visibility.Visible;
        }
    }
}
