using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715164000_SeedOnlinePropertyImageUrls")]
    public partial class SeedOnlinePropertyImageUrls : Migration
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
                CREATE TABLE IF NOT EXISTS demo_property_image_url_backup (
                    image_id uuid PRIMARY KEY,
                    image_url text NOT NULL,
                    object_key text NOT NULL,
                    caption character varying(255)
                );

                INSERT INTO demo_property_image_url_backup (image_id, image_url, object_key, caption)
                SELECT id, image_url, object_key, caption
                FROM property_images
                ON CONFLICT (image_id) DO NOTHING;

                WITH online_urls AS (
                    SELECT
                        ARRAY[
                            'https://images.pexels.com/photos/37485325/pexels-photo-37485325.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/20094613/pexels-photo-20094613.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/18350573/pexels-photo-18350573.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/20858555/pexels-photo-20858555.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/32226825/pexels-photo-32226825.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/35339499/pexels-photo-35339499.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/18587805/pexels-photo-18587805.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/18153132/pexels-photo-18153132.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/17859057/pexels-photo-17859057.png?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/16048055/pexels-photo-16048055.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/5798209/pexels-photo-5798209.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/10357202/pexels-photo-10357202.jpeg?auto=compress&cs=tinysrgb&w=1200'
                        ]::text[] AS house_images,
                        ARRAY[
                            'https://images.pexels.com/photos/8146214/pexels-photo-8146214.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/7546648/pexels-photo-7546648.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/8089172/pexels-photo-8089172.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/6297086/pexels-photo-6297086.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/7046002/pexels-photo-7046002.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/27164969/pexels-photo-27164969.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/209224/pexels-photo-209224.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/271624/pexels-photo-271624.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/1643383/pexels-photo-1643383.jpeg?auto=compress&cs=tinysrgb&w=1200',
                            'https://images.pexels.com/photos/1571460/pexels-photo-1571460.jpeg?auto=compress&cs=tinysrgb&w=1200'
                        ]::text[] AS room_images
                ),
                assigned_urls AS (
                    SELECT
                        pi.id,
                        CASE
                            WHEN pi.room_id IS NOT NULL THEN
                                u.room_images[
                                    mod(abs(hashtext(pi.id::text || ':' || pi.sort_order::text)), array_length(u.room_images, 1)) + 1
                                ]
                            ELSE
                                u.house_images[
                                    mod(abs(hashtext(pi.id::text || ':' || pi.sort_order::text)), array_length(u.house_images, 1)) + 1
                                ]
                        END AS image_url,
                        CASE
                            WHEN pi.room_id IS NOT NULL THEN 'external/pexels/rooms/'
                            ELSE 'external/pexels/houses/'
                        END AS object_key_prefix
                    FROM property_images pi
                    CROSS JOIN online_urls u
                )
                UPDATE property_images pi
                SET image_url = a.image_url,
                    object_key = a.object_key_prefix || regexp_replace(split_part(a.image_url, '?', 1), '^.*/', '')
                FROM assigned_urls a
                WHERE pi.id = a.id;
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
                UPDATE property_images pi
                SET image_url = b.image_url,
                    object_key = b.object_key,
                    caption = b.caption
                FROM demo_property_image_url_backup b
                WHERE pi.id = b.image_id;

                DROP TABLE IF EXISTS demo_property_image_url_backup;
                """);
        }
    }
}
