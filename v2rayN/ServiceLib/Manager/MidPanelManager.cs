using System;
using System.IO;
using System.Threading.Tasks;
// حذف using System.Timers; برای جلوگیری از ابهام، مستقیماً در کد استفاده می‌کنیم
using ServiceLib.Models;
using ServiceLib.Services;

namespace ServiceLib.Manager
{
    public class MidPanelManager
    {
        private static MidPanelManager _instance;
        public static MidPanelManager Instance => _instance ??= new MidPanelManager();

        // رفع ابهام: استفاده صریح از System.Timers.Timer
        private System.Timers.Timer _keepAliveTimer;
        private System.Timers.Timer _statusRefreshTimer;
        private System.Timers.Timer _notificationTimer;
        
        // نام فایل ذخیره توکن
        private const string TokenFileName = "session_token.dat";
        
        // وضعیت فعلی کاربر برای بایندینگ در UI
        public StatusResponse CurrentStatus { get; private set; }

        // رویدادها برای آگاه‌سازی UI
        public event Action<StatusResponse> StatusUpdated;
        public event Action<NotificationFetchResponse> NotificationsReceived;
        public event Action LogoutTriggered;

        public bool IsLoggedIn => !string.IsNullOrEmpty(MidPanelService.Instance.GetToken());

        public MidPanelManager()
        {
            InitializeTimers();
        }

        private void InitializeTimers()
        {
            // تایمر Keep-alive: هر 1 ساعت یکبار (پنجره سرور 48 ساعت است، 1 ساعت کاملا امن است)
            _keepAliveTimer = new System.Timers.Timer(3600000); 
            _keepAliveTimer.Elapsed += async (s, e) => await PerformKeepAlive();

            // تایمر به‌روزرسانی وضعیت و نوتیفیکیشن: هر 5 دقیقه (طبق درخواست شما)
            _statusRefreshTimer = new System.Timers.Timer(300000); // 5 دقیقه
            _statusRefreshTimer.Elapsed += async (s, e) => await RefreshStatus();

            _notificationTimer = new System.Timers.Timer(300000); // 5 دقیقه
            _notificationTimer.Elapsed += async (s, e) => await CheckNotifications();
        }

        /// <summary>
        /// تلاش برای بازیابی نشست قبلی هنگام باز شدن برنامه
        /// </summary>
        public async Task<bool> TryRestoreSessionAsync()
        {
            if (File.Exists(TokenFileName))
            {
                try 
                {
                    string savedToken = await File.ReadAllTextAsync(TokenFileName);
                    if (!string.IsNullOrWhiteSpace(savedToken))
                    {
                        MidPanelService.Instance.SetToken(savedToken.Trim());
                        
                        // تست اعتبار توکن با دریافت وضعیت
                        await RefreshStatus();
                        
                        // اگر خطا نداد یعنی معتبر است
                        StartBackgroundTasks();
                        return true;
                    }
                }
                catch
                {
                    // توکن نامعتبر یا فایل خراب
                    PerformLogoutLocal();
                }
            }
            return false;
        }

        /// <summary>
        /// لاگین کاربر و شروع سرویس‌ها
        /// </summary>
        public async Task LoginAsync(string username, string password, string deviceId, string appVersion)
        {
            var result = await MidPanelService.Instance.LoginAsync(username, password, deviceId, appVersion);
            
            if (result != null && !string.IsNullOrEmpty(result.Token))
            {
                // ذخیره توکن در فایل
                await File.WriteAllTextAsync(TokenFileName, result.Token);
                
                // دریافت وضعیت اولیه
                await RefreshStatus();
                
                // شروع تایمرها
                StartBackgroundTasks();
            }
        }

        /// <summary>
        /// خروج کاربر (درخواست به سرور + پاکسازی محلی)
        /// </summary>
        public async Task LogoutAsync()
        {
            StopBackgroundTasks();
            await MidPanelService.Instance.LogoutAsync();
            PerformLogoutLocal();
        }

        /// <summary>
        /// پاکسازی اطلاعات محلی هنگام خروج یا خطای احراز هویت
        /// </summary>
        private void PerformLogoutLocal()
        {
            if (File.Exists(TokenFileName))
            {
                File.Delete(TokenFileName);
            }
            MidPanelService.Instance.SetToken(null);
            CurrentStatus = null;
            LogoutTriggered?.Invoke();
        }

        private void StartBackgroundTasks()
        {
            _keepAliveTimer.Start();
            _statusRefreshTimer.Start();
            _notificationTimer.Start();
        }

        private void StopBackgroundTasks()
        {
            _keepAliveTimer.Stop();
            _statusRefreshTimer.Stop();
            _notificationTimer.Stop();
        }

        private async Task PerformKeepAlive()
        {
            if (!IsLoggedIn) return;
            try
            {
                await MidPanelService.Instance.KeepAliveAsync();
            }
            catch (Exception ex)
            {
                HandleAuthError(ex);
            }
        }

        public async Task RefreshStatus()
        {
            if (!IsLoggedIn) return;
            try
            {
                var status = await MidPanelService.Instance.GetStatusAsync();
                CurrentStatus = status;
                StatusUpdated?.Invoke(status);
            }
            catch (Exception ex)
            {
                HandleAuthError(ex);
            }
        }

        private async Task CheckNotifications()
        {
            if (!IsLoggedIn) return;
            try
            {
                var response = await MidPanelService.Instance.FetchNotificationsAsync();
                if (response != null && response.Notifications != null && response.Notifications.Count > 0)
                {
                    NotificationsReceived?.Invoke(response);
                }
            }
            catch (Exception ex)
            {
                HandleAuthError(ex);
            }
        }

        /// <summary>
        /// بررسی خطاهای احراز هویت برای خروج اجباری
        /// </summary>
        private void HandleAuthError(Exception ex)
        {
            // تشخیص خطای 401 یا 403 برای خروج اجباری
            // با توجه به اینکه HttpClient خطای استاندارد پرت می‌کند، متن خطا را چک می‌کنیم
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") || 
                ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                StopBackgroundTasks();
                PerformLogoutLocal();
            }
        }

        /// <summary>
        /// گزارش مشاهده پاپ‌آپ آپدیت
        /// </summary>
        public void ReportPromptSeen()
        {
             _ = MidPanelService.Instance.UpdatePromptSeenAsync();
        }
    }
}