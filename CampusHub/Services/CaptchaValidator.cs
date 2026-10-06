using System.Text.Json.Serialization;

namespace campushub.Services
{
    public class CaptchaValidator
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public CaptchaValidator(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> VerifyTokenAsync(string token)
        {
            var client = _httpClientFactory.CreateClient();

            // Send as a form body so the values are URL-encoded and never logged as part of the URL
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _configuration["Captcha:SecretKey"] ?? string.Empty,
                ["response"] = token
            });
            var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<CaptchaResponse>();
            return result?.Success == true;
        }

        private class CaptchaResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("score")]
            public float Score { get; set; }

            [JsonPropertyName("action")]
            public string? Action { get; set; }

            [JsonPropertyName("challenge_ts")]
            public string? ChallengeTs { get; set; }

            [JsonPropertyName("hostname")]
            public string? Hostname { get; set; }
        }
    }
}
