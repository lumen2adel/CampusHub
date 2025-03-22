using Npgsql;

namespace CampusHub.Services
{
    public class TrendingPostsService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        //private readonly string _connectionString = "Server=localhost;Database=CampusHub;port=5432;User id=postgres;password=YOUR_DB_PASSWORD;";
        private readonly string _connectionString = "Server=YOUR_DB_HOST;Database=campushub;port=5432;User id=postgres;password=YOUR_DB_PASSWORD;";

        public TrendingPostsService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RefreshTrendingPosts();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Refresh every 5 minutes
            }
        }

        private async Task RefreshTrendingPosts()
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand("REFRESH MATERIALIZED VIEW trending_posts;", connection);
                await command.ExecuteNonQueryAsync();

                Console.WriteLine("🔄 Trending Posts Updated!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error Refreshing Trending Posts: {ex.Message}");
            }
        }
    }
}