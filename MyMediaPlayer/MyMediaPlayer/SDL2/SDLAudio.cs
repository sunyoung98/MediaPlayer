using SDL2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MyMediaPlayer.SDL2
{
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
}
