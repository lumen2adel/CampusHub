namespace CampusHub.JwtServices
{
    public class JwtConfig
    {
        public string Secret { get; set; } = null!;
        public int ExpireDays { get; set; } = 1;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
    }
}
