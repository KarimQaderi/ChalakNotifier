using System.Windows.Forms;
using Microsoft.Win32;

namespace ChalakNotifier
{
    /// <summary>اجرای خودکار با لاگین ویندوز (بدون نیاز به Admin)</summary>
    public static class StartupRegistration
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static string Name(ProjectSettings p)
        {
            return "ChalakNotifier_" + p.ProjectKey;
        }

        public static bool IsEnabled(ProjectSettings p)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(Name(p)) != null;
        }

        public static void Enable(ProjectSettings p)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (key != null) key.SetValue(Name(p), "\"" + Application.ExecutablePath + "\"");
        }

        public static void Disable(ProjectSettings p)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (key != null) key.DeleteValue(Name(p), false);
        }
    }
}
