using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdjustPhase2MockAmenityCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO room_amenities (room_id, amenity_id)
                SELECT DISTINCT r.id, 5
                FROM rooms r
                INNER JOIN rooming_houses h ON h.id = r.rooming_house_id
                INNER JOIN room_price_tiers pt ON pt.room_id = r.id
                WHERE h.landlord_user_id = '90000000-0000-0000-0000-000000000002'
                  AND h.province_code = '48'
                  AND r.status = 'Available'
                  AND r.deleted_at IS NULL
                  AND pt.monthly_rent <= 4000000
                ON CONFLICT DO NOTHING;

                INSERT INTO room_amenities (room_id, amenity_id)
                SELECT DISTINCT r.id, 6
                FROM rooms r
                INNER JOIN rooming_houses h ON h.id = r.rooming_house_id
                INNER JOIN room_price_tiers pt ON pt.room_id = r.id
                WHERE h.landlord_user_id = '90000000-0000-0000-0000-000000000002'
                  AND h.province_code = '79'
                  AND r.status = 'Available'
                  AND r.deleted_at IS NULL
                  AND pt.monthly_rent <= 5000000
                ON CONFLICT DO NOTHING;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM room_amenities ra
                USING rooms r, rooming_houses h, room_price_tiers pt
                WHERE ra.room_id = r.id
                  AND r.rooming_house_id = h.id
                  AND pt.room_id = r.id
                  AND h.landlord_user_id = '90000000-0000-0000-0000-000000000002'
                  AND h.province_code = '48'
                  AND r.status = 'Available'
                  AND pt.monthly_rent <= 4000000
                  AND ra.amenity_id = 5;

                DELETE FROM room_amenities ra
                USING rooms r, rooming_houses h, room_price_tiers pt
                WHERE ra.room_id = r.id
                  AND r.rooming_house_id = h.id
                  AND pt.room_id = r.id
                  AND h.landlord_user_id = '90000000-0000-0000-0000-000000000002'
                  AND h.province_code = '79'
                  AND r.status = 'Available'
                  AND pt.monthly_rent <= 5000000
                  AND ra.amenity_id = 6;
                """);

        }
    }
}
