# Demo Seed Preparation

Nguon tham chieu gan nhat: branch `feat/draft-interval3`, commit `63600271 chore: capture current demo base`.

Muc tieu cua file nay la gom lai kich ban demo va tai khoan can seed sang database moi. Khong copy truc tiep cac migration legacy vi cac migration do dang bi tat trong base hien tai sau khi cutover media schema.

## File Demo Can Giu

- `server/tests/TESTING_SCENARIOS.md`: kich ban unit/integration test theo luong nghiep vu.
- `server/tests/UnitTest_Summary.md`: tong hop test case va coverage backend.
- `server/tests/SWT_Test_Coverage_Report.md`: bao cao coverage theo module.
- `docs/CICD_Demo_Deployment_Guide.md`: huong dan deploy Render/Vercel/Neon va checklist demo.

## Tai Khoan Demo Goc

Mat khau chung tu seed cu: `Demo@123456`

| Email | Role | Ten hien thi trong seed cu | Trang thai mong muon |
| --- | --- | --- | --- |
| `admin.demo@example.com` | Admin | Admin Demo Flow | Active, email confirmed, onboarding completed |
| `nguyenxuanhuan.dev@gmail.com` | Tenant | Nguyen Xuan Huan - Tenant Demo | Active, email confirmed, can dang eKYC |
| `nguyenxuanhuan21102005@gmail.com` | Landlord | Nguyen Xuan Huan - Chu tro Xuan Huan | Active, KYC approved, co nha tro |
| `xunhuns21@gmail.com` | Landlord | Xuan Huns - Chu tro Sunrise | Active, KYC approved, co nha tro phu |
| `hoctienganh4english@gmail.com` | Tenant | Le Quang Linh | Active, KYC approved, co hop dong dang thue |
| `demo.flow.guest@example.com` | Tenant | Khach Thue Demo Phu | Active, dung cho chat/lich xem/review |
| `demo.flow.newoccupant@example.com` | Tenant | Nguoi O Moi Demo | Active, dung cho luong them nguoi o/hop dong |

## De Xuat Chuan Hoa Truoc Khi Seed Production Demo

Public data khong nen co chu `Demo`, `Mock`, ma phong/khu tro kieu `#123`. Neu seed tai khoan de thao tac demo thi co the giu email ky thuat, nhung nen doi ten hien thi sang ten that:

| Email | Role | Ten hien thi nen dung |
| --- | --- | --- |
| `admin.demo@example.com` | Admin | Quan tri Smart Rental |
| `nguyenxuanhuan.dev@gmail.com` | Tenant | Nguyen Xuan Huan - login de test eKYC |
| `nguyenxuanhuan21102005@gmail.com` | Landlord | Nguyen Xuan Huan |
| `xunhuns21@gmail.com` | Landlord | Nguyen Xuan Huns |
| `hoctienganh4english@gmail.com` | Tenant | Le Quang Linh |
| `demo.flow.guest@example.com` | Tenant | Pham Ngoc Mai |
| `demo.flow.newoccupant@example.com` | Tenant | Vo Thao Vy |

## Tai Khoan Dang Duoc Seed Trong Code

`DevelopmentDataSeed` hien da seed/cap nhat idempotent cac tai khoan sau:

| Email | Role | Ten hien thi | Ghi chu |
| --- | --- | --- | --- |
| `admin.demo@example.com` | Admin | Quan tri Smart Rental | admin seed co san |
| `nguyenxuanhuan.dev@gmail.com` | Tenant | Nguyen Xuan Huan | tenant login de gui eKYC that, chua co KYC approved |
| `nguyenxuanhuan21102005@gmail.com` | Landlord | Nguyen Xuan Huan | landlord chinh, KYC approved |
| `xunhuns21@gmail.com` | Landlord | Nguyen Xuan Huns | landlord phu, KYC approved |
| `hoctienganh4english@gmail.com` | Tenant | Le Quang Linh | tenant dang thue, KYC approved |
| `pham.ngoc.mai@example.com` | Tenant | Pham Ngoc Mai | guest tenant, KYC approved |
| `vo.thao.vy@example.com` | Tenant | Vo Thao Vy | nguoi o moi, KYC approved |
| `pham.minh.landlord@example.com` | Landlord | Pham Minh | landlord phu cho draft/pending/rejected house |

## Kich Ban Demo Can Co Trong Database

- Admin da co quyen duyet KYC, duyet khu tro, duyet review/report.
- Tenant `nguyenxuanhuan.dev@gmail.com` login duoc va phai vao `/me/kyc` de gui eKYC truoc khi dung cac chuc nang yeu cau KYC.
- Tenant da KYC approved khac co the dung de test tim phong, dat lich xem phong va gui yeu cau thue.
- Landlord chinh co it nhat mot khu tro visible, nhieu phong, anh that tu S3, review co reply.
- Tenant dang thue co hop dong active, hoa don da thanh toan, hoa don dang cho thanh toan, lich su vi.
- Luong hop dong co occupant, signature, contract file, appendix neu co.
- Luong billing co service types Dien/Nuoc/Internet/Rac, meter readings co anh that hoac anh cong to that.
- Luong chat co direct chat tenant-landlord va group chat nha tro.
- Review moi khu tro: 5 review, 3-5 review co landlord reply, moderation approved.

## Luu Y Ky Thuat

- Khong bat lai legacy migration seed `20260715143000_*` den `20260715181000_*`; chung target schema cu va da co guard `LegacyDemoSeedIsDisabled()`.
- Seed moi nen nam trong seeder hien tai, uu tien `DevelopmentDataSeed`/large-scale seeder, hoac script idempotent rieng cho Neon.
- Anh that nen dung URL/object key da co tren S3, khong restore SVG placeholder.
- Khi seed Neon production demo, chay theo thu tu: migrate schema, seed role/admin, seed catalog, seed nha tro/phong/anh, seed review, seed hop dong, seed billing/chat.
