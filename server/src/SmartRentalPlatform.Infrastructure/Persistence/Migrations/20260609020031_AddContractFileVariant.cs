using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFileVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "file_variant",
                table: "contract_files",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Raw");

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_contract_id_appendix_id_file_variant",
                table: "contract_files",
                columns: new[] { "contract_id", "appendix_id", "file_variant" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contract_files_contract_id_appendix_id_file_variant",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "file_variant",
                table: "contract_files");
        }
    }
}
