# Overall Refactor Execution Plan

## Summary

Backend refactor follows a safe, facade-preserving approach. Public controllers, request/response contracts, routes, authorization behavior, database schema and migrations remain unchanged.

The current direction is to reduce large application-service files by extracting low-risk internal collaborators first, then continue with deeper workflow slices only after regression tests stay green.

## Completed Initial Slice

- Split application dependency injection into module-level registration extensions for auth, billing, payment, rental contracts, rooming houses, rooms and admin approval.
- Extracted billing response mapping, billing period resolution, invoice composition, invoice query/visibility loading, effective contract context, meter-reading input resolution and billing workflow guards from `BillingService` into focused collaborators while keeping `IBillingService` as the facade.
- Extracted contract appendix response mapping from `ContractAppendixService` into an internal mapper.
- Extracted contract appendix state checks into an internal state guard.
- Extracted contract appendix access checks, signer-role resolution and current-main-tenant projection into an internal access policy.
- Extracted contract appendix signature creation into a dedicated factory.
- Extracted contract appendix query include graphs, render option building, verified occupant-account lookup, request validation, appendix-number generation and change parsing/value normalization into internal helpers.
- Extracted rental-contract response/history mapping, preview render option building, occupant validation, state guards, term validation, document handling, signature creation, lifecycle helpers and final-invoice status resolution from `RentalContractService` while keeping `IRentalContractService` as the facade.

## Next Safe Slices

- Keep billing payment application in the facade until wallet/payment regression coverage is expanded; avoid moving side-effect-heavy payment state changes without focused tests.
- Move remaining contract appendix business validation and change-application workflow into internal collaborators after adding focused regression coverage for renewal, main-tenant transfer and tiered-pricing cases.
- Add focused regression tests around rental-contract history snapshots, occupant submission and termination/final-invoice status before any deeper rental-contract workflow extraction.

## Verification

- Run `dotnet build SmartRentalPlatform.slnx --no-restore` before and after each slice.
- Run targeted tests for the touched service, then the full backend test suite before merging.
- Do not change API DTOs, EF migrations or frontend behavior as part of this backend refactor.

Latest verification:

- `dotnet build SmartRentalPlatform.slnx --no-restore -v:q`
- `dotnet test tests\SmartRentalPlatform.UnitTests\SmartRentalPlatform.UnitTests.csproj --no-restore -v:q`
- `dotnet test SmartRentalPlatform.slnx --no-restore -v:q`
