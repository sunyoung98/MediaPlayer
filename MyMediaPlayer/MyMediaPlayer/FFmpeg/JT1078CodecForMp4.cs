using FFmpeg.AutoGen;
using MyMediaPlayer.SDL2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyMediaPlayer.FFmpeg
{
    public unsafe class JT1078CodecForMp4
    {
        public bool IsRun { get; protected set; }
        private Thread threadAudio;
        private bool exit_thread = false;
        private bool pause_thread = false;
        private int audioindex = -1;

        [Obsolete]
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
}
