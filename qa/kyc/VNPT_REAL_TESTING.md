# VNPT eKYC Real Production Environment Testing & Troubleshooting Guide

This guide details how to configure, test, and troubleshoot the real VNPT eKYC client (`UseMock=false`) in the SmartRentalPlatform application.

---

## 1. Local Secrets Setup

For security reasons, production credentials must never be committed to source control (e.g. `appsettings.json`). Instead, use the .NET Secret Manager (`dotnet user-secrets`) during local development.

### Setup Instructions
1. Navigate to the API project directory in your terminal:
   ```bash
   cd server/src/SmartRentalPlatform.Api
   ```
2. Initialize and configure the secrets for VNPT:
   ```bash
   # Initialize user-secrets
   dotnet user-secrets init

   # Set mock toggle to false
   dotnet user-secrets set "VnptEkyc:UseMock" "false"

   # Set VNPT Credentials
   dotnet user-secrets set "VnptEkyc:TokenId" "YOUR_REAL_VNPT_TOKEN_ID"
   dotnet user-secrets set "VnptEkyc:TokenKey" "YOUR_REAL_VNPT_TOKEN_KEY"
   dotnet user-secrets set "VnptEkyc:MacAddress" "YOUR_REGISTERED_MAC_ADDRESS"

   # Choose Authentication Mode: "StaticToken" or "OAuth"
   dotnet user-secrets set "VnptEkyc:AuthMode" "OAuth"
   
   # Optional: Set a static token (only used if AuthMode is "StaticToken")
   dotnet user-secrets set "VnptEkyc:AccessToken" "YOUR_STATIC_ACCESS_TOKEN"
   ```

---

## 2. Direct Integration Testing via cURL

To isolate VNPT connection/auth issues from application storage logic, you can perform direct API testing using `cURL`. 

### Testing the Upload Endpoint (`/file-service/v1/addFile`)

VNPT uses a unique two-step KYC pipeline:
1. First, you must upload files directly to their `file-service` to get a file hash.
2. Second, you call OCR, Compare, or Liveness endpoints using the returned file hashes.

Here is a `cURL` template to test the upload service using a static token:

```bash
curl -X POST "https://api.idg.vnpt.vn/file-service/v1/addFile" \
  -H "Token-id: YOUR_REAL_VNPT_TOKEN_ID" \
  -H "Token-key: YOUR_REAL_VNPT_TOKEN_KEY" \
  -H "mac-address: YOUR_REGISTERED_MAC_ADDRESS" \
  -H "Authorization: Bearer YOUR_STATIC_ACCESS_TOKEN" \
  -F "file=@/path/to/front.jpg;type=image/jpeg" \
  -F "title=front-image" \
  -F "description=KYC front upload test"
```

### Fetching a Dynamic Token via OAuth Endpoint (`/oauth/token`)

If you are using `OAuth` mode, you can verify your credentials and retrieve a temporary token directly from their OAuth service:

```bash
curl -X POST "https://api.idg.vnpt.vn/oauth/token" \
  -H "Token-id: YOUR_REAL_VNPT_TOKEN_ID" \
  -H "Token-key: YOUR_REAL_VNPT_TOKEN_KEY" \
  -H "mac-address: YOUR_REGISTERED_MAC_ADDRESS"
```

A successful response should return:
```json
{
  "message": "success",
  "object": {
    "token": "eyJhbGciOi..."
  }
}
```

---

## 3. 401 Unauthorized Troubleshooting Flow

If the integration returns a `401 Unauthorized` status or an `EkycFailed / ProviderError`, follow these diagnostics steps:

### Diagnostic Checklist

#### 1. Verify `Bearer` Prefix in Configuration
* **The Problem**: Developers often paste their `AccessToken` with the `"Bearer "` prefix already attached. In the old client implementation, the code appended `"Bearer "` directly to the string, causing the header to be sent as `Bearer Bearer <token_value>`.
* **The Fix**: The new client implementation automatically strips any pre-existing `"Bearer "` prefix (case-insensitively). However, if you are calling the raw API via Postman or cURL, ensure you do not double the `Bearer` keyword.

#### 2. Confirm the Active `AuthMode`
* **StaticToken Mode**: Ensure `VnptEkyc:AccessToken` is not empty, has not expired (VNPT tokens typically expire in 1 hour), and belongs to the active `TokenId`/`TokenKey` profile.
* **OAuth Mode**: Check that `VnptEkyc:TokenId` and `VnptEkyc:TokenKey` are correct. If the dynamic token flow fails, look at the server logs for:
  `VNPT OAuth token retrieval failed. HTTP <status>`

#### 3. Match Sandbox vs. Production Keys
* VNPT issues separate `TokenId` and `TokenKey` values for sandbox testing and production systems. 
* Sending a production `TokenId` to a sandbox baseUrl (or vice versa) will result in a 401 Unauthorized. Ensure `VnptEkyc:BaseUrl` matches the environment of the credentials.

#### 4. Validate Registered MAC Address
* VNPT eKYC requires requests to originate from/be signed with a registered MAC address.
* Ensure the value specified in `VnptEkyc:MacAddress` matches the MAC address registered in your VNPT Enterprise Console.
