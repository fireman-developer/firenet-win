using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ServiceLib.Models;
using ServiceLib.Services;
using ServiceLib.Handler; // برای دسترسی به متدهای مدیریت کانفیگ (ConfigHandler)
using ServiceLib.Events;  // برای ارسال رویداد رفرش شدن لیست (AppEvents)
using ServiceLib.Common;  // برای دسترسی به AppManager و تنظیمات

namespace ServiceLib.Manager
{
    public class MidPanelManager
    {
        // سینگلتون برای دسترسی آسان در کل برنامه
        private static MidPanelManager _instance;
        public static MidPanelManager Instance => _instance ??= new MidPanelManager();

        // استفاده صریح از System.Timers برای جلوگیری از تداخل با System.Threading
        private System.Timers.Timer _keepAliveTimer;
        private System.Timers.Timer _statusRefreshTimer;
        private System.Timers.Timer _notificationTimer;
        
        // نام فایل ذخیره توکن
        private const string TokenFileName = "session_token.dat";
        
        // شناسه اختصاصی برای گروه بندی سرورهای پنل در v2rayN
        // این باعث می‌شود سرورهای پنل با سرورهای شخصی کاربر قاطی نشوند
        private const string FireNetSubId = "FireNet_Panel"; 
        
        // وضعیت فعلی کاربر
        public StatusResponse CurrentStatus { get; private set; }

        // رویدادها برای آپدیت کردن UI
        public event Action<StatusResponse> StatusUpdated;
        public event Action<NotificationFetchResponse> NotificationsReceived;
        public event Action LogoutTriggered;

        // بررسی وضعیت لاگین بودن
        public bool IsLoggedIn => !string.IsNullOrEmpty(MidPanelService.Instance.GetToken());

        public MidPanelManager()
        {
            InitializeTimers();
        }

        private void InitializeTimers()
        {
            // تایمر Keep-alive: هر 1 ساعت
            _keepAliveTimer = new System.Timers.Timer(3600000); 
            _keepAliveTimer.Elapsed += async (s, e) => await PerformKeepAlive();

            // تایمر رفرش وضعیت: هر 5 دقیقه
            _statusRefreshTimer = new System.Timers.Timer(300000); 
            _statusRefreshTimer.Elapsed += async (s, e) => await RefreshStatus();

            // تایمر چک کردن نوتیفیکیشن: هر 5 دقیقه
            _notificationTimer = new System.Timers.Timer(300000); 
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
                // ذخیره توکن
                await File.WriteAllTextAsync(TokenFileName, result.Token);
                
                // دریافت اطلاعات اولیه و کانفیگ‌ها
                await RefreshStatus();
                
                // شروع تایمرها
                StartBackgroundTasks();
            }
        }

        /// <summary>
        /// خروج کامل (سرور + لوکال)
        /// </summary>
        public async Task LogoutAsync()
        {
            StopBackgroundTasks();
            
            // پاکسازی کانفیگ‌های پنل از لیست v2rayN هنگام خروج
            ClearPanelConfigs();

            await MidPanelService.Instance.LogoutAsync();
            PerformLogoutLocal();
        }

        /// <summary>
        /// پاکسازی اطلاعات محلی
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

        /// <summary>
        /// دریافت وضعیت جدید و آپدیت کردن کانفیگ‌ها
        /// </summary>
        public async Task RefreshStatus()
        {
            if (!IsLoggedIn) return;
            try
            {
                var status = await MidPanelService.Instance.GetStatusAsync();
                CurrentStatus = status;
                
                // === منطق حیاتی: ایمپورت کردن کانفیگ‌ها به هسته ===
                if (status.Links != null && status.Links.Count > 0)
                {
                    await ImportConfigs(status.Links);
                }

                StatusUpdated?.Invoke(status);
            }
            catch (Exception ex)
            {
                HandleAuthError(ex);
            }
        }

        /// <summary>
        /// افزودن لینک‌های دریافتی به لیست پروفایل‌های برنامه
        /// </summary>
        private async Task ImportConfigs(List<string> links)
        {
            try
            {
                var config = AppManager.Instance.Config;
                bool isListChanged = false;

                // 1. پاکسازی سرورهای قدیمی همین پنل (بر اساس SubId)
                // این کار باعث می‌شود سرورهای تکراری اضافه نشوند
                var oldItems = config.ProfileItems.Where(t => t.Subid == FireNetSubId).ToList();
                if (oldItems.Count > 0)
                {
                    foreach (var item in oldItems)
                    {
                        config.ProfileItems.Remove(item);
                    }
                    isListChanged = true;
                }

                // 2. آماده‌سازی لینک‌ها برای ایمپورت
                var linksStr = string.Join(Environment.NewLine, links);
                
                // 3. استفاده از متد داخلی v2rayN برای افزودن دسته‌جمعی سرورها
                // پارامتر دوم (subid) بسیار مهم است تا بعداً بتوانیم آنها را شناسایی کنیم
                await ConfigHandler.AddBatchServers(config, linksStr, FireNetSubId, false);
                isListChanged = true;

                // 4. لاجیک انتخاب سرور پیش‌فرض
                // اگر هیچ سروری انتخاب نشده باشد، اولین سرور این پنل را انتخاب می‌کنیم
                if (string.IsNullOrEmpty(config.IndexId))
                {
                    var firstPanelItem = config.ProfileItems.FirstOrDefault(t => t.Subid == FireNetSubId);
                    if (firstPanelItem != null)
                    {
                        config.IndexId = firstPanelItem.IndexId;
                    }
                }

                // 5. اگر تغییری در لیست ایجاد شد، به UI اطلاع می‌دهیم تا رفرش شود
                if (isListChanged)
                {
                    AppEvents.ProfilesRefreshRequested.Publish();
                }
            }
            catch (Exception ex)
            {
                // خطا در ایمپورت نباید باعث کرش برنامه شود، فقط لاگ می‌کنیم
                MidPanelService.Instance.Log("IMPORT ERROR", ex.Message, true);
            }
        }

        private void ClearPanelConfigs()
        {
            try
            {
                var config = AppManager.Instance.Config;
                var oldItems = config.ProfileItems.Where(t => t.Subid == FireNetSubId).ToList();
                foreach (var item in oldItems)
                {
                    config.ProfileItems.Remove(item);
                }
                AppEvents.ProfilesRefreshRequested.Publish();
            }
            catch { }
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
        /// مدیریت خطاهای احراز هویت (خروج اجباری در صورت 401/403)
        /// </summary>
        private void HandleAuthError(Exception ex)
        {
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") || 
                ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                StopBackgroundTasks();
                PerformLogoutLocal();
            }
        }

        public void ReportPromptSeen()
        {
             _ = MidPanelService.Instance.UpdatePromptSeenAsync();
        }
    }
}