using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    public partial class NormalizeChatAdminsToOwners : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE conversation_participants
                SET role = 'Owner'
                WHERE role = 'Admin';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only cleanup is intentionally irreversible because Owner may have existed before this migration.
        }
    }
}
