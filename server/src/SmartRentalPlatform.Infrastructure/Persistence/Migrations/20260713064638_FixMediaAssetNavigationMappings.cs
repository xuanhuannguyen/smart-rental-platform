using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixMediaAssetNavigationMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "rooming_house_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "back_media_asset_id",
                table: "contract_occupant_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "extra_media_asset_id",
                table: "contract_occupant_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "front_media_asset_id",
                table: "contract_occupant_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_rules_media_asset_id",
                table: "rooming_house_rules",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_back_media_asset_id",
                table: "contract_occupant_documents",
                column: "back_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_extra_media_asset_id",
                table: "contract_occupant_documents",
                column: "extra_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_front_media_asset_id",
                table: "contract_occupant_documents",
                column: "front_media_asset_id");

            migrationBuilder.AddForeignKey(
                name: "FK_contract_occupant_documents_media_assets_back_media_asset_id",
                table: "contract_occupant_documents",
                column: "back_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_occupant_documents_media_assets_extra_media_asset_~",
                table: "contract_occupant_documents",
                column: "extra_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_occupant_documents_media_assets_front_media_asset_~",
                table: "contract_occupant_documents",
                column: "front_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_rooming_house_rules_media_assets_media_asset_id",
                table: "rooming_house_rules",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contract_occupant_documents_media_assets_back_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_occupant_documents_media_assets_extra_media_asset_~",
                table: "contract_occupant_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_occupant_documents_media_assets_front_media_asset_~",
                table: "contract_occupant_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_rules_media_assets_media_asset_id",
                table: "rooming_house_rules");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_rules_media_asset_id",
                table: "rooming_house_rules");

            migrationBuilder.DropIndex(
                name: "IX_contract_occupant_documents_back_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropIndex(
                name: "IX_contract_occupant_documents_extra_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropIndex(
                name: "IX_contract_occupant_documents_front_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "rooming_house_rules");

            migrationBuilder.DropColumn(
                name: "back_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "extra_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "front_media_asset_id",
                table: "contract_occupant_documents");
        }
    }
}
