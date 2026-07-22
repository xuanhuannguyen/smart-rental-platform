using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageFileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE chat_messages
                    ADD COLUMN IF NOT EXISTS file_content_type character varying(100),
                    ADD COLUMN IF NOT EXISTS file_name character varying(255),
                    ADD COLUMN IF NOT EXISTS file_size bigint,
                    ADD COLUMN IF NOT EXISTS file_url character varying(1000);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE chat_messages
                    DROP COLUMN IF EXISTS file_content_type,
                    DROP COLUMN IF EXISTS file_name,
                    DROP COLUMN IF EXISTS file_size,
                    DROP COLUMN IF EXISTS file_url;
                """);
        }
    }
}
