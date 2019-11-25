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

namespace MyMediaPlayer
{
    public partial class MainWindow : Window
    {
        Thread videoThread;
        Dispatcher dispatcher = Application.Current.Dispatcher;

        private bool stopThread = false;

        public MainWindow()
        {
            InitializeComponent();

            BinariesHelper.RegisterFFmpegBinaries();
            videoThread = new Thread(new ThreadStart(PlayingMedia));
        }

        ConcurrentQueue<AVFrame> vq = new ConcurrentQueue<AVFrame>();
        ConcurrentQueue<AVFrame> aq = new ConcurrentQueue<AVFrame>();
        VideoFrameConverter vfc;
        AudioFrameConverter afc;

        private unsafe void PlayingMedia()
        {
            //string url = @"D:\Movie\[2001] 해리 포터와 마법사의 돌.1080p.BRRip.x264.YIFY.mp4";
            string url = @"C:\mpstudy\fire.avi";

            using (var sd = new StreamDecoder(url))
            {
                Task tVideoTask = Task.Factory.StartNew(() => VideoTask(sd.vcodecContext));

                var sourceSize = sd.FrameSize;
                var sourcePixelFormat = sd.PixelFormat;
                var destinationSize = sourceSize;
                var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;

                vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);
                
                while (sd.TryDecodeNextFrame(out var frame, out int type) && !stopThread)
                {                   
                    if (type == 0) vq.Enqueue(frame);
                    if (type == 1) aq.Enqueue(frame);
                    System.Threading.Thread.Sleep(33);
                }
            }
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

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (videoThread.ThreadState == System.Threading.ThreadState.Unstarted)
            {
                videoThread.Start();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (videoThread.IsAlive)
            {
                stopThread = true;
                videoThread.Join();
            }
        }
    }
}