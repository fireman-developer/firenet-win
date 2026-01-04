using System;
using System.IO;
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
        
        // آدرس سرور خود را اینجا وارد کنید
        private string _baseUrl = "https://report.soft99.sbs"; 

        private static MidPanelService _instance;
        public static MidPanelService Instance => _instance ??= new MidPanelService();

        public MidPanelService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(20); 
        }

        public void SetBaseUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _baseUrl = url.TrimEnd('/');
            }
        }

        public void SetToken(string token)
        {
            _jwtToken = token;
        }

        public string GetToken() => _jwtToken;

        private void SetupHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(_jwtToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
            }
        }

        // --- سیستم لاگ‌گیری اختصاصی ---
        private void Log(string title, string content, bool isError = false)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api_logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logFile = Path.Combine(logDir, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                string logContent = $"[{DateTime.Now:HH:mm:ss}] [{title.ToUpper()}] {(isError ? "[ERROR]" : "")}\n{content}\n--------------------------------------------------\n";
                
                File.AppendAllText(logFile, logContent);
            }
            catch (Exception) { /* نادیده گرفتن خطای لاگ */ }
        }

        public async Task<LoginResponse> LoginAsync(string username, string password, string deviceId, string appVersion)
        {
            var url = $"{_baseUrl}/api/login";
            var requestObj = new LoginRequest
            {
                Username = username,
                Password = password,
                DeviceId = deviceId,
                AppVersion = appVersion
            };

            var json = JsonSerializer.Serialize(requestObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Log("LOGIN REQUEST", $"URL: {url}\nPayload: {json}");

            _httpClient.DefaultRequestHeaders.Clear();

            try 
            {
                var response = await _httpClient.PostAsync(url, content);
                var respString = await response.Content.ReadAsStringAsync();
                
                Log("LOGIN RESPONSE", $"Status: {response.StatusCode}\nBody: {respString}", !response.IsSuccessStatusCode);

                response.EnsureSuccessStatusCode();

                var loginResp = JsonSerializer.Deserialize<LoginResponse>(respString);

                if (loginResp != null && !string.IsNullOrEmpty(loginResp.Token))
                {
                    _jwtToken = loginResp.Token;
                }

                return loginResp;
            }
            catch (Exception ex)
            {
                Log("LOGIN ERROR", ex.ToString(), true);
                throw new Exception($"Login failed: {ex.Message}");
            }
        }

        public async Task<StatusResponse> GetStatusAsync()
        {
            var url = $"{_baseUrl}/api/status";
            SetupHeaders();
            Log("STATUS REQUEST", $"URL: {url}\nToken: {_jwtToken}");

            try
            {
                var response = await _httpClient.GetAsync(url);
                var respString = await response.Content.ReadAsStringAsync();

                Log("STATUS RESPONSE", $"Status: {response.StatusCode}\nBody: {respString}", !response.IsSuccessStatusCode);

                response.EnsureSuccessStatusCode();
                return JsonSerializer.Deserialize<StatusResponse>(respString);
            }
            catch (Exception ex)
            {
                Log("STATUS ERROR", ex.ToString(), true);
                throw;
            }
        }

        public async Task<ApiResponse> KeepAliveAsync()
        {
            var url = $"{_baseUrl}/api/keepalive";
            SetupHeaders();
            // Log("KEEPALIVE", "Sending keepalive..."); // لاگ زیاد تولید نکند

            try
            {
                var content = new StringContent("{}", Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                var respString = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    Log("KEEPALIVE ERROR", $"Status: {response.StatusCode}\nBody: {respString}", true);
                }

                return JsonSerializer.Deserialize<ApiResponse>(respString);
            }
            catch (Exception ex)
            {
                Log("KEEPALIVE EXCEPTION", ex.ToString(), true);
                throw;
            }
        }

        public async Task<NotificationFetchResponse> FetchNotificationsAsync()
        {
            var url = $"{_baseUrl}/api/notifications/fetch";
            SetupHeaders();

            try 
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var respString = await response.Content.ReadAsStringAsync();
                // فقط اگر نوتیفیکیشن وجود داشت لاگ کن
                if (respString.Contains("\"notifications\":[{\"")) 
                {
                    Log("NOTIFICATION RECEIVED", respString);
                }
                
                return JsonSerializer.Deserialize<NotificationFetchResponse>(respString);
            }
            catch (Exception ex)
            {
                Log("NOTIFICATION ERROR", ex.Message, true);
                return null;
            }
        }

        public async Task<ApiResponse> UpdatePromptSeenAsync()
        {
            var url = $"{_baseUrl}/api/update-prompt-seen";
            SetupHeaders();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var respString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(respString);
        }

        public async Task<ApiResponse> ReportUpdateAsync(string newVersion)
        {
            var url = $"{_baseUrl}/api/report-update";
            SetupHeaders();
            var req = new ReportUpdateRequest { NewVersion = newVersion };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var respString = await response.Content.ReadAsStringAsync();
            Log("REPORT UPDATE", $"Ver: {newVersion}\nResponse: {respString}");
            return JsonSerializer.Deserialize<ApiResponse>(respString);
        }

        public async Task<ApiResponse> LogoutAsync()
        {
            var url = $"{_baseUrl}/api/logout";
            SetupHeaders();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            
            try 
            {
                Log("LOGOUT REQUEST", "User logging out");
                var response = await _httpClient.PostAsync(url, content);
                _jwtToken = null; 

                var respString = await response.Content.ReadAsStringAsync();
                Log("LOGOUT RESPONSE", respString);
                return JsonSerializer.Deserialize<ApiResponse>(respString);
            }
            catch (Exception ex)
            {
                Log("LOGOUT ERROR", ex.Message, true);
                _jwtToken = null;
                return new ApiResponse { Message = "Logged out locally" };
            }
        }
    }
}