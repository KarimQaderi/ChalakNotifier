using System;
using System.Collections.Generic;
using System.IO;

namespace ChalakNotifier
{
    /// <summary>
    /// وضعیت محلی اعلان‌ها در %AppData%\Chalak\{ProjectKey}\notifications.json
    /// - تاییدشده‌ها هیچ‌وقت دوباره نمایش داده نمی‌شوند (حتی اگر سرور دوباره بفرستد)
    /// - نمایش‌داده‌شده‌ی بدون تایید، فقط بعد از RemindAfter دوباره نشان داده می‌شود
    /// </summary>
    public class NotificationStore
    {
        public static readonly TimeSpan RemindAfter = TimeSpan.FromHours(24);

        public class Data
        {
            public List<long> Acknowledged { get; set; }
            public Dictionary<string, DateTime> LastShown { get; set; }
        }

        readonly string filePath;
        readonly object sync = new object();
        readonly Data data;

        public NotificationStore(ProjectSettings project)
        {
            filePath = Path.Combine(Config.DataDir(project), "notifications.json");
            try
            {
                if (File.Exists(filePath))
                    data = Json.Parse<Data>(File.ReadAllText(filePath));
            }
            catch { }

            if (data == null) data = new Data();
            if (data.Acknowledged == null) data.Acknowledged = new List<long>();
            if (data.LastShown == null) data.LastShown = new Dictionary<string, DateTime>();
        }

        public bool ShouldShow(long id)
        {
            lock (sync)
            {
                if (data.Acknowledged.Contains(id)) return false;
                DateTime t;
                return !data.LastShown.TryGetValue(id.ToString(), out t) || DateTime.UtcNow - t >= RemindAfter;
            }
        }

        public void MarkShown(long id)
        {
            lock (sync)
            {
                data.LastShown[id.ToString()] = DateTime.UtcNow;
                Save();
            }
        }

        public void Acknowledge(long id)
        {
            lock (sync)
            {
                if (!data.Acknowledged.Contains(id)) data.Acknowledged.Add(id);
                data.LastShown.Remove(id.ToString());
                Save();
            }
        }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, Json.Write(data));
            }
            catch { }
        }
    }
}
