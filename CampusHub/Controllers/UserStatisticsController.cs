using CampusHub.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace CampusHub.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class UserStatisticsController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private static readonly ConcurrentDictionary<string, DateTime> ActiveUsers = new(); // Key: UserId, Value: LastActivityTime

        public UserStatisticsController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        [HttpPost("updateActivity")] // Update user activity
        public IActionResult UpdateActivity([FromBody] string userId)
        {
            ActiveUsers[userId] = DateTime.UtcNow; // Update last activity time
            return Ok("User activity updated.");
        }

        [HttpPost("removeActivity")] // Remove user activity
        public IActionResult RemoveActivity([FromBody] string userId)
        {
            ActiveUsers.TryRemove(userId, out _);
            return Ok("User removed from active list.");
        }

        [HttpGet("getUserStatistics")] // Endpoint to fetch user statistics
        public async Task<IActionResult> GetUserStatistics()
        {
            // Remove users who haven't been active for 1 minutes
            var threshold = DateTime.UtcNow.AddMinutes(-1);
            foreach (var user in ActiveUsers.Where(u => u.Value < threshold).Select(u => u.Key).ToList())
            {
                ActiveUsers.TryRemove(user, out _);
            }

            var totalUsers = await _dataContext.Users.CountAsync();
            var activeUsersCount = ActiveUsers.Count;

            return Ok(new
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsersCount
            });
        }
    }
}
