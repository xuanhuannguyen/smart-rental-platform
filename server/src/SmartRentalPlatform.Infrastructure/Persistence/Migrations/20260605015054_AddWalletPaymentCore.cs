using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletPaymentCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wallet_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reserved_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_accounts", x => x.id);
                    table.CheckConstraint("ck_wallet_accounts_balance_non_negative", "balance >= 0");
                    table.CheckConstraint("ck_wallet_accounts_reserved_balance_lte_balance", "reserved_balance <= balance");
                    table.CheckConstraint("ck_wallet_accounts_reserved_balance_non_negative", "reserved_balance >= 0");
                    table.ForeignKey(
                        name: "FK_wallet_accounts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
                    payment_purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_order_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_transaction_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_checkout_url = table.Column<string>(type: "text", nullable: true),
                    provider_qr_code = table.Column<string>(type: "text", nullable: true),
                    gateway_response_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gateway_response_message = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.CheckConstraint("ck_payment_transactions_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "FK_payment_transactions_users_payer_user_id",
                        column: x => x.payer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_transactions_wallet_accounts_wallet_account_id",
                        column: x => x.wallet_account_id,
                        principalTable: "wallet_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_before = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reserved_balance_before = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reserved_balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_transactions", x => x.id);
                    table.CheckConstraint("ck_wallet_transactions_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallet_transactions_wallet_accounts_wallet_account_id",
                        column: x => x.wallet_account_id,
                        principalTable: "wallet_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_webhook_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    provider_order_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_transaction_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    raw_payload_hash = table.Column<string>(type: "text", nullable: false),
                    signature_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    processing_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_webhook_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_webhook_logs_payment_transactions_payment_transacti~",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_created_at",
                table: "payment_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_idempotency_key",
                table: "payment_transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_payer_user_id",
                table: "payment_transactions",
                column: "payer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_provider_order_code",
                table: "payment_transactions",
                column: "provider_order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_status",
                table: "payment_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_wallet_account_id",
                table: "payment_transactions",
                column: "wallet_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_logs_payment_transaction_id",
                table: "payment_webhook_logs",
                column: "payment_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_logs_processing_status",
                table: "payment_webhook_logs",
                column: "processing_status");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_logs_provider_order_code",
                table: "payment_webhook_logs",
                column: "provider_order_code");

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_logs_raw_payload_hash",
                table: "payment_webhook_logs",
                column: "raw_payload_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_logs_received_at",
                table: "payment_webhook_logs",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_accounts_status",
                table: "wallet_accounts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_accounts_user_id",
                table: "wallet_accounts",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_created_at",
                table: "wallet_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_related_entity_type_related_entity_id",
                table: "wallet_transactions",
                columns: new[] { "related_entity_type", "related_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_transfer_group_id",
                table: "wallet_transactions",
                column: "transfer_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_user_id",
                table: "wallet_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_wallet_account_id",
                table: "wallet_transactions",
                column: "wallet_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_webhook_logs");

            migrationBuilder.DropTable(
                name: "wallet_transactions");

            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "wallet_accounts");
        }
    }
}
