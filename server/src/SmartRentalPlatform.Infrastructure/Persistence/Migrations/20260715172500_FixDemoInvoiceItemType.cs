using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715172500_FixDemoInvoiceItemType")]
    public partial class FixDemoInvoiceItemType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE invoice_items
                SET item_type = 'Service'
                WHERE item_type = 'Utility'
                  AND invoice_id IN (
                      SELECT id
                      FROM invoices
                      WHERE invoice_no LIKE 'DEMO-FLOW-%'
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE invoice_items
                SET item_type = 'Utility'
                WHERE item_type = 'Service'
                  AND id IN (
                      SELECT it.id
                      FROM invoice_items it
                      JOIN invoices i ON i.id = it.invoice_id
                      WHERE i.invoice_no = 'DEMO-FLOW-INV-202607-CURRENT'
                        AND it.meter_reading_id IS NOT NULL
                  );
                """);
        }
    }
}
