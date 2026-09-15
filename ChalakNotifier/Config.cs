using System;
using System.IO;

namespace ChalakNotifier
{
    /// <summary>
    /// تنظیمات پروژه: notifier.json کنار exe (برای هر پروژه متفاوت)
    /// </summary>
    public class ProjectSettings
    {
        public string ProjectKey { get; set; }
        public string ProjectName { get; set; }
        public string NotificationsUrl { get; set; }
        public int IntervalMinutes { get; set; }
        public string MainAppPath { get; set; }

        /// <summary>پخش صدای اعلان ویندوز هنگام نمایش نوتیفیکیشن (پیش‌فرض: فعال)</summary>
        public bool Sound { get; set; }

        /// <summary>مسیر فایل wav دلخواه؛ خالی یعنی صدای پیش‌فرض ویندوز</summary>
        public string SoundFile { get; set; }

        /// <summary>فقط برای توسعه: نمایش آیکون در Tray. در حالت عادی Agent کاملاً مخفی است</summary>
        public bool Dev { get; set; }

        public static ProjectSettings Load()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notifier.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("فایل notifier.json کنار برنامه پیدا نشد.", path);

            var raw = File.ReadAllText(path);
            var s = Json.Parse<ProjectSettings>(raw);
            if (s != null && raw.IndexOf("\"Sound\"", StringComparison.OrdinalIgnoreCase) < 0)
                s.Sound = true;
            if (s == null || string.IsNullOrEmpty(s.ProjectKey))
                throw new InvalidDataException("ProjectKey در notifier.json مشخص نشده است.");

            Uri uri;
            if (!Uri.TryCreate(s.NotificationsUrl, UriKind.Absolute, out uri))
                throw new InvalidDataException("NotificationsUrl در notifier.json نامعتبر است.");

            if (string.IsNullOrEmpty(s.ProjectName)) s.ProjectName = s.ProjectKey;
            if (s.IntervalMinutes <= 0) s.IntervalMinutes = 15;
            return s;
        }
    }

    /// <summary>
    /// وضعیت کاربر برای هر پروژه: %AppData%\Chalak\{ProjectKey}\config.json
    /// </summary>
    public class Config
    {
        public string ClientId { get; set; }
        public string Token { get; set; }
        public bool Enabled { get; set; }

        string filePath;

        public static string DataDir(ProjectSettings project)
        {
            return Path.Combine(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chalak"), project.ProjectKey);
        }

        public static Config Load(ProjectSettings project)
        {
            var path = Path.Combine(DataDir(project), "config.json");
            Config c = null;
            try
            {
                if (File.Exists(path))
                    c = Json.Parse<Config>(File.ReadAllText(path));
            }
            catch { }

            if (c == null) c = new Config { Enabled = true };
            if (string.IsNullOrEmpty(c.ClientId)) c.ClientId = Guid.NewGuid().ToString("N");
            c.filePath = path;
            c.Save();
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, Json.Write(new { ClientId, Token, Enabled }));
            }
            catch { }
        }
    }
}
