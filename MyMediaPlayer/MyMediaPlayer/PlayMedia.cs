using System;
using System.Drawing;
using System.Threading;
using System.IO;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;
using System.Runtime.InteropServices;

namespace MyMediaPlayer
{
    public unsafe class PlayMedia
    {
        public enum State
        {
            None,
            Init,
            Run,
            Pause,
            Stop,
            Seek,
        }           

        private Thread thread;
        private SDLAudio sdlAudio;
        private PacketPool videoPool, audioPool;
        private FrameBuffer fPool;

        public State state = State.None;
        private System.Windows.Size frameSize;
        private System.Windows.Controls.Image image;

        private AVFormatContext* pFormatCtx = null;

        private AVCodecContext* pCodecCtxVideo;
        private AVCodec* pCodecVideo;
        private AVFrame* pFrameVideo;
        private SwsContext*[] swsCtxVideo;

        private AVCodecContext* pCodeCtxAudio;
        private AVCodec* pCodecAudio;
        private AVFrame* pFrameAudio;
        private SwrContext* swrCtxAudio;
        
        private int videoIndex = -1;
        private int audioIndex = -1;

        private const int scalerNum = 4;
        private const int rasterNum = 4;
        private int scalerId;
        private int rasterId;
        private int frameGap;
        private bool isEOF;
        private long playTime;
        private System.Windows.Controls.Label startTime;
        private System.Windows.Controls.Slider slider;

        public long entirePlayTime { get; set; }

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out LARGE_INTEGER lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        struct LARGE_INTEGER
        {
            [FieldOffset(0)] public Int64 QuadPart;
            [FieldOffset(0)] public UInt32 LowPart;
            [FieldOffset(4)] public Int32 HighPart;
        }
        [DllImport("user32")]
        public static extern UInt16 GetAsyncKeyState(Int32 vKey);



        [Obsolete]
        public int Init(string fileName, System.Windows.Controls.Image image, System.Windows.Controls.Label startTime, System.Windows.Controls.Slider slider)
        {         
            ffmpeg.avcodec_register_all();

            AVFormatContext* pFormatCtx;
            pFormatCtx = ffmpeg.avformat_alloc_context();
            this.pFormatCtx = pFormatCtx;

            ffmpeg.avformat_open_input(&pFormatCtx, fileName, null, null);
            ffmpeg.avformat_find_stream_info(pFormatCtx, null);

            for (int i = 0; i < pFormatCtx->nb_streams; i++)
            {
                if (pFormatCtx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoIndex = i;
                    Console.WriteLine("video.............." + videoIndex);
                }
                if (pFormatCtx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioIndex = i;
                    Console.WriteLine("audio.............." + audioIndex);
                }
            }

            if (videoIndex == -1)
            {
                Console.WriteLine("Couldn't find a video stream.");
                return -1;
            }
            if (audioIndex == -1)
            {
                Console.WriteLine("Couldn't find a audio stream.");
                return -1;
            }

            if (videoIndex > -1)
            {
                pCodecCtxVideo = pFormatCtx->streams[videoIndex]->codec;
                pCodecVideo = ffmpeg.avcodec_find_decoder(pCodecCtxVideo->codec_id);

                if (pCodecVideo == null)
                {
                    return -1;
                }

                pCodecCtxVideo->codec_id = pCodecVideo->id;
                pCodecCtxVideo->lowres = 0;

                if (pCodecCtxVideo->lowres > pCodecVideo->max_lowres)
                    pCodecCtxVideo->lowres = pCodecVideo->max_lowres;

                pCodecCtxVideo->idct_algo = ffmpeg.FF_IDCT_AUTO;
                pCodecCtxVideo->error_concealment = 3;

                if (ffmpeg.avcodec_open2(pCodecCtxVideo, pCodecVideo, null) < 0)
                {
                    return -1;
                }
                Console.WriteLine("Find a video stream. channel = " + videoIndex);

                frameSize = new System.Windows.Size(pCodecCtxVideo->width, pCodecCtxVideo->height);
                double frameRate = ffmpeg.av_q2d(pFormatCtx->streams[videoIndex]->r_frame_rate);
                frameGap = (int)(1000 / frameRate);

                pFrameVideo = ffmpeg.av_frame_alloc();
                swsCtxVideo = new SwsContext*[4];
                for (int i = 0; i < scalerNum; i++)
                {
                    swsCtxVideo[i] = ffmpeg.sws_getContext(
                        (int)frameSize.Width,
                        (int)frameSize.Height,
                        pCodecCtxVideo->pix_fmt,
                        (int)frameSize.Width,
                        (int)frameSize.Height,
                        AVPixelFormat.AV_PIX_FMT_BGR24,
                        ffmpeg.SWS_FAST_BILINEAR, null, null, null);
                }                        
            }
            if (audioIndex > -1)
            {
                pCodecAudio = ffmpeg.avcodec_find_decoder(pFormatCtx->streams[audioIndex]->codecpar->codec_id);
                pCodeCtxAudio = ffmpeg.avcodec_alloc_context3(pCodecAudio);
                ffmpeg.avcodec_parameters_to_context(pCodeCtxAudio, pFormatCtx->streams[audioIndex]->codecpar);

                if (pCodecAudio == null)
                {
                    return -1;
                }
                if (ffmpeg.avcodec_open2(pCodeCtxAudio, pCodecAudio, null) < 0)
                {
                    return -1;
                }
                Console.WriteLine("Find a audio stream. channel = " + audioIndex);

                sdlAudio = new SDLAudio();
                sdlAudio.SDL_Init(pCodeCtxAudio);

                pFrameAudio = ffmpeg.av_frame_alloc();
                swrCtxAudio = ffmpeg.swr_alloc();

                ffmpeg.av_opt_set_channel_layout(swrCtxAudio, "in_channel_layout", (long)pCodeCtxAudio->channel_layout, 0);
                ffmpeg.av_opt_set_channel_layout(swrCtxAudio, "out_channel_layout", (long)pCodeCtxAudio->channel_layout, 0);
                ffmpeg.av_opt_set_int(swrCtxAudio, "in_sample_rate", pCodeCtxAudio->sample_rate, 0);
                ffmpeg.av_opt_set_int(swrCtxAudio, "out_sample_rate", pCodeCtxAudio->sample_rate, 0);
                ffmpeg.av_opt_set_sample_fmt(swrCtxAudio, "in_sample_fmt", pCodeCtxAudio->sample_fmt, 0);
                ffmpeg.av_opt_set_sample_fmt(swrCtxAudio, "out_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_FLT, 0);
                ffmpeg.swr_init(swrCtxAudio);       
            }

            this.image = image;
            entirePlayTime = pFormatCtx->duration;
            this.startTime = startTime;
            this.slider = slider;

            scalerId = 0;
            rasterId = 0;

            videoPool = new PacketPool();
            audioPool = new PacketPool();
            fPool = new FrameBuffer(frameSize);

            isEOF = false;           
            state = State.Init;

            return 0;
        }

        private unsafe int RunMedia()
        {
            AVPacket* packet;
            packet = (AVPacket*)ffmpeg.av_malloc((ulong)(sizeof(AVPacket)));

            Thread videoDecodeTask = new Thread(new ThreadStart(VideoDecodeTask));
            Thread audioDecodeTask = new Thread(new ThreadStart(AudioDecodeTask));

            videoDecodeTask.Start();
            audioDecodeTask.Start();       

            try
            {
                while (ffmpeg.av_read_frame(pFormatCtx, packet) == 0)
                {                  
                    if (state == State.Seek)
                    {
                        while (state == State.Seek)
                        {                          
                            Thread.Sleep(100);
                        }
                    }
                    while (videoPool.isFull() || audioPool.isFull())
                    {
                        if (state == State.Stop) break;
                        Thread.Sleep(20);
                    }
                    if (state == State.Stop) break;
                    if (packet->stream_index == videoIndex)
                    {
                        videoPool.putPacket(*packet);                                             
                    }
                    if (packet->stream_index == audioIndex)
                    {
                        audioPool.putPacket(*packet);
                    }                 
                }              
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            isEOF = true;

            videoDecodeTask.Join();
            audioDecodeTask.Join();

            state = State.Stop;
            return 0;
        }

        private unsafe void VideoDecodeTask()
        {
            AVPacket packet;
            int ret;

            Thread[] videoScaleTask = new Thread[scalerNum];
            for (int i = 0; i < scalerNum; i++)
            {
                videoScaleTask[i] = new Thread(new ThreadStart(VideoScaleTask));
                videoScaleTask[i].Start();
            }
            Thread[] videoRasterTask = new Thread[rasterNum];
            for (int i = 0; i < rasterNum; i++)
            {
                videoRasterTask[i] = new Thread(new ThreadStart(VideoRasterTask));
                videoRasterTask[i].Start();
            }
            Thread videoDrawTask = new Thread(new ThreadStart(VideoDrawTask));
            videoDrawTask.Start();        

            try
            {
                while (true)
                {
                    if (state == State.Stop)
                    {
                        break;
                    }
                    if (state == State.Seek)
                    {
                        while (state == State.Seek)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    if (videoPool.isEmpty())
                    {
                        if (isEOF == true) break;
                        Thread.Sleep(20);
                        continue;
                    }

                    packet = videoPool.getPacket();
                    ret = ffmpeg.avcodec_send_packet(pCodecCtxVideo, &packet);
                    if (ret != 0) { continue; }
                    do
                    {
                        ret = ffmpeg.avcodec_receive_frame(pCodecCtxVideo, pFrameVideo);
                        if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;

                        while (true)
                        {
                            if (state == State.Stop) break;
                            if (fPool.status[fPool.fPut] == FrameBuffer.eFrameStatus.F_EMPTY || fPool.status[fPool.fPut] == FrameBuffer.eFrameStatus.F_DRAW)
                            {
                                break;
                            }
                            Thread.Sleep(20);
                        }

                        if (state == State.Stop)
                        {
                            ffmpeg.av_packet_unref(&packet);
                            ffmpeg.av_frame_unref(pFrameVideo);
                            break;
                        }

                        AVFrame* tFrame = ffmpeg.av_frame_clone(pFrameVideo);
                        fPool.vFrame[fPool.fPut] = *tFrame;
                        fPool.status[fPool.fPut] = FrameBuffer.eFrameStatus.F_FRAME;
                        fPool.pts[fPool.fPut] = pFrameVideo->best_effort_timestamp;

                        if (++fPool.fPut == fPool.fSize) fPool.fPut = 0;

                    } while (true);
                    ffmpeg.av_packet_unref(&packet);
                }           
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            for (int i = 0; i < scalerNum; i++)
            {
                videoScaleTask[i].Join();
            }
            for (int i = 0; i < rasterNum; i++)
            {
                videoRasterTask[i].Join();
            }
            videoDrawTask.Join();

            ResetFramePool();
            videoPool.clear();
            ffmpeg.av_frame_unref(pFrameVideo);        
        }

        private unsafe void VideoScaleTask()
        {
            AVFrame vFrame;
            int scalerNum = scalerId;
            int scalePos;
            lock (this) { scalerId++; }

            while (true)
            {
                if (state == State.Stop)
                {
                    break;
                }

                if (state == State.Seek)
                {
                    while (state == State.Seek)
                    {                   
                        Thread.Sleep(100);
                    }
                }

                lock (this)
                {
                    scalePos = -1;
                    for (int i = fPool.fPut, count = 0; count < fPool.fSize; i = (i + 1) % fPool.fSize, count++)
                    {
                        if (fPool.status[i] == FrameBuffer.eFrameStatus.F_FRAME)
                        {
                            fPool.status[i] = FrameBuffer.eFrameStatus.F_SCALING;
                            scalePos = i;
                            break;
                        }
                    }
                }

                if (scalePos == -1)
                {
                    if (isEOF == true) break;
                    Thread.Sleep(20);
                    continue;
                }

                vFrame = fPool.vFrame[scalePos];
                ffmpeg.sws_scale(swsCtxVideo[scalerNum],
                            vFrame.data, vFrame.linesize, 0, vFrame.height, fPool._dstData[scalePos], fPool._dstLinesize[scalePos]);

                var data = new byte_ptrArray8();
                data.UpdateFrom(fPool._dstData[scalePos]);
                var linesize = new int_array8();
                linesize.UpdateFrom(fPool._dstLinesize[scalePos]);

                AVFrame frame_converted = new AVFrame
                {
                    data = data,
                    linesize = linesize,
                    width = (int)frameSize.Width,
                    height = (int)frameSize.Height
                };

                fPool.RGBFrame[scalePos] = frame_converted;                   

                ffmpeg.av_frame_unref(&vFrame);
                fPool.status[scalePos] = FrameBuffer.eFrameStatus.F_SCALE;
            }
        }

        private unsafe void VideoRasterTask()
        {
            int rasterNum = rasterId;
            int rastPos;
            lock (this) { rasterId++; }

            while (true)
            {
                if (state == State.Stop)
                {
                    break;
                }

                if (state == State.Seek)
                {
                    while (state == State.Seek)
                    {
                        Thread.Sleep(100);
                    }
                }

                lock (this)
                {
                    rastPos = -1;
                    for (int i = fPool.fPut, count = 0; count < fPool.fSize; i = (i + 1) % fPool.fSize, count++)
                    {
                        if (fPool.status[i] == FrameBuffer.eFrameStatus.F_SCALE)
                        {
                            fPool.status[i] = FrameBuffer.eFrameStatus.F_RASTERING;
                            rastPos = i;
                            break;
                        }
                    }
                }

                if (rastPos == -1)
                {
                    if (isEOF == true) break;
                    Thread.Sleep(20);
                    continue;
                }

                fPool.bitmap[rastPos] = new Bitmap(
                            fPool.RGBFrame[rastPos].width,
                            fPool.RGBFrame[rastPos].height,
                            fPool.RGBFrame[rastPos].linesize[0],
                            System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                            (IntPtr)fPool.RGBFrame[rastPos].data[0]);
                fPool.status[rastPos] = FrameBuffer.eFrameStatus.F_RASTER;
            }
        }

        private unsafe void VideoDrawTask()
        {
            int drawpos = 0;
            long elapse=0;
            long delay = 0;
            LARGE_INTEGER lastDrawTick, nowtick;

            lastDrawTick.HighPart = 0;
            lastDrawTick.LowPart = 0;
            lastDrawTick.QuadPart = 0;


            while (true)
            {
                if (state == State.Stop)
                {
                    break;
                }
                if (state == State.Pause || state == State.Seek)
                {
                    while (state == State.Pause || state == State.Seek)
                    {
                        Thread.Sleep(100);
                    }
                }

                if (fPool.status[drawpos] != FrameBuffer.eFrameStatus.F_RASTER)
                {
                    if (isEOF == true) break;
                    Thread.Sleep(20);
                    continue;
                }

                QueryPerformanceCounter(out nowtick);


                BitmapToImageSource(fPool.bitmap[drawpos]);

                fPool.status[drawpos] = FrameBuffer.eFrameStatus.F_DRAW;

                elapse = (long)(nowtick.QuadPart - lastDrawTick.QuadPart);
                // Console.WriteLine(elapse);

                if (elapse > ffmpeg.AV_TIME_BASE || elapse < 0)
                {
                    elapse = 0;
                }

                delay = frameGap * 100 - elapse;


                if (delay > 0)
                {
                    Thread.Sleep((int)delay / 100);
                    Console.WriteLine((int)delay / 100);

                }
                QueryPerformanceCounter(out lastDrawTick);

                if (++drawpos == fPool.fSize) drawpos = 0;
            }
        }

        private unsafe void AudioDecodeTask()
        {
            AVPacket packet;
            int ret;
            byte* out_audio_buffer = (byte*)ffmpeg.av_malloc((192000 * 3) / 2);
            try
            {
                while (true)
                {
                    if (state == State.Stop)
                    {
                        break;
                    }
                    if (audioPool.isEmpty())
                    {
                        if (isEOF == true) break;
                        Thread.Sleep(20);
                        continue;
                    }
                    if (state == State.Seek)
                    {
                        while (state == State.Seek)
                        {
                            Thread.Sleep(100);
                        }
                    }

                    packet = audioPool.getPacket();
                    ret = ffmpeg.avcodec_send_packet(pCodeCtxAudio, &packet);
                    if (ret != 0) { continue; }
                    do
                    {
                        ret = ffmpeg.avcodec_receive_frame(pCodeCtxAudio, pFrameAudio);
                        if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;

                        ffmpeg.swr_convert(swrCtxAudio, &out_audio_buffer, pFrameAudio->nb_samples, (byte**)&pFrameAudio->data, pFrameAudio->nb_samples);
                        int out_buffer_size_audio = ffmpeg.av_samples_get_buffer_size(null, pCodeCtxAudio->channels, pFrameAudio->nb_samples, AVSampleFormat.AV_SAMPLE_FMT_FLT, 1);
                        var data = out_audio_buffer;
                        playTime = pFrameAudio->best_effort_timestamp/pFrameAudio->sample_rate;
                        slider.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            slider.Value = playTime;
                        }));
                        
                        startTime.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            startTime.Content = (playTime / 3600).ToString() + ":" + (playTime % 3600 / 60).ToString() + ":" + (playTime % 3600 % 60).ToString();
                        }));

                        sdlAudio.PlayAudio((IntPtr)data, out_buffer_size_audio);                     

                    } while (true);
                }
                ffmpeg.av_packet_unref(&packet);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            audioPool.clear();
            ffmpeg.av_frame_unref(pFrameAudio);
        }

        private void ResetFramePool()
        {
            for (int i = 0; i < fPool.fSize; i++)
            {
                if (fPool.status[i] == FrameBuffer.eFrameStatus.F_FRAME)
                {
                    AVFrame frame = fPool.vFrame[i];
                    ffmpeg.av_frame_unref(&frame);
                }
                fPool.status[i] = FrameBuffer.eFrameStatus.F_EMPTY;
            }
        }

        public void mediaFlush()
        {
            sdlAudio.SDL_Pause();
            if (audioIndex >= 0)
            {
                sdlAudio.Clear();
            }
            sdlAudio.SDL_Play();

            videoPool.clear();
            audioPool.clear();

            if (pCodecCtxVideo != null) ffmpeg.avcodec_flush_buffers(pCodecCtxVideo);
            if (pCodeCtxAudio != null) ffmpeg.avcodec_flush_buffers(pCodeCtxAudio);

            ResetFramePool();
        }

        public void Seek(long offset)
        {                 
            ffmpeg.av_seek_frame(pFormatCtx, -1, pFormatCtx->start_time + offset * ffmpeg.AV_TIME_BASE, ffmpeg.AVSEEK_FLAG_BACKWARD);
            state = State.Run;
        }

        public void Start()
        {
            thread = new Thread(() =>
            {
                try
                {
                    RunMedia();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
            thread.IsBackground = true;
            thread.Start();

            state = State.Run;
        }

        public void GoOn()
        {
            state = State.Run;
            sdlAudio.SDL_Play();
        }

        public void Pause()
        {
            state = State.Pause;
            sdlAudio.SDL_Pause();
        }

        public void Stop()
        {
            state = State.Stop;
        }

        private void BitmapToImageSource(Bitmap bitmap)
        {

                image.Dispatcher.BeginInvoke((Action)(() =>
                {
                        image.Visibility = System.Windows.Visibility.Hidden;
                        using (MemoryStream memory = new MemoryStream())
                        {
                            if (thread.IsAlive)
                            {
                                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                                memory.Position = 0;

                                BitmapImage bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.StreamSource = memory;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze();
                                image.Source = bitmapImage;
                                image.Visibility = System.Windows.Visibility.Visible;
                                memory.Dispose();
                            }
                            bitmap.Dispose();
                        }
                }));
        }

        private class FrameBuffer
        {
            public enum eFrameStatus
            {
                F_EMPTY, F_FRAME, F_SCALING, F_SCALE,
                F_RASTERING, F_RASTER, F_DRAW
            };

            public int fSize = 6;
            public int fPut;
            public int convertedFrameBufferSize;
            public AVFrame[] vFrame;
            public AVFrame[] RGBFrame;
            public Bitmap[] bitmap;
            public eFrameStatus[] status;
            public byte_ptrArray4[] _dstData;
            public int_array4[] _dstLinesize;
            public IntPtr[] _convertedFrameBufferPtr;
            public long[] pts;

            public FrameBuffer(System.Windows.Size frameSize)
            {
                vFrame = new AVFrame[fSize];
                status = new eFrameStatus[fSize];
                RGBFrame = new AVFrame[fSize];
                bitmap = new Bitmap[fSize];
                _dstData = new byte_ptrArray4[fSize];
                _dstLinesize = new int_array4[fSize];
                _convertedFrameBufferPtr = new IntPtr[fSize];
                pts = new long[fSize];

                convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24,
                    (int)frameSize.Width, (int)frameSize.Height, 1);
                fPut = 0;
            
                for (int i = 0; i < fSize; i++)
                {
                    status[i] = eFrameStatus.F_EMPTY;
                    _dstData[i] = new byte_ptrArray4();
                    _dstLinesize[i] = new int_array4();
                    _convertedFrameBufferPtr[i] = (IntPtr)ffmpeg.av_malloc((ulong)convertedFrameBufferSize);

                    ffmpeg.av_image_fill_arrays(
                        ref _dstData[i],
                        ref _dstLinesize[i],
                        (byte*)_convertedFrameBufferPtr[i],
                        AVPixelFormat.AV_PIX_FMT_BGR24,
                        (int)frameSize.Width,
                        (int)frameSize.Height, 1);
                }
            }
        };

        private class PacketPool
        {
            private int pSize;
            private AVPacket[] packetPool;
            private int put;
            private int get;

            public PacketPool()
            {
                pSize = 60;
                packetPool = new AVPacket[pSize];
                put = 0;
                get = 0;
            }

            public void putPacket(AVPacket packet)
            {
                packetPool[put++] = packet;
                put = put % 60;
            }

            public AVPacket getPacket()
            {
                AVPacket packet = packetPool[get++];
                get = get % 60;
                return packet;
            }

            public bool isEmpty()
            {
                return put == get;
            }

            public bool isFull()
            {
                return (put + 1) % pSize == get;
            }

            public void clear()
            {
                for (int i = get; i != put; i = (i + 1) % pSize)
                {
                    AVPacket packet = packetPool[i];
                    ffmpeg.av_packet_unref(&packet);
                }

                get = 0;
                put = 0;
            }
        }
    }
}