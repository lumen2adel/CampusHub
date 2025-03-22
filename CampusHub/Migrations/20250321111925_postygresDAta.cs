using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace campushub.Migrations
{
    /// <inheritdoc />
    public partial class postygresDAta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add search_vector column to Posts

            // Create trigger function to automatically update search_vector
            migrationBuilder.Sql(@"
        CREATE FUNCTION posts_search_vector_trigger() RETURNS trigger AS $$
        BEGIN
          NEW.search_vector :=
             to_tsvector('english', coalesce(NEW.""Description"", ''));
          RETURN NEW;
        END
        $$ LANGUAGE plpgsql;
    ");

            // Attach the trigger to the Posts table
            migrationBuilder.Sql(@"
        CREATE TRIGGER tsvectorupdate
        BEFORE INSERT OR UPDATE ON ""Posts""
        FOR EACH ROW EXECUTE FUNCTION posts_search_vector_trigger();
    ");

            // Create GIN index for faster search
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS tsvectorupdate ON ""Posts"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS posts_search_vector_trigger();");
            migrationBuilder.Sql(@"ALTER TABLE ""Posts"" DROP COLUMN IF EXISTS ""search_vector"";");
        }

    }
}
