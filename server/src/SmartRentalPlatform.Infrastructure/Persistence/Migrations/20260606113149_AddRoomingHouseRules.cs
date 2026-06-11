using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomingHouseRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooming_house_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rooming_house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pdf_object_key = table.Column<string>(type: "text", nullable: false),
                    general_rules = table.Column<string>(type: "text", nullable: true),
                    quiet_hours = table.Column<string>(type: "text", nullable: true),
                    security_policy = table.Column<string>(type: "text", nullable: true),
                    cleaning_policy = table.Column<string>(type: "text", nullable: true),
                    guest_policy = table.Column<string>(type: "text", nullable: true),
                    parking_policy = table.Column<string>(type: "text", nullable: true),
                    utility_policy = table.Column<string>(type: "text", nullable: true),
                    damage_compensation_policy = table.Column<string>(type: "text", nullable: true),
                    additional_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooming_house_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooming_house_rules_rooming_houses_rooming_house_id",
                        column: x => x.rooming_house_id,
                        principalTable: "rooming_houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_rules_rooming_house_id",
                table: "rooming_house_rules",
                column: "rooming_house_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rooming_house_rules");
        }
    }
}
