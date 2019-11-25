using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MyMediaPlayer.FFmpeg
{
    public sealed unsafe class StreamDecoder : IDisposable
    {
        private readonly AVFormatContext* _pFormatContext;
        private readonly AVFrame* _pFrame;
        private readonly AVPacket* _pPacket;

        public StreamDecoder(string url)
        {
            _pFormatContext = ffmpeg.avformat_alloc_context();
            var pFormatContext = _pFormatContext;

            ffmpeg.avformat_open_input(&pFormatContext, url, null, null).ThrowExceptionIfError();
            ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();

            videoStreamIndex = ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            audioStreamIndex = ffmpeg.av_find_best_stream(_pFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, videoStreamIndex, null, 0);

            vcodecContext = _pFormatContext->streams[videoStreamIndex]->codec;
            acodecContext = _pFormatContext->streams[audioStreamIndex]->codec;

            if (videoStreamIndex >= 0)
            {
                AVCodecContext* avctx = OpenStream(vcodecContext);
                FrameSize = new System.Windows.Size(avctx->width, avctx->height);
                PixelFormat = avctx->pix_fmt;
            }

            if (audioStreamIndex >= 0)
                OpenStream(acodecContext);

            _pPacket = ffmpeg.av_packet_alloc();
            _pFrame = ffmpeg.av_frame_alloc();
        }

        public string CodecName { get; }
        public System.Windows.Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }
        public int videoStreamIndex { get; }
        public int audioStreamIndex { get; }
        public AVCodecContext* vcodecContext { get; }
        public AVCodecContext* acodecContext { get; }

        private AVCodecContext* OpenStream(AVCodecContext* avctx)
        {
            AVCodec* codec = ffmpeg.avcodec_find_decoder(avctx->codec_id);
            if (codec == null) throw new InvalidOperationException("No codec could be found.");

            avctx->codec_id = codec->id;
            avctx->lowres = 0;
            if (avctx->lowres > codec->max_lowres)
                avctx->lowres = codec->max_lowres;

            avctx->idct_algo = ffmpeg.FF_IDCT_AUTO;
            avctx->error_concealment = 3;

            ffmpeg.avcodec_open2(avctx, codec, null).ThrowExceptionIfError();

            return avctx;
        }

        public void Dispose()
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_free(_pFrame);

            ffmpeg.av_packet_unref(_pPacket);
            ffmpeg.av_free(_pPacket);

            ffmpeg.avcodec_close(vcodecContext);
            ffmpeg.avcodec_close(acodecContext);

            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);
        }

        public bool TryDecodeNextFrame(out AVFrame frame, out int type)
        {
            int ret;

            while (ffmpeg.av_read_frame(_pFormatContext, _pPacket) == 0)
            {
                if (_pPacket->stream_index == videoStreamIndex)
                {
                    ret = ffmpeg.avcodec_send_packet(vcodecContext, _pPacket);
                    if (ret != 0) { continue; }
                    do
                    {
                        ret = ffmpeg.avcodec_receive_frame(vcodecContext, _pFrame);
                        if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;

                        frame = *_pFrame;
                        type = 0;
                        return true;
                    } while (true);                 
                }

                //오디오 프레임 출력
                /*if (_pPacket->stream_index == audioStreamIndex)
                {
                    ret = ffmpeg.avcodec_send_packet(acodecContext, _pPacket);
                    if (ret != 0) { continue; }
                    do
                    {
                        ret = ffmpeg.avcodec_receive_frame(acodecContext, _pFrame);
                        if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;

                        frame = *_pFrame;
                        type = 1;
                        return true;
                    } while (true);                   
                } */

                ffmpeg.av_packet_unref(_pPacket);
            }

            ffmpeg.av_frame_unref(_pFrame);

            frame = *_pFrame;
            type = -1;
            return false;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();
            while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }
    }
}