namespace CampusHub.Classes.UserAccount
{
    public class VerifyCodeDto
    {
        public string Email { get; set; }
        public string Code { get; set; }
    }
    public class ResendCodeDto
    {
        public string Email { get; set; }
    }
}
