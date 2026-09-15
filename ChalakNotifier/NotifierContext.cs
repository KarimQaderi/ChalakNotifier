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
        readonly Queue<AppNotification> pendingBalloons = new Queue<AppNotification>();
        AppNotification currentBalloon;
        bool clickHandled;
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
                Visible = true, // برای نمایش اعلان ویندوزی (Balloon) آیکون باید فعال باشد
                ContextMenuStrip = menu
            };
            tray.DoubleClick += delegate { OpenMainApp(); };
            tray.BalloonTipClicked += delegate { BalloonClicked(); };
            tray.BalloonTipClosed += delegate { BalloonFinished(); };

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
            foreach (var n in list)
            {
                pendingBalloons.Enqueue(n);
                store.MarkShown(n.id);
                var item = n;
                Background(delegate { api.MarkDelivered(item.id); });
            }

            ShowNextBalloon();
        }

        /// <summary>
        /// اعلان‌ها یکی‌یکی به‌صورت اعلان ویندوزی (Balloon/Toast) نمایش داده می‌شوند
        /// چون ویندوز هم‌زمان فقط یک Balloon نشان می‌دهد
        /// </summary>
        void ShowNextBalloon()
        {
            if (currentBalloon != null) return;
            if (pendingBalloons.Count == 0) return;

            currentBalloon = pendingBalloons.Dequeue();

            var icon = ToolTipIcon.Info;
            if (!project.Sound)
                icon = ToolTipIcon.None;
            else if (!string.IsNullOrEmpty(project.SoundFile))
            {
                icon = ToolTipIcon.None;
                NotificationSound.Play(project.SoundFile);
            }

            var title = string.IsNullOrEmpty(currentBalloon.title) ? project.ProjectName : currentBalloon.title;
            var body = string.IsNullOrEmpty(currentBalloon.body) ? " " : currentBalloon.body;

            try
            {
                tray.ShowBalloonTip(BalloonTimeout, Truncate(title, 63), Truncate(body, 255), icon);
            }
            catch
            {
                currentBalloon = null;
            }
        }

        /// <summary>کلیک روی اعلان = تایید؛ اگر لینک داشته باشد باز می‌شود</summary>
        void BalloonClicked()
        {
            var n = currentBalloon;
            if (n == null) return;

            currentBalloon = null;
            clickHandled = true;
            Acknowledge(n.id);
            OpenUrl(n.url);
            ShowNextBalloon();
        }

        /// <summary>بسته شدن بدون کلیک = «بعداً»؛ ۲۴ ساعت بعد دوباره یادآوری می‌شود</summary>
        void BalloonFinished()
        {
            // در بعضی نسخه‌های ویندوز بعد از کلیک، Closed هم فرستاده می‌شود
            if (clickHandled)
            {
                clickHandled = false;
                return;
            }

            currentBalloon = null;
            ShowNextBalloon();
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

        const int BalloonTimeout = 20000;

        void Exit()
        {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}
