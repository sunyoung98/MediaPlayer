using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;

namespace MyMediaPlayer
{
    public unsafe class PlayMedia
    {
        private Thread decodeThread;
        System.Windows.Threading.DispatcherTimer dispatcherTimer;
        private SDLAudio sdlAudio;
        private List<Bitmap> bitmaps = new List<Bitmap>();
        public long entirePlayTime { get; set; }

        public enum State
        {
            None,
            Init,
            Run,
            Pause,
            Stop
        }

        public State state = State.None;

        AVFormatContext* ofmt_ctx = null;
        AVPacket* packet;

        AVCodecContext* pCodecCtx_Video;
        AVCodec* pCodec_Video;
        AVFrame* pFrame_Video;
        SwsContext* swsCtx_Video;
        System.Windows.Size frameSize;

        AVCodecContext* pCodeCtx_Audio;
        AVCodec* pCodec_Audio;
        AVFrame* frame_Audio;
        SwrContext* swrCtx_Audio;
        
        private int videoindex = -1;
        private int audioindex = -1;

        byte* out_buffer_audio;
        int out_channel_nb;
        int framegap;

        System.Windows.Controls.Image image;    

        public int Init(string fileName, System.Windows.Controls.Image image)
        {         
            ffmpeg.avcodec_register_all();

            AVFormatContext* ofmt_ctx;
            ofmt_ctx = ffmpeg.avformat_alloc_context();
            this.ofmt_ctx = ofmt_ctx;

            ffmpeg.avformat_open_input(&ofmt_ctx, fileName, null, null);
            ffmpeg.avformat_find_stream_info(ofmt_ctx, null);

            for (int i = 0; i < ofmt_ctx->nb_streams; i++)
            {
                if (ofmt_ctx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoindex = i;
                    Console.WriteLine("video.............." + videoindex);
                }
                if (ofmt_ctx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    audioindex = i;
                    Console.WriteLine("audio.............." + audioindex);
                }
            }

            if (videoindex == -1)
            {
                Console.WriteLine("Couldn't find a video stream.");
                return -1;
            }
            if (audioindex == -1)
            {
                Console.WriteLine("Couldn't find a audio stream.");
                return -1;
            }

            if (videoindex > -1)
            {
                pCodecCtx_Video = ofmt_ctx->streams[videoindex]->codec;
                pCodec_Video = ffmpeg.avcodec_find_decoder(pCodecCtx_Video->codec_id);

                if (pCodec_Video == null)
                {
                    return -1;
                }

                pCodecCtx_Video->codec_id = pCodec_Video->id;
                pCodecCtx_Video->lowres = 0;

                if (pCodecCtx_Video->lowres > pCodec_Video->max_lowres)
                    pCodecCtx_Video->lowres = pCodec_Video->max_lowres;

                pCodecCtx_Video->idct_algo = ffmpeg.FF_IDCT_AUTO;
                pCodecCtx_Video->error_concealment = 3;

                if (ffmpeg.avcodec_open2(pCodecCtx_Video, pCodec_Video, null) < 0)
                {
                    return -1;
                }
                Console.WriteLine("Find a video stream. channel = " + videoindex);

                frameSize = new System.Windows.Size(pCodecCtx_Video->width, pCodecCtx_Video->height);
                double framerate = ffmpeg.av_q2d(ofmt_ctx->streams[videoindex]->r_frame_rate);
                framegap = (int)(1000 / framerate);

                pFrame_Video = ffmpeg.av_frame_alloc();
                swsCtx_Video = ffmpeg.sws_getContext(
                (int)frameSize.Width,
                (int)frameSize.Height,
                pCodecCtx_Video->pix_fmt,
                (int)frameSize.Width,
                (int)frameSize.Height,
                AVPixelFormat.AV_PIX_FMT_BGR24,
                ffmpeg.SWS_FAST_BILINEAR, null, null, null);
            }
            if (audioindex > -1)
            {
                pCodec_Audio = ffmpeg.avcodec_find_decoder(ofmt_ctx->streams[audioindex]->codecpar->codec_id);
                pCodeCtx_Audio = ffmpeg.avcodec_alloc_context3(pCodec_Audio);
                ffmpeg.avcodec_parameters_to_context(pCodeCtx_Audio, ofmt_ctx->streams[audioindex]->codecpar);

                if (pCodec_Audio == null)
                {
                    return -1;
                }
                if (ffmpeg.avcodec_open2(pCodeCtx_Audio, pCodec_Audio, null) < 0)
                {
                    return -1;
                }
                Console.WriteLine("Find a audio stream. channel = " + audioindex);

                sdlAudio = new SDLAudio();
                sdlAudio.SDL_Init(pCodeCtx_Audio);

                frame_Audio = ffmpeg.av_frame_alloc();
                swrCtx_Audio = ffmpeg.swr_alloc();

                ffmpeg.av_opt_set_channel_layout(swrCtx_Audio, "in_channel_layout", (long)pCodeCtx_Audio->channel_layout, 0);
                ffmpeg.av_opt_set_channel_layout(swrCtx_Audio, "out_channel_layout", (long)pCodeCtx_Audio->channel_layout, 0);
                ffmpeg.av_opt_set_int(swrCtx_Audio, "in_sample_rate", pCodeCtx_Audio->sample_rate, 0);
                ffmpeg.av_opt_set_int(swrCtx_Audio, "out_sample_rate", pCodeCtx_Audio->sample_rate, 0);
                ffmpeg.av_opt_set_sample_fmt(swrCtx_Audio, "in_sample_fmt", pCodeCtx_Audio->sample_fmt, 0);
                ffmpeg.av_opt_set_sample_fmt(swrCtx_Audio, "out_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_FLT, 0);
                ffmpeg.swr_init(swrCtx_Audio);

                out_channel_nb = pCodeCtx_Audio->channels;
                out_buffer_audio = (byte*)ffmpeg.av_malloc((192000 * 3) / 2);              
            }

            packet = (AVPacket*)ffmpeg.av_malloc((ulong)(sizeof(AVPacket)));
            this.image = image;
            state = State.Init;
            entirePlayTime = ofmt_ctx->duration;

            return 0;
        }

        public unsafe int RunMedia()
        {
            decodeThread = Thread.CurrentThread;
            int ret;
            byte* out_audio_buffer = out_buffer_audio;

            var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24,
                (int)frameSize.Width, (int)frameSize.Height, 1);
            IntPtr _convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
            byte_ptrArray4 _dstData = new byte_ptrArray4();
            int_array4 _dstLinesize = new int_array4();

            ffmpeg.av_image_fill_arrays(
                ref _dstData,
                ref _dstLinesize,
                (byte*)_convertedFrameBufferPtr,
                AVPixelFormat.AV_PIX_FMT_BGR24,
                (int)frameSize.Width,
                (int)frameSize.Height, 1);

            try
            {
                while (ffmpeg.av_read_frame(ofmt_ctx, packet) == 0)
                {
                    if (state == State.Stop)
                    {
                        break;
                    }
                    if (state == State.Pause)
                    {
                        while (state == State.Pause)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    if (packet->stream_index == videoindex)
                    {
                        ret = ffmpeg.avcodec_send_packet(pCodecCtx_Video, packet);
                        if (ret != 0) { continue; }
                        do
                        {
                            ret = ffmpeg.avcodec_receive_frame(pCodecCtx_Video, pFrame_Video);
                            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;                        
        
                            ffmpeg.sws_scale(swsCtx_Video,
                                pFrame_Video->data, pFrame_Video->linesize, 0, pFrame_Video->height, _dstData, _dstLinesize);                                       

                            var data = new byte_ptrArray8();
                            data.UpdateFrom(_dstData);
                            var linesize = new int_array8();
                            linesize.UpdateFrom(_dstLinesize);

                            AVFrame frame_converted = new AVFrame
                            {
                                data = data,
                                linesize = linesize,
                                width = (int)frameSize.Width,
                                height = (int)frameSize.Height
                            };

                            Bitmap bitmap = new Bitmap(
                                frame_converted.width,
                                frame_converted.height,
                                frame_converted.linesize[0],
                                System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                                (IntPtr)frame_converted.data[0]);

                            BitmapToImageSource(bitmap);
                            Thread.Sleep(33);
                            //lock (this) { bitmaps.Add(bitmap); }
                        } while (true);
                    }
                    if (packet->stream_index == audioindex)
                    {
                        ret = ffmpeg.avcodec_send_packet(pCodeCtx_Audio, packet);
                        if (ret != 0) continue;
                        do
                        {
                            ret = ffmpeg.avcodec_receive_frame(pCodeCtx_Audio, frame_Audio);
                            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;
                            
                            ffmpeg.swr_convert(swrCtx_Audio, &out_audio_buffer, frame_Audio->nb_samples, (byte**)&frame_Audio->data, frame_Audio->nb_samples);
                            int out_buffer_size_audio = ffmpeg.av_samples_get_buffer_size(null, out_channel_nb, frame_Audio->nb_samples, AVSampleFormat.AV_SAMPLE_FMT_FLT, 1);
                            var data = out_audio_buffer;
                            sdlAudio.PlayAudio((IntPtr)data, out_buffer_size_audio);
                        } while (true);
                    }
                    ffmpeg.av_packet_unref(packet);
                }
                ffmpeg.av_frame_unref(pFrame_Video);
                ffmpeg.av_frame_unref(frame_Audio);             
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            state = State.Stop;
            return 0;
        }

        public void Start()
        {
            decodeThread = new Thread(() =>
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
            decodeThread.IsBackground = true;
            decodeThread.Start();         

            /*dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(VideoTask);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 333);
            dispatcherTimer.Start();*/

            state = State.Run;
        }

        public void GoOn()
        {
            state = State.Run;
        }

        public void Pause()
        {
            state = State.Pause;
        }

        public void Stop()
        {
            state = State.Stop;
        }

        public void BitmapToImageSource(Bitmap bitmap)
        {
            image.Dispatcher.BeginInvoke((Action)(() =>
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    if (decodeThread.IsAlive)
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

        private void VideoTask(object sender, EventArgs e)
        {
            if (bitmaps.Count > 0 && state == State.Run)
            {
                BitmapToImageSource(bitmaps[0]);
                lock (this) { bitmaps.RemoveAt(0); }             
            }
        }
    }
}