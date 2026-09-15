using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ChalakNotifier
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ProjectSettings project;
            try
            {
                project = ProjectSettings.Load();
            }
            catch (Exception ex)
            {
                if (!args.Contains("--silent"))
                    MessageBox.Show(ex.Message, "ChalakNotifier", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (args.Contains("--dev")) project.Dev = true;

            // آرگومان‌ها برای Installer:  --register  /  --unregister
            if (args.Contains("--register")) { StartupRegistration.Enable(project); return; }
            if (args.Contains("--unregister")) { StartupRegistration.Disable(project); return; }

            // هر پروژه فقط یک نمونه
            bool isNew;
            using (var mutex = new Mutex(true, "ChalakNotifier_" + project.ProjectKey, out isNew))
            {
                if (!isNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new NotifierContext(project));
                GC.KeepAlive(mutex);
            }
        }
    }
}
