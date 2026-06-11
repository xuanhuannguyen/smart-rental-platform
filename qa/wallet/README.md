# Wallet and PayOS QA

Use this checklist to test the Person 5 wallet top-up flow in local mock mode or with real PayOS credentials.

Do not commit real PayOS credentials to source code or appsettings files.

## Accounts

Development/QA/Test seed creates stable Wallet QA accounts. These accounts are for local and QA testing only.

```text
Tenant
Email: tenant.demo@example.com
Password: Demo@123456
UserId: 10000000-0000-0000-0000-000000000001
Role: Tenant
KYC: Approved
Wallet: Active VND
Initial balance: 500000
Use for: top-up, debit, transfer source, future invoice/deposit payment tests
```

```text
Landlord
Email: landlord.demo@example.com
Password: Demo@123456
UserId: 10000000-0000-0000-0000-000000000002
Role: Landlord
KYC: Approved
Wallet: Active VND
Initial balance: 100000
Use for: transfer target, future rent/deposit receive tests, reserved balance tests
```

```text
Admin
Email: admin.demo@example.com
Password: Demo@123456
UserId: 10000000-0000-0000-0000-000000000099
Role: Admin
KYC: not required
Use for: admin-only endpoint testing and KYC/payment debugging
```

The seeder is idempotent. It checks normalized email, role assignment, approved KYC, and wallet existence before adding data. If a test account or wallet already exists, the seeder does not reset the password or wallet balance.

## Start Apps

Start the backend in Development:

```powershell
dotnet run --project server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj
```

Start the frontend:

```powershell
cd client
npm run dev
```

Log in as the KYC-approved tenant.

## Recommended QA Account Usage

Tenant top-up:

1. Log in as `tenant.demo@example.com`.
2. Open `/me/wallet/topup`.
3. Create a PayOS top-up.
4. Use mock success locally or real PayOS when credentials/webhook are configured.

Tenant transfer to landlord:

1. Log in as `tenant.demo@example.com`.
2. Use Swagger endpoint `POST /api/dev/wallet-test/transfer`.
3. Set `targetUserId` to `10000000-0000-0000-0000-000000000002`.
4. Verify tenant balance decreases, landlord balance increases, and both ledger rows share the same `transfer_group_id`.

Reserve balance test:

1. Log in as `landlord.demo@example.com` or `tenant.demo@example.com`.
2. Use Swagger endpoint `POST /api/dev/wallet-test/reserve`.
3. Verify `reserved_balance` increases but never exceeds `balance`.
4. Use `POST /api/dev/wallet-test/release-reserve` to release the reserved amount.

Debit insufficient balance:

1. Log in as `tenant.demo@example.com`.
2. Use `POST /api/dev/wallet-test/debit` with an amount greater than available balance.
3. Verify the request fails and wallet balance is unchanged.

Mock payment flow:

1. Log in as `tenant.demo@example.com`.
2. Create a top-up from `/me/wallet/topup`.
3. Open `/dev/mock-payment` with the returned `paymentTransactionId`.
4. Run success, duplicate success, wrong amount, and failed tests.

Real PayOS flow:

1. Configure PayOS credentials with user-secrets or environment variables.
2. Log in as `tenant.demo@example.com`.
3. Create top-up from `/me/wallet/topup`.
4. Open the real PayOS payment URL.
5. Use a public webhook URL for real webhook confirmation; localhost is not reachable by PayOS without a tunnel.

## Local Mock Mode

Mock mode is used when any of these PayOS options are missing or placeholders:

- `PayOS:ClientId`
- `PayOS:ApiKey`
- `PayOS:ChecksumKey`
- `PayOS:BaseUrl`

Create a top-up from `/me/wallet/topup`. The response should include:

- `paymentTransactionId`
- `providerOrderCode`
- fake `paymentUrl`
- fake `qrCode`
- `expiredAt`

Mock success:

1. Create a top-up with amount `10000`.
2. Copy `paymentTransactionId`, or click `Thanh toán thử bằng Mock (Dev only)`.
3. Open `/dev/mock-payment`.
4. Run `Mock success`.
5. Open `/me/wallet`.
6. Verify wallet balance increased by `10000`.
7. Open `/me/wallet/transactions`.
8. Verify one `WalletTopUp` ledger row exists.

Duplicate success:

1. Reuse the same `paymentTransactionId`.
2. Run `Mock success` again.
3. Verify wallet balance did not increase a second time.

Wrong amount:

1. Create a new top-up with amount `10000`.
2. Open `/dev/mock-payment` with the new `paymentTransactionId`.
3. Enter amount override `20000`.
4. Run `Mock success`.
5. Verify wallet balance did not increase.

Failed payment:

1. Create a new top-up with amount `10000`.
2. Open `/dev/mock-payment` with the new `paymentTransactionId`.
3. Run `Mock failed`.
4. Verify payment status becomes `Failed`.
5. Verify wallet balance did not increase.
6. Verify no `WalletTopUp` ledger row was created for that failed payment.

## Real PayOS Local Setup

Use dotnet user-secrets for local real credentials. From the `server` directory, run:

```powershell
dotnet user-secrets init --project src/SmartRentalPlatform.Api
dotnet user-secrets set "PayOS:ClientId" "<REAL_CLIENT_ID>" --project src/SmartRentalPlatform.Api
dotnet user-secrets set "PayOS:ApiKey" "<REAL_API_KEY>" --project src/SmartRentalPlatform.Api
dotnet user-secrets set "PayOS:ChecksumKey" "<REAL_CHECKSUM_KEY>" --project src/SmartRentalPlatform.Api
dotnet user-secrets set "PayOS:BaseUrl" "https://api-merchant.payos.vn" --project src/SmartRentalPlatform.Api
dotnet user-secrets set "PayOS:ReturnUrl" "http://localhost:5173/me/wallet/topup/success" --project src/SmartRentalPlatform.Api
dotnet user-secrets set "PayOS:CancelUrl" "http://localhost:5173/me/wallet/topup/cancel" --project src/SmartRentalPlatform.Api
```

Equivalent environment variables are also supported by .NET configuration:

```powershell
$env:PayOS__ClientId="<REAL_CLIENT_ID>"
$env:PayOS__ApiKey="<REAL_API_KEY>"
$env:PayOS__ChecksumKey="<REAL_CHECKSUM_KEY>"
$env:PayOS__BaseUrl="https://api-merchant.payos.vn"
$env:PayOS__ReturnUrl="http://localhost:5173/me/wallet/topup/success"
$env:PayOS__CancelUrl="http://localhost:5173/me/wallet/topup/cancel"
```

Do not put real values in committed `appsettings.json`.

## Real PayOS Create-Payment Test

1. Configure real credentials with user-secrets or environment variables.
2. Restart the backend.
3. Log in as the KYC-approved tenant.
4. Open `/me/wallet/topup`.
5. Create a top-up with amount `10000`.
6. Verify a real PayOS `paymentUrl` and `qrCode` are returned.
7. Click `Mở trang thanh toán PayOS`.
8. Payment creation must leave `payment_transactions.status = Pending`.
9. Wallet balance must not increase until a valid webhook is processed.

## Real PayOS Webhook Limitation

PayOS cannot call a plain localhost webhook from the public internet. For real webhook testing, expose the backend through a public tunnel such as ngrok/cloudflared, or deploy the backend to a public test server.

Webhook URL to configure later:

```text
https://<public-domain>/api/payment-webhooks/payos
```

After a successful real webhook:

- the webhook payload is logged in `payment_webhook_logs`
- duplicate payloads are ignored by `raw_payload_hash`
- wallet balance is credited exactly once
- one `WalletTopUp` ledger row is created

## DEV ONLY Wallet Spending Tests

These endpoints are temporary Person 5 development endpoints. They must not be used for real invoice/deposit flows.

Base route:

```text
/api/dev/wallet-test
```

Requirements:

- Development environment, or Admin role outside Development.
- Bearer token for the current authenticated user.
- Do not call these endpoints from production navigation.

Credit wallet:

```http
POST /api/dev/wallet-test/credit
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "amount": 50000,
  "note": "dev credit test"
}
```

Debit wallet:

```http
POST /api/dev/wallet-test/debit
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "amount": 50000,
  "note": "dev debit test"
}
```

Debit insufficient balance:

```http
POST /api/dev/wallet-test/debit
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "amount": 999999999,
  "note": "dev insufficient debit test"
}
```

Expected: request fails and balance remains unchanged.

Reserve balance:

```http
POST /api/dev/wallet-test/reserve
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "amount": 100000,
  "note": "dev reserve test"
}
```

Reserve more than balance:

```http
POST /api/dev/wallet-test/reserve
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "amount": 999999999,
  "note": "dev reserve too much test"
}
```

Expected: request fails because `reserved_balance <= balance` must hold.

Release reserve:

```http
POST /api/dev/wallet-test/release-reserve
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "amount": 100000,
  "note": "dev release reserve test"
}
```

Expected: request fails if reserved balance would go below zero.

Transfer between wallets:

```http
POST /api/dev/wallet-test/transfer
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "targetUserId": "<TARGET_USER_GUID>",
  "amount": 50000,
  "note": "dev transfer test"
}
```

Expected:

- source wallet debited
- target wallet credited
- exactly two wallet transaction rows
- both rows share the same `transfer_group_id`

Concurrent duplicate debit:

1. Credit wallet with a known amount.
2. Send two debit requests for the full available balance at the same time.
3. Verify at most one succeeds.
4. Verify balance never becomes negative.
5. Verify every successful mutation has exactly one ledger row.

Balance invariant checks:

- `balance >= 0`
- `reserved_balance >= 0`
- `reserved_balance <= balance`
- debit cannot spend reserved funds

## SQL Verification

```sql
select id, user_id, balance, reserved_balance, currency, status
from wallet_accounts
order by created_at desc;
```

```sql
select id, wallet_account_id, payer_user_id, amount, payment_method, payment_purpose,
       provider_order_code, status, created_at, paid_at, failed_at, confirmed_at
from payment_transactions
order by created_at desc;
```

```sql
select id, wallet_account_id, user_id, transaction_type, direction, amount,
       balance_before, balance_after, related_entity_type, related_entity_id, created_at
from wallet_transactions
order by created_at desc;
```

```sql
select id, payment_transaction_id, payment_method, provider_order_code,
       raw_payload_hash, signature_status, processing_status, error_message, received_at, processed_at
from payment_webhook_logs
order by received_at desc;
```
