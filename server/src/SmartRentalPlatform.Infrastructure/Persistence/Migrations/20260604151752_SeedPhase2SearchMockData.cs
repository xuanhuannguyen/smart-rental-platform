using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedPhase2SearchMockData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    phase2_landlord_id uuid := '90000000-0000-0000-0000-000000000002';
                    city_index int;
                    i int;
                    room_index int;
                    occupant int;
                    house_number int := 0;
                    province_code_value text;
                    city_name text;
                    ward_codes text[];
                    ward_code_value text;
                    base_lat numeric;
                    base_lng numeric;
                    house_lat numeric;
                    house_lng numeric;
                    house_id uuid;
                    room_id uuid;
                    price_id uuid;
                    image_id uuid;
                    house_amenities int[];
                    room_amenities int[];
                    room_count int;
                    max_occupants int;
                    base_price numeric;
                    area_m2 numeric;
                    keyword text;
                    street text;
                    location_hint text;
                    created_at timestamptz := '2026-06-04T00:00:00+00:00';

                    uuid_from_text text;
                BEGIN
                    INSERT INTO users (
                        id,
                        email,
                        normalized_email,
                        phone_number,
                        password_hash,
                        display_name,
                        avatar_url,
                        status,
                        onboarding_status,
                        email_confirmed,
                        phone_confirmed,
                        access_failed_count,
                        lockout_end_at,
                        last_login_at,
                        created_at,
                        updated_at,
                        deleted_at
                    )
                    VALUES (
                        phase2_landlord_id,
                        'phase2.landlord@example.com',
                        'PHASE2.LANDLORD@EXAMPLE.COM',
                        NULL,
                        NULL,
                        'Phase 2 Mock Landlord',
                        NULL,
                        'Active',
                        'Completed',
                        TRUE,
                        FALSE,
                        0,
                        NULL,
                        NULL,
                        created_at,
                        created_at,
                        NULL
                    )
                    ON CONFLICT (normalized_email) DO NOTHING;

                    INSERT INTO user_profiles (user_id, full_name, created_at, updated_at)
                    VALUES (phase2_landlord_id, 'Phase 2 Mock Landlord', created_at, created_at)
                    ON CONFLICT (user_id) DO NOTHING;

                    INSERT INTO user_roles (user_id, role_id, created_at)
                    SELECT phase2_landlord_id, 3, created_at
                    WHERE EXISTS (SELECT 1 FROM roles WHERE id = 3)
                    ON CONFLICT DO NOTHING;

                    FOR city_index IN 1..3 LOOP
                        IF city_index = 1 THEN
                            province_code_value := '48';
                            city_name := 'Đà Nẵng';
                            base_lat := 16.0471;
                            base_lng := 108.2062;
                        ELSIF city_index = 2 THEN
                            province_code_value := '79';
                            city_name := 'TP.HCM';
                            base_lat := 10.7769;
                            base_lng := 106.7009;
                        ELSE
                            province_code_value := '01';
                            city_name := 'Hà Nội';
                            base_lat := 21.0278;
                            base_lng := 105.8342;
                        END IF;

                        SELECT array_agg(code ORDER BY code)
                        INTO ward_codes
                        FROM administrative_wards
                        WHERE province_code = province_code_value AND is_active = TRUE;

                        IF ward_codes IS NULL OR array_length(ward_codes, 1) IS NULL THEN
                            RAISE EXCEPTION 'No active ward found for province %', province_code_value;
                        END IF;

                        FOR i IN 1..80 LOOP
                            house_number := house_number + 1;
                            uuid_from_text := md5('phase2-house-' || house_number);
                            house_id := (
                                substr(uuid_from_text, 1, 8) || '-' ||
                                substr(uuid_from_text, 9, 4) || '-' ||
                                substr(uuid_from_text, 13, 4) || '-' ||
                                substr(uuid_from_text, 17, 4) || '-' ||
                                substr(uuid_from_text, 21, 12)
                            )::uuid;

                            ward_code_value := ward_codes[((i - 1) % array_length(ward_codes, 1)) + 1];
                            house_lat := base_lat + (((i % 10) - 5) * 0.008) + ((city_index - 2) * 0.001);
                            house_lng := base_lng + ((((i / 10) % 8) - 4) * 0.009) + ((city_index - 2) * 0.001);

                            keyword := CASE i % 9
                                WHEN 0 THEN 'gần FPT, yên tĩnh, an ninh'
                                WHEN 1 THEN 'gần Bách Khoa, máy lạnh, full nội thất'
                                WHEN 2 THEN 'gần chợ, có gác, giá tốt'
                                WHEN 3 THEN 'gần bến xe, camera an ninh'
                                WHEN 4 THEN 'ban công thoáng, nhà vệ sinh riêng'
                                WHEN 5 THEN 'gần trường đại học, wifi mạnh'
                                WHEN 6 THEN 'full nội thất, máy giặt chung'
                                WHEN 7 THEN 'khu dân cư yên tĩnh, giữ xe'
                                ELSE 'phòng sáng, có gác lửng, tiện đi học'
                            END;

                            street := CASE i % 10
                                WHEN 0 THEN 'Trần Đại Nghĩa'
                                WHEN 1 THEN 'Nguyễn Văn Linh'
                                WHEN 2 THEN 'Lê Lợi'
                                WHEN 3 THEN 'Điện Biên Phủ'
                                WHEN 4 THEN 'Cách Mạng Tháng 8'
                                WHEN 5 THEN 'Nguyễn Trãi'
                                WHEN 6 THEN 'Phạm Văn Đồng'
                                WHEN 7 THEN 'Hoàng Diệu'
                                WHEN 8 THEN 'Lý Thường Kiệt'
                                ELSE 'Nguyễn Hữu Cảnh'
                            END;

                            location_hint := CASE i % 8
                                WHEN 0 THEN 'FPT'
                                WHEN 1 THEN 'Bách Khoa'
                                WHEN 2 THEN 'chợ trung tâm'
                                WHEN 3 THEN 'bến xe'
                                WHEN 4 THEN 'khu đại học'
                                WHEN 5 THEN 'công viên'
                                WHEN 6 THEN 'trạm xe buýt'
                                ELSE 'siêu thị'
                            END;

                            INSERT INTO rooming_houses (
                                id,
                                landlord_user_id,
                                name,
                                description,
                                address_line,
                                ward_code,
                                province_code,
                                address_display,
                                latitude,
                                longitude,
                                google_map_url,
                                approval_status,
                                visibility_status,
                                rejected_reason,
                                reviewed_by_admin_id,
                                reviewed_at,
                                created_at,
                                updated_at,
                                deleted_at
                            )
                            VALUES (
                                house_id,
                                phase2_landlord_id,
                                'Khu trọ Phase2 ' || city_name || ' #' || lpad(i::text, 2, '0'),
                                'Khu trọ mock phục vụ tìm kiếm Phase 2, ' || keyword || ', gần ' || location_hint || '.',
                                (10 + i)::text || ' ' || street,
                                ward_code_value,
                                province_code_value,
                                (10 + i)::text || ' ' || street || ', ' || city_name,
                                house_lat,
                                house_lng,
                                'https://www.google.com/maps/search/?api=1&query=' || house_lat || ',' || house_lng,
                                'Approved',
                                'Visible',
                                NULL,
                                NULL,
                                created_at,
                                created_at + (house_number || ' minutes')::interval,
                                created_at + (house_number || ' minutes')::interval,
                                NULL
                            )
                            ON CONFLICT (id) DO NOTHING;

                            house_amenities := ARRAY[1, 3];
                            IF i % 2 = 0 THEN house_amenities := house_amenities || 2; END IF;
                            IF i % 3 = 0 THEN house_amenities := house_amenities || 4; END IF;

                            FOREACH occupant IN ARRAY house_amenities LOOP
                                INSERT INTO rooming_house_amenities (rooming_house_id, amenity_id)
                                VALUES (house_id, occupant)
                                ON CONFLICT DO NOTHING;
                            END LOOP;

                            uuid_from_text := md5('phase2-house-image-' || house_number);
                            image_id := (
                                substr(uuid_from_text, 1, 8) || '-' ||
                                substr(uuid_from_text, 9, 4) || '-' ||
                                substr(uuid_from_text, 13, 4) || '-' ||
                                substr(uuid_from_text, 17, 4) || '-' ||
                                substr(uuid_from_text, 21, 12)
                            )::uuid;

                            INSERT INTO property_images (
                                id,
                                rooming_house_id,
                                room_id,
                                object_key,
                                image_url,
                                caption,
                                is_cover,
                                sort_order,
                                created_at
                            )
                            VALUES (
                                image_id,
                                house_id,
                                NULL,
                                'demo/phase2/houses/' || house_number || '/cover.jpg',
                                '/uploads/demo/phase2/houses/' || house_number || '/cover.jpg',
                                'Ảnh cover mock Phase 2',
                                TRUE,
                                1,
                                created_at
                            )
                            ON CONFLICT (id) DO NOTHING;

                            room_count := (i % 4) + 1;
                            FOR room_index IN 1..room_count LOOP
                                uuid_from_text := md5('phase2-room-' || house_number || '-' || room_index);
                                room_id := (
                                    substr(uuid_from_text, 1, 8) || '-' ||
                                    substr(uuid_from_text, 9, 4) || '-' ||
                                    substr(uuid_from_text, 13, 4) || '-' ||
                                    substr(uuid_from_text, 17, 4) || '-' ||
                                    substr(uuid_from_text, 21, 12)
                                )::uuid;

                                max_occupants := 1 + ((i + room_index) % 3);
                                area_m2 := 16 + ((i + room_index * 3) % 22);
                                base_price := 1800000 + ((i % 9) * 350000) + (room_index * 250000) + ((city_index - 1) * 300000);

                                INSERT INTO rooms (
                                    id,
                                    rooming_house_id,
                                    room_number,
                                    floor,
                                    area_m2,
                                    max_occupants,
                                    is_tiered_pricing,
                                    status,
                                    description,
                                    created_at,
                                    updated_at,
                                    deleted_at
                                )
                                VALUES (
                                    room_id,
                                    house_id,
                                    'P' || room_index || '0' || ((i % 9) + 1),
                                    room_index,
                                    area_m2,
                                    max_occupants,
                                    max_occupants > 1,
                                    CASE WHEN room_index = 1 OR (i + room_index) % 3 = 0 THEN 'Available' ELSE 'Occupied' END,
                                    'Phòng mock Phase 2, ' || keyword || '.',
                                    created_at,
                                    created_at,
                                    NULL
                                )
                                ON CONFLICT (id) DO NOTHING;

                                FOR occupant IN 1..max_occupants LOOP
                                    uuid_from_text := md5('phase2-price-' || house_number || '-' || room_index || '-' || occupant);
                                    price_id := (
                                        substr(uuid_from_text, 1, 8) || '-' ||
                                        substr(uuid_from_text, 9, 4) || '-' ||
                                        substr(uuid_from_text, 13, 4) || '-' ||
                                        substr(uuid_from_text, 17, 4) || '-' ||
                                        substr(uuid_from_text, 21, 12)
                                    )::uuid;

                                    INSERT INTO room_price_tiers (
                                        id,
                                        room_id,
                                        occupant_count,
                                        monthly_rent,
                                        is_active,
                                        created_at,
                                        updated_at
                                    )
                                    VALUES (
                                        price_id,
                                        room_id,
                                        occupant,
                                        base_price + ((occupant - 1) * 350000),
                                        TRUE,
                                        created_at,
                                        created_at
                                    )
                                    ON CONFLICT (id) DO NOTHING;
                                END LOOP;

                                room_amenities := ARRAY[1];
                                IF (i + room_index) % 2 = 0 THEN room_amenities := room_amenities || 5; END IF;
                                IF (i + room_index) % 3 = 0 THEN room_amenities := room_amenities || 6; END IF;
                                IF (i + room_index) % 4 = 0 THEN room_amenities := room_amenities || 7; END IF;
                                IF (i + room_index) % 5 = 0 THEN room_amenities := room_amenities || 8; END IF;
                                IF (i + room_index) % 6 = 0 THEN room_amenities := room_amenities || 4; END IF;

                                FOREACH occupant IN ARRAY room_amenities LOOP
                                    INSERT INTO room_amenities (room_id, amenity_id)
                                    VALUES (room_id, occupant)
                                    ON CONFLICT DO NOTHING;
                                END LOOP;

                                uuid_from_text := md5('phase2-room-image-' || house_number || '-' || room_index);
                                image_id := (
                                    substr(uuid_from_text, 1, 8) || '-' ||
                                    substr(uuid_from_text, 9, 4) || '-' ||
                                    substr(uuid_from_text, 13, 4) || '-' ||
                                    substr(uuid_from_text, 17, 4) || '-' ||
                                    substr(uuid_from_text, 21, 12)
                                )::uuid;

                                INSERT INTO property_images (
                                    id,
                                    rooming_house_id,
                                    room_id,
                                    object_key,
                                    image_url,
                                    caption,
                                    is_cover,
                                    sort_order,
                                    created_at
                                )
                                VALUES (
                                    image_id,
                                    NULL,
                                    room_id,
                                    'demo/phase2/rooms/' || house_number || '/' || room_index || '.jpg',
                                    '/uploads/demo/phase2/rooms/' || house_number || '/' || room_index || '.jpg',
                                    'Ảnh phòng mock Phase 2',
                                    TRUE,
                                    1,
                                    created_at
                                )
                                ON CONFLICT (id) DO NOTHING;
                            END LOOP;
                        END LOOP;
                    END LOOP;
                END $$;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    phase2_landlord_id uuid := '90000000-0000-0000-0000-000000000002';
                BEGIN
                    DELETE FROM room_price_tiers
                    WHERE room_id IN (
                        SELECT id FROM rooms
                        WHERE rooming_house_id IN (
                            SELECT id FROM rooming_houses
                            WHERE landlord_user_id = phase2_landlord_id
                        )
                    );

                    DELETE FROM room_amenities
                    WHERE room_id IN (
                        SELECT id FROM rooms
                        WHERE rooming_house_id IN (
                            SELECT id FROM rooming_houses
                            WHERE landlord_user_id = phase2_landlord_id
                        )
                    );

                    DELETE FROM property_images
                    WHERE object_key LIKE 'demo/phase2/%';

                    DELETE FROM rooms
                    WHERE rooming_house_id IN (
                        SELECT id FROM rooming_houses
                        WHERE landlord_user_id = phase2_landlord_id
                    );

                    DELETE FROM rooming_house_amenities
                    WHERE rooming_house_id IN (
                        SELECT id FROM rooming_houses
                        WHERE landlord_user_id = phase2_landlord_id
                    );

                    DELETE FROM rooming_houses
                    WHERE landlord_user_id = phase2_landlord_id;

                    DELETE FROM user_roles
                    WHERE user_id = phase2_landlord_id;

                    DELETE FROM user_profiles
                    WHERE user_id = phase2_landlord_id;

                    DELETE FROM users
                    WHERE id = phase2_landlord_id;
                END $$;
                """);

        }
    }
}
