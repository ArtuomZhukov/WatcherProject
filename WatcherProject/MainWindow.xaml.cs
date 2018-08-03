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
using System.Windows.Threading;
using System.Runtime.InteropServices;
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
using System.Drawing;
using System.Threading;

namespace WatcherProject
{
    public partial class MainWindow : Window
    {
        public FaceRecognizer FaceRecognizer { get; set; }
        public CascadeClassifier HaarCascade { get; set; }
        public PeopleData PeopleData { get; set; }
        public ObservableCollection<string> Logs { get; set; }
        public bool FullScreen { get; set; } = false;
        List<Image<Bgr, Byte>> selectedFaces = new List<Image<Bgr, Byte>>();
        List<Image<Bgr, Byte>> selectedImages = new List<Image<Bgr, Byte>>();
        List<string> selectedVideos = new List<string>();
        List<string> processedVideos = new List<string>();
        int currentFace, currentPreviewImage, currentVideo;
        bool isPlaying = false,
            lockLoadFaces = false,
            lockGetLoadVideos = false,
            lockProcessVideo = false;
        DispatcherTimer AutoSaveLogTimer, VideoTimer;
        TimeSpan videoTimeSpan;
        public MainWindow()
        {
            if (!File.Exists("haarcascade_frontalface_default.xml"))
            {
                MessageBox.Show("Файл haarcascade_frontalface_default.xml не найден.");
                Environment.Exit(0);
            }
            InitializeComponent();

            PeopleData = Watcher.ReadPeopleDataJson();
            HaarCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
            FaceRecognizer = LoadFaceRecognizer();
            Refresh_Click(null, null);
            if (PeopleData.Face.Count > 0)
            {
                for (int i = 0; i < PeopleData.Face.Count; i++)
                {
                    FaceItemsPlace.Children.Add(new FaceItem(this, i));
                }
                NoFaces.Visibility = Visibility.Collapsed;
            }
            Logs = Watcher.ReadLogsJson();
            LogList.ItemsSource = Logs;
            Logs.CollectionChanged += Logs_CollectionChanged;

            AutoSaveLogTimer = new DispatcherTimer();
            AutoSaveLogTimer.Tick += new EventHandler(SaveLogs);
            AutoSaveLogTimer.Interval = new TimeSpan(0, 1, 0);
            AutoSaveLogTimer.Start();

            VideoTimer = new DispatcherTimer();
            VideoTimer.Tick += new EventHandler(VideoTimerTick);
            VideoTimer.Interval = new TimeSpan(0, 0, 1);
            Logs.Add(DateTime.Now.ToString() + " - программа запущена -");
        }

        //TODO:

        //Фиксить добаление обработаного видео 

        //фиксить обработку, замену и сохранение видосика
        //оптимизировать распознавание с камер(раз в 5 кадров)
        //пофиксить автоскролл логов
        //пройтись по проге и коду, мелкие фиксы


        //фиксить утечки памяти
        //Вынести в потоки операции, которые занимают много времени(вроде готово)
        //Дизайн проги(по возможности)


        //Нельзя добавлять одинаковые камеры(пофиг)
        //Отлов ошибок при отрубании камеры(тестить)
        //Поменять таймеры в камерах на потоки(не успеваю)

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (CameraList.SelectedItem.ToString() != "" && CameraPlace.Children.Count < 4)
            {
                string name = Watcher.SetName("Введите название камеры: ");
                if (name == null)
                    return;
                CameraPlace.Children.Add(new VideoCamera(this, CameraList.SelectedIndex, name));
            }
        }

        #region Выбор лица
        private void ChooseFace_Click(object sender, RoutedEventArgs e)
        {
            if (CameraPlace.Children.Count == 0)
                return;
            List<Image<Bgr, Byte>> findFaces = new List<Image<Bgr, byte>>();
            foreach (VideoCamera camera in CameraPlace.Children)
            {
                Image<Bgr, Byte> capturedImage = camera.CurrentFrame;
                foreach (System.Drawing.Rectangle face in camera.Faces)
                {
                    findFaces.Add(capturedImage.Copy(face));
                }
            }
            if (findFaces.Count() == 0)
            {
                MessageBox.Show("Лица не найдены");
                return;
            }
            selectedFaces = new List<Image<Bgr, Byte>>(findFaces);
            currentFace = 0;
            SetImageFace(currentFace);
            SelectedFaceCount.Content = "Всего выбрано: " + selectedFaces.Count();
        }
        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFaces.Count() == 0)
                return;
            if (currentFace == 0)
                currentFace = selectedFaces.Count() - 1;
            else
                currentFace--;
            SetImageFace(currentFace);
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFaces.Count() == 0)
                return;
            if (currentFace == selectedFaces.Count() - 1)
                currentFace = 0;
            else
                currentFace++;
            SetImageFace(currentFace);
        }
        private void SetImageFace(int index)
        {
            if (selectedFaces.Count() > 0)
            {
                Dispatcher.Invoke(() =>
                {
                    SelectedFaceIndex.Content = index + 1;
                    Face.Source = Watcher.ToBitmapSource(selectedFaces[index]);
                    ClearFaces.Visibility = Visibility.Visible;
                });
            }
        }
        private void SaveFace_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFaces.Count() == 0)
            {
                MessageBox.Show("Лицо не выбрано");
                return;
            }
            if (String.IsNullOrWhiteSpace(PersonName.Text))
            {
                MessageBox.Show("Поле для ввода имени должно быть заполнено");
                return;
            }

            using (UMat ugray = new UMat())
            {
                Image<Bgr, Byte> img = selectedFaces[currentFace].Resize(100, 100, Inter.Cubic);
                CvInvoke.CvtColor(img, ugray, ColorConversion.Bgr2Gray);
                CvInvoke.EqualizeHist(ugray, ugray);

                DateTime date = DateTime.UtcNow;
                string filepath = "/Faces/face" + date.Year + date.Month +
                    date.Day + date.Hour + date.Minute + date.Second + date.Millisecond + ".bmp";
                ugray.Save(Environment.CurrentDirectory + filepath);

                PeopleData.Add(PersonName.Text, filepath);

                Watcher.WriteJson(PeopleData, ConfigurationManager.AppSettings["JsonData"]);

                TrainFaceRecognizer();
            }

            FaceItemsPlace.Children.Add(new FaceItem(this, PeopleData.Face.Count - 1));
            NoFaces.Visibility = Visibility.Collapsed;
            MessageBox.Show("Лицо " + PersonName.Text + " сохранено");
        }
        #endregion

        #region FaceRecognizer Методы
        private FaceRecognizer LoadFaceRecognizer()
        {
            if (File.Exists(ConfigurationManager.AppSettings["FaceRecognizerData"]))
            {
                FaceRecognizer faceRecognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);
                faceRecognizer.Read(ConfigurationManager.AppSettings["FaceRecognizerData"]);
                return faceRecognizer;
            }
            return new LBPHFaceRecognizer(1, 8, 8, 8, 100);
        }

        private void SaveFaceRecognizer()
        {
            FaceRecognizer.Write(ConfigurationManager.AppSettings["FaceRecognizerData"]);
        }

        public void TrainFaceRecognizer()
        {
            if (PeopleData.Face.Count() == 0)
                return;

            Image<Gray, Byte>[] trainFaces = new Image<Gray, Byte>[PeopleData.Face.Count()];

            for (int i = 0; i < PeopleData.Face.Count(); i++)
            {
                trainFaces[i] = new Image<Gray, byte>(Environment.CurrentDirectory + PeopleData.Face[i]);
            }
            FaceRecognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);
            FaceRecognizer.Train(trainFaces, PeopleData.NameId.ToArray());

            SaveFaceRecognizer();
        }
        #endregion
        private void Face_Drop(object sender, DragEventArgs e)
        {
            GetLoadFaces((string[])e.Data.GetData(DataFormats.FileDrop));
        }

        private void LoadFace_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog myDialog = new OpenFileDialog
            {
                Filter = "Картинки(*.JPG;*.GIF;*.PNG)|*.JPG;*.GIF;*.PNG" + "|Все файлы (*.*)|*.* ",
                CheckFileExists = true,
                Multiselect = true
            };
            if (myDialog.ShowDialog() == true)
            {
                GetLoadFaces(myDialog.FileNames);
            }
        }
        //Есть небольшие утечки памяти
        private void GetLoadFaces(string[] files)
        {
            if (lockLoadFaces)
                return;
            lockLoadFaces = true;
            TaskItem taskItem = null;
            taskItem = new TaskItem("Получение лиц с фотографий", false, () =>
            {
                if (files == null)
                    return;
                ImageSourceConverter converter = new ImageSourceConverter();
                List<string> correctFiles = new List<string>();
                for (int i = 0; i < files.Count(); i++)
                    if (converter.IsValid(files[i]))
                        correctFiles.Add(files[i]);

                if (correctFiles.Count < 0)
                    return;

                List<Image<Bgr, Byte>> findImages = new List<Image<Bgr, byte>>();
                foreach (string file in correctFiles)
                {
                    findImages.Add(new Image<Bgr, byte>(file));
                }
                if (findImages.Count < 0)
                    return;

                List<Image<Bgr, Byte>> findFaces = new List<Image<Bgr, byte>>();
                foreach (var img in findImages)
                {
                    using (UMat ugray = new UMat())
                    {
                        CvInvoke.CvtColor(img, ugray, ColorConversion.Bgr2Gray);
                        CvInvoke.EqualizeHist(ugray, ugray);
                        var rects = HaarCascade.DetectMultiScale(ugray, 1.1, 10, new System.Drawing.Size(20, 20));
                        foreach (var rect in rects)
                        {
                            findFaces.Add(img.Copy(rect));
                        }
                    }
                }
                if (findFaces.Count < 0)
                    return;
                selectedFaces = new List<Image<Bgr, byte>>(findFaces);
                Dispatcher.Invoke(() =>
                {
                    SelectedFaceCount.Content = "Всего выбрано: " + selectedFaces.Count();
                });
                currentFace = 0;

                SetImageFace(currentFace);
                Dispatcher.Invoke(() =>
                {
                    taskItem.Remove();
                });
                lockLoadFaces = false;
            });

            TaskList.Items.Add(taskItem);
        }

        private void DeleteFaces_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно собираетесь удалить базу лиц?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                FaceItemsPlace.Children.Clear();
                foreach (var file in PeopleData.Face)
                    if (File.Exists(Environment.CurrentDirectory + file))
                        File.Delete(Environment.CurrentDirectory + file);
                PeopleData = new PeopleData();
                if (File.Exists(Environment.CurrentDirectory + ConfigurationManager.AppSettings["JsonData"]))
                    File.Delete(Environment.CurrentDirectory + ConfigurationManager.AppSettings["JsonData"]);

                if (File.Exists(ConfigurationManager.AppSettings["FaceRecognizerData"]))
                    File.Delete(ConfigurationManager.AppSettings["FaceRecognizerData"]);
                FaceRecognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);
                MessageBox.Show("Данные обучения удалены");
            }
        }

        private void ClearFaces_Click(object sender, RoutedEventArgs e)
        {
            Face.Source = null;
            selectedFaces.Clear();
            currentFace = 0;
            SelectedFaceIndex.Content = null;
            SelectedFaceCount.Content = "Всего выбрано: 0";
            ClearFaces.Visibility = Visibility.Collapsed;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CameraList.Items.Clear();
            List<string> Names = new List<string>();
            foreach (var item in DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice))
            {
                CameraList.Items.Add(item.Name);
            }
            CameraList.SelectedIndex = 0;
        }

        private void DeleteLogs_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно собираетесь удалить все логи?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Logs.Clear();
                Logs.Add(DateTime.Now.ToString() + " - логи удалены -");
            }
        }

        private void AutoSaveLogs_Click(object sender, RoutedEventArgs e)
        {
            if (AutoSaveLogs.IsChecked == true)
                AutoSaveLogTimer.Start();
            else
                AutoSaveLogTimer.Stop();
        }

        public void SaveLogs(object sender, EventArgs e)
        {
            Watcher.WriteJson(Logs, ConfigurationManager.AppSettings["LogData"]);
        }

        private void Logs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && Autoscroll.IsChecked == true)
                ScrollTobottom();
        }

        private void ScrollTobottom()
        {
            if (VisualTreeHelper.GetChildrenCount(LogList) > 0)
            {
                Border border = (Border)VisualTreeHelper.GetChild(LogList, 0);
                ScrollViewer scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                scrollViewer.ScrollToBottom();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logs.Add(DateTime.Now.ToString() + " - программа выключена -");
            if (AutoSaveLogs.IsChecked == true)
                SaveLogs(null, null);
            RemoveProcessedVideo();
        }

        private void SaveLogsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveLogs(null, null);
            MessageBox.Show("Логи сохранены");
        }

        //---------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------

        #region Image

        private void PreviewImage_Drop(object sender, DragEventArgs e)
        {
            GetLoadImages((string[])e.Data.GetData(DataFormats.FileDrop));
        }
        private void LoadFaceImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog myDialog = new OpenFileDialog
            {
                Filter = "Картинки(*.JPG;*.GIF;*.PNG)|*.JPG;*.GIF;*.PNG" + "|Все файлы (*.*)|*.* ",
                CheckFileExists = true,
                Multiselect = true
            };
            if (myDialog.ShowDialog() == true)
            {
                GetLoadImages(myDialog.FileNames);
            }
        }
        public void SetImagePreview(int index)
        {
            if (selectedImages.Count() > 0)
            {
                SelectedFaceImageIndex.Content = String.Format("({0}/{1})", index + 1, selectedImages.Count());
                PreviewImage.Source = Watcher.ToBitmapSource(selectedImages[index]);
                FindFaces.Visibility = Visibility.Visible;
                PrevImage.Visibility = Visibility.Visible;
                NextImage.Visibility = Visibility.Visible;
                SaveFaceImage.Visibility = Visibility.Visible;
                ClearFaceImage.Visibility = Visibility.Visible;
                PutImageHereLabel.Visibility = Visibility.Hidden;
            }
        }
        private void GetLoadImages(string[] files)
        {
            if (files == null)
                return;

            ImageSourceConverter converter = new ImageSourceConverter();
            List<string> correctFiles = new List<string>();
            for (int i = 0; i < files.Count(); i++)
                if (converter.IsValid(files[i]))
                    correctFiles.Add(files[i]);

            if (correctFiles.Count < 0)
                return;

            List<Image<Bgr, Byte>> findImages = new List<Image<Bgr, byte>>();
            foreach (string file in correctFiles)
            {
                findImages.Add(new Image<Bgr, byte>(file));
            }
            if (findImages.Count < 0)
                return;

            selectedImages = new List<Image<Bgr, byte>>(findImages);
            currentPreviewImage = 0;
            SetImagePreview(currentPreviewImage);
        }
        private void SaveFaceImage_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog SaveImageDialog = new SaveFileDialog
            {
                Filter = "Изображение(*.bmp)|*.bmp"
            };
            if (SaveImageDialog.ShowDialog() == true)
            {
                string sFilePic = SaveImageDialog.FileName;
                if (sFilePic == "")
                    return;
                try //хз нужен ли тут трай кетч
                {
                    selectedImages[currentPreviewImage].ToBitmap().Save(String.Format("{0}.{1}", sFilePic, ImageFormat.Bmp));
                }
                catch (IOException)
                {
                    MessageBox.Show("Ошибка открытия файла на запись", "Ошибка");
                    return;
                }
            }
        }
        private void ClearFaceImage_Click(object sender, RoutedEventArgs e)
        {
            PutImageHereLabel.Visibility = Visibility.Visible;
            FindFaces.Visibility = Visibility.Hidden;
            PrevImage.Visibility = Visibility.Hidden;
            NextImage.Visibility = Visibility.Hidden;
            SaveFaceImage.Visibility = Visibility.Hidden;
            ClearFaceImage.Visibility = Visibility.Hidden;
            SelectedFaceImageIndex.Content = "";
            selectedImages.Clear();
            PreviewImage.Source = null;
        }
        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            if (selectedImages.Count() == 0)
                return;
            if (currentPreviewImage == 0)
                currentPreviewImage = selectedImages.Count() - 1;
            else
                currentPreviewImage--;
            SetImagePreview(currentPreviewImage);
        }
        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (selectedImages.Count() == 0)
                return;
            if (currentPreviewImage == selectedImages.Count() - 1)
                currentPreviewImage = 0;
            else
                currentPreviewImage++;
            SetImagePreview(currentPreviewImage);
        }
        //Есть утечки памяти
        private void FindFaces_Click(object sender, RoutedEventArgs e)
        {
            TaskItem taskItem = null;
            taskItem = new TaskItem("Поиск лиц на фотографии", false, () =>
            {
                Image<Bgr, Byte> image = selectedImages[currentPreviewImage].Copy();
                using (UMat ugray = new UMat())
                {
                    CvInvoke.CvtColor(image, ugray, ColorConversion.Bgr2Gray);
                    CvInvoke.EqualizeHist(ugray, ugray);
                    List<System.Drawing.Rectangle> Faces = new List<System.Drawing.Rectangle>();
                    Faces.AddRange(HaarCascade.DetectMultiScale(ugray, 1.1, 10, new System.Drawing.Size(20, 20)));
                    foreach (var face in Faces)
                    {
                        image.Draw(face, new Bgr(0, 0, 255), 3);
                        if (PeopleData.Face.Count() != 0)
                        {
                            var result = FaceRecognizer.Predict(ugray.ToImage<Gray, Byte>().Copy(face).Resize(100, 100, Inter.Cubic));
                            if (result.Label > 0 && result.Distance <= 100)
                            {
                                image.Draw(PeopleData.Name[result.Label - 1],
                                    new System.Drawing.Point(face.X, face.Y - 10),
                                    FontFace.HersheyComplex,
                                    0.8,
                                    new Bgr(0, 0, 255));
                            }
                        }
                    }
                }
                selectedImages[currentPreviewImage] = image.Copy();
                Dispatcher.Invoke(() =>
                {
                    SetImagePreview(currentPreviewImage);
                    taskItem.Remove();
                });
            });
            TaskList.Items.Add(taskItem);
        }

        #endregion

        #region Video

        private void LoadVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog myDialog = new OpenFileDialog
            {
                Filter = /*"Видео(*.*)|*.*"*/"Видео(*.MP4;*.AVI;)|*.MP4;*.AVI",
                CheckFileExists = true,
                Multiselect = true
            };
            if (myDialog.ShowDialog() == true)
            {
                GetLoadVideos(myDialog.FileNames);
            }
        }

        private void GetLoadVideos(string[] files)
        {
            if (files == null || lockGetLoadVideos || lockProcessVideo)
                return;
            lockGetLoadVideos = true;
            //RemoveProcessedVideo();
            //TODO проверка на правильные файлы
            selectedVideos = new List<string>();

            foreach (var file in files)
            {
                if (System.IO.Path.GetExtension(file) == ".mp4" ||
                    System.IO.Path.GetExtension(file) == ".avi")
                    selectedVideos.Add(file);
            }
            if (selectedVideos.Count() == 0)
                return;
            VideoList.Children.Clear();

            foreach (var videoPath in selectedVideos)
            {
                VideoList.Children.Add(new VideoItem(this, videoPath));
            }
            SetLoadVideos(0);
            lockGetLoadVideos = false;
        }

        public void SetLoadVideos(int index)
        {
            //фиксить краш при загрузке видео после очистки
            Dispatcher.Invoke(() =>
            {
                PutVideoHereLabel.Visibility = Visibility.Hidden;
                ((VideoItem)VideoList.Children[currentVideo]).VideoBorder(false);
            });
            currentVideo = index;
            string videoName = System.IO.Path.GetFileNameWithoutExtension(selectedVideos[index]);
            Uri source = new Uri(selectedVideos[index]);
            Dispatcher.Invoke(() =>
            {
                VideoNameLabel.Content = videoName;
                VideoPlayer.Volume = VideoVolume.Value / 100;
                VideoPlayer.Source = source;
                ((VideoItem)VideoList.Children[currentVideo]).VideoBorder(true);
            });
        }

        private void StartStopVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                VideoPlayer.Pause();
                VideoTimer.Stop();
                StartStopVideoButton.Content = "➤";
            }
            else
            {
                if (VideoTimeSlider.Value == VideoTimeSlider.Maximum)
                    VideoPlayer.Position = TimeSpan.MinValue;
                VideoPlayer.Play();
                VideoTimer.Start();
                VideoPlayer.Volume = VideoVolume.Value / 100;
                StartStopVideoButton.Content = "I I";
            }
            isPlaying = !isPlaying;
        }

        private void VideoVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VideoPlayer.Volume = VideoVolume.Value / 100;
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            videoTimeSpan = TimeSpan.FromMilliseconds(VideoPlayer.NaturalDuration.TimeSpan.TotalMilliseconds);
            VideoTimeSlider.Maximum = videoTimeSpan.TotalSeconds;
        }

        private void VideoPlayer_MouseEnter(object sender, MouseEventArgs e)
        {
            if (VideoPlayer.Source != null)
                VideoPlayerControls.Visibility = Visibility.Visible;
        }

        private void VideoPlayer_MouseLeave(object sender, MouseEventArgs e)
        {
            VideoPlayerControls.Visibility = Visibility.Hidden;
        }

        private void VideoTimeSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(VideoTimeSlider.Value);
            VideoPlayer.Position = timeSpan;
        }

        private void VideoPlayer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StartStopVideoButton_Click(null, null);
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            GetLoadVideos((string[])e.Data.GetData(DataFormats.FileDrop));
        }

        private void ClearVideo_Click(object sender, RoutedEventArgs e)
        {
            if (lockProcessVideo)
                return;
            PutVideoHereLabel.Visibility = Visibility.Visible;
            VideoList.Children.Clear();
            VideoPlayer.Source = null;
            VideoPlayerControls.Visibility = Visibility.Hidden;
            RemoveProcessedVideo();
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StartStopVideoButton_Click(null, null);
        }

        private void ProcessVideo_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVideos.Count() == 0 || lockProcessVideo)
                return;
            lockProcessVideo = true;
            TaskItem taskItem = null;
            taskItem = new TaskItem("Поиск лиц на видеозаписи(" +
                System.IO.Path.GetFileName(selectedVideos[currentVideo]) + ")", false, () =>
            {
                string path = selectedVideos[currentVideo];
                int processedVideoIndex = currentVideo;
                VideoCapture videoReader = new VideoCapture(path);

                int fourcc = Convert.ToInt32(videoReader.GetCaptureProperty(CapProp.FourCC));
                int fps = Convert.ToInt32(videoReader.GetCaptureProperty(CapProp.Fps));
                int width = Convert.ToInt32(videoReader.GetCaptureProperty(CapProp.FrameWidth));
                int heigth = Convert.ToInt32(videoReader.GetCaptureProperty(CapProp.FrameHeight));
                int framecount = (int)Math.Floor(videoReader.GetCaptureProperty(CapProp.FrameCount));

                string writePath = Environment.CurrentDirectory + "\\video" + currentVideo + System.IO.Path.GetExtension(path);

                if (File.Exists(writePath))
                    File.Delete(writePath);
                VideoWriter videoWriter = new VideoWriter(writePath, fourcc, fps,
                    new System.Drawing.Size(width, heigth), true);

                List<System.Drawing.Rectangle> Faces = new List<System.Drawing.Rectangle>();
                List<string> Names = new List<string>();

                Mat mat = new Mat();

                int frameNumber = 0;
                while (frameNumber < framecount)
                {
                    videoReader.Read(mat);
                    Image<Bgr, Byte> image = mat.ToImage<Bgr, Byte>();

                    if (frameNumber % 100 == 0)
                    {
                        int value = (int)(((double)frameNumber / (double)framecount) * 100);
                        Dispatcher.Invoke(() =>
                        {
                            taskItem.SetProgress(value);
                        });
                    }

                    if (frameNumber % 5 == 0)
                    {
                        using (UMat ugray = new UMat())
                        {
                            CvInvoke.CvtColor(image, ugray, ColorConversion.Bgr2Gray);
                            CvInvoke.EqualizeHist(ugray, ugray);
                            Names.Clear();
                            Faces.Clear();
                            Faces.AddRange(HaarCascade.DetectMultiScale(ugray, 1.1, 10, new System.Drawing.Size(20, 20)));
                            int recognizeCount = 0;
                            foreach (var face in Faces)
                            {
                                image.Draw(face, new Bgr(0, 0, 255), 3);
                                if (PeopleData.Face.Count() != 0)
                                {
                                    var result = FaceRecognizer.Predict(ugray.ToImage<Gray, Byte>().Copy(face).Resize(100, 100, Inter.Cubic));
                                    if (result.Label > 0 && result.Distance <= 100)
                                    {
                                        recognizeCount++;
                                        string name = PeopleData.Name[result.Label - 1];
                                        Names.Add(name);
                                        image.Draw(name,
                                            new System.Drawing.Point(face.X, face.Y - 10),
                                            FontFace.HersheyComplex,
                                            0.8,
                                            new Bgr(0, 0, 255));
                                    }
                                    else
                                    {
                                        Names.Add("");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        int i = 0;
                        foreach (var face in Faces)
                        {
                            image.Draw(face, new Bgr(0, 0, 255), 3);
                            if (Names.Count() > 0)
                            {
                                image.Draw(Names[i],
                                            new System.Drawing.Point(face.X, face.Y - 10),
                                            FontFace.HersheyComplex,
                                            0.8,
                                            new Bgr(0, 0, 255));
                                i++;
                            }
                        }
                    }
                    videoWriter.Write(image.Mat);
                    image.Dispose();
                    frameNumber++;
                }
                if (videoWriter.IsOpened)
                {
                    videoWriter.Dispose();
                }
                if (videoReader.IsOpened)
                {
                    videoReader.Dispose();
                }
                Dispatcher.Invoke(() =>
                {
                    selectedVideos[processedVideoIndex] = writePath;
                    bool isActive = ((VideoItem)VideoList.Children[processedVideoIndex]).IsActive();
                    if (isActive && isPlaying)
                    {
                        VideoPlayer.Pause();
                        VideoTimer.Stop();
                        StartStopVideoButton.Content = "➤";
                    }
                    ((VideoItem)VideoList.Children[processedVideoIndex]).Refresh(this, writePath);
                    ((VideoItem)VideoList.Children[processedVideoIndex]).VideoBorder(isActive);
                    //VideoList.Children[processedVideoIndex].
                    processedVideos.Add(selectedVideos[processedVideoIndex]);
                    //GetLoadVideos(selectedVideos.ToArray());
                    lockProcessVideo = false;
                    taskItem.Remove();
                });
            });
            TaskList.Items.Add(taskItem);
        }

        private void RemoveProcessedVideo()
        {
            foreach (var file in processedVideos)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        private void SaveVideo_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog SaveImageDialog = new SaveFileDialog
            {
                Filter = "Видео(*.MP4;*.AVI;)|*.MP4;*.AVI"
            };
            if (SaveImageDialog.ShowDialog() == true)
            {
                string sFilePic = SaveImageDialog.FileName;
                if (sFilePic == "")
                    return;
                if (selectedVideos[currentVideo].Count() > 0 && File.Exists(selectedVideos[currentVideo]))
                {
                    File.Copy(selectedVideos[currentVideo], sFilePic);
                }
            }
        }
        private void VideoTimerTick(object sender, EventArgs e)
        {
            VideoTimeSlider.Value = VideoPlayer.Position.TotalSeconds;
            if (VideoTimeSlider.Value == VideoTimeSlider.Maximum)
                StartStopVideoButton_Click(null, null);
            else
                VideoTimeLabel.Content = String.Format("{0}:{1}/{2}:{3}", VideoPlayer.Position.Minutes, VideoPlayer.Position.Seconds,
                videoTimeSpan.Minutes, videoTimeSpan.Seconds);
        }
        #endregion
    }
}