using Npgsql;

namespace CampusHub.Services
{
    public class FeedRankingService : BackgroundService
    {
        private readonly string _connectionString;
        private readonly ILogger<FeedRankingService> _logger;

        public FeedRankingService(IConfiguration configuration, ILogger<FeedRankingService> logger)
        {
            _connectionString = configuration.GetConnectionString("DataContext")!;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("🔄 Refreshing Trending & Hot Posts...");

                try
                {
                    await using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync(stoppingToken);

                    await using var trendingCommand = new NpgsqlCommand("REFRESH MATERIALIZED VIEW CONCURRENTLY trending_posts;", connection);
                    await trendingCommand.ExecuteNonQueryAsync(stoppingToken);

                    await using var hotCommand = new NpgsqlCommand("REFRESH MATERIALIZED VIEW CONCURRENTLY hot_posts;", connection);
                    await hotCommand.ExecuteNonQueryAsync(stoppingToken);

                    _logger.LogInformation("✅ Trending & Hot Posts Updated!");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error refreshing views: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}