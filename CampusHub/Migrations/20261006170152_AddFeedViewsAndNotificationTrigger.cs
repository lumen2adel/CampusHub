using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace campushub.Migrations
{
    /// <summary>
    /// Database objects that were created by hand on the original 2025 server and never
    /// captured in migrations: the feed ranking views and the realtime notification trigger.
    /// Statements are idempotent so they also run where these objects were created by hand.
    /// </summary>
    /// <remarks>
    /// The views select <c>p.*</c>, which Postgres expands at creation time. If columns are
    /// added to "Posts" later, drop and re-create both views in that migration.
    /// </remarks>
    public partial class AddFeedViewsAndNotificationTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trending: engagement over the last 7 days with time decay ("gravity"),
            // so a fresh post with a few reactions can outrank an older popular one.
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW IF NOT EXISTS trending_posts AS
                SELECT p.*,
                       (p."LikeCount" + 2 * p."CommentCount" + 3 * p."ShareCount")
                       / POWER(EXTRACT(EPOCH FROM (now() - p."CreatedAt")) / 3600 + 2, 1.5) AS "Score"
                FROM "Posts" p
                WHERE p."CreatedAt" > now() - INTERVAL '7 days'
                  AND NOT p."IsFlagged"
                ORDER BY "Score" DESC
                LIMIT 50;
                """);

            // Hot: raw engagement in the last 24 hours.
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW IF NOT EXISTS hot_posts AS
                SELECT p.*,
                       (p."LikeCount" + 2 * p."CommentCount" + 3 * p."ShareCount")::double precision AS "Score"
                FROM "Posts" p
                WHERE p."CreatedAt" > now() - INTERVAL '24 hours'
                  AND NOT p."IsFlagged"
                ORDER BY "Score" DESC
                LIMIT 50;
                """);

            // REFRESH ... CONCURRENTLY (used by FeedRankingService) requires a unique index
            migrationBuilder.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS ix_trending_posts_id ON trending_posts ("Id");""");
            migrationBuilder.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS ix_hot_posts_id ON hot_posts ("Id");""");

            // Every new notification row is published on the 'new_notification' channel;
            // NotificationListenerService LISTENs and forwards it to the receiver over SignalR.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION notify_new_notification() RETURNS trigger AS $$
                BEGIN
                    PERFORM pg_notify('new_notification', json_build_object(
                        'Id', NEW."Id",
                        'ReceiverId', NEW."ReceiverId",
                        'SenderId', NEW."SenderId",
                        'Message', NEW."Message",
                        'Type', NEW."Type",
                        'PostId', NEW."PostId",
                        'CreatedAt', NEW."CreatedAt")::text);
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_notification_posts_groups_notify ON "Notification_Posts_Groups";
                CREATE TRIGGER trg_notification_posts_groups_notify
                AFTER INSERT ON "Notification_Posts_Groups"
                FOR EACH ROW EXECUTE FUNCTION notify_new_notification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS trg_notification_posts_groups_notify ON "Notification_Posts_Groups";""");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS notify_new_notification();");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS hot_posts;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS trending_posts;");
        }
    }
}
