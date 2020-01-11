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

        PlayMedia playMedia, playDMedia;

        public MainWindow()
        {
            InitializeComponent();

            BinariesHelper.RegisterFFmpegBinaries();
            playMedia = new PlayMedia();
            playDMedia = new PlayMedia();
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
                }));
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Filter = "Video Files | *.mp4; *.wmv; *.avi";
                if (dlg.ShowDialog() == true)
                {
                    videoFile = dlg.FileName;
                }
                playMedia.Init(videoFile, image);
                playDMedia.Init(videoFile, Dimage);
                fullTime = playMedia.entirePlayTime / AV_TIME_BASE;
                finishTime = fullTime;
                finish_Time.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new UpdateUI(TimeCheck));
            }

            if (playMedia.state == PlayMedia.State.Init)
            {
                playMedia.Start();
                playDMedia.Start();

            }
            else if (playMedia.state == PlayMedia.State.Run)
            {
                playMedia.Pause();
                playDMedia.Pause();
            }
            else if (playMedia.state == PlayMedia.State.Pause)
            {
                playMedia.GoOn();
                playDMedia.GoOn();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            playMedia.Stop();
            playDMedia.Stop();
        }

        private void ContentControl_MouseDoubleClick_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Dimage.Visibility == Visibility.Hidden)
            {
                Dimage.Visibility = Visibility.Visible;
                image.Margin = new Thickness(0, 70, 0, 0);
                image.Width = 387;
            }
            else if (image.Visibility == Visibility.Visible)
            {
                Dimage.Visibility = Visibility.Hidden;
                image.Margin = new Thickness(0, 70, 9.6, 0);
                image.Width = 774;

            }
        }

        private void ContentControl_MouseDoubleClick_2(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (image.Visibility == Visibility.Hidden)
            {
                image.Visibility = Visibility.Visible;
                Dimage.Margin = new Thickness(396.6, 70, 0, 0);
                Dimage.Width = 387;
            }
            else if (Dimage.Visibility == Visibility.Visible)
            {
                image.Visibility = Visibility.Hidden;
                Dimage.Margin = new Thickness(0, 70, 0, 0);
                Dimage.Width = 774;
            }
        }
    }
}