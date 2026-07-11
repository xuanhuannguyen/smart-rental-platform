using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatarMediaAssetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "avatar_media_asset_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_avatar_media_asset_id",
                table: "users",
                column: "avatar_media_asset_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_media_assets_avatar_media_asset_id",
                table: "users",
                column: "avatar_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_media_assets_avatar_media_asset_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_avatar_media_asset_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatar_media_asset_id",
                table: "users");
        }
    }
}
