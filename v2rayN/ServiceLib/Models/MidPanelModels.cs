using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ServiceLib.Models
{
    /// <summary>
    /// مدل درخواست لاگین
    /// Endpoint: POST /api/login
    /// </summary>
    public class LoginRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; }
    }

    /// <summary>
    /// مدل پاسخ لاگین
    /// </summary>
    public class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    /// <summary>
    /// مدل پاسخ دریافت وضعیت کاربر
    /// Endpoint: GET /api/status
    /// </summary>
    public class StatusResponse
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("used_traffic")]
        public long UsedTraffic { get; set; }

        [JsonPropertyName("data_limit")]
        public long DataLimit { get; set; }

        [JsonPropertyName("expire")]
        public long Expire { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("links")]
        public List<string> Links { get; set; }

        // طبق داکیومنت، این مقادیر به صورت رشته "true" یا "false" ارسال می‌شوند
        [JsonPropertyName("need_to_update")]
        public object NeedToUpdateRaw { get; set; }

        [JsonPropertyName("is_ignoreable")]
        public object IsIgnoreableRaw { get; set; }

        // تبدیل خودکار به Boolean برای استفاده راحت‌تر در برنامه
        [JsonIgnore]
        public bool NeedToUpdate => NeedToUpdateRaw?.ToString()?.ToLower() == "true";

        [JsonIgnore]
        public bool IsIgnoreable => IsIgnoreableRaw?.ToString()?.ToLower() == "true";
    }

    /// <summary>
    /// مدل عمومی برای پیام‌های ساده (مثل KeepAlive یا خروج)
    /// </summary>
    public class ApiResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// مدل درخواست آپدیت توکن FCM
    /// Endpoint: POST /api/update-fcm-token
    /// </summary>
    public class FcmTokenRequest
    {
        [JsonPropertyName("fcm_token")]
        public string FcmToken { get; set; }
    }

    /// <summary>
    /// مدل دریافت نوتیفیکیشن‌ها
    /// Endpoint: GET /api/notifications/fetch
    /// </summary>
    public class NotificationFetchResponse
    {
        [JsonPropertyName("notifications")]
        public List<NotificationItem> Notifications { get; set; }

        [JsonPropertyName("check_time")]
        public string CheckTime { get; set; }
    }

    public class NotificationItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// مدل گزارش آپدیت موفق
    /// Endpoint: POST /api/report-update
    /// </summary>
    public class ReportUpdateRequest
    {
        [JsonPropertyName("new_version")]
        public string NewVersion { get; set; }
    }
}