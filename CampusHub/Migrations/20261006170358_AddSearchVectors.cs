using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace campushub.Migrations
{
    /// <summary>
    /// Full-text search columns that SearchService queries but the 2025 migrations never created
    /// (the original "postygresDAta" migration only added the Posts trigger, which fails on insert
    /// without the column). Statements are idempotent so they also run on databases where these
    /// objects were created by hand.
    /// </summary>
    public partial class AddSearchVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pg_trgm provides the '%' similarity operator used for fuzzy matching
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql("""
                ALTER TABLE "Posts"  ADD COLUMN IF NOT EXISTS search_vector tsvector;
                ALTER TABLE "Groups" ADD COLUMN IF NOT EXISTS search_vector tsvector;
                ALTER TABLE "Users"  ADD COLUMN IF NOT EXISTS search_vector tsvector;
                """);

            // Posts already has the tsvectorupdate trigger from the postygresDAta migration.
            // Groups and Users get their own; e-mail is deliberately not indexed.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION groups_search_vector_trigger() RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('english',
                        coalesce(NEW."Name", '') || ' ' || coalesce(NEW."Description", ''));
                    RETURN NEW;
                END
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS groups_tsvectorupdate ON "Groups";
                CREATE TRIGGER groups_tsvectorupdate
                BEFORE INSERT OR UPDATE ON "Groups"
                FOR EACH ROW EXECUTE FUNCTION groups_search_vector_trigger();

                CREATE OR REPLACE FUNCTION users_search_vector_trigger() RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('simple',
                        coalesce(NEW."FirstName", '') || ' ' || coalesce(NEW."LastName", ''));
                    RETURN NEW;
                END
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS users_tsvectorupdate ON "Users";
                CREATE TRIGGER users_tsvectorupdate
                BEFORE INSERT OR UPDATE ON "Users"
                FOR EACH ROW EXECUTE FUNCTION users_search_vector_trigger();
                """);

            // Backfill existing rows (the BEFORE UPDATE triggers compute the value)
            migrationBuilder.Sql("""
                UPDATE "Posts"  SET "Id" = "Id";
                UPDATE "Groups" SET "Id" = "Id";
                UPDATE "Users"  SET "Id" = "Id";
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_posts_search_vector  ON "Posts"  USING GIN (search_vector);
                CREATE INDEX IF NOT EXISTS ix_groups_search_vector ON "Groups" USING GIN (search_vector);
                CREATE INDEX IF NOT EXISTS ix_users_search_vector  ON "Users"  USING GIN (search_vector);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS users_tsvectorupdate ON "Users";
                DROP TRIGGER IF EXISTS groups_tsvectorupdate ON "Groups";
                DROP FUNCTION IF EXISTS users_search_vector_trigger();
                DROP FUNCTION IF EXISTS groups_search_vector_trigger();
                DROP INDEX IF EXISTS ix_users_search_vector;
                DROP INDEX IF EXISTS ix_groups_search_vector;
                DROP INDEX IF EXISTS ix_posts_search_vector;
                ALTER TABLE "Users"  DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE "Groups" DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE "Posts"  DROP COLUMN IF EXISTS search_vector;
                """);
        }
    }
}
