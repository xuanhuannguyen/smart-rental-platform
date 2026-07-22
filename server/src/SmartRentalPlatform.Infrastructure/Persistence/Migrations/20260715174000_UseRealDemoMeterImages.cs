using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715174000_UseRealDemoMeterImages")]
    public partial class UseRealDemoMeterImages : Migration
    {
        private static bool LegacyDemoSeedIsDisabled() => true;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (LegacyDemoSeedIsDisabled())
            {
                // Legacy demo seed SQL targets pre-media columns. Current demo data is seeded by DevelopmentDataSeed.
                return;
            }

            migrationBuilder.Sql("""
                UPDATE meter_readings
                SET proof_image_object_key = 'demo-flow/meters/b201-electric-202606.png',
                    updated_at = now()
                WHERE proof_image_object_key = 'demo-flow/meters/b201-electric-202606.svg';

                UPDATE meter_readings
                SET proof_image_object_key = 'demo-flow/meters/b201-water-202606.png',
                    updated_at = now()
                WHERE proof_image_object_key = 'demo-flow/meters/b201-water-202606.svg';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (LegacyDemoSeedIsDisabled())
            {
                // No-op: matching legacy demo seed Up() is disabled after media schema cutover.
                return;
            }

            migrationBuilder.Sql("""
                UPDATE meter_readings
                SET proof_image_object_key = 'demo-flow/meters/b201-electric-202606.svg',
                    updated_at = now()
                WHERE proof_image_object_key = 'demo-flow/meters/b201-electric-202606.png';

                UPDATE meter_readings
                SET proof_image_object_key = 'demo-flow/meters/b201-water-202606.svg',
                    updated_at = now()
                WHERE proof_image_object_key = 'demo-flow/meters/b201-water-202606.png';
                """);
        }
    }
}
