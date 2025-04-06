using CampusHub.Data;

namespace campushub.Services
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public RefreshTokenCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();

                var cutoff = DateTime.UtcNow.AddDays(-1);
                var oldTokens = db.RefreshTokens
                    .Where(t => t.ExpiresAt < DateTime.UtcNow || t.RevokedAt != null)
                    .ToList();

                db.RefreshTokens.RemoveRange(oldTokens);
                await db.SaveChangesAsync();

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken); // run every 6 hrs
            }
        }
    }

}
