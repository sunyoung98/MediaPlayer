using System;
using System.Runtime.InteropServices;
using System.Windows;
using FFmpeg.AutoGen;
/*
namespace MyMediaPlayer.FFmpeg
{
    public sealed unsafe class AudioFrameConverter : IDisposable
    {
        private SwrContext* swrCtx { get; }
        struct sWave
        {
            HWAVEOUT hWaveDev;
            WAVEFORMATEX wf;
            static const int hdrnum = 10;
            static const int bufsize = 17640;
            static const int pktsize = 20000;
            WAVEHDR hdr[hdrnum];
            char samplebuf[hdrnum][bufsize];
	        long availhdr;
            int nowhdr;
            char* pktbuf;
            char* pktptr;
            char* bufptr;
        };
        private sWave wa;

        public AudioFrameConverter(AVFrame aFrame)
        {
            // 장치 초기화 및 주요 변수 초기화. 리샘플러 초기화
            if (wa.hWaveDev == NULL)
            {
                wa.wf.cbSize = sizeof(WAVEFORMATEX);
                wa.wf.wFormatTag = WAVE_FORMAT_PCM;
                wa.wf.nChannels = 2;
                wa.wf.nSamplesPerSec = 44100;
                wa.wf.wBitsPerSample = 16;
                wa.wf.nBlockAlign = wa.wf.nChannels * wa.wf.wBitsPerSample / 8;
                wa.wf.nAvgBytesPerSec = wa.wf.nSamplesPerSec * wa.wf.nBlockAlign;
                waveOutOpen(&wa.hWaveDev, WAVE_MAPPER, &wa.wf,
                    (DWORD_PTR)CallbackThreadID, 0, CALLBACK_THREAD);

                for (int i = 0; i < wa.hdrnum; i++)
                {
                    wa.hdr[i].lpData = wa.samplebuf[i];
                }

                wa.availhdr = wa.hdrnum;
                wa.nowhdr = 0;
                wa.bufptr = wa.samplebuf[wa.nowhdr];
                wa.pktbuf = (char*)malloc(wa.pktsize);
            }

            int srate = aFrame.sample_rate;
            int channel = aFrame.channels;
            long chanlay = ffmpeg.av_get_default_channel_layout(channel);
            AVSampleFormat format = (AVSampleFormat)aFrame.format;
            swrCtx = ffmpeg.swr_alloc_set_opts(null, ffmpeg.AV_CH_LAYOUT_STEREO, AVSampleFormat.AV_SAMPLE_FMT_S16,
                44100, chanlay, format, srate, 0, null);
            ffmpeg.swr_init(swrCtx);
        }

        unsafe public void Dispose()
        {
            if (swrCtx != null) { ffmpeg.swr_free(&swrCtx); }
        }

        public AVFrame Convert(AVFrame aFrame)
        {
            // Mp3를 Wave로 리샘플링한 패킷을 읽는다.
            int sampnum = ffmpeg.swr_convert(swrCtx, (long**)&wa.pktbuf, wa.pktsize / 4,
                (long**)aFrame.extended_data, aFrame.nb_samples);

            // 패킷 포인터 초기화. 남은 패킷은 바이트 단위로 바꾼다.
            wa.pktptr = wa.pktbuf;
            int remainpkt = sampnum * 4;

            // 패킷을 다 쓸 때까지 반복한다.
            for (; ; )
            {
                int remainbuf = int(wa.bufsize - (wa.bufptr - wa.samplebuf[wa.nowhdr]));

                // 패킷이 작으면 채워 넣고 다음 패킷 대기
                if (remainpkt < remainbuf)
                {
                    memcpy(wa.bufptr, wa.pktptr, remainpkt);
                    wa.bufptr += remainpkt;
                    break;
                }

                // 버퍼 가득 채우로 남은 패킷 및 패킷 포인터 조정
                memcpy(wa.bufptr, wa.pktptr, remainbuf);
                remainpkt -= remainbuf;
                wa.pktptr += remainbuf;

                // 장치로 보내 재생
                wa.hdr[wa.nowhdr].dwBufferLength = wa.bufsize;
                waveOutPrepareHeader(wa.hWaveDev, &wa.hdr[wa.nowhdr], sizeof(WAVEHDR));
                waveOutWrite(wa.hWaveDev, &wa.hdr[wa.nowhdr], sizeof(WAVEHDR));

                // 헤더 수 감소시키고 남은 헤더가 생길 때까지 대기
                InterlockedDecrement(&wa.availhdr);
                while (wa.availhdr == 0)
                {
                    if (status == P_EXIT) break;
                    Sleep(20);
                }
                if (status == P_EXIT) break;

                if (++wa.nowhdr == wa.hdrnum) wa.nowhdr = 0;
                wa.bufptr = wa.samplebuf[wa.nowhdr];
            }
        }
    }
}*/