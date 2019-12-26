using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using MyMediaPlayer.FFmpeg;
using System.Windows.Controls;

namespace MyMediaPlayer
{
    public partial class MainWindow : Window
    {
        delegate void UpdateUI();
        Thread videoThread;
        Dispatcher dispatcher = Application.Current.Dispatcher;
        private static AutoResetEvent _wait;
        private bool stopThread = false;
        string videoFile;
        long fullTime;
        long finishTime, startTime;
        volatile bool isPause=false;
        const int AV_TIME_BASE =1000000;
        double speed = 1.0;
        public MainWindow()
        {
            InitializeComponent();

            BinariesHelper.RegisterFFmpegBinaries();
            videoThread = new Thread(new ThreadStart(PlayingMedia));
        }

        ConcurrentQueue<AVFrame> vq = new ConcurrentQueue<AVFrame>();
        ConcurrentQueue<AVFrame> aq = new ConcurrentQueue<AVFrame>();
        VideoFrameConverter vfc;
        //AudioFrameConverter afc;

        private unsafe void PlayingMedia()
        {
            //string url = @"D:\Movie\[2001] 해리 포터와 마법사의 돌.1080p.BRRip.x264.YIFY.mp4";

            using (var sd = new StreamDecoder(videoFile))
            {
                Task tVideoTask = Task.Factory.StartNew(() => VideoTask(sd.vcodecContext));
                _wait = new AutoResetEvent(false);
                var sourceSize = sd.FrameSize;
                var sourcePixelFormat = sd.PixelFormat;
                var destinationSize = sourceSize;
                var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                fullTime = sd.FullTime/AV_TIME_BASE;
                finishTime = fullTime;
                finish_Time.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new UpdateUI(TimeCheck));
                vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);
                
                while (sd.TryDecodeNextFrame(out var frame, out int type) && !stopThread)
                {                   
                    if (type == 0) vq.Enqueue(frame);
                    if (type == 1) aq.Enqueue(frame);
                    System.Threading.Thread.Sleep(33);
               
                    
                }
            }
        }

        
        private void TimeCheck(){

            finish_Time.Content = (finishTime/3600).ToString()+":"+ (finishTime%3600/60).ToString()+":"+ (finishTime%3600%60).ToString();
        }

        private unsafe void VideoTask(AVCodecContext* vcodecContext)
        {
            while (true)
            {
                if (vq.TryDequeue(out var frame))
                {
                    var convertedFrame = vfc.Convert(frame);

                    Bitmap bitmap = new Bitmap(
                        convertedFrame.width,
                        convertedFrame.height,
                        convertedFrame.linesize[0],
                        System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                        (IntPtr)convertedFrame.data[0]);
                    BitmapToImageSource(bitmap);
                }
            }
        }

        [Obsolete]
        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (videoThread.ThreadState == System.Threading.ThreadState.Unstarted)
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    image.Visibility = Visibility.Visible;
                    screen.Visibility = Visibility.Hidden;
                }));
                videoThread.Start();
            }
            else if(videoThread.ThreadState==System.Threading.ThreadState.Suspended)
            {
                isPause = false;
                videoThread.Resume();
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

        void BitmapToImageSource(Bitmap bitmap)
        {
            dispatcher.BeginInvoke((Action)(() =>
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    if (videoThread.IsAlive)
                    {
                        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                        memory.Position = 0;

                        BitmapImage bitmapimage = new BitmapImage();
                        bitmapimage.BeginInit();
                        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapimage.StreamSource = memory;
                        bitmapimage.EndInit();

                        image.Source = bitmapimage;
                    }
                }
            }));

        }

        private unsafe void AudioTask(AVCodecContext* acodecContext)
        {
            while (true)
            {
                if (aq.TryDequeue(out var frame))
                {
                    
                }
            }
        }

        [Obsolete]
        private void Pause_Button_Click(object sender, RoutedEventArgs e)
        {
            isPause = true;
            videoThread.Suspend();
        }

        private void Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            if (videoThread.ThreadState == System.Threading.ThreadState.Running)
            {
                
            }
        }

        private void Speed_Button_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            Speed_Button.Content = menu.Header.ToString();
            speed = double.Parse(menu.Header.ToString());
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (videoThread.IsAlive)
            {
                stopThread = true;
                videoThread.Abort();
            }
        }
    }
}