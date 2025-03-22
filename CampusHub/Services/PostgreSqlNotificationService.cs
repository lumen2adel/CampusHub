using Npgsql;

namespace CampusHub.Services
{
    public class PostgreSqlNotificationService : BackgroundService
    {
        //private readonly string _connectionString = "Server=localhost;Database=CampusHub;port=5432;User id=postgres;password=YOUR_DB_PASSWORD;";
        private readonly string _connectionString = "Server=YOUR_DB_HOST;Database=campushub;port=5432;User id=postgres;password=YOUR_DB_PASSWORD;";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(stoppingToken);

            using var command = new NpgsqlCommand("LISTEN new_notification;", connection);
            command.ExecuteNonQuery();

            Console.WriteLine("🔔 Listening for PostgreSQL notifications...");

            connection.Notification += async (sender, e) =>
            {
                Console.WriteLine($"📩 New Notification Received: {e.Payload}");

                // You can broadcast this to your frontend using SignalR or WebSockets
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                await connection.WaitAsync(stoppingToken);
            }
        }
    }
}