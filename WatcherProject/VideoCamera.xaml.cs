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
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Windows.Threading;
using System.Threading;
using System.ComponentModel;

namespace WatcherProject
{
    public partial class VideoCamera : UserControl
    {
        int CameraIndex;
        public List<System.Drawing.Rectangle> Faces { get; set; } = new List<System.Drawing.Rectangle>();
        public VideoCapture Capture { get; set; }
        public Image<Bgr, Byte> CurrentFrame { get; set; }
        public DispatcherTimer Timer { get; set; }
        private MainWindow MainWindow;
        private string prevReportLog;
        public VideoCamera(MainWindow MW, int cameraIndex, string deviceName)
        {
            InitializeComponent();
            MainWindow = MW;
            CameraName.Content = string.IsNullOrEmpty(deviceName) ? "Камера " + cameraIndex : deviceName;
            Capture = new VideoCapture(cameraIndex);
            CameraIndex = cameraIndex;

            Timer = new DispatcherTimer();
            Timer.Tick += new EventHandler(Timer_Tick);
            Timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            Timer.Start();
            MainWindow.Logs.Add(String.Format("------- {0} - {1}: ",
                DateTime.Now.ToString(), CameraName.Content) + " - камера добавлена -");
            if (MainWindow.FullScreen)
                Visibility = Visibility.Collapsed;
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            //Тестить, фиксить
            try
            {
                CurrentFrame = Capture.QueryFrame().ToImage<Bgr, Byte>();
            }
            catch (Exception)
            {
                try
                {
                    Thread.Sleep(100);
                    Capture = new VideoCapture(CameraIndex);
                }
                catch (Exception)
                {
                    CameraDisconected.Visibility = Visibility.Visible;
                    StartStopButton_Click(null, null);
                }
                return;
            }
            string reportLog = "",
                log = String.Format("{0} - {1}: ", DateTime.Now.ToString(), CameraName.Content);
            Image<Bgr, Byte> image = CurrentFrame.Copy();
            using (UMat ugray = new UMat())
            {
                CvInvoke.CvtColor(image, ugray, ColorConversion.Bgr2Gray);
                CvInvoke.EqualizeHist(ugray, ugray);
                Faces.Clear();
                Faces = new List<System.Drawing.Rectangle>();
                Faces.AddRange(MainWindow.HaarCascade.DetectMultiScale(ugray, 1.1, 10, new System.Drawing.Size(20, 20)));
                int recognizeCount = 0;
                foreach (var face in Faces)
                {
                    image.Draw(face, new Bgr(0, 0, 255), 3);
                    if (MainWindow.PeopleData.Face.Count() != 0)
                    {
                        var result = MainWindow.FaceRecognizer.Predict(ugray.ToImage<Gray, Byte>().Copy(face).Resize(100, 100, Inter.Cubic));
                        if (result.Label > 0 && result.Distance <= 100)
                        {
                            recognizeCount++;
                            string name = MainWindow.PeopleData.Name[result.Label - 1];
                            image.Draw(100 - (int)result.Distance + " " + name,
                                new System.Drawing.Point(face.X, face.Y - 10),
                                FontFace.HersheyComplex,
                                0.8,
                                new Bgr(0, 0, 255));
                            reportLog += name + "; ";
                        }
                    }
                }

                int unknownFaces = Faces.Count() - recognizeCount;
                if (unknownFaces > 0)
                    reportLog += unknownFaces + " неизвестных";
                else if (reportLog == "")
                    reportLog = " ---";
                if (reportLog != prevReportLog)
                {
                    MainWindow.Logs.Add(log + reportLog);
                }
                prevReportLog = reportLog;
            }

            VideoStream.Source = Watcher.ToBitmapSource(image);
            RecognizeCount.Content = "(" + Faces.Count() + ")";
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            CameraName.Content = Watcher.SetName("Введите новое название камеры:");
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (Timer.IsEnabled)
            {
                Timer.Stop();
                Capture.Dispose();
                StartStopButton.Content = "➤";
                MainWindow.Logs.Add(String.Format("------- {0} - {1}: ",
                    DateTime.Now.ToString(), CameraName.Content) + " - камера остановлена -");
            }
            else
            {
                CameraDisconected.Visibility = Visibility.Collapsed;
                Capture = new VideoCapture(CameraIndex);
                Timer.Start();
                StartStopButton.Content = "I I";
                MainWindow.Logs.Add(String.Format("------- {0} - {1}: ",
                    DateTime.Now.ToString(), CameraName.Content) + " - камера запущена -");
            }
        }

        private void FullLowScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.CameraPlace.Children.Count > 1)
                if (FullLowScreenButton.Content.ToString() == "☐")
                {
                    foreach (VideoCamera item in MainWindow.CameraPlace.Children)
                    {
                        item.Visibility = Visibility.Collapsed;
                    }
                    Visibility = Visibility.Visible;
                    FullLowScreenButton.Content = "_";
                    MainWindow.FullScreen = true;
                }
                else
                {
                    foreach (VideoCamera item in MainWindow.CameraPlace.Children)
                    {
                        item.Visibility = Visibility.Visible;
                    }
                    FullLowScreenButton.Content = "☐";
                    MainWindow.FullScreen = false;
                }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.FullScreen)
                FullLowScreenButton_Click(null, null);
            MainWindow.Logs.Add(String.Format("------- {0} - {1}: ",
                DateTime.Now.ToString(), CameraName.Content) + " - камера отключена -");
            Timer.Stop();
            Timer = null;
            Capture.Dispose();
            //((System.Windows.Controls.Primitives.UniformGrid)Parent).Children.Remove(this);
            MainWindow.CameraPlace.Children.Remove(this);
            //MainWindow.CameraList.Items.Remove(this);
        }
    }
}
