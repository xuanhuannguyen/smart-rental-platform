using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicRoomingHousePerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rooms_rooming_house_id",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooming_houses_landlord_user_id",
                table: "rooming_houses");

            migrationBuilder.DropIndex(
                name: "IX_rooming_houses_province_code",
                table: "rooming_houses");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_reviews_rooming_house_id",
                table: "rooming_house_reviews");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_amenities_amenity_id",
                table: "rooming_house_amenities");

            migrationBuilder.DropIndex(
                name: "IX_room_price_tiers_room_id",
                table: "room_price_tiers");

            migrationBuilder.DropIndex(
                name: "IX_room_amenities_amenity_id",
                table: "room_amenities");

            migrationBuilder.DropIndex(
                name: "IX_property_images_room_id",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "IX_property_images_rooming_house_id",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "IX_property_images_rooming_house_review_id",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "IX_administrative_wards_province_code",
                table: "administrative_wards");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_house_status_active",
                table: "rooms",
                columns: new[] { "rooming_house_id", "status", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_public_filters",
                table: "rooms",
                columns: new[] { "status", "deleted_at", "area_m2", "max_occupants" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_houses_geo_bounds",
                table: "rooming_houses",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_houses_landlord_dashboard",
                table: "rooming_houses",
                columns: new[] { "landlord_user_id", "deleted_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_houses_public_listing",
                table: "rooming_houses",
                columns: new[] { "approval_status", "visibility_status", "deleted_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_houses_public_location",
                table: "rooming_houses",
                columns: new[] { "province_code", "ward_code", "approval_status", "visibility_status", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_house_reviews_ai_queue",
                table: "rooming_house_reviews",
                columns: new[] { "moderation_status", "ai_reviewed_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_house_reviews_public_thread",
                table: "rooming_house_reviews",
                columns: new[] { "rooming_house_id", "is_hidden", "moderation_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rooming_house_amenities_amenity_house",
                table: "rooming_house_amenities",
                columns: new[] { "amenity_id", "rooming_house_id" });

            migrationBuilder.CreateIndex(
                name: "ix_room_price_tiers_public_rent",
                table: "room_price_tiers",
                columns: new[] { "is_active", "monthly_rent", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_room_price_tiers_room_active_rent",
                table: "room_price_tiers",
                columns: new[] { "room_id", "is_active", "monthly_rent" });

            migrationBuilder.CreateIndex(
                name: "ix_room_amenities_amenity_room",
                table: "room_amenities",
                columns: new[] { "amenity_id", "room_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_house_cover_sort",
                table: "property_images",
                columns: new[] { "rooming_house_id", "is_cover", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_house_media_sort",
                table: "property_images",
                columns: new[] { "rooming_house_id", "media_asset_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_review_sort",
                table: "property_images",
                columns: new[] { "rooming_house_review_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_property_images_room_media_sort",
                table: "property_images",
                columns: new[] { "room_id", "media_asset_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_administrative_wards_province_active_name",
                table: "administrative_wards",
                columns: new[] { "province_code", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_administrative_provinces_active_name",
                table: "administrative_provinces",
                columns: new[] { "is_active", "name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rooms_house_status_active",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ix_rooms_public_filters",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ix_rooming_houses_geo_bounds",
                table: "rooming_houses");

            migrationBuilder.DropIndex(
                name: "ix_rooming_houses_landlord_dashboard",
                table: "rooming_houses");

            migrationBuilder.DropIndex(
                name: "ix_rooming_houses_public_listing",
                table: "rooming_houses");

            migrationBuilder.DropIndex(
                name: "ix_rooming_houses_public_location",
                table: "rooming_houses");

            migrationBuilder.DropIndex(
                name: "ix_rooming_house_reviews_ai_queue",
                table: "rooming_house_reviews");

            migrationBuilder.DropIndex(
                name: "ix_rooming_house_reviews_public_thread",
                table: "rooming_house_reviews");

            migrationBuilder.DropIndex(
                name: "ix_rooming_house_amenities_amenity_house",
                table: "rooming_house_amenities");

            migrationBuilder.DropIndex(
                name: "ix_room_price_tiers_public_rent",
                table: "room_price_tiers");

            migrationBuilder.DropIndex(
                name: "ix_room_price_tiers_room_active_rent",
                table: "room_price_tiers");

            migrationBuilder.DropIndex(
                name: "ix_room_amenities_amenity_room",
                table: "room_amenities");

            migrationBuilder.DropIndex(
                name: "ix_property_images_house_cover_sort",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "ix_property_images_house_media_sort",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "ix_property_images_review_sort",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "ix_property_images_room_media_sort",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "ix_administrative_wards_province_active_name",
                table: "administrative_wards");

            migrationBuilder.DropIndex(
                name: "ix_administrative_provinces_active_name",
                table: "administrative_provinces");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_rooming_house_id",
                table: "rooms",
                column: "rooming_house_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_houses_landlord_user_id",
                table: "rooming_houses",
                column: "landlord_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_houses_province_code",
                table: "rooming_houses",
                column: "province_code");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_reviews_rooming_house_id",
                table: "rooming_house_reviews",
                column: "rooming_house_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_amenities_amenity_id",
                table: "rooming_house_amenities",
                column: "amenity_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_price_tiers_room_id",
                table: "room_price_tiers",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_amenities_amenity_id",
                table: "room_amenities",
                column: "amenity_id");

            migrationBuilder.CreateIndex(
                name: "IX_property_images_room_id",
                table: "property_images",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_property_images_rooming_house_id",
                table: "property_images",
                column: "rooming_house_id");

            migrationBuilder.CreateIndex(
                name: "IX_property_images_rooming_house_review_id",
                table: "property_images",
                column: "rooming_house_review_id");

            migrationBuilder.CreateIndex(
                name: "IX_administrative_wards_province_code",
                table: "administrative_wards",
                column: "province_code");
        }
    }
}
