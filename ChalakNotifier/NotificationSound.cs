using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;

namespace ChalakNotifier
{
    /// <summary>
    /// پخش صدای اعلان پیش‌فرض ویندوز هنگام نمایش نوتیفیکیشن
    /// (ویندوز ویستا به بعد: Notification.Default ؛ ویندوز XP: صدای Asterisk)
    /// </summary>
    public static class NotificationSound
    {
        const int SND_ASYNC = 0x0001;
        const int SND_NODEFAULT = 0x0002;
        const int SND_ALIAS = 0x00010000;
        const int SND_FILENAME = 0x00020000;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool PlaySound(string pszSound, IntPtr hmod, int fdwSound);

        public static void Play(string customWavPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(customWavPath))
                {
                    var path = customWavPath;
                    if (!Path.IsPathRooted(path))
                        path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

                    if (File.Exists(path) && PlaySound(path, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT))
                        return;
                }

                if (PlaySound("Notification.Default", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT))
                    return;

                if (PlaySound("SystemAsterisk", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT))
                    return;

                SystemSounds.Asterisk.Play();
            }
            catch
            {
                try { SystemSounds.Asterisk.Play(); }
                catch { }
            }
        }
    }
}
