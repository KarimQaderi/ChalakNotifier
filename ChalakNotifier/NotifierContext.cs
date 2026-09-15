using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ChalakNotifier
{
    public class NotifierContext : ApplicationContext
    {
        readonly ProjectSettings project;
        readonly Config config;
        readonly NotificationApi api;
        readonly NotificationStore store;
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer;
        readonly ToolStripMenuItem enabledItem;
        readonly Control ui; // برای برگشت از Thread پس‌زمینه به UI
        int checking;

        public NotifierContext(ProjectSettings project)
        {
            this.project = project;
            config = Config.Load(project);
            api = new NotificationApi(project, config);
            store = new NotificationStore(project);

            ui = new Control();
            ui.CreateControl();

            enabledItem = new ToolStripMenuItem("اعلان‌ها فعال", null, delegate { ToggleEnabled(); })
            {
                Checked = config.Enabled
            };

            var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
            menu.Items.Add("باز کردن برنامه", null, delegate { OpenMainApp(); });
            menu.Items.Add("بررسی اعلان‌ها", null, delegate { CheckAsync(); });
            menu.Items.Add(enabledItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("خروج", null, delegate { Exit(); });

            tray = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = Truncate("اعلان‌های " + project.ProjectName, 63),
                Visible = project.Dev, // در حالت عادی مخفی؛ فقط Dev آیکون دارد
                ContextMenuStrip = menu
            };
            tray.DoubleClick += delegate { OpenMainApp(); };

            timer = new System.Windows.Forms.Timer { Interval = project.IntervalMinutes * 60000 };
            timer.Tick += delegate { CheckAsync(); };
            timer.Start();

            CheckAsync();
        }

        void CheckAsync()
        {
            if (!config.Enabled) return;
            if (Interlocked.Exchange(ref checking, 1) == 1) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var list = api.GetPending();
                    var toShow = new List<AppNotification>();
                    foreach (var n in list)
                        if (store.ShouldShow(n.id)) toShow.Add(n);

                    if (toShow.Count > 0)
                        ui.BeginInvoke((MethodInvoker)delegate { ShowAll(toShow); });
                }
                catch
                {
                    // اینترنت یا سرور در دسترس نیست؛ دفعه بعد دوباره تلاش می‌شود
                }
                finally
                {
                    Interlocked.Exchange(ref checking, 0);
                }
            });
        }

        void ShowAll(List<AppNotification> list)
        {
            if (project.Sound && list.Count > 0)
                NotificationSound.Play(project.SoundFile);

            foreach (var n in list)
            {
                var item = n;
                var popup = new NotificationPopup(project.ProjectName, item);
                popup.Acknowledged += delegate { Acknowledge(item.id); };
                popup.OpenRequested += delegate { OpenUrl(item.url); };
                popup.Show();

                store.MarkShown(item.id);
                Background(delegate { api.MarkDelivered(item.id); });
            }
        }

        void Acknowledge(long id)
        {
            store.Acknowledge(id); // محلی ثبت می‌شود حتی اگر اینترنت قطع باشد
            Background(delegate { api.Acknowledge(id); });
        }

        static void Background(Action action)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { action(); }
                catch { }
            });
        }

        void ToggleEnabled()
        {
            config.Enabled = !config.Enabled;
            enabledItem.Checked = config.Enabled;
            config.Save();
            if (config.Enabled) CheckAsync();
        }

        void OpenMainApp()
        {
            if (string.IsNullOrEmpty(project.MainAppPath)) return;
            var path = Path.IsPathRooted(project.MainAppPath)
                ? project.MainAppPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, project.MainAppPath);
            try
            {
                if (File.Exists(path)) Process.Start(path);
            }
            catch { }
        }

        static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try { Process.Start(url); }
            catch { }
        }

        static Icon LoadIcon()
        {
            try
            {
                var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) return ico;
            }
            catch { }
            return SystemIcons.Information;
        }

        static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        void Exit()
        {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}
