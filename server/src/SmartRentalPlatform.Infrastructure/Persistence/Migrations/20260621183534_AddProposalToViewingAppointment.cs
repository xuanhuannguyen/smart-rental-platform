using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalToViewingAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE viewing_appointments
                ADD COLUMN IF NOT EXISTS proposed_duration_minutes integer;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE viewing_appointments
                ADD COLUMN IF NOT EXISTS proposed_scheduled_at timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE viewing_appointments
                DROP COLUMN IF EXISTS proposed_duration_minutes;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE viewing_appointments
                DROP COLUMN IF EXISTS proposed_scheduled_at;
                """);
        }
    }
}
