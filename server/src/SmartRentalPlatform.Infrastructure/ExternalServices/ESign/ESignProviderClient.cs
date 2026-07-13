using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models.ESign;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.ESign;

public class ESignProviderClient : IESignProviderClient
{
    public const string HttpClientName = "ESignClient";
    private const int A4PageWidthPoints = 596;
    private const int A4PageHeightPoints = 842;
    private const int VnptSignatureVisibleType = 4;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    private readonly HttpClient _httpClient;
    private readonly ESignOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ESignProviderClient> _logger;

    public ESignProviderClient(
        IHttpClientFactory httpClientFactory, 
        IOptions<ESignOptions> options,
        IMemoryCache memoryCache,
        ILogger<ESignProviderClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _options = options.Value;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<CreateEnvelopeResult> CreateEnvelopeAsync(CreateEnvelopeInput input, CancellationToken cancellationToken = default)
    {
        var signersList = input.Signers.ToList();
        var landlordInput = signersList.FirstOrDefault(s => s.SignerRole == "Landlord");
        var tenantInput = signersList.FirstOrDefault(s => s.SignerRole == "Tenant");

        if (landlordInput == null || tenantInput == null)
        {
            throw new InvalidOperationException("VNPT eContract requires both Landlord and Tenant signers.");
        }

        foreach (var signer in signersList)
        {
            if (string.IsNullOrWhiteSpace(signer.Email))
            {
                throw new InvalidOperationException($"VNPT signer {signer.SignerRole} must have an email address.");
            }
        }

        var landlordPos = RequireSignaturePosition(input.SignatureZones, "Landlord");
        var tenantPos = RequireSignaturePosition(input.SignatureZones, "Tenant");

        _logger.LogInformation(
            "Using renderer-captured signature zones. Landlord: page {Page} ({X},{Y},{W},{H}); Tenant: page {Page2} ({X2},{Y2},{W2},{H2}).",
            landlordPos.Page, landlordPos.X, landlordPos.Y, landlordPos.W, landlordPos.H,
            tenantPos.Page, tenantPos.X, tenantPos.Y, tenantPos.W, tenantPos.H);

        var token = await GetAccessTokenAsync(cancellationToken);

        // 2. Upload file & Create contract (API: tao-hop-dong-tu-file)
        using var formData = new MultipartFormDataContent();
        var dataObject = new 
        {
            tenHopDong = input.Title,
            kieuHopDongId = 1,
            loaiTaiLieuId = int.Parse(_options.LoaiTaiLieuId)
        };
        var dataJson = JsonSerializer.Serialize(dataObject);
        formData.Add(new StringContent(dataJson, Encoding.UTF8, "application/json"), "data");

        var fileContent = new StreamContent(input.FileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formData.Add(fileContent, "files", "contract.pdf");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "econtract-integration-service/vnpt-econtract/tao-hop-dong-tu-file")
        {
            Content = formData
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var uploadResponse = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var uploadResponseString = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var providerError = ParseProviderError(uploadResponseString, (int)uploadResponse.StatusCode);
            _logger.LogError(
                "VNPT Upload failed. HTTP {HttpStatus}; provider code {ProviderCode}; provider status {ProviderStatus}.",
                (int)uploadResponse.StatusCode,
                providerError.Code,
                providerError.StatusCode);
            throw new Exception($"VNPT Upload failed ({providerError.Code ?? "unknown provider error"}).");
        }

        var uploadResult = JsonSerializer.Deserialize<VnptUploadResponse>(uploadResponseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (uploadResult?.Status != "OK" || uploadResult.Object == null)
        {
            throw new Exception($"VNPT Upload API returned provider code {uploadResult?.Message ?? "unknown"}.");
        }

        var hopDongId = uploadResult.Object.HopDongId;
        var hopDongChiTietId = uploadResult.Object.ListDetailContract?.FirstOrDefault()?.HopDongCtId;

        if (hopDongId == null || hopDongChiTietId == null)
        {
            throw new Exception("VNPT Upload API returned invalid IDs.");
        }

        // 3. Update signers using the provider demo API (cap-nhat-nguoi-ky)
        var signerInfos = new List<object>();

        if (landlordInput != null)
        {
            signerInfos.Add(new
            {
                p_tk_kh_id = (int?)null,
                p_ten_taikhoan = string.IsNullOrWhiteSpace(landlordInput.FullName) ? landlordInput.Email : landlordInput.FullName,
                p_email = landlordInput.Email,
                p_sdt = string.IsNullOrWhiteSpace(landlordInput.PhoneNumber) ? null : landlordInput.PhoneNumber,
                p_tinnhan = "Vui lòng ký hợp đồng",
                p_thoigian_xuly = (DateTime?)null,
                p_flag_chuyenky = 0,
                p_flag_themky = 0,
                p_vaitro_ky_id = 3, // 3: Ký dấu/Ký chính
                p_ht_ky = "3", // Temporarily disable SMS OTP because the VNPT account is out of SMS quota.
                p_thutu_bb_ky = landlordInput.SigningOrder,
                p_luong_nguoiky_id = (int?)null,
                p_thutu_ky = landlordInput.SigningOrder,
                p_flag_ky_tuantu = 1
            });
        }

        if (tenantInput != null)
        {
            signerInfos.Add(new
            {
                p_tk_kh_id = (int?)null,
                p_ten_taikhoan = string.IsNullOrWhiteSpace(tenantInput.FullName) ? tenantInput.Email : tenantInput.FullName,
                p_email = tenantInput.Email,
                p_sdt = string.IsNullOrWhiteSpace(tenantInput.PhoneNumber) ? null : tenantInput.PhoneNumber,
                p_tinnhan = "Vui lòng ký hợp đồng",
                p_thoigian_xuly = (DateTime?)null,
                p_flag_chuyenky = 0,
                p_flag_themky = 0,
                p_vaitro_ky_id = 3, // 3: Ký dấu/Ký chính
                p_ht_ky = "3", // Temporarily disable SMS OTP because the VNPT account is out of SMS quota.
                p_thutu_bb_ky = tenantInput.SigningOrder,
                p_luong_nguoiky_id = (int?)null,
                p_thutu_ky = tenantInput.SigningOrder,
                p_flag_ky_tuantu = 1
            });
        }

        var signersPayload = new
        {
            p_hop_dong_id = hopDongId,
            p_ds_nguoi_ky = new[]
            {
                new
                {
                    p_hd_chitiet_id = hopDongChiTietId,
                    p_ct_nk = signerInfos.ToArray()
                }
            }
        };

        var sendRequestMessage = new HttpRequestMessage(HttpMethod.Post, "econtract-integration-service/vnpt-econtract/cap-nhat-nguoi-ky")
        {
            Content = new StringContent(JsonSerializer.Serialize(signersPayload), Encoding.UTF8, "application/json")
        };
        sendRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var sendResponse = await _httpClient.SendAsync(sendRequestMessage, cancellationToken);
        var sendResponseString = await sendResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!sendResponse.IsSuccessStatusCode)
        {
            var providerError = ParseProviderError(sendResponseString, (int)sendResponse.StatusCode);
            _logger.LogError(
                "VNPT signer update failed. HTTP {HttpStatus}; provider code {ProviderCode}; provider status {ProviderStatus}; response body {ResponseBody}.",
                (int)sendResponse.StatusCode,
                providerError.Code,
                providerError.StatusCode,
                sendResponseString);
            throw new Exception($"VNPT signer update failed ({providerError.Code ?? "unknown provider error"}).");
        }

        using var sendDocument = JsonDocument.Parse(sendResponseString);
        var sendRoot = sendDocument.RootElement;
        if (!TryGetString(sendRoot, "status", out var sendStatus) || !string.Equals(sendStatus, "OK", StringComparison.OrdinalIgnoreCase))
        {
            TryGetString(sendRoot, "message", out var providerCode);
            _logger.LogError(
                "VNPT signer update returned non-OK status {ProviderStatus}; provider code {ProviderCode}; response body {ResponseBody}.",
                sendStatus,
                providerCode,
                sendResponseString);
            throw new Exception($"VNPT signer update returned provider code {providerCode ?? "unknown"}.");
        }

        var chiTietNguoiKy = ParseSignerDetails(sendRoot);
        if (chiTietNguoiKy.Count == 0)
        {
            throw new Exception("VNPT signer response did not contain CHITIET_NGUOIKY.");
        }

        _logger.LogInformation(
            "VNPT signer configuration completed for envelope {EnvelopeId}. Signer count: {SignerCount}.",
            hopDongId,
            chiTietNguoiKy.Count);
        
        return new CreateEnvelopeResult
        {
            IsSuccess = true,
            ProviderEnvelopeId = hopDongId.ToString(),
            Signers = input.Signers.Select(s => {
                var vnptSigner = chiTietNguoiKy.FirstOrDefault(x => 
                    (s.Email != null && s.Email.Equals(x.Email, StringComparison.OrdinalIgnoreCase)) ||
                    (s.PhoneNumber != null && s.PhoneNumber == x.SoDt));

                var pos = s.SignerRole == "Landlord" ? landlordPos : tenantPos;
                var evidence = new {
                    HdctId = hopDongChiTietId,
                    PositionX = pos.X,
                    PositionY = pos.Y,
                    PositionW = pos.W,
                    PositionH = pos.H,
                    PositionPage = pos.Page
                };

                return new ESignSignerResult
                {
                    UserId = s.UserId,
                    SignerRole = s.SignerRole,
                    ProviderParticipantId = vnptSigner?.HdctNguoiKyId?.ToString() ?? s.UserId.ToString(), 
                    ProviderAccessCode = vnptSigner?.MaTruyCap,
                    SigningUrl = vnptSigner?.UrlDinhKem ?? string.Empty,
                    ProviderEvidenceJson = JsonSerializer.Serialize(evidence)
                };
            }).ToList()
        };
    }

    public async Task<EnvelopeStatusResult> GetEnvelopeStatusAsync(string providerEnvelopeId, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        
        var request = new HttpRequestMessage(HttpMethod.Get, $"econtract-saas-service/api/hopdong/chitiet/{providerEnvelopeId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;

        var status = SigningEnvelopeStatus.WaitingForSigners;
        var reason = string.Empty;

        if (root.TryGetProperty("object", out var objElements) && objElements.ValueKind == JsonValueKind.Array && objElements.GetArrayLength() > 0)
        {
            var firstObj = objElements[0];
            if (firstObj.TryGetProperty("TRANGTHAI_ID", out var trangThaiIdElem))
            {
                var trangThaiId = trangThaiIdElem.GetInt32();
                // Map from VNPT Trạng thái
                // 4: Chờ ký, 10: Có hiệu lực, 7: Đã huỷ, 5: Ký lỗi
                status = trangThaiId switch
                {
                    10 => SigningEnvelopeStatus.Completed,
                    7 => SigningEnvelopeStatus.Cancelled,
                    5 => SigningEnvelopeStatus.Failed,
                    _ => SigningEnvelopeStatus.WaitingForSigners
                };
            }
            if (firstObj.TryGetProperty("TRANGTHAI_HD", out var trangThaiHdElem))
            {
                reason = trangThaiHdElem.GetString();
            }
        }

        return new EnvelopeStatusResult
        {
            IsSuccess = true,
            Status = status,
            ProviderEnvelopeId = providerEnvelopeId,
            ErrorMessage = reason
        };
    }

    public async Task<Stream> DownloadSignedPdfAsync(string providerEnvelopeId, CancellationToken cancellationToken = default)
    {
        // First we need hopDongChiTietId to download specific file
        var token = await GetAccessTokenAsync(cancellationToken);
        
        var request = new HttpRequestMessage(HttpMethod.Get, $"econtract-saas-service/api/hopdong/chitiet/{providerEnvelopeId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;
        
        int? hopDongChiTietId = null;

        if (root.TryGetProperty("object", out var objElements) && objElements.ValueKind == JsonValueKind.Array && objElements.GetArrayLength() > 0)
        {
            var firstObj = objElements[0];
            if (firstObj.TryGetProperty("CHITIET_FILE", out var fileElements) && fileElements.ValueKind == JsonValueKind.Array && fileElements.GetArrayLength() > 0)
            {
                if (fileElements[0].TryGetProperty("HD_CHITIET_ID", out var hdCtIdElem))
                {
                    hopDongChiTietId = hdCtIdElem.GetInt32();
                }
            }
        }

        if (hopDongChiTietId == null)
        {
            throw new Exception("Could not find HD_CHITIET_ID for the contract.");
        }

        var downloadRequest = new HttpRequestMessage(HttpMethod.Get, $"econtract-saas-service/api/hopdong/{providerEnvelopeId}/tai-file/{hopDongChiTietId}");
        downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var downloadResponse = await _httpClient.SendAsync(downloadRequest, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();

        var memStream = new MemoryStream();
        await downloadResponse.Content.CopyToAsync(memStream, cancellationToken);
        memStream.Position = 0;
        return memStream;
    }

    public Task<Stream?> DownloadEvidenceAsync(string providerEnvelopeId, CancellationToken cancellationToken = default)
    {
        // Not specifically documented as a separate endpoint in VNPT for evidence without Zip. 
        // Returning null for now as evidence is optional.
        return Task.FromResult<Stream?>(null);
    }

    public async Task<SendSignOtpResult> SendSignOtpAsync(
        string providerEnvelopeId,
        string providerDocumentDetailId,
        string signerContact,
        string providerAccessCode,
        ESignOtpMethod method,
        CancellationToken cancellationToken = default)
    {
        var token = await GetSignerAccessTokenAsync(signerContact, providerAccessCode, cancellationToken);
        var endpoint = method switch
        {
            ESignOtpMethod.EmailOtp => "econtract-integration-service/api/v1/tich-hop-ky/email-otp/khoi-tao",
            ESignOtpMethod.SmsOtp => "econtract-integration-service/api/v1/tich-hop-ky/sms-otp/khoi-tao",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Only VNPT methods 2 and 3 are supported.")
        };

        var requestBody = new
        {
            hopDongId = long.Parse(providerEnvelopeId),
            hdChiTietId = long.Parse(providerDocumentDetailId)
        };

        _logger.LogInformation(
            "VNPT SendOtp request. Method {Method}; Endpoint {Endpoint}; EnvelopeId {EnvelopeId}; DocumentDetailId {DocumentDetailId}.",
            method,
            endpoint,
            providerEnvelopeId,
            providerDocumentDetailId);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var providerError = ParseProviderError(responseString, (int)response.StatusCode);
            _logger.LogError(
                "VNPT SendOtp failed. HTTP {HttpStatus}; provider code {ProviderCode}; provider status {ProviderStatus}.",
                (int)response.StatusCode,
                providerError.Code,
                providerError.StatusCode);
            return new SendSignOtpResult
            {
                IsSuccess = false,
                ErrorMessage = providerError.Message,
                ProviderCode = providerError.Code,
                ProviderStatusCode = providerError.StatusCode
            };
        }

        var result = JsonSerializer.Deserialize<VnptSendOtpResponse>(responseString, JsonOptions);
        if (result == null || !string.Equals(result.Status, "OK", StringComparison.OrdinalIgnoreCase) || result.Object == null ||
            result.Object.OtpId == null || result.Object.HdctPhienKyId == null)
        {
            _logger.LogError(
                "VNPT SendOtp API returned an invalid response. Provider code {ProviderCode}; provider status {ProviderStatus}.",
                result?.Message,
                result?.StatusCode);
            return new SendSignOtpResult
            {
                IsSuccess = false,
                ErrorMessage = result?.Message ?? "Invalid VNPT OTP response",
                ProviderCode = result?.Message
            };
        }

        return new SendSignOtpResult
        {
            IsSuccess = true,
            OtpId = result.Object.OtpId,
            HdctPhienKyId = result.Object.HdctPhienKyId,
            ValiditySeconds = result.Object.ValiditySeconds,
            Destination = method == ESignOtpMethod.EmailOtp ? result.Object.Email : result.Object.PhoneNumber
        };
    }

    public async Task<SubmitSignOtpResult> SubmitSignOtpAsync(
        long otpId,
        long phienKyId,
        string otpCode,
        ESignSignatureImage signatureImage,
        string providerEvidenceJson,
        string signerContact,
        string providerAccessCode,
        ESignOtpMethod method,
        CancellationToken cancellationToken = default)
    {
        var token = await GetSignerAccessTokenAsync(signerContact, providerAccessCode, cancellationToken);
        using var evidenceDoc = JsonDocument.Parse(providerEvidenceJson);
        var evidence = evidenceDoc.RootElement;

        var (position, capturedRectangle) = BuildVnptSignPosition(evidence);

        object requestBody = method switch
        {
            ESignOtpMethod.EmailOtp => new
            {
                hopDongChiTietPhienKyId = phienKyId,
                otpId,
                otp = otpCode,
                listPosition = new[] { position },
                signerBy = true,
                signerDate = true,
                emailFlag = false,
                otpFlag = false,
                fontSize = 14,
                visibleType = VnptSignatureVisibleType,
                base64Image = signatureImage.Base64
            },
            ESignOtpMethod.SmsOtp => new
            {
                hopDongChiTietPhienKyId = phienKyId,
                otpId,
                otp = otpCode,
                listPosition = new[] { position },
                signerBy = true,
                signerDate = true,
                phoneNumberFlag = false,
                otpFlag = false,
                fontSize = 14,
                visibleType = VnptSignatureVisibleType,
                base64Image = signatureImage.Base64
            },
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Only VNPT methods 2 and 3 are supported.")
        };

        var endpoint = method == ESignOtpMethod.EmailOtp
            ? "econtract-integration-service/api/v1/tich-hop-ky/email-otp/hoan-thanh"
            : "econtract-integration-service/api/v1/tich-hop-ky/sms-otp/hoan-thanh";

        _logger.LogInformation(
            "Submitting VNPT OTP signature. Method {Method}; OtpId {OtpId}; SigningSessionId {SigningSessionId}; Page {Page}; CapturedRectangle {CapturedRectangle}; SubmittedRectangle {SubmittedRectangle}; VisibleType {VisibleType}; ImageType {ImageType}; ImageBytes {ImageBytes}; ImageDimensions {ImageWidth}x{ImageHeight}; ImageHashPrefix {ImageHashPrefix}.",
            method,
            otpId,
            phienKyId,
            position.Page,
            capturedRectangle,
            position.Rectangle,
            VnptSignatureVisibleType,
            signatureImage.MediaType,
            signatureImage.ByteLength,
            signatureImage.Width,
            signatureImage.Height,
            signatureImage.Sha256Hash[..12]);

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        _logger.LogInformation("VNPT SubmitOtp Request Payload: {Payload}", jsonPayload);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var providerError = ParseProviderError(responseString, (int)response.StatusCode);
            _logger.LogError(
                "VNPT SubmitOtp failed. HTTP {HttpStatus}; provider code {ProviderCode}; provider status {ProviderStatus}.",
                (int)response.StatusCode,
                providerError.Code,
                providerError.StatusCode);
            return new SubmitSignOtpResult
            {
                IsSuccess = false,
                ErrorMessage = providerError.Message,
                ProviderCode = providerError.Code,
                ProviderStatusCode = providerError.StatusCode
            };
        }

        var result = JsonSerializer.Deserialize<VnptSubmitOtpResponse>(responseString, JsonOptions);
        if (!string.Equals(result?.Status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "VNPT SubmitOtp API returned provider code {ProviderCode} with status {ProviderStatus}.",
                result?.Message,
                result?.StatusCode);
            return new SubmitSignOtpResult
            {
                IsSuccess = false,
                ErrorMessage = result?.Message ?? "Unknown error",
                ProviderCode = result?.Message,
                ProviderStatusCode = result?.StatusCode
            };
        }

        return new SubmitSignOtpResult { IsSuccess = true };
    }

    private static ProviderError ParseProviderError(string responseBody, int httpStatusCode)
    {
        try
        {
            var result = JsonSerializer.Deserialize<VnptErrorResponse>(responseBody, JsonOptions);
            var code = result?.Message ?? result?.Error?.FirstOrDefault();
            return new ProviderError(
                code,
                result?.StatusCode ?? httpStatusCode,
                string.IsNullOrWhiteSpace(code) ? "VNPT provider request failed." : code);
        }
        catch (JsonException)
        {
            return new ProviderError(null, httpStatusCode, "VNPT provider returned an invalid error response.");
        }
    }

    private async Task<string> GetSignerAccessTokenAsync(
        string signerContact,
        string providerAccessCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signerContact) || string.IsNullOrWhiteSpace(providerAccessCode))
        {
            throw new InvalidOperationException("VNPT signer contact and MA_TRUYCAP are required for login-ktk.");
        }

        var payload = new
        {
            emailSdt = signerContact,
            maTruyCap = providerAccessCode,
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            nenTangId = _options.NenTangId
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("users-profile-service/auth/login-ktk", content, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var providerError = ParseProviderError(responseString, (int)response.StatusCode);
            _logger.LogError(
                "VNPT login-ktk failed. HTTP {HttpStatus}; provider code {ProviderCode}; provider status {ProviderStatus}.",
                (int)response.StatusCode,
                providerError.Code,
                providerError.StatusCode);
            throw new InvalidOperationException("VNPT signer authentication failed.");
        }

        var loginResult = JsonSerializer.Deserialize<VnptLoginResponse>(responseString, JsonOptions);
        var signerAccessToken = loginResult?.Object?.AccessToken;
        if (!string.Equals(loginResult?.Status, "OK", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(signerAccessToken))
        {
            _logger.LogError("VNPT login-ktk returned an invalid response without an access token.");
            throw new InvalidOperationException("VNPT signer authentication returned an invalid response.");
        }

        return signerAccessToken;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var cacheKey = "VnptEContract_AccessToken";
        if (_memoryCache.TryGetValue(cacheKey, out string? token) && !string.IsNullOrEmpty(token))
        {
            return token;
        }

        var payload = new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            taiKhoan = _options.Username,
            matKhau = _options.Password,
            nenTangId = _options.NenTangId
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("users-profile-service/auth/login", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        var loginResult = JsonSerializer.Deserialize<VnptLoginResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (loginResult?.Status != "OK" || loginResult.Object?.AccessToken == null)
        {
            _logger.LogError("VNPT service login returned an invalid response without an access token.");
            throw new Exception("Failed to get VNPT AccessToken");
        }

        token = loginResult.Object.AccessToken;
        var cacheDuration = TimeSpan.FromMinutes(_options.TokenCacheDurationMinutes);
        _memoryCache.Set(cacheKey, token, cacheDuration);

        return token;
    }

    private static VnptSignaturePosition RequireSignaturePosition(
        IReadOnlyDictionary<string, SignatureZone> signatureZones,
        string signerRole)
    {
        if (signatureZones.Count != 2 || !signatureZones.TryGetValue(signerRole, out var zone))
        {
            throw new InvalidOperationException(
                "VNPT eContract requires exactly two renderer-captured signature zones: Landlord and Tenant.");
        }

        var right = (long)zone.X + zone.Width;
        var bottom = (long)zone.Y + zone.Height;
        if (zone.Page < 1 ||
            zone.X < 0 ||
            zone.Y < 0 ||
            zone.Width <= 0 ||
            zone.Height <= 0 ||
            right > A4PageWidthPoints ||
            bottom > A4PageHeightPoints)
        {
            throw new InvalidOperationException(
                $"Renderer-captured signature zone for {signerRole} is outside the A4 page bounds.");
        }

        return new VnptSignaturePosition
        {
            X = zone.X,
            Y = zone.Y,
            W = zone.Width,
            H = zone.Height,
            Page = zone.Page
        };
    }

    private static (VnptSignPosition Position, string CapturedRectangle) BuildVnptSignPosition(
        JsonElement evidence)
    {
        var x = evidence.GetProperty("PositionX").GetInt32();
        var y = evidence.GetProperty("PositionY").GetInt32();
        var width = evidence.GetProperty("PositionW").GetInt32();
        var height = evidence.GetProperty("PositionH").GetInt32();
        var page = evidence.GetProperty("PositionPage").GetInt32();

        var capturedRectangle = $"{x},{y},{x + width},{y + height}";
        var submittedY = A4PageHeightPoints - y - height;

        var position = new VnptSignPosition
        {
            Rectangle = $"{x},{submittedY},{x + width},{submittedY + height}",
            Page = page
        };

        return (position, capturedRectangle);
    }

    private static List<VnptSignerDetail> ParseSignerDetails(JsonElement root)
    {
        if (!root.TryGetProperty("object", out var objectElement))
        {
            return [];
        }

        JsonElement resultArray;
        if (objectElement.ValueKind == JsonValueKind.Array)
        {
            resultArray = objectElement;
        }
        else if (objectElement.ValueKind == JsonValueKind.Object &&
                 objectElement.TryGetProperty("guiHopDongResult", out var guiHopDongResult) &&
                 guiHopDongResult.ValueKind == JsonValueKind.Array)
        {
            resultArray = guiHopDongResult;
        }
        else
        {
            return [];
        }

        if (resultArray.GetArrayLength() == 0 ||
            !resultArray[0].TryGetProperty("CHI_TIET_HOP_DONG", out var contractDetail) ||
            !contractDetail.TryGetProperty("CHITIET_FILE", out var files) ||
            files.ValueKind != JsonValueKind.Array || files.GetArrayLength() == 0 ||
            !files[0].TryGetProperty("CHITIET_NGUOIKY", out var signers) ||
            signers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<VnptSignerDetail>();
        foreach (var signer in signers.EnumerateArray())
        {
            result.Add(new VnptSignerDetail
            {
                HdctNguoiKyId = TryGetInt32(signer, "HDCT_NGUOIKY_ID"),
                Email = GetOptionalString(signer, "EMAIL"),
                SoDt = GetOptionalString(signer, "SO_DT"),
                UrlDinhKem = GetOptionalString(signer, "URL_DINHKEM"),
                MaTruyCap = GetOptionalString(signer, "MA_TRUYCAP")
            });
        }

        return result;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private class VnptSignaturePosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Page { get; set; }
    }

    private class VnptLoginResponse
    {
        public string? Status { get; set; }
        public VnptLoginObject? Object { get; set; }
    }

    private class VnptLoginObject
    {
        public string? AccessToken { get; set; }
    }

    private class VnptUploadResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public VnptUploadObject? Object { get; set; }
    }

    private class VnptUploadObject
    {
        public int? HopDongId { get; set; }
        public List<VnptUploadDetail>? ListDetailContract { get; set; }
    }

    private class VnptUploadDetail
    {
        public int? HopDongCtId { get; set; }
    }

    private class VnptSendResponse
    {
        public string? Status { get; set; }
        public List<VnptSendObject>? Object { get; set; }
    }

    private class VnptSendObject
    {
        [JsonPropertyName("CHI_TIET_HOP_DONG")]
        public VnptSendDetail? ChiTietHopDong { get; set; }
    }

    private class VnptSendDetail
    {
        [JsonPropertyName("CHITIET_FILE")]
        public List<VnptFileDetail>? ChiTietFile { get; set; }
    }

    private class VnptFileDetail
    {
        [JsonPropertyName("CHITIET_NGUOIKY")]
        public List<VnptSignerDetail>? ChiTietNguoiKy { get; set; }
    }

    private class VnptSignerDetail
    {
        [JsonPropertyName("HDCT_NGUOIKY_ID")]
        public int? HdctNguoiKyId { get; set; }

        [JsonPropertyName("EMAIL")]
        public string? Email { get; set; }

        [JsonPropertyName("SO_DT")]
        public string? SoDt { get; set; }

        [JsonPropertyName("URL_DINHKEM")]
        public string? UrlDinhKem { get; set; }

        [JsonPropertyName("MA_TRUYCAP")]
        public string? MaTruyCap { get; set; }
    }

    private class VnptSendOtpResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public int? StatusCode { get; set; }
        public VnptSendOtpObject? Object { get; set; }
    }

    private class VnptSendOtpObject
    {
        public long? OtpId { get; set; }
        [JsonPropertyName("hdctPhienKyId")]
        public long? HdctPhienKyId { get; set; }
        [JsonPropertyName("soGiaythoiGianHieuLucConfig")]
        public int? ValiditySeconds { get; set; }
        public string? Email { get; set; }
        [JsonPropertyName("soDienThoai")]
        public string? PhoneNumber { get; set; }
    }

    private sealed class VnptSignPosition
    {
        [JsonPropertyName("RECTANGLE")]
        public string Rectangle { get; set; } = string.Empty;

        [JsonPropertyName("PAGE")]
        public int Page { get; set; }
    }

    private class VnptSubmitOtpResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public int? StatusCode { get; set; }
    }

    private sealed class VnptErrorResponse
    {
        public int? StatusCode { get; set; }
        public string? Message { get; set; }
        public string[]? Error { get; set; }
    }

    private readonly record struct ProviderError(string? Code, int? StatusCode, string Message);
}
