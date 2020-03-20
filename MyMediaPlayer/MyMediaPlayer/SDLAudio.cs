using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SDL2;
using FFmpeg.AutoGen;

namespace MyMediaPlayer
{
    public unsafe class SDLAudio
    {
        class Data
        {
            public byte[] pcm;
            public int len;
        }

        private List<Data> data = new List<Data>();

        SDL.SDL_AudioCallback Callback;
        public void PlayAudio(IntPtr pcm, int len)
        {
            lock (this)
            {
                byte[] bts = new byte[len];
                Marshal.Copy(pcm, bts, 0, len);
                data.Add(new Data
                {
                    len = len,
                    pcm = bts
                });
            }
        }
        void SDL_AudioCallback(IntPtr userdata, IntPtr stream, int len)
        {
            lock (data)
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
        }
        public int SDL_Init(AVCodecContext* audioCtx)
        {
            Callback = SDL_AudioCallback;

            SDL.SDL_AudioSpec wanted_spec = new SDL.SDL_AudioSpec();
            wanted_spec.freq = audioCtx->sample_rate;
            wanted_spec.format = SDL.AUDIO_F32;
            wanted_spec.channels = (byte)audioCtx->channels;
            wanted_spec.silence = 0;
            wanted_spec.samples = 1024;
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

        public void SDL_Pause()
        {
            SDL.SDL_PauseAudio(1);
        }

        public void SDL_Play()
        {
            SDL.SDL_PauseAudio(0);
        }

        public void Clear()
        {
            data.Clear();
        }
    }
}
