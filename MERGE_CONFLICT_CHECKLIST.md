# Merge Conflict Resolution Checklist

Muc tieu:

- Bo han `IPrivateStorageService`
- Chuyen toan bo private file sang `media`
- Giu logic nghiep vu moi cua `develop`
- Resolve conflict theo cum de co the kiem tra tung chang

## Trang thai chung

- [x] Ra soat `docs/` va xac nhan khong xoa `docs/interval-plan/*` trong nhanh hien tai
- [x] Resolve xong toan bo conflict
- [x] Build server pass
- [x] Build client pass
- [x] Chay regression checks can thiet

## Cum 1: Rental Contract Data Model

- [x] Resolve `server/src/SmartRentalPlatform.Domain/Entities/RentalContracts/ContractFile.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Domain/Entities/RentalContracts/ContractSigningEnvelope.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/RentalContracts/ContractFileConfiguration.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/RentalContracts/ContractSigningEnvelopeConfiguration.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/ContractAppendixResponseMapper.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractResponseMapper.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractOccupantValidator.cs`
- [x] Loai bo `StorageObjectKey`, `UnsignedFileObjectKey`, `SignedFileObjectKey`, `EvidenceFileObjectKey` trong cac file Cum 1
- [x] Loai bo `FrontImageObjectKey`, `BackImageObjectKey`, `ExtraImageObjectKey` trong cac file Cum 1
- [x] Giu `Purpose`, `Sha256Hash`, `IsLegallySigned`, `ContractSigningEnvelopeId`
- [x] Dung `MediaAssetId` lam source of truth

Validation:

```powershell
git diff --name-only --diff-filter=U -- server/src/SmartRentalPlatform.Domain/Entities/RentalContracts/ContractFile.cs server/src/SmartRentalPlatform.Domain/Entities/RentalContracts/ContractSigningEnvelope.cs server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/RentalContracts/ContractFileConfiguration.cs server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/RentalContracts/ContractSigningEnvelopeConfiguration.cs server/src/SmartRentalPlatform.Application/RentalContracts/ContractAppendixResponseMapper.cs server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractResponseMapper.cs server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractOccupantValidator.cs
rg -n "StorageObjectKey|UnsignedFileObjectKey|SignedFileObjectKey|EvidenceFileObjectKey|FrontImageObjectKey|BackImageObjectKey|ExtraImageObjectKey|FileVariant" server/src/SmartRentalPlatform.Domain/Entities/RentalContracts/ContractFile.cs server/src/SmartRentalPlatform.Domain/Entities/RentalContracts/ContractSigningEnvelope.cs server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/RentalContracts/ContractFileConfiguration.cs server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/RentalContracts/ContractSigningEnvelopeConfiguration.cs server/src/SmartRentalPlatform.Application/RentalContracts/ContractAppendixResponseMapper.cs server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractResponseMapper.cs server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractOccupantValidator.cs
```

Ghi chu:

- Legacy field van con xuat hien trong `ContractAppendixService.cs`, `ContractFileService.cs`, `ContractPreviewAttachmentService.cs`, `RentalContractService.cs`
- Cac file do thuoc Cum 2 va se duoc xu ly tiep o buoc sau

## Cum 2: Rental Contract Services

- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/ContractFileService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/ContractAppendixService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/RentalContractService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RentalContracts/ContractPreviewAttachmentService.cs`
- [x] Bo dependency `IPrivateStorageService` trong Cum 2
- [x] Upload/read/download contract files qua media service
- [x] Giu flow `Purpose`, preview, e-sign, signing envelope cua `develop`
- [x] Dong bo `server/src/SmartRentalPlatform.Infrastructure/Media/DefaultMediaPermissionService.cs` theo `Purpose`

Validation:

```powershell
git diff --name-only --diff-filter=U -- server/src/SmartRentalPlatform.Application/RentalContracts
rg -n "IPrivateStorageService|privateStorageService|StorageObjectKey|FrontImageObjectKey|BackImageObjectKey|ExtraImageObjectKey" server/src/SmartRentalPlatform.Application/RentalContracts -g *.cs
```

Ghi chu:

- `ContractFileService` va `ContractAppendixService` da doi sang upload PDF qua media va link `ContractFile.MediaAssetId`
- `ContractPreviewAttachmentService` da doi tu object-key sang `mediaAssetId -> MediaAsset -> media storage`
- `RentalContractService` da giu flow media cho occupant document, bo hoan toan object-key legacy

## Cum 3: Billing + Meter Reading Backend

- [x] Resolve `server/src/SmartRentalPlatform.Application/Billing/BillingService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/Billing/BillingInvoiceBuilder.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/Billing/BillingResponseMapper.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/Billing/MeterReadingAiService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/Billing/MeterReadingInputResolver.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Contracts/Billing/Requests/MeterReadingInput.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Contracts/Billing/Responses/InvoiceItemResponse.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Domain/Entities/Billing/MeterReading.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Billing/MeterReadingConfiguration.cs`
- [x] Giu AI fields cua `develop`
- [x] Dung `ProofMediaAssetId`, bo `ProofImageObjectKey`

Validation:

```powershell
git diff --name-only --diff-filter=U -- server/src/SmartRentalPlatform.Application/Billing server/src/SmartRentalPlatform.Contracts/Billing server/src/SmartRentalPlatform.Domain/Entities/Billing server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Billing
rg -n "ProofImageObjectKey" server/src/SmartRentalPlatform.Application/Billing server/src/SmartRentalPlatform.Contracts/Billing server/src/SmartRentalPlatform.Domain/Entities/Billing server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Billing -g *.cs
rg -n "ProofMediaAssetId" server/src/SmartRentalPlatform.Application/Billing server/src/SmartRentalPlatform.Contracts/Billing server/src/SmartRentalPlatform.Domain/Entities/Billing server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Billing -g *.cs
```

## Cum 4: Property Image + Review/Rule Backend

- [x] Resolve `server/src/SmartRentalPlatform.Domain/Entities/Properties/PropertyImage.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Properties/PropertyImageConfiguration.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RoomingHouses/ReviewModeration/ReviewAiModerationService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Application/RoomingHouses/RoomingHouseReviewService.cs`
- [x] Giu `RoomingHouseReviewId` cua `develop`
- [x] Dung `MediaAssetId`, bo `ObjectKey`
- [x] Chuyen review moderation sang `IMediaAccessService`

Validation:

```powershell
git diff --name-only --diff-filter=U -- server/src/SmartRentalPlatform.Domain/Entities/Properties server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Properties server/src/SmartRentalPlatform.Application/RoomingHouses
rg -n "\bObjectKey\b|IPrivateStorageService|privateStorageService" server/src/SmartRentalPlatform.Domain/Entities/Properties server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Properties server/src/SmartRentalPlatform.Application/RoomingHouses -g *.cs
```

## Cum 5: Upload Abstraction + Remove Legacy Private Storage

- [x] Resolve `server/src/SmartRentalPlatform.Contracts/Files/FileUploadScope.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/Storage/MediaBackedFileStorageService.cs`
- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/DependencyInjection.cs`
- [x] Quy dinh so phan `server/src/SmartRentalPlatform.Infrastructure/Storage/LocalFileStorageService.cs`
- [x] Xac nhan khong con `server/src/SmartRentalPlatform.Application/Common/Interfaces/IPrivateStorageService.cs`
- [x] Xac nhan khong con `server/src/SmartRentalPlatform.Infrastructure/Storage/LocalPrivateStorageService.cs`
- [x] `IFileStorageService` map ve `MediaBackedFileStorageService`
- [x] Support du scope moi: `ChatImage`, `ChatFile`, `MeterReading`, `HouseRule`
- [x] Chat upload response doi sang `mediaAssetId` thay vi `objectKey`

Validation:

```powershell
git diff --name-only --diff-filter=U -- server/src/SmartRentalPlatform.Contracts/Files server/src/SmartRentalPlatform.Infrastructure/Storage server/src/SmartRentalPlatform.Infrastructure/DependencyInjection.cs server/src/SmartRentalPlatform.Application/Common/Interfaces
rg -n "IPrivateStorageService|LocalPrivateStorageService|privateStorageService" server/src server/tests -g *.cs
rg -n "ChatAttachment|ChatImage|ChatFile|MeterReading" server/src/SmartRentalPlatform.Contracts/Files server/src/SmartRentalPlatform.Infrastructure/Storage -g *.cs
```

## Cum 6: Seed + Snapshot + Migration

- [x] Resolve `server/src/SmartRentalPlatform.Infrastructure/Persistence/Seed/DevelopmentDataSeed.cs`
- [x] Dong bo `server/src/SmartRentalPlatform.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` theo model hien tai
- [x] Regenerate migration sau khi model on dinh
- [x] Giu logic seed moi cua `develop`
- [x] Giu backfill media cua nhanh media

Validation:

```powershell
git diff --name-only --diff-filter=U -- server/src/SmartRentalPlatform.Infrastructure/Persistence/Seed server/src/SmartRentalPlatform.Infrastructure/Persistence/Migrations
dotnet build server/src/SmartRentalPlatform.Infrastructure/SmartRentalPlatform.Infrastructure.csproj
```

Ghi chu:

- Da merge helper `EnsureSeedUserAsync` cua `develop` voi flow `BackfillPublicSeedMediaAsync` cua nhanh media
- Migration media cutover da duoc squash thanh `20260715041303_AddMediaSchemaCutover`
- `AppDbContextModelSnapshot.cs` da duoc regenerate tu model hien tai; `dotnet ef migrations has-pending-model-changes` tra ve khong co thay doi
- Dev database da reset/reseed; property image khong con thieu hoac dangling `media_asset_id`, khong con occupant document va avatar URL legacy

## Cum 7: Client Billing

- [x] Resolve `client/src/features/billing/pages/LandlordBillingPage.tsx`
- [x] Resolve `client/src/features/billing/pages/TenantInvoicesPage.tsx`
- [x] Resolve `client/src/features/billing/types.ts`
- [x] Resolve `client/src/features/landlord/pages/RoomDetailPage.tsx`
- [x] Giu UX moi cua `develop`
- [x] Dung `proofMediaAssetId`, bo `proofImageObjectKey`

Validation:

```powershell
git diff --name-only --diff-filter=U -- client/src/features/billing client/src/features/landlord/pages/RoomDetailPage.tsx
rg -n "proofImageObjectKey|proofMediaAssetId|meterReadingProofMediaAssetId" client/src/features/billing client/src/features/landlord/pages/RoomDetailPage.tsx -g *.ts -g *.tsx
```

## Cum 8: Client Contract

- [x] Resolve `client/src/features/contracts/pages/LandlordContractDetailPage.tsx`
- [x] Resolve `client/src/features/contracts/types.ts`
- [x] Giu `purpose` cua `develop`
- [x] Giu `mediaAssetId` va `viewUrl` cua media
- [x] Bo `storageObjectKey`
- [x] Khong dua logic ve `fileVariant` lam source chinh

Validation:

```powershell
git diff --name-only --diff-filter=U -- client/src/features/contracts
rg -n "storageObjectKey|fileVariant|mediaAssetId|viewUrl|purpose" client/src/features/contracts -g *.ts -g *.tsx
```

## Cum 9: Client Misc + Lockfile

- [x] Resolve `client/src/features/home/pages/MePage.tsx`
- [x] Resolve `client/src/features/rooming-houses/components/RoomingHouseRuleEditor.tsx`
- [x] Regenerate `client/package-lock.json`
- [x] `RoomingHouseRuleEditor` dung `pdfMediaAssetId`, bo `pdfObjectKey`

Validation:

```powershell
git diff --name-only --diff-filter=U -- client/src/features/home/pages/MePage.tsx client/src/features/rooming-houses/components/RoomingHouseRuleEditor.tsx client/package-lock.json
npm install
npm run build
```

## Kiem tra tong

- [x] `git diff --name-only --diff-filter=U` tra ve rong
- [x] `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj` (0 errors)
- [x] Unit tests (277 tests)
- [x] Integration tests (13 tests)
- [x] `MediaMigrationRegressionTests` (6 tests)
- [x] Meter AI Python tests (3 tests)
- [x] `npm run build`
- [x] `npm run test:run` (2 test files, 3 tests)
- [x] Chay lai test muc tieu sau khi resolve xong
- [x] Browser regression: listing/search/detail, contract PDF, avatar va KYC media flow
