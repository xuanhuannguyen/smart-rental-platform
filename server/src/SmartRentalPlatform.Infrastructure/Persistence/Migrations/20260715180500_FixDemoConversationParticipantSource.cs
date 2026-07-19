using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715180500_FixDemoConversationParticipantSource")]
    public partial class FixDemoConversationParticipantSource : Migration
    {
        private static bool LegacyDemoSeedIsDisabled() => true;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (LegacyDemoSeedIsDisabled())
            {
                // Legacy demo seed SQL targets pre-media columns. Current demo data is seeded by DevelopmentDataSeed.
                return;
            }

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
            if (LegacyDemoSeedIsDisabled())
            {
                // No-op: matching legacy demo seed Up() is disabled after media schema cutover.
                return;
            }
// No-op: 'Direct' is not a valid ConversationParticipantSource value.
        }
    }
}
