using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715181000_NormalizeDemoLandlordKycNames")]
    public partial class NormalizeDemoLandlordKycNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE kyc_verifications kv
                SET ocr_full_name = 'Nguyễn Xuân Huấn',
                    updated_at = now()
                FROM users u
                WHERE kv.user_id = u.id
                  AND u.normalized_email IN (
                      'NGUYENXUANHUAN21102005@GMAIL.COM',
                      'XUNHUNS21@GMAIL.COM'
                  );

                UPDATE user_profiles up
                SET full_name = 'Nguyễn Xuân Huấn',
                    updated_at = now()
                FROM users u
                WHERE up.user_id = u.id
                  AND u.normalized_email IN (
                      'NGUYENXUANHUAN21102005@GMAIL.COM',
                      'XUNHUNS21@GMAIL.COM'
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
