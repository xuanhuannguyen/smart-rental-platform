# Smart Rental Platform - CI/CD Demo Deployment Guide

Tai lieu nay dung de deploy toan bo he thong Smart Rental Platform theo huong de demo, de sua code, it anh huong production data.

## 1. Muc tieu

- Deploy du 5 thanh phan: frontend, backend API, PostgreSQL, AWS S3 media, Meter AI.
- Code day len `main` la tu dong build, test, build image, deploy.
- Co the sua frontend/backend rieng ma khong phai thao tac tay nhieu.
- Seed data demo co kiem soat, khong tu reset database moi lan deploy.
- Secret khong commit len GitHub.
- Co rollback khi deploy loi.

## 2. Kien truc khuyen nghi

### Phuong an A - EC2/VPS Docker Compose, on dinh nhat cho demo

Phuong an nay phu hop nhat voi base code hien tai vi repo da co:

- `docker-compose.prod.yml`
- `server/Dockerfile.api`
- `client/Dockerfile`
- `server/meter-ai/Dockerfile`
- `.github/workflows/deploy-production.yml`
- `deploy/env.production.template`
- `deploy/Caddyfile`

Kien truc:

```text
GitHub main
  -> GitHub Actions
    -> dotnet build/test
    -> npm build/test
    -> build Docker images
    -> push GHCR
    -> SSH vao EC2/VPS
    -> pull image moi
    -> run migration
    -> restart Docker Compose

Browser
  -> Caddy :80/:443
    -> client nginx container
    -> api .NET container
      -> postgres container
      -> meter-ai container
      -> AWS S3
```

Uu diem:

- Kiem soat full stack.
- Reproduce local/prod gan nhau.
- De deploy backend .NET + Python service.
- Khong bi cold start nang nhu free hosting.
- Chi phi re neu dung VPS/EC2 nho.

Nhuoc diem:

- Can tao server Linux.
- Can quan ly SSH, Docker, backup.

### Phuong an B - Vercel + Render/Neon, re va nhanh

Kien truc:

- Frontend: Vercel.
- Backend API: Render Web Service.
- Meter AI: Render Web Service rieng.
- Database: Neon PostgreSQL hoac Render PostgreSQL.
- Media: AWS S3.

Uu diem:

- Setup nhanh.
- Gan nhu free cho demo nho.
- Vercel deploy frontend rat nhanh.

Nhuoc diem:

- Free tier co cold start.
- Meter AI co the cham.
- Cai dat migration/seed can lam ky hon.

Khuyen nghi hien tai: dung **Phuong an A** neu demo can on dinh; dung **Phuong an B** neu uu tien free.

## 3. Cong nghe su dung

### Frontend

- React + TypeScript.
- Vite.
- Nginx container de serve static file production.
- Bien moi truong build-time:
  - `VITE_API_BASE_URL`
  - `VITE_GOOGLE_CLIENT_ID`
  - `VITE_VIETMAP_API_KEY`
  - `VITE_VIETMAP_TILE_STYLE_URL`
  - `VITE_LEAFLET_TILE_URL`

### Backend

- ASP.NET Core / .NET 10.
- Entity Framework Core.
- PostgreSQL.
- JWT authentication.
- AWS S3 media storage.
- VNPT eKYC.
- PayOS payment.
- Gemini/DeepSeek AI optional.
- ESign optional.

### Meter AI

- Python FastAPI.
- Roboflow model.
- Docker service rieng.

### CI/CD

- GitHub Actions.
- GHCR container registry.
- Docker Compose production.
- SSH deployment to EC2/VPS.
- Caddy reverse proxy.

## 4. Branching va workflow lam viec

Dung branch don gian de demo:

```text
main
  production/demo deploy tu dong

develop
  staging/local integration neu can

feature/*
  code tung tinh nang

fix/*
  fix bug nho
```

Quy tac:

- Chi merge vao `main` khi da build/test pass.
- Migration database phai review truoc khi merge.
- Seed/reset data khong tu dong chay moi deploy.
- Secret chi nam trong GitHub Secrets hoac file `.env.production` tren server.

## 5. Chuan bi GitHub

### 5.1. Repository secrets

Vao GitHub repository:

```text
Settings -> Secrets and variables -> Actions -> Secrets
```

Them cac secret:

```text
EC2_HOST=dia-chi-ip-hoac-domain-server
EC2_USER=ubuntu
EC2_SSH_KEY=private-key-ssh
EC2_DEPLOY_PATH=/opt/smart-rental-platform
GHCR_PAT=optional-neu-can-pull-private-image
```

Ghi chu:

- `EC2_SSH_KEY` la private key, bat dau bang `-----BEGIN OPENSSH PRIVATE KEY-----`.
- Neu GitHub package public hoac server pull duoc bang permission mac dinh thi co the de trong `GHCR_PAT`.

### 5.2. Repository variables

Vao:

```text
Settings -> Secrets and variables -> Actions -> Variables
```

Them:

```text
PRODUCTION_SITE_URL=https://your-domain.com
VITE_GOOGLE_CLIENT_ID=
VITE_VIETMAP_API_KEY=
VITE_VIETMAP_TILE_STYLE_URL=https://maps.vietmap.vn/maps/styles/tm/style.json
VITE_LEAFLET_TILE_URL=https://maps.chotot.com/tile/{z}/{x}/{y}.png
```

Neu demo bang IP chua co domain:

```text
PRODUCTION_SITE_URL=http://your-server-ip
```

## 6. Chuan bi server EC2/VPS

### 6.1. Yeu cau server

Minimum cho demo:

- Ubuntu 22.04 hoac 24.04.
- 2 vCPU.
- 2 GB RAM tro len.
- 20 GB disk tro len.

Neu chay meter-ai + database tren cung server, nen dung:

- 2 vCPU.
- 4 GB RAM.
- 40 GB disk.

### 6.2. Mo port

Security group/firewall:

```text
22/tcp   SSH, chi mo cho IP ca nhan neu co the
80/tcp   HTTP
443/tcp  HTTPS
```

Khong expose PostgreSQL ra public.

### 6.3. Cai Docker

SSH vao server:

```bash
sudo apt update
sudo apt install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
```

Dang xuat SSH roi dang nhap lai, sau do test:

```bash
docker --version
docker compose version
```

### 6.4. Tao thu muc deploy

```bash
sudo mkdir -p /opt/smart-rental-platform
sudo chown "$USER:$USER" /opt/smart-rental-platform
cd /opt/smart-rental-platform
```

## 7. Cau hinh production env

File template nam tai:

```text
deploy/env.production.template
```

Lan deploy dau tien workflow se copy file nay len server thanh:

```text
/opt/smart-rental-platform/.env.production
```

Sau do can SSH vao server va fill gia tri that.

### 7.1. Nhom site

Neu dung IP demo:

```env
SITE_ADDRESS=:80
PUBLIC_SITE_URL=http://your-server-ip
ACME_EMAIL=
```

Neu dung domain:

```env
SITE_ADDRESS=your-domain.com
PUBLIC_SITE_URL=https://your-domain.com
ACME_EMAIL=your-email@example.com
```

### 7.2. Nhom image

Workflow se tu cap nhat 3 bien nay moi lan deploy:

```env
API_IMAGE=ghcr.io/owner/repo/api:latest
CLIENT_IMAGE=ghcr.io/owner/repo/client:latest
METER_AI_IMAGE=ghcr.io/owner/repo/meter-ai:latest
```

Khong can sua tay sau khi CI/CD da chay.

### 7.3. Nhom database

Neu dung Postgres container noi bo:

```env
POSTGRES_DB=smart_rental_platform
POSTGRES_USER=smart_rental
POSTGRES_PASSWORD=change-this-long-password
CONNECTIONSTRINGS__DEFAULTCONNECTION=Host=postgres;Port=5432;Database=smart_rental_platform;Username=smart_rental;Password=change-this-long-password
```

Quan trong:

- Password trong `POSTGRES_PASSWORD` va connection string phai khop nhau.
- Khong doi password sau khi database da tao neu chua cap nhat user trong Postgres.

Neu dung Neon/Supabase/Postgres ngoai:

```env
CONNECTIONSTRINGS__DEFAULTCONNECTION=Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true
```

### 7.4. Nhom JWT

```env
JWT__ISSUER=RentalPlatform
JWT__AUDIENCE=RentalPlatformClient
JWT__SECRETKEY=your-long-random-secret-at-least-32-chars
```

Tao secret bang local:

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

Khi doi `JWT__SECRETKEY`, tat ca user dang login se phai login lai.

### 7.5. Nhom AWS S3

```env
AWS__S3__ACCESSKEYID=...
AWS__S3__SECRETACCESSKEY=...
AWS__S3__REGION=ap-southeast-1
AWS__S3__BUCKETNAME=...
```

Bucket S3 can co CORS cho frontend/backend:

```json
[
  {
    "AllowedHeaders": ["*"],
    "AllowedMethods": ["GET", "PUT", "POST", "DELETE", "HEAD"],
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://your-domain.com"
    ],
    "ExposeHeaders": ["ETag"],
    "MaxAgeSeconds": 3000
  }
]
```

IAM user nen gioi han vao dung bucket, khong dung admin key.

### 7.6. Nhom VNPT eKYC

```env
VNPTEKYC__USEMOCK=false
VNPTEKYC__BASEURL=https://api.idg.vnpt.vn
VNPTEKYC__ACCESSTOKEN=
VNPTEKYC__TOKENID=
VNPTEKYC__TOKENKEY=
VNPTEKYC__MACADDRESS=TEST1
VNPTEKYC__ENABLEFACEVERIFICATION=false
VNPTEKYC__TIMEOUTSECONDS=30
```

Cho demo an toan:

```env
VNPTEKYC__USEMOCK=true
```

Neu dung real VNPT ma provider loi, logic hien tai se cho ho so vao `PendingAdminReview`, admin van duyet duoc.

### 7.7. Nhom Meter AI

```env
ROBOFLOW_API_KEY=
ROBOFLOW_MODEL_ID=utility-meter-reading-dataset-for-automatic-reading-yolo/1
METER_MIN_CONFIDENCE=0.60
METER_MAX_IMAGE_BYTES=10485760
```

Neu demo khong dung doc cong to bang AI, co the de API key rong tuy theo code validation hien tai. Neu app yeu cau key khi startup thi fill key hoac tat tinh nang bang config tuong ung.

### 7.8. Nhom email

```env
EMAIL__FROMEMAIL=no-reply@your-domain.com
EMAIL__FROMNAME=Smart Rental Platform
EMAIL__SMTP__HOST=smtp.gmail.com
EMAIL__SMTP__PORT=587
EMAIL__SMTP__USERNAME=
EMAIL__SMTP__PASSWORD=
EMAIL__SMTP__USESSL=false
```

Gmail nen dung App Password, khong dung password Gmail chinh.

### 7.9. Nhom PayOS

```env
PAYOS__CLIENTID=
PAYOS__APIKEY=
PAYOS__CHECKSUMKEY=
PAYOS__PAYOUTCLIENTID=
PAYOS__PAYOUTAPIKEY=
PAYOS__PAYOUTCHECKSUMKEY=
PAYOS__RETURNURL=https://your-domain.com/wallet/top-up/result
PAYOS__CANCELURL=https://your-domain.com/wallet
```

Sau khi co domain production, cap nhat return/cancel URL trong PayOS dashboard neu can.

### 7.10. Nhom VietMap va frontend map

```env
VIETMAP__APIKEY=
VITE_VIETMAP_API_KEY=
VITE_VIETMAP_TILE_STYLE_URL=https://maps.vietmap.vn/maps/styles/tm/style.json
VITE_LEAFLET_TILE_URL=https://maps.chotot.com/tile/{z}/{x}/{y}.png
```

### 7.11. Nhom AI moderation/chat

```env
GEMINI__ENABLED=false
GEMINI__APIKEY=
DEEPSEEK__ENABLED=false
DEEPSEEK__APIKEY=
```

Chi bat provider nao dang dung.

### 7.12. Nhom ESign

```env
ESIGN__BASEURL=
ESIGN__CLIENTID=
ESIGN__CLIENTSECRET=
ESIGN__USERNAME=
ESIGN__PASSWORD=
ESIGN__CALLBACKURL=https://your-domain.com/api/esign/webhook
ESIGN__RETURNURL=https://your-domain.com/contracts
```

## 8. Chay deploy lan dau

### 8.1. Push code len GitHub

Truoc khi push:

```powershell
git status --ignored --short
git diff -- server/src/SmartRentalPlatform.Api/appsettings.json
```

Dam bao:

- Khong push `.env`.
- Khong push `deploy/secrets.local.md`.
- Khong push AWS/VNPT/PayOS key that trong `appsettings.json`.

### 8.2. Run workflow lan dau

Vao GitHub:

```text
Actions -> Production CI/CD -> Run workflow
```

Lan dau workflow co the dung o buoc tao `.env.production`.

SSH vao server:

```bash
cd /opt/smart-rental-platform
nano .env.production
```

Fill tat ca gia tri production.

### 8.3. Run workflow lan hai

Chay lai workflow.

Workflow se:

1. Build/test backend.
2. Build/test frontend.
3. Build image API/client/meter-ai.
4. Push image len GHCR.
5. SSH vao server.
6. Pull image.
7. Start Postgres + Meter AI.
8. Run EF migration bang `api-migrate`.
9. Restart full stack.
10. Smoke test `/api/health`.

## 9. Kiem tra production sau deploy

SSH vao server:

```bash
cd /opt/smart-rental-platform
docker compose --env-file .env.production -f docker-compose.prod.yml ps
```

Xem logs API:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api
```

Xem logs Caddy:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f caddy
```

Test API:

```bash
curl -i http://localhost/api/health
curl -i http://localhost/api/administrative/provinces
```

Tu may ca nhan:

```powershell
curl https://your-domain.com/api/health
curl https://your-domain.com/api/administrative/provinces
```

## 10. Seed data demo production

Nguyen tac:

- Seed demo data chi chay mot lan hoac chay khi ban chu dong.
- Khong reset database moi lan deploy.
- Anh demo nen upload S3 truoc, database chi luu object key/url.
- Account demo nen co mat khau ro rang va doi duoc.

### 10.1. Khuyen nghi config seed

Nen co cac bien moi truong de kiem soat:

```env
SEED__RUNONSTARTUP=false
SEED__RESETDEMODATA=false
SEED__DISPLAYCATALOG=false
SEED__UPLOADIMAGES=false
```

Neu code hien tai chua du cac flag nay, nen them truoc khi deploy that.

### 10.2. Chay seed mot lan

Quy trinh an toan:

1. Backup database.
2. Bat flag seed/reset neu can.
3. Restart API hoac chay job seed.
4. Kiem tra data.
5. Tat flag seed/reset.
6. Restart API lan nua.

Lenh mau neu seed chay khi API startup:

```bash
cd /opt/smart-rental-platform
nano .env.production
docker compose --env-file .env.production -f docker-compose.prod.yml up -d api
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api
```

Sau khi seed xong, doi:

```env
SEED__RUNONSTARTUP=false
SEED__RESETDEMODATA=false
SEED__DISPLAYCATALOG=false
SEED__UPLOADIMAGES=false
```

### 10.3. Checklist data demo

Can co:

- 500 khu tro hoac tap data demo da chot.
- Moi khu tro co ten that, khong dung `demo`, `#123`, placeholder.
- Anh khu tro/phong tro hien thi tu S3.
- Review/rating tinh dung ngoai card.
- Moi khu tro co review/reply neu can demo social proof.
- Account admin/landlord/tenant demo.
- Contract/billing/KYC data mau.

## 11. Sua code sau khi da deploy

### 11.1. Sua frontend it anh huong

Quy trinh:

```powershell
git checkout -b feature/ui-small-fix
cd client
npm ci
npm run build
npm run test:run
git add .
git commit -m "fix(client): update demo UI"
git push origin feature/ui-small-fix
```

Tao PR vao `main`.

Khi merge:

- CI build/test FE.
- Build client image moi.
- Deploy lai client container.
- Database khong bi anh huong.

### 11.2. Sua backend logic it anh huong

Quy trinh:

```powershell
git checkout -b fix/api-small-fix
dotnet restore server/SmartRentalPlatform.slnx
dotnet build server/SmartRentalPlatform.slnx --configuration Release
dotnet test server/SmartRentalPlatform.slnx --configuration Release --no-build
git add .
git commit -m "fix(api): adjust ekyc review fallback"
git push origin fix/api-small-fix
```

Khi merge:

- CI build/test backend.
- Build API image moi.
- Deploy lai API container.
- Migration chi chay neu co migration moi.

### 11.3. Sua database schema

Quy trinh:

```powershell
dotnet ef migrations add YourMigrationName `
  --project server/src/SmartRentalPlatform.Infrastructure `
  --startup-project server/src/SmartRentalPlatform.Api
```

Sau do:

```powershell
dotnet build server/SmartRentalPlatform.slnx --configuration Release
dotnet test server/SmartRentalPlatform.slnx --configuration Release --no-build
```

Can review ky migration:

- Co drop column/table khong.
- Co update data lon khong.
- Co seed/reset data bat ngo khong.
- Co default value cho column moi khong.

### 11.4. Sua config/secret

Khong sua secret trong code.

Sua tren server:

```bash
cd /opt/smart-rental-platform
nano .env.production
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

Neu sua frontend build-time env nhu `VITE_*`, can rebuild frontend image qua GitHub Actions.

## 12. Rollback

### 12.1. Rollback code nhanh

Tim image cu trong GHCR, sau do SSH vao server:

```bash
cd /opt/smart-rental-platform
nano .env.production
```

Doi:

```env
API_IMAGE=ghcr.io/owner/repo/api:old-sha
CLIENT_IMAGE=ghcr.io/owner/repo/client:old-sha
METER_AI_IMAGE=ghcr.io/owner/repo/meter-ai:old-sha
```

Restart:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml pull
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

### 12.2. Rollback database

Neu migration da thay doi schema/data, rollback code chua du.

Can:

- Backup truoc migration quan trong.
- Restore backup neu migration loi nang.

Backup:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml exec postgres \
  pg_dump -U smart_rental -d smart_rental_platform > backup-before-deploy.sql
```

Restore can can than vi co the ghi de data.

## 13. Backup va bao ve data

### 13.1. Backup PostgreSQL

Tao backup thu cong:

```bash
cd /opt/smart-rental-platform
docker compose --env-file .env.production -f docker-compose.prod.yml exec postgres \
  pg_dump -U smart_rental -d smart_rental_platform > "backup-$(date +%Y%m%d-%H%M%S).sql"
```

Nen copy backup ra ngoai server:

```bash
scp ubuntu@your-server:/opt/smart-rental-platform/backup-*.sql .
```

### 13.2. Backup S3

Anh nam tren S3 nen can:

- Bat versioning neu chi phi cho phep.
- Khong dung script delete S3 hang loat trong production neu chua dry-run.
- Tao prefix rieng:
  - `production/`
  - `demo/`
  - `local/`

## 14. Smoke test checklist cho demo

Sau moi deploy, test nhanh:

### Public

- Trang home load duoc.
- Listing khu tro load duoc.
- Anh S3 hien thi that.
- Rating/review count tren card dung.
- Detail khu tro co anh, phong, rule, review, reply.

### Auth

- Register/login.
- Refresh token.
- `/api/users/me` tra ve user.
- Logout.

### Tenant

- Search khu tro.
- Xem detail phong.
- Dat lich xem.
- Gui yeu cau thue.
- Chat voi chu tro.

### Landlord

- Tao/sua khu tro.
- Quan ly phong.
- Quan ly request.
- Tao hoa don.
- Doc meter AI neu bat.

### Admin

- Duyet khu tro.
- Duyet KYC.
- Quan ly user.

### KYC

- Upload mat truoc/mat sau/selfie.
- Neu VNPT loi, user van thay submit thanh cong.
- Ho so vao `PendingAdminReview`.
- Admin duyet duoc.

### Payment/Billing

- Tao invoice.
- Xem invoice tenant.
- Top up hoac mock payment neu demo.

## 15. Lenh local nhanh sau khi da cleanup

Vì cleanup deploy da xoa `node_modules`, chay lai frontend:

```powershell
cd client
npm ci
npm run dev
```

Backend:

```powershell
dotnet restore server/SmartRentalPlatform.slnx
dotnet run --project server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj
```

Build/test truoc khi push:

```powershell
dotnet build server/SmartRentalPlatform.slnx --configuration Release
dotnet test server/SmartRentalPlatform.slnx --configuration Release --no-build

cd client
npm ci
npm run build
npm run test:run
```

## 16. Nhung file khong duoc commit

Khong commit:

```text
client/.env
server/meter-ai/.env
deploy/secrets.local.md
server/nuget.config
client/node_modules/
client/dist/
server/**/bin/
server/**/obj/
server/artifacts/
Ảnh trọ/
```

Kiem tra truoc khi push:

```powershell
git status --ignored --short
git diff --cached
```

## 17. Thu tu thuc hien de chot demo

1. Xoa secret that khoi `appsettings.json`, chuyen sang env production.
2. Push code len GitHub.
3. Tao EC2/VPS.
4. Cai Docker.
5. Tao GitHub secrets/variables.
6. Run workflow lan dau de tao `.env.production`.
7. Fill `.env.production` tren server.
8. Run workflow lan hai de deploy.
9. Check `/api/health`.
10. Seed demo data mot lan.
11. Tat flag seed/reset.
12. Smoke test full luong.
13. Ghi lai account demo, link demo, va known limitations.

## 18. Troubleshooting nhanh

### Frontend goi API bi CORS

Kiem tra:

- `PUBLIC_SITE_URL`.
- `VITE_API_BASE_URL`.
- Config CORS backend.
- Domain co `https` hay `http`.

### API 401 sau deploy

Kiem tra:

- `JWT__ISSUER`.
- `JWT__AUDIENCE`.
- `JWT__SECRETKEY`.
- User can logout/login lai neu secret vua doi.

### Anh khong hien thi

Kiem tra:

- S3 bucket CORS.
- Object key/url trong database.
- Backend signed URL/public URL.
- S3 IAM permission.

### VNPT OCR 401

Kiem tra:

- `VNPTEKYC__ACCESSTOKEN`.
- `VNPTEKYC__TOKENID`.
- `VNPTEKYC__TOKENKEY`.
- `VNPTEKYC__MACADDRESS`.
- Token/account co active tren VNPT khong.

Neu VNPT loi, logic hien tai van cho admin duyet manual.

### Migration loi

Kiem tra logs:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml logs api-migrate
```

Neu migration co drop/seed sai, restore backup truoc khi deploy tiep.

### Render/Vercel thay the EC2

Neu chuyen sang Vercel + Render:

- Vercel root: `client`
- Vercel build: `npm ci && npm run build`
- Vercel output: `dist`
- Render backend Dockerfile: `server/Dockerfile.api`
- Render meter-ai Dockerfile: `server/meter-ai/Dockerfile`
- Database: Neon/Render PostgreSQL
- Tat workflow EC2 hoac tao workflow rieng goi deploy hook.

