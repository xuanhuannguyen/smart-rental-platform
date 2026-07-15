using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomingHouseReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_property_images_owner_exclusive",
                table: "property_images");

            migrationBuilder.AddColumn<double>(
                name: "average_rating",
                table: "rooming_houses",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "total_reviews",
                table: "rooming_houses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "rooming_house_review_id",
                table: "property_images",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rooming_house_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rooming_house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    landlord_reply = table.Column<string>(type: "text", nullable: true),
                    landlord_reply_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooming_house_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooming_house_reviews_contracts_rental_contract_id",
                        column: x => x.rental_contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rooming_house_reviews_rooming_houses_rooming_house_id",
                        column: x => x.rooming_house_id,
                        principalTable: "rooming_houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rooming_house_reviews_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rooming_house_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    admin_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_review_reports_rooming_house_reviews_rooming_house_review_id",
                        column: x => x.rooming_house_review_id,
                        principalTable: "rooming_house_reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_property_images_rooming_house_review_id",
                table: "property_images",
                column: "rooming_house_review_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_images_owner_exclusive",
                table: "property_images",
                sql: "(rooming_house_id IS NOT NULL AND room_id IS NULL AND rooming_house_review_id IS NULL) OR (rooming_house_id IS NULL AND room_id IS NOT NULL AND rooming_house_review_id IS NULL) OR (rooming_house_id IS NULL AND room_id IS NULL AND rooming_house_review_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_review_reports_reporter_user_id",
                table: "review_reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "uix_reports_review_reporter",
                table: "review_reports",
                columns: new[] { "rooming_house_review_id", "reporter_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_reviews_rooming_house_id",
                table: "rooming_house_reviews",
                column: "rooming_house_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_reviews_tenant_user_id",
                table: "rooming_house_reviews",
                column: "tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "uix_reviews_contract_tenant",
                table: "rooming_house_reviews",
                columns: new[] { "rental_contract_id", "tenant_user_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_property_images_rooming_house_reviews_rooming_house_review_~",
                table: "property_images",
                column: "rooming_house_review_id",
                principalTable: "rooming_house_reviews",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_property_images_rooming_house_reviews_rooming_house_review_~",
                table: "property_images");

            migrationBuilder.DropTable(
                name: "review_reports");

            migrationBuilder.DropTable(
                name: "rooming_house_reviews");

            migrationBuilder.DropIndex(
                name: "IX_property_images_rooming_house_review_id",
                table: "property_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_images_owner_exclusive",
                table: "property_images");

            migrationBuilder.DropColumn(
                name: "average_rating",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "total_reviews",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "rooming_house_review_id",
                table: "property_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_images_owner_exclusive",
                table: "property_images",
                sql: "(rooming_house_id IS NOT NULL AND room_id IS NULL) OR (rooming_house_id IS NULL AND room_id IS NOT NULL)");
        }
    }
}
