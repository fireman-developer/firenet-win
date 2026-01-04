using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ServiceLib.Manager;
using ServiceLib.Models;
using ServiceLib.Handler;
using ServiceLib.Services;

// برای استفاده از NotifyIcon ویندوز و رفع ابهام
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace v2rayN.Views
{
    public partial class LoginWindow : Window
    {
        private bool _isConnected = false;
        private WinForms.NotifyIcon _notifyIcon;

        public LoginWindow()
        {
            InitializeComponent();
            InitializeSystemTray();

            Loaded += MainWindow_Loaded;
            
            MidPanelManager.Instance.StatusUpdated += UpdateUI;
            MidPanelManager.Instance.NotificationsReceived += OnNotificationsReceived;
            MidPanelManager.Instance.LogoutTriggered += OnLogoutTriggered;
        }

        private void InitializeSystemTray()
        {
            try
            {
                _notifyIcon = new WinForms.NotifyIcon();
                _notifyIcon.Visible = true;
                _notifyIcon.Text = "FireNet v2ray Client";

                var appPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(appPath))
                {
                    _notifyIcon.Icon = Drawing.Icon.ExtractAssociatedIcon(appPath);
                }
                else
                {
                    _notifyIcon.Icon = Drawing.SystemIcons.Application;
                }

                _notifyIcon.DoubleClick += (s, e) => 
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error init tray: {ex.Message}");
            }
        }

        private void OnNotificationsReceived(NotificationFetchResponse response)
        {
            if (_notifyIcon == null || response?.Notifications == null) return;

            foreach (var note in response.Notifications)
            {
                _notifyIcon.ShowBalloonTip(
                    5000, 
                    note.Title ?? "Notification", 
                    note.Body ?? "", 
                    WinForms.ToolTipIcon.Info
                );
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (MidPanelManager.Instance.CurrentStatus != null)
            {
                UpdateUI(MidPanelManager.Instance.CurrentStatus);
            }
            else
            {
                 _ = MidPanelManager.Instance.RefreshStatus();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new OptionSettingWindow();
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MidPanelService.Instance.Log("SETTINGS ERROR", ex.ToString(), true);
                MessageBox.Show("Error opening settings: " + ex.Message);
            }
        }

        private void UpdateUI(StatusResponse status)
        {
            Dispatcher.Invoke(() =>
            {
                if (status == null) return;

                txtUsername.Text = status.Username ?? "User";
                
                bool isActive = status.Status?.ToLower() == "active";
                txtStatus.Text = isActive ? "ACTIVE" : "EXPIRED";
                txtStatus.Foreground = isActive ? new SolidColorBrush(Color.FromRgb(49, 128, 229)) : Brushes.Red;
                borderStatus.Background = isActive ? new SolidColorBrush(Color.FromArgb(32, 49, 128, 229)) : new SolidColorBrush(Color.FromArgb(32, 255, 0, 0));

                if (status.DataLimit == null || status.DataLimit == 0)
                {
                    txtTotalData.Text = "Unlimited Data";
                    txtRemainingData.Text = "∞";
                    DrawCircularProgress(pathUsageArc, 1.0, "#3180e5");
                }
                else
                {
                    long limit = status.DataLimit.Value;
                    long used = status.UsedTraffic ?? 0;
                    long remaining = limit - used;
                    if (remaining < 0) remaining = 0;

                    double totalGB = limit / 1073741824.0;
                    double remainingGB = remaining / 1073741824.0;

                    txtRemainingData.Text = $"{remainingGB:0.##} GB";
                    txtTotalData.Text = $"Total: {totalGB:0.##} GB";

                    double usagePercent = (double)used / limit;
                    double remainingPercent = 1.0 - usagePercent;
                    if (remainingPercent < 0) remainingPercent = 0;

                    DrawCircularProgress(pathUsageArc, remainingPercent, "#3180e5");
                }

                if (status.Expire == null || status.Expire == 0)
                {
                    txtRemainingDays.Text = "∞";
                    txtExpireDate.Text = "No Expiry";
                    DrawCircularProgress(pathDaysArc, 1.0, "#5255ca");
                }
                else
                {
                    DateTime expireDate = DateTimeOffset.FromUnixTimeSeconds(status.Expire.Value).LocalDateTime;
                    txtExpireDate.Text = $"Expire: {expireDate:yyyy-MM-dd}";

                    TimeSpan left = expireDate - DateTime.Now;
                    int daysLeft = left.Days;
                    if (daysLeft < 0) daysLeft = 0;

                    txtRemainingDays.Text = daysLeft.ToString();

                    double daysPercent = (daysLeft > 30) ? 1.0 : (double)daysLeft / 30.0;
                    DrawCircularProgress(pathDaysArc, daysPercent, "#5255ca");
                }

                CheckForUpdate(status);
            });
        }

        private void DrawCircularProgress(System.Windows.Shapes.Path pathObj, double percentage, string colorHex)
        {
            if (percentage > 0.999) percentage = 0.9999;

            double angle = percentage * 360;
            double radius = 40;
            double startAngle = -90;
            double endAngle = startAngle + angle;

            Point startPoint = new Point(
                radius + radius * Math.Cos(startAngle * Math.PI / 180),
                radius + radius * Math.Sin(startAngle * Math.PI / 180));

            Point endPoint = new Point(
                radius + radius * Math.Cos(endAngle * Math.PI / 180),
                radius + radius * Math.Sin(endAngle * Math.PI / 180));

            bool isLargeArc = angle > 180;

            PathFigure figure = new PathFigure();
            figure.StartPoint = startPoint;
            figure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            pathObj.Data = geometry;
            pathObj.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private void CheckForUpdate(StatusResponse status)
        {
            if (status.NeedToUpdate)
            {
                popupContainer.Visibility = Visibility.Visible;
                if (status.IsIgnoreable)
                {
                    btnUpdateLater.Visibility = Visibility.Visible;
                }
                else
                {
                    btnUpdateLater.Visibility = Visibility.Collapsed;
                }
                MidPanelManager.Instance.ReportPromptSeen();
            }
            else
            {
                popupContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://sub.your-domain.com/download-latest", 
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnUpdateLater_Click(object sender, RoutedEventArgs e)
        {
            popupContainer.Visibility = Visibility.Collapsed;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            btnConnect.IsEnabled = false;
            
            try 
            {
                _isConnected = !_isConnected;
                UpdateConnectionUI(_isConnected);

                MidPanelService.Instance.Log("CONNECTION CLICK", $"User clicked connect. Target State: {(_isConnected ? "ON" : "OFF")}");

                if (_isConnected)
                {
                    MidPanelService.Instance.Log("CORE START", "Fetching default server...");
                    
                    var config = AppManager.Instance.Config;
                    var node = await ConfigHandler.GetDefaultServer(config);
                    
                    if (node == null)
                    {
                        MidPanelService.Instance.Log("CORE ERROR", "No default server selected!", true);
                        MessageBox.Show("Please select a server in settings first.");
                        _isConnected = false;
                        UpdateConnectionUI(false);
                        return;
                    }

                    MidPanelService.Instance.Log("CORE START", $"Starting core with server: {node.Remarks}");
                    await CoreManager.Instance.LoadCore(node);
                    MidPanelService.Instance.Log("CORE STARTED", "Core loaded successfully.");
                }
                else
                {
                    MidPanelService.Instance.Log("CORE STOP", "Stopping core...");
                    await CoreManager.Instance.CoreStop();
                    MidPanelService.Instance.Log("CORE STOPPED", "Core stopped successfully.");
                }
            }
            catch (Exception ex)
            {
                MidPanelService.Instance.Log("CONNECTION EXCEPTION", ex.ToString(), true);
                MessageBox.Show($"Connection Error: {ex.Message}");
                _isConnected = false;
                UpdateConnectionUI(false);
            }
            finally
            {
                btnConnect.IsEnabled = true;
            }
        }

        private void UpdateConnectionUI(bool connected)
        {
            var outerRing = btnConnect.Template.FindName("outerRing", btnConnect) as System.Windows.Shapes.Ellipse;
            var dropShadow = outerRing?.Effect as DropShadowEffect;

            if (connected)
            {
                txtConnectionState.Text = "CONNECTED";
                txtConnectionState.Foreground = new SolidColorBrush(Color.FromRgb(49, 128, 229));
                btnConnect.Opacity = 1.0;
                
                if (dropShadow != null) dropShadow.Color = Color.FromRgb(49, 128, 229);
            }
            else
            {
                txtConnectionState.Text = "DISCONNECTED";
                txtConnectionState.Foreground = Brushes.White;
                btnConnect.Opacity = 0.8;
                
                if (dropShadow != null) dropShadow.Color = Color.FromRgb(82, 85, 202); 
            }
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            await MidPanelManager.Instance.LogoutAsync();
        }

        private void OnLogoutTriggered()
        {
            Dispatcher.Invoke(() =>
            {
                new LoginWindow().Show();
                this.Close();
            });
        }
    }
}
