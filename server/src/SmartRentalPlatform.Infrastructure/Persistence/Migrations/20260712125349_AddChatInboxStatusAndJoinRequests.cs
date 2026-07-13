using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatInboxStatusAndJoinRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE conversations ADD COLUMN IF NOT EXISTS avatar_url character varying(1000);");
            migrationBuilder.Sql("ALTER TABLE conversations ADD COLUMN IF NOT EXISTS requires_join_approval boolean NOT NULL DEFAULT FALSE;");
            migrationBuilder.Sql("ALTER TABLE conversations ADD COLUMN IF NOT EXISTS rooming_house_id uuid;");

            migrationBuilder.Sql("ALTER TABLE conversation_participants ADD COLUMN IF NOT EXISTS inbox_status character varying(20) NOT NULL DEFAULT 'Main';");
            migrationBuilder.Sql("ALTER TABLE conversation_participants ADD COLUMN IF NOT EXISTS inbox_status_updated_at timestamp with time zone;");
            migrationBuilder.Sql("ALTER TABLE conversation_participants ADD COLUMN IF NOT EXISTS inbox_status_updated_by_user_id uuid;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS conversation_join_requests (
                    id uuid NOT NULL CONSTRAINT ""PK_conversation_join_requests"" PRIMARY KEY,
                    conversation_id uuid NOT NULL,
                    requester_user_id uuid NOT NULL,
                    status character varying(20) NOT NULL,
                    created_at timestamp with time zone NOT NULL DEFAULT now(),
                    reviewed_by_user_id uuid,
                    reviewed_at timestamp with time zone
                );
            ");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_conversations_rooming_house_id\" ON conversations (rooming_house_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_conversation_participants_inbox_status_updated_by_user_id\" ON conversation_participants (inbox_status_updated_by_user_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_conversation_join_requests_conversation_id\" ON conversation_join_requests (conversation_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_conversation_join_requests_requester_user_id\" ON conversation_join_requests (requester_user_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_conversation_join_requests_reviewed_by_user_id\" ON conversation_join_requests (reviewed_by_user_id);");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_conversation_participants_users_inbox_status_updated_by_use~') THEN
                        ALTER TABLE conversation_participants 
                        ADD CONSTRAINT ""FK_conversation_participants_users_inbox_status_updated_by_use~"" 
                        FOREIGN KEY (inbox_status_updated_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
                    END IF;
                END;
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_conversations_rooming_houses_rooming_house_id') THEN
                        ALTER TABLE conversations 
                        ADD CONSTRAINT ""FK_conversations_rooming_houses_rooming_house_id"" 
                        FOREIGN KEY (rooming_house_id) REFERENCES rooming_houses(id) ON DELETE RESTRICT;
                    END IF;
                END;
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_conversation_join_requests_conversations_conversation_id') THEN
                        ALTER TABLE conversation_join_requests 
                        ADD CONSTRAINT ""FK_conversation_join_requests_conversations_conversation_id"" 
                        FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE;
                    END IF;
                END;
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_conversation_join_requests_users_requester_user_id') THEN
                        ALTER TABLE conversation_join_requests 
                        ADD CONSTRAINT ""FK_conversation_join_requests_users_requester_user_id"" 
                        FOREIGN KEY (requester_user_id) REFERENCES users(id) ON DELETE RESTRICT;
                    END IF;
                END;
                $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_conversation_join_requests_users_reviewed_by_user_id') THEN
                        ALTER TABLE conversation_join_requests 
                        ADD CONSTRAINT ""FK_conversation_join_requests_users_reviewed_by_user_id"" 
                        FOREIGN KEY (reviewed_by_user_id) REFERENCES users(id) ON DELETE RESTRICT;
                    END IF;
                END;
                $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE conversation_participants DROP CONSTRAINT IF EXISTS \"FK_conversation_participants_users_inbox_status_updated_by_use~\";");
            migrationBuilder.Sql("ALTER TABLE conversations DROP CONSTRAINT IF EXISTS \"FK_conversations_rooming_houses_rooming_house_id\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS conversation_join_requests;");
            
            migrationBuilder.Sql("ALTER TABLE conversations DROP COLUMN IF EXISTS avatar_url;");
            migrationBuilder.Sql("ALTER TABLE conversations DROP COLUMN IF EXISTS requires_join_approval;");
            migrationBuilder.Sql("ALTER TABLE conversations DROP COLUMN IF EXISTS rooming_house_id;");
            
            migrationBuilder.Sql("ALTER TABLE conversation_participants DROP COLUMN IF EXISTS inbox_status;");
            migrationBuilder.Sql("ALTER TABLE conversation_participants DROP COLUMN IF EXISTS inbox_status_updated_at;");
            migrationBuilder.Sql("ALTER TABLE conversation_participants DROP COLUMN IF EXISTS inbox_status_updated_by_user_id;");
        }
    }
}
