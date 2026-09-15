using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace ChalakNotifier
{
    /// <summary>ارتباط با سرور؛ متدها همگام (sync) هستند و در Thread پس‌زمینه صدا زده می‌شوند</summary>
    public class NotificationApi
    {
        readonly ProjectSettings project;
        readonly Config config;
        readonly string baseUrl;

        static NotificationApi()
        {
            // TLS 1.2 (3072) و 1.1 (768)؛ روی سیستم‌هایی که پشتیبانی ندارند نادیده گرفته می‌شود
            try
            {
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
            }

            // خطای گواهی SSL نادیده گرفته شود (گواهی منقضی، ویندوز قدیمی بدون Root CA جدید و ...)
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.Expect100Continue = false;
        }

        public NotificationApi(ProjectSettings project, Config config)
        {
            this.project = project;
            this.config = config;
            baseUrl = project.NotificationsUrl.TrimEnd('/');
        }

        /// <summary>
        /// اگر اتصال امن برقرار نشد (مثلاً XP بدون TLS 1.2)، همان آدرس با http امتحان می‌شود
        /// </summary>
        static T WithHttpFallback<T>(string url, Func<string, T> call)
        {
            try
            {
                return call(url);
            }
            catch (WebException ex)
            {
                var sslFailure = ex.Status == WebExceptionStatus.SecureChannelFailure
                                 || ex.Status == WebExceptionStatus.TrustFailure
                                 || ex.Status == WebExceptionStatus.ConnectFailure && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                if (!sslFailure || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw;
                return call("http://" + url.Substring("https://".Length));
            }
        }

        string Query
        {
            get { return "project=" + Uri.EscapeDataString(project.ProjectKey) + "&client_id=" + config.ClientId; }
        }

        WebClient CreateClient()
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.Accept] = "application/json";
            if (!string.IsNullOrEmpty(config.Token))
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + config.Token;
            return wc;
        }

        /// <summary>اعلان‌های تاییدنشده؛ همزمان last_seen روی سرور به‌روز می‌شود</summary>
        public List<AppNotification> GetPending()
        {
            var url = baseUrl + "?" + Query + "&source=agent&version=" + Uri.EscapeDataString(Application.ProductVersion);
            var json = WithHttpFallback(url, u =>
            {
                using (var wc = CreateClient())
                    return wc.DownloadString(u);
            });
            return Json.Parse<List<AppNotification>>(json) ?? new List<AppNotification>();
        }

        public void MarkDelivered(long id)
        {
            Post(baseUrl + "/" + id + "/delivered?" + Query);
        }

        /// <summary>کاربر تایید کرده؛ سرور دیگر نباید این اعلان را برگرداند</summary>
        public void Acknowledge(long id)
        {
            Post(baseUrl + "/" + id + "/ack?" + Query);
        }

        void Post(string url)
        {
            WithHttpFallback(url, u =>
            {
                using (var wc = CreateClient())
                    return wc.UploadString(u, "POST", "");
            });
        }
    }
}
