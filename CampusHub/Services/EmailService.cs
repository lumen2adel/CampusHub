using CampusHub.Classes.UserAccount;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace CampusHub.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        private SmtpClient CreateSmtpClient()
        {
            return new SmtpClient(_emailSettings.Host)
            {
                Port = _emailSettings.Port,
                Credentials = new System.Net.NetworkCredential(_emailSettings.Email, _emailSettings.Password),
                EnableSsl = true,
            };
        }

        // Centralized method to generate email bodies
        private string GenerateEmailBody(string content)
        {
            return $@"
            <html>
            <body>
                {content}
                <br>
                <p>Best regards,</p>
                <p>Laith</p>
                <br>
                <p>For any support, contact us at: <a href='mailto:support@yourdomain.com'>support@yourdomain.com</a></p>
                <p>Visit our website: <a href='https://www.yourdomain.com'>www.yourdomain.com</a></p>
            </body>
            </html>";
        }

        // Centralized method to send emails
        private async Task SendEmail(string email, string subject, string body)
        {
            var smtpClient = CreateSmtpClient();

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.Email, _emailSettings.DisplayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
        }

        public async Task SendWelcomeMessage(string email, string firstName)
        {
            var body = GenerateEmailBody($@"
                <p>Dear {firstName},</p>
                <p>Welcome to our app! We're excited to have you on board.</p>
                <p>Feel free to explore and let us know if you have any questions or need any assistance.</p>");
            await SendEmail(email, "Welcome to Our App", body);
        }

        public async Task SendLoginWarningMessage(string email)
        {
            var body = GenerateEmailBody($@"
                <p>Dear User,</p>
                <p>Someone tried to use your email to log in to our app, but you don't have an account with us.</p>
                <p>If this wasn't you, please make sure your email and passwords are secure.</p>");
            await SendEmail(email, "Unsuccessful Login Attempt", body);
        }

        public async Task SendVerificationCode(string email, string code)
        {
            var body = GenerateEmailBody($@"
                <p>Dear User,</p>
                <p>Thank you for registering with us. Your verification code is:</p>
                <h2>{code}</h2>
                <p>Please enter this code to complete your registration.</p>
                <p><strong>Do not share this code with anyone.</strong> If you did not request this code, you can safely ignore this email.</p>");
            await SendEmail(email, "Your Verification Code", body);
        }

        public async Task ResendVerificationCode(string email, string code)
        {
            var body = GenerateEmailBody($@"
                <p>Dear User,</p>
                <p>As requested, we are resending your verification code:</p>
                <h2>{code}</h2>
                <p>Please enter this code to complete your registration.</p>
                <p><strong>Do not share this code with anyone.</strong> If you did not request this code, you can safely ignore this email.</p>");
            await SendEmail(email, "Resend Verification Code", body);
        }

        public async Task SendPasswordRecoveryEmail(string email, string token)
        {
            var body = GenerateEmailBody($@"
                <p>Dear User,</p>
                <p>We received a request to reset your password. Please use the following token to reset it:</p>
                <h2>{token}</h2>
                <p>If you did not request this, please ignore this email.</p>");
            await SendEmail(email, "Password Recovery Request", body);
        }
    }
}













//using CampusHub.Classes.UserAccount;
//using Microsoft.Extensions.Options;
//using System.Net.Mail;

//namespace CampusHub.Services
//{
//    public class EmailService
//    {
//        private readonly EmailSettings _emailSettings;

//        public EmailService(IOptions<EmailSettings> emailSettings)
//        {
//            _emailSettings = emailSettings.Value;
//        }

//        private SmtpClient CreateSmtpClient()
//        {
//            return new SmtpClient(_emailSettings.Host)
//            {
//                Port = _emailSettings.Port,
//                Credentials = new System.Net.NetworkCredential(_emailSettings.Email, _emailSettings.Password),
//                EnableSsl = true,
//            };
//        }
//        public async Task SendWelcomeMessage(string email, string firstName)
//        {
//            var smtpClient = CreateSmtpClient();

//            var mailMessage = new MailMessage
//            {
//                From = new MailAddress(_emailSettings.Email, _emailSettings.DisplayName),
//                Subject = "Welcome to Our App",
//                Body = $@"
//            <html>
//            <body>
//                <p>Dear {firstName},</p>
//                <p>Welcome to our app! We're excited to have you on board.</p>
//                <p>Feel free to explore and let us know if you have any questions or need any assistance.</p>
//                <p>Best regards,</p>
//                <p>Laith</p>
//                <br>
//                <p>For any support, contact us at: <a href='mailto:support@yourdomain.com'>support@yourdomain.com</a></p>
//                <p>Visit our website: <a href='https://www.yourdomain.com'>www.yourdomain.com</a></p>
//            </body>
//            </html>",
//                IsBodyHtml = true,
//            };
//            mailMessage.To.Add(email);

//            await smtpClient.SendMailAsync(mailMessage);
//        }

//        public async Task SendLoginWarningMessage(string email)
//        {
//            var smtpClient = CreateSmtpClient();

//            var mailMessage = new MailMessage
//            {
//                From = new MailAddress(_emailSettings.Email, _emailSettings.DisplayName),
//                Subject = "Unsuccessful Login Attempt",
//                Body = $@"
//    <html>
//    <body>
//        <p>Dear User,</p>
//        <p>Someone tried to use your email to log in to our app, but you don't have an account with us.</p>
//        <p>If this wasn't you, please make sure your email and passwords are secure.</p>
//        <p>Best regards,</p>
//        <p>Laith</p>
//        <br>
//        <p>For any support, contact us at: <a href='mailto:support@yourdomain.com'>support@yourdomain.com</a></p>
//        <p>Visit our website: <a href='https://www.yourdomain.com'>www.yourdomain.com</a></p>
//    </body>
//    </html>",
//                IsBodyHtml = true,
//            };
//            mailMessage.To.Add(email);

//            await smtpClient.SendMailAsync(mailMessage);
//        }

//        public async Task SendVerificationCode(string email, string code)
//        {
//            var smtpClient = CreateSmtpClient();

//            var mailMessage = new MailMessage
//            {
//                From = new MailAddress(_emailSettings.Email, _emailSettings.DisplayName),
//                Subject = "Your Verification Code",
//                Body = $@"
//    <html>
//    <body>
//        <p>Dear User,</p>
//        <p>Thank you for registering with us. Your verification code is:</p>
//        <h2>{code}</h2>
//        <p>Please enter this code to complete your registration.</p>
//        <p><strong>Do not share this code with anyone.</strong> If you did not request this code, you can safely ignore this email.</p>
//        <br>
//        <p>Best regards,</p>
//        <p>Laith</p>
//        <br>
//        <p>For any support, contact us at: <a href='mailto:support@yourdomain.com'>support@yourdomain.com</a></p>
//        <p>Visit our website: <a href='https://www.yourdomain.com'>www.yourdomain.com</a></p>
//    </body>
//    </html>",
//                IsBodyHtml = true,
//            };
//            mailMessage.To.Add(email);

//            await smtpClient.SendMailAsync(mailMessage);
//        }


//        public async Task ResendVerificationCode(string email, string code)
//        {
//            var smtpClient = CreateSmtpClient();

//            var mailMessage = new MailMessage
//            {
//                From = new MailAddress(_emailSettings.Email, _emailSettings.DisplayName),
//                Subject = "Resend Verification Code",
//                Body = $@"
//            <html>
//            <body>
//                <p>Dear User,</p>
//                <p>As requested, we are resending your verification code:</p>
//                <h2>{code}</h2>
//                <p>Please enter this code to complete your registration.</p>
//                <p><strong>Do not share this code with anyone.</strong> If you did not request this code, you can safely ignore this email.</p>
//                <br>
//                <p>Best regards,</p>
//                <p>Laith</p>
//                <br>
//                <p>For any support, contact us at: <a href='mailto:support@yourdomain.com'>support@yourdomain.com</a></p>
//                <p>Visit our website: <a href='https://www.yourdomain.com'>www.yourdomain.com</a></p>
//            </body>
//            </html>",
//                IsBodyHtml = true,
//            };
//            mailMessage.To.Add(email);

//            await smtpClient.SendMailAsync(mailMessage);
//        }
//    }
//}