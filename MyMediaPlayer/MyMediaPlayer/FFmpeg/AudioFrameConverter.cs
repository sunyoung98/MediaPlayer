using System;
using System.Runtime.InteropServices;
using System.Windows;
using FFmpeg.AutoGen;

namespace MyMediaPlayer.FFmpeg
{
    public sealed unsafe class AudioFrameConverter : IDisposable
    {
        SwrContext* swrCtx;

        public AudioFrameConverter(AVFrame aFrame)
        {
            if (swrCtx == null)
            {
                int srate = aFrame.sample_rate;
                int channel = aFrame.channels;
                long chanlay = ffmpeg.av_get_default_channel_layout(channel);
                AVSampleFormat format = (AVSampleFormat)aFrame.format;
                swrCtx = ffmpeg.swr_alloc_set_opts(null, ffmpeg.AV_CH_LAYOUT_STEREO, AVSampleFormat.AV_SAMPLE_FMT_S16,
                    44100, chanlay, format, srate, 0, null);
                ffmpeg.swr_init(swrCtx);
            }
        }

        unsafe public void Dispose()
        {
            //if (swrCtx != null) { ffmpeg.swr_free(&swrCtx); }
        }

        public int Convert(AVFrame aFrame, out byte* data)
        {
            byte* pktbuf = (byte*)ffmpeg.av_malloc(2 * 44100);
            var data_ = aFrame.data;

            int sampnum = ffmpeg.swr_convert(swrCtx, &pktbuf, 2 * 44100,
                (byte**)&data_, aFrame.nb_samples);

            data = pktbuf;
            return sampnum;
        }
    }


}