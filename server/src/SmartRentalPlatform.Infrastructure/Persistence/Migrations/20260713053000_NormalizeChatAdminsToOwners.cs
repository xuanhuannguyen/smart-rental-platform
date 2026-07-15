using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartRentalPlatform.Infrastructure.Persistence;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713053000_NormalizeChatAdminsToOwners")]
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
