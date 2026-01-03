using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ServiceLib.Manager;

namespace v2rayN.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            
            // فوکوس اولیه روی فیلد نام کاربری
            Loaded += (s, e) => txtUsername.Focus();
        }

        // قابلیت جابجایی پنجره با موس (چون WindowStyle="None" است)
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // دکمه بستن برنامه
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // دکمه ورود
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password.Trim();

            // 1. اعتبارسنجی ساده
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter username and password");
                return;
            }

            // 2. تغییر وضعیت دکمه به حالت "در حال انجام"
            btnLogin.IsEnabled = false;
            btnLogin.Content = "LOGGING IN...";
            txtError.Visibility = Visibility.Collapsed;

            try
            {
                // ساخت شناسه دستگاه یکتا (ساده) و دریافت نسخه
                string deviceId = GetDeviceId();
                string appVersion = "1.0.0"; // یا دریافت داینامیک از اسمبلی

                // 3. تلاش برای لاگین از طریق منیجر
                await MidPanelManager.Instance.LoginAsync(username, password, deviceId, appVersion);

                // 4. لاگین موفق: باز کردن صفحه اصلی و بستن این پنجره
                // فرض بر این است که MainWindow قبلاً در پروژه وجود دارد
                // اگر MainWindow سازنده‌ای دارد که پارامتر نمی‌گیرد:
                new MainWindow().Show();
                this.Close();
            }
            catch (Exception ex)
            {
                // 5. نمایش خطا
                ShowError(ex.Message.Contains("401") ? "Invalid username or password" : "Connection failed. Try again.");
                
                // برگرداندن دکمه به حالت اول
                btnLogin.IsEnabled = true;
                btnLogin.Content = "LOGIN";
            }
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
            
            // لرزش کوچک برای جلب توجه (اختیاری)
            // Animation logic can be added here
        }

        /// <summary>
        /// تولید یک شناسه دستگاه نسبتاً ثابت برای ویندوز
        /// </summary>
        private string GetDeviceId()
        {
            // در محیط پروداکشن می‌توان از ترکیب مشخصات سخت‌افزاری استفاده کرد
            // اینجا برای سادگی از MachineName استفاده می‌کنیم
            return Environment.MachineName + "_" + Environment.UserName;
        }
    }
}