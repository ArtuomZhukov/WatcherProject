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

namespace WatcherProject
{
    class Watcher
    {
        #region Методы конвертации
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);
        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); 

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(ptr);
                return bs;
            }
        }

        //Пока не юзал
        public static Mat ToMat(BitmapSource source)
        {
            if (source.Format == PixelFormats.Bgra32)
            {
                Mat result = new Mat();
                result.Create(source.PixelHeight, source.PixelWidth, DepthType.Cv8U, 4);
                source.CopyPixels(Int32Rect.Empty, result.DataPointer, result.Step * result.Rows, result.Step);
                return result;
            }
            else if (source.Format == PixelFormats.Bgr24)
            {
                Mat result = new Mat();
                result.Create(source.PixelHeight, source.PixelWidth, DepthType.Cv8U, 3);
                source.CopyPixels(Int32Rect.Empty, result.DataPointer, result.Step * result.Rows, result.Step);
                return result;
            }
            else
            {
                throw new Exception(String.Format("Convertion from BitmapSource of format {0} is not supported.", source.Format));
            }
        }
        #endregion

        #region JSON Методы
        public static void WriteJson(object data, string jsonPath)
        {
            string json = JsonConvert.SerializeObject(data);
            File.WriteAllText(Environment.CurrentDirectory + jsonPath, json);
        }

        public static PeopleData ReadPeopleDataJson()
        {
            if (File.Exists(Environment.CurrentDirectory + ConfigurationManager.AppSettings["JsonData"]))
                return JsonConvert.DeserializeObject<PeopleData>(File.ReadAllText(Environment.CurrentDirectory + ConfigurationManager.AppSettings["JsonData"]));
            return new PeopleData();
        }
        #endregion

        public static string SetName(string text)
        {
            ModalGetNameWindow getName = new ModalGetNameWindow(text);
            if (getName.ShowDialog() == true)
            {
                return getName.ItemName;
            }
            return null;
        }

        public static List<string> GetCameraNames()
        {
            List<string> Names = new List<string>();
            foreach (var item in DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice))
            {
                Names.Add(item.Name);
            }
            return Names;
        }

        public static ObservableCollection<string> ReadLogsJson()
        {
            if (File.Exists(Environment.CurrentDirectory + ConfigurationManager.AppSettings["LogData"]))
                return JsonConvert.DeserializeObject<ObservableCollection<string>>(File.ReadAllText(Environment.CurrentDirectory + ConfigurationManager.AppSettings["LogData"]));
            return new ObservableCollection<string>();
        }



    }
}
