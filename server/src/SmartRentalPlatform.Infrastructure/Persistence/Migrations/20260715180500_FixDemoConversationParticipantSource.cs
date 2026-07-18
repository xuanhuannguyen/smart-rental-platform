using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715180500_FixDemoConversationParticipantSource")]
    public partial class FixDemoConversationParticipantSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE conversation_participants cp
                SET source = 'Manual'
                FROM conversations c
                WHERE c.id = cp.conversation_id
                  AND cp.source = 'Direct'
                  AND (
                      c.title LIKE 'DEMO-FLOW:%'
                      OR c.title LIKE 'DEMO-BULK:%'
                      OR c.type = 'Direct'
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: 'Direct' is not a valid ConversationParticipantSource value.
        }
    }
}
