using FFmpeg.AutoGen;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MyMediaPlayer.FFmpeg
{
    public class BinariesHelper
    {
        internal static void RegisterFFmpegBinaries()
        {
            var current = Environment.CurrentDirectory;
            //var probe = Path.Combine("FFmpeg", "lib", Environment.Is64BitProcess ? "x64" : "x86");
            var probe = Path.Combine("FFmpeg", "lib");
            while (current != null)
            {
                var ffmpegBinaryPath = Path.Combine(current, probe);
                if (Directory.Exists(ffmpegBinaryPath))
                {
                    ffmpeg.RootPath = ffmpegBinaryPath;
                    return;
                }
                current = Directory.GetParent(current)?.FullName;
            }

        }
    }
}