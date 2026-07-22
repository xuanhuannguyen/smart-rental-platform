using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvelopeDocumentSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "unsigned_file_sha256_hash",
                table: "contract_signing_envelopes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "unsigned_file_object_key",
                table: "contract_signing_envelopes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "document_prepared_at",
                table: "contract_signing_envelopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_snapshot_encrypted",
                table: "contract_signing_envelopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_snapshot_sha256_hash",
                table: "contract_signing_envelopes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_template_version",
                table: "contract_signing_envelopes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_prepared_at",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "document_snapshot_encrypted",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "document_snapshot_sha256_hash",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "document_template_version",
                table: "contract_signing_envelopes");

            migrationBuilder.AlterColumn<string>(
                name: "unsigned_file_sha256_hash",
                table: "contract_signing_envelopes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unsigned_file_object_key",
                table: "contract_signing_envelopes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
