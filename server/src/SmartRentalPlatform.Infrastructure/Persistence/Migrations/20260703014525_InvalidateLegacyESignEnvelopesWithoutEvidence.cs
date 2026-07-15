using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvalidateLegacyESignEnvelopesWithoutEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE contract_signing_envelopes AS envelope
                SET status = 'Failed',
                    provider_status_reason = 'Legacy envelope missing VNPT signing evidence'
                WHERE envelope.status IN (
                    'Draft',
                    'SentToProvider',
                    'WaitingForSigners',
                    'PartiallySigned'
                )
                  AND EXISTS (
                      SELECT 1
                      FROM contract_signatures AS signature
                      WHERE signature.signing_envelope_id = envelope.id
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM contract_signatures AS signature
                      WHERE signature.signing_envelope_id = envelope.id
                        AND signature.provider_evidence_json IS NOT NULL
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM contract_signatures AS signature
                      WHERE signature.signing_envelope_id = envelope.id
                        AND signature.signed_at IS NOT NULL
                  );

                DELETE FROM contract_signatures AS signature
                USING contract_signing_envelopes AS envelope
                WHERE signature.signing_envelope_id = envelope.id
                  AND envelope.status = 'Failed'
                  AND envelope.provider_status_reason = 'Legacy envelope missing VNPT signing evidence'
                  AND signature.signed_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removed legacy signature rows cannot be reconstructed safely.
        }
    }
}
