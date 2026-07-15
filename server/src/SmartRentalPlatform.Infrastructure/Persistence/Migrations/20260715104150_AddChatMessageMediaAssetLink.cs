using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageMediaAssetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "avatar_media_asset_id",
                table: "conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "chat_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_conversations_avatar_media_asset_id",
                table: "conversations",
                column: "avatar_media_asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_chat_messages_media_asset_id",
                table: "chat_messages",
                column: "media_asset_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_media_assets_media_asset_id",
                table: "chat_messages",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_media_assets_avatar_media_asset_id",
                table: "conversations",
                column: "avatar_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE media_assets
                SET visibility = 'Public',
                    updated_at = CURRENT_TIMESTAMP
                WHERE scope = 'RoomingHouseRulePdf'
                  AND status <> 'Deleted';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE media_assets
                SET visibility = 'Private',
                    updated_at = CURRENT_TIMESTAMP
                WHERE scope = 'RoomingHouseRulePdf'
                  AND status <> 'Deleted';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_media_assets_media_asset_id",
                table: "chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_conversations_media_assets_avatar_media_asset_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ux_conversations_avatar_media_asset_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ux_chat_messages_media_asset_id",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "avatar_media_asset_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "chat_messages");
        }
    }
}
