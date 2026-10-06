namespace campushub.Services
{
    /// <summary>Verifies a reCAPTCHA token with Google. Abstracted so tests do not call the real service.</summary>
    public interface ICaptchaValidator
    {
        Task<bool> VerifyTokenAsync(string token);
    }
}
