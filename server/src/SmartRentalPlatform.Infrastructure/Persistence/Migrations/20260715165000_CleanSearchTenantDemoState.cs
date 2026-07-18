using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715165000_CleanSearchTenantDemoState")]
    public partial class CleanSearchTenantDemoState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $demo$
                DECLARE
                    search_tenant_id uuid;
                BEGIN
                    SELECT id INTO search_tenant_id
                    FROM users
                    WHERE normalized_email = 'NGUYENXUANHUAN.DEV@GMAIL.COM'
                    LIMIT 1;

                    IF search_tenant_id IS NULL THEN
                        RETURN;
                    END IF;

                    CREATE TEMP TABLE demo_clean_contract_ids ON COMMIT DROP AS
                    SELECT id
                    FROM contracts
                    WHERE main_tenant_user_id = search_tenant_id;

                    CREATE TEMP TABLE demo_clean_request_ids ON COMMIT DROP AS
                    SELECT id
                    FROM rental_requests
                    WHERE tenant_user_id = search_tenant_id
                       OR id IN (SELECT rental_request_id FROM contracts WHERE id IN (SELECT id FROM demo_clean_contract_ids));

                    CREATE TEMP TABLE demo_clean_deposit_ids ON COMMIT DROP AS
                    SELECT id
                    FROM room_deposits
                    WHERE tenant_user_id = search_tenant_id
                       OR rental_request_id IN (SELECT id FROM demo_clean_request_ids)
                       OR id IN (SELECT room_deposit_id FROM contracts WHERE id IN (SELECT id FROM demo_clean_contract_ids));

                    CREATE TEMP TABLE demo_clean_review_house_ids ON COMMIT DROP AS
                    SELECT DISTINCT rooming_house_id
                    FROM rooming_house_reviews
                    WHERE tenant_user_id = search_tenant_id;

                    DELETE FROM review_reports
                    WHERE reporter_user_id = search_tenant_id
                       OR rooming_house_review_id IN (
                            SELECT id FROM rooming_house_reviews WHERE tenant_user_id = search_tenant_id
                       );

                    DELETE FROM property_images
                    WHERE rooming_house_review_id IN (
                        SELECT id FROM rooming_house_reviews WHERE tenant_user_id = search_tenant_id
                    );

                    DELETE FROM rooming_house_reviews
                    WHERE tenant_user_id = search_tenant_id;

                    DELETE FROM invoice_items
                    WHERE invoice_id IN (
                        SELECT id
                        FROM invoices
                        WHERE tenant_user_id = search_tenant_id
                           OR contract_id IN (SELECT id FROM demo_clean_contract_ids)
                    );

                    DELETE FROM meter_readings
                    WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids);

                    DELETE FROM invoices
                    WHERE tenant_user_id = search_tenant_id
                       OR contract_id IN (SELECT id FROM demo_clean_contract_ids);

                    DELETE FROM contract_signatures
                    WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids)
                       OR appendix_id IN (
                            SELECT id FROM contract_appendices WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids)
                       );

                    DELETE FROM contract_files
                    WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids)
                       OR appendix_id IN (
                            SELECT id FROM contract_appendices WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids)
                       );

                    DELETE FROM contract_appendix_changes
                    WHERE appendix_id IN (
                        SELECT id FROM contract_appendices WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids)
                    );

                    DELETE FROM contract_appendices
                    WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids);

                    DELETE FROM contract_occupant_documents
                    WHERE contract_occupant_id IN (
                        SELECT id FROM contract_occupants WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids)
                    );

                    DELETE FROM contract_occupants
                    WHERE contract_id IN (SELECT id FROM demo_clean_contract_ids);

                    DELETE FROM contracts
                    WHERE id IN (SELECT id FROM demo_clean_contract_ids);

                    DELETE FROM room_deposits
                    WHERE id IN (SELECT id FROM demo_clean_deposit_ids);

                    DELETE FROM rental_requests
                    WHERE id IN (SELECT id FROM demo_clean_request_ids);

                    DELETE FROM viewing_appointments
                    WHERE tenant_user_id = search_tenant_id
                       OR created_by_user_id = search_tenant_id;

                    DELETE FROM chat_messages
                    WHERE sender_id = search_tenant_id
                       OR conversation_id IN (
                            SELECT conversation_id
                            FROM conversation_participants
                            WHERE user_id = search_tenant_id
                       );

                    DELETE FROM conversation_participants
                    WHERE conversation_id IN (
                        SELECT conversation_id
                        FROM conversation_participants
                        WHERE user_id = search_tenant_id
                    );

                    DELETE FROM conversations
                    WHERE created_by_user_id = search_tenant_id
                       OR direct_user_a_id = search_tenant_id
                       OR direct_user_b_id = search_tenant_id;

                    UPDATE rooming_houses h
                    SET average_rating = COALESCE(r.avg_rating, 0),
                        total_reviews = COALESCE(r.total_reviews, 0),
                        updated_at = now()
                    FROM (
                        SELECT h2.id AS rooming_house_id,
                               ROUND(AVG(rr.rating)::numeric, 2) AS avg_rating,
                               COUNT(rr.id)::int AS total_reviews
                        FROM rooming_houses h2
                        LEFT JOIN rooming_house_reviews rr
                            ON rr.rooming_house_id = h2.id
                           AND rr.is_hidden = FALSE
                           AND rr.moderation_status IN ('Approved', 'PendingAdminReview')
                        WHERE h2.id IN (SELECT rooming_house_id FROM demo_clean_review_house_ids)
                        GROUP BY h2.id
                    ) r
                    WHERE h.id = r.rooming_house_id;
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cleanup-only migration. Removed demo activity is intentionally not recreated here.
        }
    }
}
