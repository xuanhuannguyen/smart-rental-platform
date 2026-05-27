# Smart Rental Platform Architecture

## Overview

Smart Rental Platform is a full-stack rental management platform for tenants, landlords, and admins.

The backend follows Clean Architecture with feature-oriented folders. The frontend follows React feature folders with shared API, UI, and router infrastructure.

## Backend Layers

```text
server/src/
├── SmartRentalPlatform.Api
├── SmartRentalPlatform.Application
├── SmartRentalPlatform.Domain
├── SmartRentalPlatform.Infrastructure
└── SmartRentalPlatform.Contracts
```

### Api

HTTP entrypoint only.

Responsibilities:
- Controllers
- Middleware
- Swagger, CORS, authentication setup
- Current HTTP request boundary

Controllers are grouped by feature:

```text
SmartRentalPlatform.Api/Controllers/
├── Admin
├── Auth
├── Catalog
├── Files
├── Health
├── Kyc
├── Properties
├── Public
└── Users
```

Route ownership:
- `/api/auth/...` auth
- `/api/users/me/...` current user/profile/session
- `/api/kyc/...` user KYC submission/status
- `/api/rooming-houses/...` landlord rooming-house management
- `/api/rooms/...` landlord room management
- `/api/public/rooming-houses/...` public listing
- `/api/admin/...` admin review and management

Controllers should not contain long business rules. They validate HTTP shape, call Application services, and return `ApiResponse<T>` or `ApiErrorResponse`.

### Application

Business use cases and workflow rules.

Current feature areas:

```text
SmartRentalPlatform.Application/
├── AdminApproval
├── Administrative
├── Amenities
├── Auth
├── Common
├── Kyc
├── RoomingHouses
├── Rooms
└── Users
```

Application owns:
- Auth/session/OTP/password flows
- User profile and onboarding rules
- KYC submit/status/history rules
- Rooming-house draft, submit, update rules
- Room and price-tier rules
- Admin approve/reject rules

Application depends on abstractions in `Common/Interfaces`, not concrete Infrastructure classes.

### Domain

Core entities and enums.

Domain owns:
- `User`, `Role`, `UserProfile`, `UserToken`, `KycVerification`
- `RoomingHouse`, `Room`, `RoomPriceTier`, `Amenity`, `LeasePolicy`
- Approval/status enums

Domain should not depend on Api, Application, Infrastructure, or Contracts.

### Infrastructure

Technical implementation details.

Infrastructure owns:
- EF Core `AppDbContext`
- Entity configurations and migrations
- Seed data
- JWT/password/email/google implementations
- File/private storage implementations
- VNPT eKYC real/mock clients

Infrastructure should not decide business state transitions. It implements technical interfaces used by Application.

### Contracts

API request/response DTOs and shared response models.

```text
SmartRentalPlatform.Contracts/
├── Admin
│   ├── Requests
│   └── Responses
├── Auth
├── Common
├── Kyc
├── RoomingHouses
├── Rooms
├── Users
└── ...
```

Contracts should not contain EF entities, DbContext, or business services.

## Business Flows

### Auth and Onboarding

```text
Register
→ Verify email
→ Complete profile
→ Submit KYC
→ KYC approved
→ Completed
```

Rules:
- Locked/banned/deleted users cannot log in.
- Refresh tokens must be rotated/revoked.
- Logout all revokes all active sessions.
- Frontend may guide onboarding, but backend enforces actions.

### KYC

```text
None
→ PendingAdminReview
→ Approved
→ Rejected
```

Rules:
- User cannot submit another KYC while one is pending.
- Approved KYC blocks resubmission unless a future re-verification flow is designed.
- Reject requires a reason.
- KYC images must use private storage.
- VNPT integration belongs to Infrastructure; KYC workflow belongs to Application.

### Rooming House

```text
None
→ Draft
→ Pending
→ Approved
→ Rejected
```

Rules:
- Draft and Rejected can be edited by the landlord.
- Pending cannot be edited as normal draft content.
- Only Admin can approve/reject through `/api/admin/rooming-houses/...`.
- Approving a rooming house grants the landlord role when eligible.
- Reject requires a reason and audit log.

### Room

Rules:
- Rooms can be created only under an approved rooming house.
- Room number must be unique inside the same rooming house.
- Price tiers must not duplicate occupant count.
- Public listing shows only available rooms.

### Public Listing

Public listing should only expose:

```text
RoomingHouse.ApprovalStatus == Approved
RoomingHouse.VisibilityStatus == Visible
RoomingHouse.DeletedAt == null
At least one Room.Status == Available
```

## Frontend Structure

```text
client/src/
├── app
│   ├── providers
│   └── router
├── features
│   ├── admin
│   ├── auth
│   ├── kyc
│   ├── landlord
│   ├── me
│   ├── profile
│   ├── rooming-houses
│   └── rooms
└── shared
    ├── api
    ├── components
    └── utils
```

## Current Refactor Status

Completed structure changes:
- Admin API endpoints are centralized under `/api/admin/...`.
- Public rooming-house listing uses `/api/public/rooming-houses/...`.
- API controllers are grouped by feature folder instead of one flat controller folder.
- Admin contracts are split into `Requests` and `Responses`.
- KYC business workflow lives in Application; Infrastructure keeps technical storage/VNPT implementations.
- Rooming-house use cases are split by responsibility:
  - `IRoomingHouseQueryService`
  - `IRoomingHouseDraftService`
  - `IRoomingHouseMediaService`
  - `IRoomingHouseLeasePolicyService`
  - `IRoomingHouseSubmissionService`
- Auth use cases are split by responsibility:
  - `IAuthService` for register, login, email verification
  - `IAuthSessionService` for refresh/logout/session revocation
  - `IAuthPasswordService` for forgot/reset/change password
  - `IGoogleLoginService` for Google login
- Room use cases are split by responsibility:
  - `IRoomQueryService`
  - `IRoomCommandService`
  - `IRoomMediaService`
  - `IRoomPriceTierService`
  - `IRoomStatusService`
- Frontend admin routes use role guard.
- Frontend API errors preserve HTTP status, backend error code, details, and raw response.
- Frontend shared utilities own common date/money/status formatting, image request cleanup, asset URL generation, and private admin image loading.

Refactor guardrails:
- Controllers should depend on use-case interfaces, not monolithic services.
- Application services may use small internal/public helper services for ownership checks, mapping, and validation.
- New admin endpoints should be added under `Controllers/Admin`.
- New public listing endpoints should be added under `Controllers/Public`.
- New landlord property management endpoints should be added under `Controllers/Properties`.
- Feature pages should reuse shared utilities/components instead of redefining API error parsing, status labels, image mappers, or authenticated image loading.

Rules:
- Components call feature services, not `fetch` directly.
- API calls go through `shared/api/apiClient`.
- Admin routes use `ProtectedRoute` plus `RoleGuard`.
- Backend remains the authority for permissions and state transitions.

## Refactor Priorities

1. Keep route ownership clean: admin actions under `/api/admin`, public listing under `/api/public`.
2. Keep business workflows in Application.
3. Keep Infrastructure technical.
4. Keep Contracts as request/response only.
5. Split very large services/pages after behavior is covered by tests.
6. Add tests for Auth, KYC approval, rooming-house approval, and room price-tier validation.
