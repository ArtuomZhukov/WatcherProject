using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace WatcherProject
{
    public partial class TaskItem : UserControl
    {
        Thread Task;
        public TaskItem(string taskName, bool canCancel, ThreadStart task)
        {
            InitializeComponent();

            TaskName.Content = taskName;
            TaskName.ToolTip = taskName;
            Task = new Thread(task);
            Task.SetApartmentState(ApartmentState.STA);
            Task.Start();
            CancelTask.Visibility = canCancel ? Visibility.Visible : Visibility.Hidden;

        }

        public void SetProgress(int value)
        {
            Progress.Value = value;
        }

        public void Remove()
        {
            ((ListBox)Parent).Items.Remove(this);
        }

        private void CancelTask_Click(object sender, RoutedEventArgs e)
        {
            //тестить
            Task.Abort();
            Remove();
        }
    }
}
