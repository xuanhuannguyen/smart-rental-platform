using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicDetailQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_rooms_public_detail_order",
                table: "rooms",
                columns: new[] { "rooming_house_id", "deleted_at", "status", "floor", "room_number" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_house_service_prices_public_detail",
                table: "rooming_house_service_prices",
                columns: new[] { "rooming_house_id", "is_active", "service_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_room_price_tiers_room_occupant_order",
                table: "room_price_tiers",
                columns: new[] { "room_id", "occupant_count" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_house_detail_order",
                table: "property_images",
                columns: new[] { "rooming_house_id", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_review_media_sort",
                table: "property_images",
                columns: new[] { "rooming_house_review_id", "media_asset_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_room_detail_order",
                table: "property_images",
                columns: new[] { "room_id", "sort_order", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rooms_public_detail_order",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ix_rooming_house_service_prices_public_detail",
                table: "rooming_house_service_prices");

            migrationBuilder.DropIndex(
                name: "ix_room_price_tiers_room_occupant_order",
                table: "room_price_tiers");

            migrationBuilder.DropIndex(
                name: "ix_property_images_house_detail_order",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "ix_property_images_review_media_sort",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "ix_property_images_room_detail_order",
                table: "property_images");
        }
    }
}
