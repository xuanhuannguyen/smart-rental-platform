using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_order_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_transaction_code = table.Column<string>(type: "text", nullable: true),
                    bank_bin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    account_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_wallet_accounts_wallet_account_id",
                        column: x => x.wallet_account_id,
                        principalTable: "wallet_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_webhook_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    withdrawal_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_order_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_webhook_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_withdrawal_webhook_logs_withdrawal_requests_withdrawal_requ~",
                        column: x => x.withdrawal_request_id,
                        principalTable: "withdrawal_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_idempotency_key",
                table: "withdrawal_requests",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_provider_order_code",
                table: "withdrawal_requests",
                column: "provider_order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_wallet_account_id",
                table: "withdrawal_requests",
                column: "wallet_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_webhook_logs_withdrawal_request_id_status",
                table: "withdrawal_webhook_logs",
                columns: new[] { "withdrawal_request_id", "status" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "withdrawal_webhook_logs");

            migrationBuilder.DropTable(
                name: "withdrawal_requests");
        }
    }
}
