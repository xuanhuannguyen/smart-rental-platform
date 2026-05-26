# smart-rental-platform

Nền tảng quản lý thuê trọ / tìm trọ - ASP.NET Core Web API + React + PostgreSQL

## KYC module (Interval 1)

### Backend

- API base path: `/api/kyc`
- DbContext: `AppDbContext` (PostgreSQL, enums stored as `varchar`)
- Private file storage: `private-storage/` (gitignored)
- Dev auth shim: header `X-Dev-User-Id` or query `?userId=` until JWT (Person 1) is wired

```bash
docker compose up -d
# PostgreSQL host port: 5433 (mapped in docker-compose.yml)
cd server/src/SmartRentalPlatform.Api
dotnet ef database update
dotnet run
```

### Frontend

```bash
cd client
npm install
npm run dev
```

Use the Dev User ID field on KYC pages (must exist in `users` table). JWT from `localStorage` key `srp_access_token` is sent automatically when present.
