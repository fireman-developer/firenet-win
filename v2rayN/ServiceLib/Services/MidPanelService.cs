using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ServiceLib.Models;

namespace ServiceLib.Services
{
    public class MidPanelService
    {
        private readonly HttpClient _httpClient;
        private string _jwtToken;
        
        // آدرس پیش‌فرض سرور (قابل تغییر توسط کاربر در تنظیمات)
        private string _baseUrl = "http://your-server:5000"; 

        private static MidPanelService _instance;
        public static MidPanelService Instance => _instance ??= new MidPanelService();

        public MidPanelService()
        {
            _httpClient = new HttpClient();
            // تایم‌اوت ۱۰ ثانیه برای جلوگیری از هنگ کردن برنامه
            _httpClient.Timeout = TimeSpan.FromSeconds(10); 
        }

        /// <summary>
        /// تنظیم آدرس سرور
        /// </summary>
        public void SetBaseUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _baseUrl = url.TrimEnd('/');
            }
        }

        /// <summary>
        /// تنظیم دستی توکن (مثلاً هنگام لود شدن برنامه از تنظیمات ذخیره شده)
        /// </summary>
        public void SetToken(string token)
        {
            _jwtToken = token;
        }

        public string GetToken() => _jwtToken;

        /// <summary>
        /// افزودن توکن به هدر درخواست‌ها
        /// </summary>
        private void SetupHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(_jwtToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
            }
        }

        /// <summary>
        /// لاگین کاربر
        /// Endpoint: POST /api/login
        /// </summary>
        public async Task<LoginResponse> LoginAsync(string username, string password, string deviceId, string appVersion)
        {
            var requestObj = new LoginRequest
            {
                Username = username,
                Password = password,
                DeviceId = deviceId,
                AppVersion = appVersion
            };

            var json = JsonSerializer.Serialize(requestObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // لاگین نیاز به توکن ندارد
            _httpClient.DefaultRequestHeaders.Clear();

            try 
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/login", content);
                response.EnsureSuccessStatusCode();

                var respString = await response.Content.ReadAsStringAsync();
                var loginResp = JsonSerializer.Deserialize<LoginResponse>(respString);

                if (loginResp != null && !string.IsNullOrEmpty(loginResp.Token))
                {
                    _jwtToken = loginResp.Token;
                }

                return loginResp;
            }
            catch (Exception ex)
            {
                // مدیریت خطا در لایه فراخوان انجام می‌شود
                throw new Exception($"Login failed: {ex.Message}");
            }
        }

        /// <summary>
        /// دریافت وضعیت کاربر
        /// Endpoint: GET /api/status
        /// </summary>
        public async Task<StatusResponse> GetStatusAsync()
        {
            SetupHeaders();
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/status");
            response.EnsureSuccessStatusCode();

            var respString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<StatusResponse>(respString);
        }

        /// <summary>
        /// تمدید نشست
        /// Endpoint: POST /api/keepalive
        /// </summary>
        public async Task<ApiResponse> KeepAliveAsync()
        {
            SetupHeaders();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/keepalive", content);
            
            if (!response.IsSuccessStatusCode) return null;

            var respString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(respString);
        }

        /// <summary>
        /// آپدیت توکن نوتیفیکیشن
        /// Endpoint: POST /api/update-fcm-token
        /// </summary>
        public async Task<ApiResponse> UpdateFcmTokenAsync(string fcmToken)
        {
            SetupHeaders();
            var req = new FcmTokenRequest { FcmToken = fcmToken };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/update-fcm-token", content);
            response.EnsureSuccessStatusCode();

            var respString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(respString);
        }

        /// <summary>
        /// دریافت نوتیفیکیشن‌های جدید (Polling)
        /// Endpoint: GET /api/notifications/fetch
        /// </summary>
        public async Task<NotificationFetchResponse> FetchNotificationsAsync()
        {
            SetupHeaders();
            try 
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/notifications/fetch");
                if (!response.IsSuccessStatusCode) return null;

                var respString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<NotificationFetchResponse>(respString);
            }
            catch
            {
                return null; // خطا در دریافت نوتیفیکیشن نباید برنامه را متوقف کند
            }
        }

        /// <summary>
        /// گزارش مشاهده پاپ‌آپ آپدیت
        /// Endpoint: POST /api/update-prompt-seen
        /// </summary>
        public async Task<ApiResponse> UpdatePromptSeenAsync()
        {
            SetupHeaders();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/update-prompt-seen", content);
            response.EnsureSuccessStatusCode();

            var respString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(respString);
        }

        /// <summary>
        /// گزارش آپدیت موفقیت‌آمیز
        /// Endpoint: POST /api/report-update
        /// </summary>
        public async Task<ApiResponse> ReportUpdateAsync(string newVersion)
        {
            SetupHeaders();
            var req = new ReportUpdateRequest { NewVersion = newVersion };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/report-update", content);
            response.EnsureSuccessStatusCode();

            var respString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(respString);
        }

        /// <summary>
        /// خروج از حساب
        /// Endpoint: POST /api/logout
        /// </summary>
        public async Task<ApiResponse> LogoutAsync()
        {
            SetupHeaders();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            
            try 
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/logout", content);
                // چه موفق باشد چه نباشد، توکن سمت کلاینت باید پاک شود
                _jwtToken = null; 

                if (!response.IsSuccessStatusCode) return null;

                var respString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponse>(respString);
            }
            catch
            {
                _jwtToken = null;
                return new ApiResponse { Message = "Logged out locally" };
            }
        }
    }
}