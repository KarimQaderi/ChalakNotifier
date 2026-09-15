using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChalakNotifier
{
    /// <summary>
    /// پنجره اعلان سفارشی گوشه پایین-راست؛ روی همه نسخه‌های ویندوز یکسان است
    /// (Toast ویندوز 10 روی نسخه‌های قدیمی وجود ندارد و Balloon دکمه ندارد)
    /// </summary>
    public class NotificationPopup : Form
    {
        public event EventHandler Acknowledged;
        public event EventHandler OpenRequested;

        readonly Timer autoClose = new Timer { Interval = 60000 };

        public NotificationPopup(string projectName, AppNotification n)
        {
            Text = projectName;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = new Font("Tahoma", 9f);
            BackColor = Color.White;
            ClientSize = new Size(340, 150);

            var title = new Label
            {
                Text = n.title,
                Font = new Font("Tahoma", 10f, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(320, 22),
                AutoEllipsis = true
            };

            var body = new Label
            {
                Text = n.body,
                Location = new Point(10, 36),
                Size = new Size(320, 72),
                AutoEllipsis = true
            };

            var ok = new Button { Text = "متوجه شدم", Size = new Size(95, 28), Location = new Point(235, 114) };
            ok.Click += delegate { Raise(Acknowledged); Close(); };
            Controls.AddRange(new Control[] { title, body, ok });

            if (!string.IsNullOrEmpty(n.url))
            {
                var open = new Button { Text = "مشاهده", Size = new Size(95, 28), Location = new Point(135, 114) };
                open.Click += delegate { Raise(OpenRequested); Raise(Acknowledged); Close(); };
                Controls.Add(open);
            }

            var later = new Button { Text = "بعداً", Size = new Size(70, 28), Location = new Point(10, 114) };
            later.Click += delegate { Close(); };
            Controls.Add(later);

            AcceptButton = ok;
            autoClose.Tick += delegate { Close(); };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var area = Screen.PrimaryScreen.WorkingArea;
            var offset = (OpenCount - 1) * 20;
            Location = new Point(area.Right - Width - 10 - offset, area.Bottom - Height - 10 - offset);
            autoClose.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            autoClose.Dispose();
            base.OnFormClosed(e);
        }

        static int OpenCount
        {
            get
            {
                var c = 0;
                foreach (Form f in Application.OpenForms)
                    if (f is NotificationPopup) c++;
                return c;
            }
        }

        void Raise(EventHandler h)
        {
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
