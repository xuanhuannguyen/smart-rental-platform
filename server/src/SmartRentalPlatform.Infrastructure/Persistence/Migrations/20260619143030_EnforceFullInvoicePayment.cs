using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceFullInvoicePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE invoices
                SET status = CASE
                    WHEN paid_amount >= total_amount OR remaining_amount <= 0 THEN 'Paid'
                    WHEN due_date < CURRENT_DATE THEN 'Overdue'
                    ELSE 'Issued'
                END
                WHERE status = 'PartiallyPaid';
                """);

            migrationBuilder.DropIndex(
                name: "IX_invoice_payments_invoice_id",
                table: "invoice_payments");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "remaining_amount",
                table: "invoices");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_invoice_id",
                table: "invoice_payments",
                column: "invoice_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoice_payments_invoice_id",
                table: "invoice_payments");

            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "remaining_amount",
                table: "invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE invoices
                SET
                    paid_amount = CASE WHEN status = 'Paid' THEN total_amount ELSE 0 END,
                    remaining_amount = CASE WHEN status = 'Paid' THEN 0 ELSE total_amount END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_invoice_id",
                table: "invoice_payments",
                column: "invoice_id");
        }
    }
}
