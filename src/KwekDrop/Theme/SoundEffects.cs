using System;
using System.IO;
using System.Media;
using System.Reflection;

namespace KwekDrop;

internal static class SoundEffects
{
    private static SoundPlayer? player;
    private static long lastPlayTicks;
    private static readonly object sync = new();

    static SoundEffects()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream("KwekDrop.HighlightSound");
            if (stream != null)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                player = new SoundPlayer(ms);
                player.Load();
            }
            else
            {
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "sounds", "highlight.wav");
                if (File.Exists(localPath))
                {
                    player = new SoundPlayer(localPath);
                    player.Load();
                }
            }
        }
        catch { }
    }

    public static void PlayTick()
    {
        try
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - lastPlayTicks < TimeSpan.FromMilliseconds(18).Ticks)
                return;
            lastPlayTicks = now;

            lock (sync)
            {
                if (player != null)
                {
                    if (player.Stream != null)
                        player.Stream.Position = 0;
                    player.Play();
                }
            }
        }
        catch { }
    }
}
