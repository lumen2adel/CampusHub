using Newtonsoft.Json;

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
            var secret = _configuration["Captcha:SecretKey"];
            var client = _httpClientFactory.CreateClient();

            var response = await client.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={token}",
                null
            );

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<CaptchaResponse>(json);
            return result?.Success == true;
        }

        private class CaptchaResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("score")]
            public float Score { get; set; }

            [JsonProperty("action")]
            public string Action { get; set; }

            [JsonProperty("challenge_ts")]
            public string ChallengeTs { get; set; }

            [JsonProperty("hostname")]
            public string Hostname { get; set; }
        }
    }
}