using Microsoft.Extensions.Hosting;
using Npgsql;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using CampusHub.Hubs;

namespace CampusHub.Services
{
    public class NotificationListenerService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationListenerService(IConfiguration configuration, IHubContext<NotificationHub> hubContext)
        {
            _configuration = configuration;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connString = _configuration.GetConnectionString("DataContext");

            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync(stoppingToken);
            connection.Notification += async (_, e) =>
            {
                try
                {
                    var notificationData = JsonSerializer.Deserialize<NotificationPayload>(e.Payload);
                    if (notificationData != null)
                    {
                        // Emit real-time notification via SignalR to the specific user
                        await _hubContext.Clients
                            .User(notificationData.ReceiverId)
                            .SendAsync("ReceiveNotification", notificationData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] NotificationListener: {ex.Message}");
                }
            };

            await using (var cmd = new NpgsqlCommand("LISTEN new_notification;", connection))
            {
                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }

            Console.WriteLine("✅ NotificationListenerService: Listening to 'new_notification' channel...");

            while (!stoppingToken.IsCancellationRequested)
            {
                await connection.WaitAsync(stoppingToken);
            }
        }

        private record NotificationPayload
        {
            public Guid Id { get; init; }
            public string ReceiverId { get; init; } = string.Empty;
            public string SenderId { get; init; } = string.Empty;
            public string Message { get; init; } = string.Empty;
            public string Type { get; init; } = string.Empty;
            public Guid PostId { get; init; }
            public DateTime CreatedAt { get; init; }
        }
    }
}
