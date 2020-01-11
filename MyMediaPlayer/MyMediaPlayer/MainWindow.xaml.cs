using System;
using System.Windows;
using System.Windows.Threading;
using MyMediaPlayer.FFmpeg;
using System.Windows.Controls;

namespace MyMediaPlayer
{
    public partial class MainWindow : Window
    {
        delegate void UpdateUI();
        string videoFile;
        long fullTime;
        long finishTime, startTime;
        const int AV_TIME_BASE =1000000;
        double speed = 1.0;

        PlayMedia playMedia;

        public MainWindow()
        {
            InitializeComponent();

            BinariesHelper.RegisterFFmpegBinaries();
            playMedia = new PlayMedia();
        }
        
        private void TimeCheck(){

            finish_Time.Content = (finishTime/3600).ToString()+":"+ (finishTime%3600/60).ToString()+":"+ (finishTime%3600%60).ToString();
        }

        [Obsolete]
        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (playMedia.state == PlayMedia.State.None)
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    image.Visibility = Visibility.Visible;
                    screen.Visibility = Visibility.Hidden;
                }));
                playMedia.Init(videoFile, image);
                fullTime = playMedia.entirePlayTime / AV_TIME_BASE;
                finishTime = fullTime;
                finish_Time.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new UpdateUI(TimeCheck));               
            }

            if (playMedia.state == PlayMedia.State.Init)
            {
                playMedia.Start();

            }

            if (playMedia.state == PlayMedia.State.Pause)
            {
                playMedia.GoOn();

            }
        }

        private void FileOpenClick(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Video Files | *.mp4; *.wmv; *.avi";
            if (dlg.ShowDialog() == true)
            {
                videoFile = dlg.FileName;
            }
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                txt.Text = videoFile;
            }));
        }

        [Obsolete]
        private void Pause_Button_Click(object sender, RoutedEventArgs e)
        {
            playMedia.Pause();
        }

        private void Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            playMedia.Stop();
        }

        private void Speed_Button_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            Speed_Button.Content = menu.Header.ToString();
            speed = double.Parse(menu.Header.ToString());
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            playMedia.Stop();
        }
    }
}