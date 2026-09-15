# ChalakNotifier

Agent پس‌زمینه (مخفی) برای دریافت اعلان از سایت و نمایش پنجره اعلان، حتی وقتی برنامه اصلی بسته است.
یک کد برای همه پروژه‌ها؛ فقط `notifier.json` کنار exe برای هر پروژه متفاوت است.

## سازگاری
- **.NET Framework 4.0** → Windows XP SP3 / Vista / 7 / 8 / 10 / 11
- ویندوز 8 به بعد: بدون نصب چیز اضافه. XP و 7: اگر .NET 4 ندارند، در Installer نصب شود.
- بدون پکیج خارجی (JSON با `System.Web.Extensions`، HTTP با `WebClient`، TLS 1.2 در صورت پشتیبانی سیستم).
- اعلان با **اعلان ویندوزی (Balloon/Toast)** کنار ساعت ویندوز نمایش داده می‌شود؛ روی XP تا ویندوز 11 کار می‌کند.
- ⚠️ XP به‌طور پیش‌فرض TLS 1.2 ندارد؛ اگر سرور فقط TLS 1.2 قبول کند روی XP وصل نمی‌شود.

## notifier.json (کنار exe)
```json
{
  "ProjectKey": "slug-type-licence",
  "ProjectName": "چالاک املاک",
  "NotificationsUrl": "https://chalakapp.ir/api/v1/notifications",
  "IntervalMinutes": 15,
  "MainAppPath": "ChalakAmlak.exe",
  "Sound": true,
  "SoundFile": "",
  "Dev": false
}
```
- `ProjectKey` = slug نوع لایسنس در سایت؛ یکتا (رجیستری، Mutex و پوشه داده بر اساس آن).
- `Sound`: پخش صدای اعلان ویندوز (پیش‌فرض `true`). `SoundFile`: مسیر یک فایل wav دلخواه؛ خالی یعنی صدای پیش‌فرض ویندوز.
- `Dev`: منوی Tray برای بررسی دستی/خروج. توجه: برای نمایش اعلان ویندوزی، آیکون Tray همیشه فعال است.
  اجرا با `--dev` هم Dev را فعال می‌کند (Build در حالت Debug تاثیری ندارد).

## Build
Visual Studio (2017+) یا:
```
dotnet build ChalakNotifier -c Release
```

## نصب / حذف
- `ChalakNotifier.exe --register` ← اجرای خودکار با ویندوز
- `ChalakNotifier.exe --unregister` ← هنگام Uninstall (و بستن پروسس `ChalakNotifier.exe`)

## استفاده در برنامه اصلی
`ChalakNotifier.exe` و `notifier.json` را کنار exe برنامه اصلی قرار دهید و این کلاس را به برنامه اصلی اضافه کنید
(سازگار با .NET Framework 4.0 و C# قدیمی):

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public static class ChalakNotifierLauncher
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// با هر بار اجرای برنامه اصلی صدا زده شود:
    /// 1) Agent را در Startup ویندوز ثبت می‌کند (اگر ثبت نشده یا مسیرش عوض شده)
    /// 2) اگر Agent در حال اجرا نیست، اجرایش می‌کند
    /// </summary>
    public static void EnsureRunning(string projectKey)
    {
        try
        {
            string exe = Path.Combine(Application.StartupPath, "ChalakNotifier.exe");
            if (!File.Exists(exe)) return;

            // ثبت در Startup (بدون نیاز به Admin)
            string name = "ChalakNotifier_" + projectKey;
            string value = "\"" + exe + "\"";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key != null && !string.Equals(key.GetValue(name) as string, value, StringComparison.OrdinalIgnoreCase))
                    key.SetValue(name, value);
            }

            // اگر در حال اجراست (Mutex خود Agent)، دوباره اجرا نشود
            bool created;
            using (Mutex m = new Mutex(false, "ChalakNotifier_" + projectKey, out created))
            {
                if (!created) return;
            }

            ProcessStartInfo psi = new ProcessStartInfo(exe);
            psi.WorkingDirectory = Application.StartupPath;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch
        {
            // نبودن Agent یا خطای دسترسی نباید برنامه اصلی را متوقف کند
        }
    }

    /// <summary>برای گزینه «غیرفعال کردن اعلان‌ها» یا هنگام حذف برنامه</summary>
    public static void Unregister(string projectKey)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key != null) key.DeleteValue("ChalakNotifier_" + projectKey, false);
            }

            foreach (Process p in Process.GetProcessesByName("ChalakNotifier"))
            {
                try { p.Kill(); } catch { }
            }
        }
        catch { }
    }
}
```

**فراخوانی** در `Program.cs` برنامه اصلی (قبل از `Application.Run`) یا در `Load` فرم اصلی:

```csharp
ChalakNotifierLauncher.EnsureRunning("slug-type-licence"); // همان ProjectKey داخل notifier.json
```

> نام Registry و Mutex باید دقیقاً `ChalakNotifier_{ProjectKey}` باشد تا با خود Agent یکی شود.

## داده‌های کاربر
`%AppData%\Chalak\{ProjectKey}\`
- `config.json` ← `ClientId`, `Token`, `Enabled`
- `notifications.json` ← اعلان‌های تاییدشده / نمایش‌داده‌شده

## API سرور (لاراول: `NotificationController`)
- `GET  {url}?project=..&client_id=..&source=agent&version=..` → `[{ "id", "title", "body", "url" }]`
- `POST {url}/{id}/delivered?project=..&client_id=..`
- `POST {url}/{id}/ack?project=..&client_id=..`

## جلوگیری از تکرار
- کلیک روی اعلان ویندوزی → تایید روی سرور و محلی (و باز شدن لینک در صورت وجود)؛ دیگر هرگز نمایش داده نمی‌شود.
- بستن یا نادیده گرفتن اعلان → ۲۴ ساعت بعد دوباره یادآوری می‌شود.
