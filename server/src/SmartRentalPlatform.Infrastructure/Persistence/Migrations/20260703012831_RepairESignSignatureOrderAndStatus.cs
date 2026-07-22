using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairESignSignatureOrderAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE contract_signatures AS signature
                SET signing_order = CASE signature.signer_role
                        WHEN 'Landlord' THEN 1
                        WHEN 'Tenant' THEN 2
                        ELSE signature.signing_order
                    END,
                    status = CASE
                        WHEN signature.status = '0' THEN 'Pending'
                        ELSE signature.status
                    END,
                    signature_method = CASE
                        WHEN signature.signature_method = '0' THEN 'Unknown'
                        ELSE signature.signature_method
                    END,
                    created_at = CASE
                        WHEN signature.created_at = '-infinity'::timestamptz
                            THEN COALESCE(envelope.created_at, CURRENT_TIMESTAMP)
                        ELSE signature.created_at
                    END
                FROM contract_signing_envelopes AS envelope
                WHERE signature.signing_envelope_id = envelope.id
                  AND (
                      signature.signing_order <= 0
                      OR signature.status = '0'
                      OR signature.signature_method = '0'
                      OR signature.created_at = '-infinity'::timestamptz
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair is intentionally irreversible.
        }
    }
}
