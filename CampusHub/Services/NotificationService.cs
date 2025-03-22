using CampusHub.Hubs;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace CampusHub.Services
{
    public class NotificationService : BackgroundService
    {
        //private readonly string _connectionString = "Server=localhost;Database=CampusHub;port=5432;User id=postgres;password=YOUR_DB_PASSWORD;";
        private readonly string _connectionString = "Server=YOUR_DB_HOST;Database=campushub;port=5432;User id=postgres;password=YOUR_DB_PASSWORD;";
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IHubContext<NotificationHub> hubContext, ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(stoppingToken);

            using var command = new NpgsqlCommand("LISTEN new_notification;", connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("🔔 Listening for PostgreSQL notifications...");

            connection.Notification += async (sender, e) =>
            {
                _logger.LogInformation($"📩 New Notification Received: {e.Payload}");

                // ✅ Send to frontend using SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", e.Payload);
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                await connection.WaitAsync(stoppingToken);
            }
        }
    }
}