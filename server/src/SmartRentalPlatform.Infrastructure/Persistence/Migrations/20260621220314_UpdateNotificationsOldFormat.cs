using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNotificationsOldFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE notifications SET body = REPLACE(body, 'nADà26', 'ngày') WHERE body LIKE '%nADà26%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE notifications SET body = REPLACE(body, 'ngày', 'nADà26') WHERE body LIKE '%ngày%';");
        }
    }
}
