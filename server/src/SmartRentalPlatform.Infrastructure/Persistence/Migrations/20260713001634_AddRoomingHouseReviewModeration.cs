using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomingHouseReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "admin_note",
                table: "rooming_house_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "admin_reviewed_at",
                table: "rooming_house_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_moderation_categories",
                table: "rooming_house_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_moderation_json",
                table: "rooming_house_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_moderation_provider",
                table: "rooming_house_reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_moderation_risk_level",
                table: "rooming_house_reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ai_reviewed_at",
                table: "rooming_house_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderation_reason",
                table: "rooming_house_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "rooming_house_reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_admin_id",
                table: "rooming_house_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE rooming_house_reviews
                SET moderation_status = 'Rejected',
                    moderation_reason = COALESCE(moderation_reason, 'Đánh giá đã bị ẩn trước khi thêm luồng kiểm duyệt.')
                WHERE is_hidden = TRUE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_reviews_reviewed_by_admin_id",
                table: "rooming_house_reviews",
                column: "reviewed_by_admin_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rooming_house_reviews_users_reviewed_by_admin_id",
                table: "rooming_house_reviews",
                column: "reviewed_by_admin_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_reviews_users_reviewed_by_admin_id",
                table: "rooming_house_reviews");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_reviews_reviewed_by_admin_id",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "admin_note",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "admin_reviewed_at",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "ai_moderation_categories",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "ai_moderation_json",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "ai_moderation_provider",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "ai_moderation_risk_level",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "ai_reviewed_at",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "moderation_reason",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "rooming_house_reviews");

            migrationBuilder.DropColumn(
                name: "reviewed_by_admin_id",
                table: "rooming_house_reviews");
        }
    }
}
