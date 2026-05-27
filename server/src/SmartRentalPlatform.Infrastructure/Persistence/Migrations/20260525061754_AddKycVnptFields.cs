using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKycVnptFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_check_result",
                table: "kyc_verifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ekyc_error_code",
                table: "kyc_verifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ekyc_error_message",
                table: "kyc_verifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ekyc_provider",
                table: "kyc_verifications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VNPT");

            migrationBuilder.AddColumn<string>(
                name: "ekyc_result",
                table: "kyc_verifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ProviderError");

            migrationBuilder.AddColumn<string>(
                name: "ekyc_session_id",
                table: "kyc_verifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "face_match_result",
                table: "kyc_verifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "face_match_score",
                table: "kyc_verifications",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "liveness_result",
                table: "kyc_verifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risk_level",
                table: "kyc_verifications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "High");

            migrationBuilder.AddColumn<string>(
                name: "selfie_capture_method",
                table: "kyc_verifications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Upload");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_verifications_created_at",
                table: "kyc_verifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_verifications_status",
                table: "kyc_verifications",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_created_at",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_status",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "document_check_result",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "ekyc_error_code",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "ekyc_error_message",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "ekyc_provider",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "ekyc_result",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "ekyc_session_id",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "face_match_result",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "face_match_score",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "liveness_result",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "risk_level",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "selfie_capture_method",
                table: "kyc_verifications");
        }
    }
}
