using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedDocumentNumberAndDefaultPaymentDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "default_payment_day",
                table: "rental_policies",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "document_number_encrypted",
                table: "kyc_verifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number_encrypted",
                table: "contract_occupant_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rental_policies_default_payment_day_range",
                table: "rental_policies",
                sql: "default_payment_day >= 1 AND default_payment_day <= 28");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signatures_appendix_id_signer_role",
                table: "contract_signatures",
                columns: new[] { "appendix_id", "signer_role" },
                unique: true,
                filter: "appendix_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signatures_contract_id_signer_role",
                table: "contract_signatures",
                columns: new[] { "contract_id", "signer_role" },
                unique: true,
                filter: "contract_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rental_policies_default_payment_day_range",
                table: "rental_policies");

            migrationBuilder.DropIndex(
                name: "IX_contract_signatures_appendix_id_signer_role",
                table: "contract_signatures");

            migrationBuilder.DropIndex(
                name: "IX_contract_signatures_contract_id_signer_role",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "default_payment_day",
                table: "rental_policies");

            migrationBuilder.DropColumn(
                name: "document_number_encrypted",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "document_number_encrypted",
                table: "contract_occupant_documents");
        }
    }
}
