using System.Windows;
using ServiceLib.Manager;
using v2rayN.Views;

namespace v2rayN
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // سازنده پیش‌فرض
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // تلاش برای بازیابی نشست قبلی (Session Restore)
            // اگر توکن معتبر ذخیره شده باشد، true برمی‌گرداند
            bool isSessionRestored = await MidPanelManager.Instance.TryRestoreSessionAsync();

            if (isSessionRestored)
            {
                // اگر کاربر قبلاً لاگین کرده و توکن معتبر است، مستقیم به صفحه اصلی برود
                new MainWindow().Show();
            }
            else
            {
                // در غیر این صورت، صفحه لاگین را نمایش بده
                new LoginWindow().Show();
            }
        }
    }
}