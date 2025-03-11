using AutoMapper;
using CampusHub.Classes.UserAccount;
using CampusHub.Data;
using CampusHub.Enums;
using CampusHub.Hubs;
using CampusHub.JwtServices;
using CampusHub.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusHub.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [EnableCors("dev")]
    public class AccountController : ControllerBase
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly DataContext _dataContext;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IMapper _mapper;
        private readonly EmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IHubContext<ChatHub> hubContext, DataContext dataContext, IJwtTokenGenerator jwtTokenGenerator, IMapper mapper, EmailService emailService, ILogger<AccountController> logger)
        {
            _dataContext = dataContext;
            _jwtTokenGenerator = jwtTokenGenerator;
            _mapper = mapper;
            _emailService = emailService;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Register registerDto)
        {
            if (registerDto.Email == null || await UserExists(registerDto.Email)) return BadRequest("Email already in use.");

            var verificationCode = new Random().Next(100000, 999999).ToString();
            var tempUser = new TempUser
            {
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Password = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                Email = registerDto.Email,
                VerificationCode = verificationCode,
                CodeSentAt = DateTime.UtcNow
            };

            await _dataContext.TempUsers.AddAsync(tempUser);
            await _dataContext.SaveChangesAsync();

            await _emailService.SendVerificationCode(registerDto.Email, verificationCode);

            return Ok("Verification code sent.");
        }

        [HttpPost("verifyCode")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDto verifyCodeDto)
        {
            var tempUser = await _dataContext.TempUsers.FirstOrDefaultAsync(u => u.Email == verifyCodeDto.Email && u.VerificationCode == verifyCodeDto.Code);

            if (tempUser == null || tempUser.CodeSentAt.AddDays(3) < DateTime.UtcNow)
                return BadRequest("Invalid or expired code.");

            var user = new AppUser
            {
                FirstName = tempUser.FirstName,
                LastName = tempUser.LastName,
                Password = tempUser.Password,
                Email = tempUser.Email,
                Role = tempUser.Role,
                UniqueIdentifier = Guid.NewGuid().ToString()
            };

            await _dataContext.Users.AddAsync(user);
            _dataContext.TempUsers.Remove(tempUser);
            await _dataContext.SaveChangesAsync();

            await _emailService.SendWelcomeMessage(user.Email, user.FirstName);

            return Ok("User registered successfully.");
        }


        [HttpPost("resendVerificationCode")]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendCodeDto resendCodeDto)
        {
            var tempUser = await _dataContext.TempUsers.FirstOrDefaultAsync(u => u.Email == resendCodeDto.Email);

            if (tempUser == null)
                return BadRequest("User not found.");

            var verificationCode = new Random().Next(100000, 999999).ToString();
            tempUser.VerificationCode = verificationCode;
            tempUser.CodeSentAt = DateTime.UtcNow;

            _dataContext.TempUsers.Update(tempUser);
            await _dataContext.SaveChangesAsync();

            await _emailService.ResendVerificationCode(resendCodeDto.Email, verificationCode);

            return Ok("Verification code resent.");
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Login loginDto)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                return BadRequest("Email and password are required.");

            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);
            if (user == null)
            {
                await _emailService.SendLoginWarningMessage(loginDto.Email);
                return BadRequest("Invalid email.");
            }

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password))
                return BadRequest("Invalid password.");

            user.UpdatedAt = DateTime.UtcNow;
            await _dataContext.SaveChangesAsync();

            var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Role, user.FirstName, user.LastName, user.Email);

            return Ok(new { Token = token });
        }

        private async Task<bool> UserExists(string email)
        {
            return await _dataContext.Users.AnyAsync(x => x.Email == email);
        }




        [HttpPost("recoverPassword")]
        public async Task<IActionResult> RecoverPassword([FromBody] PasswordRecoveryDto recoveryDto)
        {
            // Validate the provided email
            if (string.IsNullOrWhiteSpace(recoveryDto.Email))
                return BadRequest("Email is required.");

            // Find the user by email
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.Email == recoveryDto.Email);
            if (user == null)
                return NotFound("User not found.");

            // Generate a unique reset token
            var resetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                                      .Replace("=", "")
                                      .Replace("+", "")
                                      .Replace("/", "");

            // Update user entity with the reset token and expiry
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _dataContext.SaveChangesAsync();

            // Send recovery email
            await _emailService.SendPasswordRecoveryEmail(recoveryDto.Email, resetToken);

            return Ok("Password recovery email sent successfully.");
        }


        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            // Validate the provided token and new password
            if (string.IsNullOrWhiteSpace(resetPasswordDto.Token) || string.IsNullOrWhiteSpace(resetPasswordDto.NewPassword))
                return BadRequest("Token and new password are required.");

            // Find the user by reset token
            var user = await _dataContext.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == resetPasswordDto.Token);
            if (user == null)
                return BadRequest("Invalid token.");

            // Check if the reset token has expired
            if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
                return BadRequest("Token has expired.");

            // Hash the new password and update the user entity
            user.Password = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _dataContext.SaveChangesAsync();

            return Ok("Password has been reset successfully.");
        }


        [HttpGet("profile")]
        public async Task<IActionResult> UserProfile()
        {
            // Extract the user ID from the token
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            // Validate the extracted user ID
            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            // Fetch the user from the database
            var user = await _dataContext.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found.");

            // Return user profile data
            return Ok(new
            {
                FullName = $"{user.FirstName} {user.LastName}",
                user.Email,
                Role = user.Role.ToString(),
                user.UniqueIdentifier
            });
        }


        [HttpPost("sendFriendRequest")]
        public async Task<IActionResult> SendFriendRequest([FromBody] Guid receiverId)
        {
            var senderIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(senderIdString, out Guid senderId))
                return BadRequest("Invalid User ID format.");

            if (senderId == receiverId)
                return BadRequest("You cannot send a friend request to yourself.");

            var receiver = await _dataContext.Users.FindAsync(receiverId);
            if (receiver == null)
                return NotFound("Receiver not found.");

            var existingRequest = await _dataContext.FriendRequests
                .FirstOrDefaultAsync(fr => fr.SenderId == senderId && fr.ReceiverId == receiverId);

            if (existingRequest != null)
            {
                if (existingRequest.Status == FriendRequestStatus.Rejected)
                {
                    // Reset and resend the friend request
                    existingRequest.Status = FriendRequestStatus.Pending;
                    existingRequest.CreatedAt = DateTime.UtcNow; // Update timestamp
                    await _dataContext.SaveChangesAsync();

                    // ✅ Move SignalR Notification **after** database update
                    await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveFriendRequestNotification", new
                    {
                        SenderName = $"{User.FindFirst(ClaimTypes.GivenName)?.Value} {User.FindFirst(ClaimTypes.Surname)?.Value}"
                    });

                    return Ok("Friend request resent successfully.");
                }

                return BadRequest("Friend request already sent.");
            }

            var friendRequest = new FriendRequest
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = FriendRequestStatus.Pending
            };

            _dataContext.FriendRequests.Add(friendRequest);
            await _dataContext.SaveChangesAsync();

            // ✅ Move SignalR Notification **after** database update
            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveFriendRequestNotification", new
            {
                SenderName = $"{User.FindFirst(ClaimTypes.GivenName)?.Value} {User.FindFirst(ClaimTypes.Surname)?.Value}"
            });

            return Ok("Friend request sent successfully.");
        }




        [HttpGet("friendRequests")]
        public async Task<IActionResult> GetFriendRequests()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var incomingRequests = await _dataContext.FriendRequests
                .Where(fr => fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Pending)
                .Select(fr => new
                {
                    fr.Id,
                    SenderId = fr.SenderId,
                    SenderName = $"{fr.Sender.FirstName} {fr.Sender.LastName}",
                    fr.CreatedAt
                })
                .ToListAsync();

            var outgoingRequests = await _dataContext.FriendRequests
                .Where(fr => fr.SenderId == userId && fr.Status == FriendRequestStatus.Pending)
                .Select(fr => new
                {
                    fr.Id,
                    ReceiverId = fr.ReceiverId,
                    ReceiverName = $"{fr.Receiver.FirstName} {fr.Receiver.LastName}",
                    fr.CreatedAt
                })
                .ToListAsync();

            return Ok(new { IncomingRequests = incomingRequests, OutgoingRequests = outgoingRequests });
        }

        [HttpPost("respondFriendRequest")]
        public async Task<IActionResult> RespondFriendRequest([FromBody] FriendRequestResponseDto responseDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var friendRequest = await _dataContext.FriendRequests
                .FirstOrDefaultAsync(fr => fr.Id == responseDto.RequestId && fr.ReceiverId == userId);

            if (friendRequest == null)
                return NotFound("Friend request not found.");

            if (responseDto.Accept)
            {
                friendRequest.Status = FriendRequestStatus.Accepted;
            }
            else
            {
                _dataContext.FriendRequests.Remove(friendRequest); // Remove request on rejection
            }
            _dataContext.SaveChangesAsync();
            return Ok("Friend request response recorded.");
        }

        [HttpDelete("deleteFriendship/{friendId}")]
        public async Task<IActionResult> DeleteFriendship(Guid friendId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var friendship = await _dataContext.FriendRequests
                .FirstOrDefaultAsync(fr =>
                    (fr.SenderId == userId && fr.ReceiverId == friendId && fr.Status == FriendRequestStatus.Accepted) ||
                    (fr.SenderId == friendId && fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Accepted));

            if (friendship == null)
                return NotFound("Friendship not found.");

            _dataContext.FriendRequests.Remove(friendship);
            await _dataContext.SaveChangesAsync();

            return Ok("Friendship deleted successfully.");
        }


        [HttpGet("friends")]
        public async Task<IActionResult> GetFriends([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            if (pageNumber < 1 || pageSize < 1)
                return BadRequest("Page number and page size must be greater than 0.");

            // Query friendships where the user is either sender or receiver and the request is accepted
            var friendsQuery = _dataContext.FriendRequests
                .Where(fr =>
                    (fr.SenderId == userId || fr.ReceiverId == userId) &&
                    fr.Status == FriendRequestStatus.Accepted)
                .Select(fr => new
                {
                    FriendId = fr.SenderId == userId ? fr.ReceiverId : fr.SenderId, // Get the friend's ID
                    FriendName = fr.SenderId == userId
                        ? $"{fr.Receiver.FirstName} {fr.Receiver.LastName}"
                        : $"{fr.Sender.FirstName} {fr.Sender.LastName}",
                    fr.CreatedAt
                });

            // Pagination
            var totalFriends = await friendsQuery.CountAsync();
            var friendsList = await friendsQuery
                .OrderBy(fr => fr.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalFriends = totalFriends,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Friends = friendsList
            });
        }

    }
}
