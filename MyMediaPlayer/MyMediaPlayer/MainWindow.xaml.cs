using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MyMediaPlayer.FFmpeg;

namespace MyMediaPlayer
{
    public partial class MainWindow : Window
    {
        delegate void UpdateUI();
        string videoFile;
        long fullTime;
        long finishTime;
        const int AV_TIME_BASE =1000000;

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
                    Dimage.Visibility = Visibility.Visible;
                }));
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Filter = "Video Files | *.mp4; *.wmv; *.avi";
                if (dlg.ShowDialog() == true)
                {
                    videoFile = dlg.FileName;
                }
                playMedia.Init(videoFile, image, Dimage, start_Time, slider,slider2);                
                fullTime = playMedia.entirePlayTime / AV_TIME_BASE;
                finishTime = fullTime;
                finish_Time.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new UpdateUI(TimeCheck));
                slider.Dispatcher.BeginInvoke((Action)(() =>
                {
                    slider.Minimum = 0;
                    slider.Maximum = fullTime;
                }));
            }

            if (playMedia.state == PlayMedia.State.Init)
            {
                lock (Dispatcher)
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                    {
                        Play_Button.Content = "||";
                    }));
                }
                playMedia.Start();
                
            }
            else if (playMedia.state == PlayMedia.State.Run)
            {
                lock (Dispatcher)
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                    {
                        Play_Button.Content = "▶";
                    }));
                }
                playMedia.Pause();            
            }
            else if (playMedia.state == PlayMedia.State.Pause)
            {
                lock (Dispatcher)
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                    {
                        Play_Button.Content = "||";
                    }));
                }
                playMedia.GoOn();             
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            playMedia.Stop();         
        }


        private void slider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Console.WriteLine("slider Preview Mouse Left Button Up");
            long pos = (long)slider.Value;
            Console.WriteLine((pos / 3600).ToString() + ":" + (pos % 3600 / 60).ToString() + ":" + (pos % 3600 % 60).ToString());

            if (playMedia.state == PlayMedia.State.Seek)
            {
                playMedia.Seek(pos);
            }
        }

        private void slider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Console.WriteLine("slider Preview Mouse Left Button Down");
            if (playMedia.state == PlayMedia.State.Run)
            {
                playMedia.state = PlayMedia.State.Seek;
                playMedia.MediaFlush();
            }

        }

        private void slider_PreviewMouseLeftButtonUp2(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            playMedia.overlayRate = slider2.Value / slider2.Maximum;
        }
    }
}