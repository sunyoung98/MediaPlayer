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
using System.Collections.Generic;
using SDL2;
using System.Runtime.InteropServices;

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

        ConcurrentQueue<AVFrame> vq = new ConcurrentQueue<AVFrame>();
        ConcurrentQueue<AVFrame> aq = new ConcurrentQueue<AVFrame>();
        VideoFrameConverter vfc;
        AudioFrameConverter afc;

        SDLAudio sdlAudio;
        JT1078CodecForMp4 jt1078CodecForMp4;

        public unsafe class SDLAudio
        {
            class aa
            {
                public byte[] pcm;
                public int len;
            }

            private List<aa> data = new List<aa>();

            SDL.SDL_AudioCallback Callback;
            public void PlayAudio(IntPtr pcm, int len)
            {
                lock (this)
                {
                    byte[] bts = new byte[len];
                    Marshal.Copy(pcm, bts, 0, len);
                    data.Add(new aa
                    {
                        len = len,
                        pcm = bts
                    });
                }
            }
            void SDL_AudioCallback(IntPtr userdata, IntPtr stream, int len)
            {
                if (data.Count == 0)
                {
                    for (int i = 0; i < len; i++)
                    {
                        ((byte*)stream)[i] = 0;
                    }
                    return;
                }
                for (int i = 0; i < len; i++)
                {
                    if (data[0].len > i)
                    {
                        ((byte*)stream)[i] = data[0].pcm[i];
                    }
                    else
                        ((byte*)stream)[i] = 0;
                }
                data.RemoveAt(0);



            }
            public int SDL_Init()
            {
                Callback = SDL_AudioCallback;

                SDL.SDL_AudioSpec wanted_spec = new SDL.SDL_AudioSpec();
                wanted_spec.freq = 22050;
                wanted_spec.format = SDL.AUDIO_S16;
                wanted_spec.channels = 2;
                wanted_spec.silence = 0;
                wanted_spec.samples = 512;
                wanted_spec.callback = Callback;


                if (SDL.SDL_OpenAudio(ref wanted_spec, IntPtr.Zero) < 0)
                {
                    Console.WriteLine("can't open audio.");
                    return -1;
                }
                //Play  
                SDL.SDL_PauseAudio(0);
                return 0;
            }
        }

        public unsafe class JT1078CodecForMp4
        {
            public bool IsRun { get; protected set; }
            private Thread threadAudio;
            private bool exit_thread = false;
            private bool pause_thread = false;
            private int audioindex = -1;

            public unsafe int RunAudio(string fileName, SDLAudio sdlAudio)
            {
                IsRun = true;
                exit_thread = false;
                pause_thread = false;
                threadAudio = Thread.CurrentThread;
                int error, frame_count = 0;
                int got_frame, ret;
                AVFormatContext* ofmt_ctx = null;
                SwsContext* pSwsCtx = null;
                IntPtr convertedFrameBufferPtr = IntPtr.Zero;
                try
                {
                    ffmpeg.avcodec_register_all();
                    ofmt_ctx = ffmpeg.avformat_alloc_context();

                    error = ffmpeg.avformat_open_input(&ofmt_ctx, fileName, null, null);
                    if (error != 0)
                    {

                    }

                    for (int i = 0; i < ofmt_ctx->nb_streams; i++)
                    {
                        if (ofmt_ctx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                        {
                            audioindex = i;
                            Console.WriteLine("audio.............." + audioindex);
                        }
                    }

                    if (audioindex == -1)
                    {
                        Console.WriteLine("Couldn't find a  audio stream.");
                        return -1;
                    }

                    if (audioindex > -1)
                    {
                        AVCodecContext* pCodeCtx = ofmt_ctx->streams[audioindex]->codec;
                        AVCodec* pCodec = ffmpeg.avcodec_find_decoder(pCodeCtx->codec_id);
                        if (pCodec == null)
                        {
                            return -1;
                        }
                        if (ffmpeg.avcodec_open2(pCodeCtx, pCodec, null) < 0)
                        {
                            return -1;
                        }
                        Console.WriteLine("Find a  audio stream. channel=" + audioindex);

                        AVPacket* packet = (AVPacket*)ffmpeg.av_malloc((ulong)(sizeof(AVPacket)));
                        AVFrame* frame = ffmpeg.av_frame_alloc();
                        SwrContext* swrCtx = ffmpeg.swr_alloc();

                        AVSampleFormat in_sample_fmt = pCodeCtx->sample_fmt;
                        AVSampleFormat out_sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16;
                        int in_sample_rate = pCodeCtx->sample_rate;
                        int out_sample_rate = 44100;
                        long in_ch_layout = (long)pCodeCtx->channel_layout;
                        int out_ch_layout = ffmpeg.AV_CH_LAYOUT_MONO;

                        ffmpeg.swr_alloc_set_opts(swrCtx, out_ch_layout, out_sample_fmt, out_sample_rate, in_ch_layout, in_sample_fmt, in_sample_rate, 0, null);
                        ffmpeg.swr_init(swrCtx);
                        int out_channel_nb = ffmpeg.av_get_channel_layout_nb_channels((ulong)out_ch_layout);
                        byte* out_buffer = (byte*)ffmpeg.av_malloc(2 * 44100);

                        while (ffmpeg.av_read_frame(ofmt_ctx, packet) >= 0)
                        {
                            if (exit_thread)
                            {
                                break;
                            }
                            if (pause_thread)
                            {
                                while (pause_thread)
                                {
                                    Thread.Sleep(100);
                                }
                            }
                            if (packet->stream_index == audioindex)
                            {
                                ret = ffmpeg.avcodec_decode_audio4(pCodeCtx, frame, &got_frame, packet);
                                if (ret < 0)
                                {
                                    return -1;
                                }
                                if (got_frame > 0)
                                {
                                    frame_count++;
                                    var data_ = frame->data;
                                    ffmpeg.swr_convert(swrCtx, &out_buffer, 2 * 44100, (byte**)&data_, frame->nb_samples);
                                    int out_buffer_size = ffmpeg.av_samples_get_buffer_size(null, out_channel_nb, frame->nb_samples, out_sample_fmt, 1);
                                    var data = out_buffer;
                                    sdlAudio.PlayAudio((IntPtr)data, out_buffer_size);
                                }
                            }
                            ffmpeg.av_free_packet(packet);
                        }

                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    if (&ofmt_ctx != null)
                    {
                        ffmpeg.avformat_close_input(&ofmt_ctx);
                    }

                }
                IsRun = false;
                return 0;
            }

            public void Start(string fileName, SDLAudio sdlAudio)
            {
                threadAudio = new Thread(() =>
                {
                    try
                    {
                        RunAudio(fileName, sdlAudio);
                    }
                    catch (Exception ex)
                    {

                    }
                });
                threadAudio.IsBackground = true;
                threadAudio.Start();
            }
            public void GoOn()
            {
                pause_thread = false;

            }
            public void Pause()
            {
                pause_thread = true;
            }
            public void Stop()
            {
                exit_thread = true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            BinariesHelper.RegisterFFmpegBinaries();
            videoThread = new Thread(new ThreadStart(PlayingMedia));
            sdlAudio = new SDLAudio();
            jt1078CodecForMp4 = new JT1078CodecForMp4();
            sdlAudio.SDL_Init();
        }
        private unsafe void AudioTask(AVCodecContext* acodecContext)
        {
            while (true)
            {
                if (aq.TryDequeue(out var frame))
                {
                    byte* pktbuf;

                    if (afc == null) afc = new AudioFrameConverter(frame);
                    int sampnum = afc.Convert(frame, out pktbuf);


                    int out_channel_nb = ffmpeg.av_get_channel_layout_nb_channels((ulong)ffmpeg.AV_CH_LAYOUT_STEREO);
                    int out_buffer_size_audio = ffmpeg.av_samples_get_buffer_size(null, out_channel_nb, frame.nb_samples, AVSampleFormat.AV_SAMPLE_FMT_S16, 1);
                    sdlAudio.PlayAudio((IntPtr)pktbuf, out_buffer_size_audio);
                }
            }
        }




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
                jt1078CodecForMp4.Start(videoFile, sdlAudio);
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