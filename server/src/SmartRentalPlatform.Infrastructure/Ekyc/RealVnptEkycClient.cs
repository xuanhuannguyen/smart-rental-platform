using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Abstractions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.Ekyc;

public class RealVnptEkycClient : IVnptEkycClient
{
    public const string HttpClientName = "VnptEkyc";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VnptEkycOptions _options;
    private readonly IPrivateStorageService _storage;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RealVnptEkycClient> _logger;

    public RealVnptEkycClient(
        IHttpClientFactory httpClientFactory,
        IOptions<VnptEkycOptions> options,
        IPrivateStorageService storage,
        IMemoryCache cache,
        ILogger<RealVnptEkycClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _storage = storage;
        _cache = cache;
        _logger = logger;
    }

    public async Task<VnptEkycClientResult> VerifyAsync(
        VnptEkycVerifyInput input,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var diagnostics = new { UserId = input.UserId, SessionId = (string?)null };
        
        if (!HasCredentials())
        {
            _logger.LogWarning("VNPT eKYC credentials are not configured.");
            return ProviderFailure("VNPT_CREDENTIALS_MISSING", "VNPT credentials are not configured.");
        }

        var clientSession = BuildClientSession(input.UserId);
        diagnostics = new { UserId = input.UserId, SessionId = (string?)clientSession };
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            _logger.LogInformation("VNPT verification started. SessionId={SessionId}, DocumentType={DocumentType}", 
                clientSession, input.DocumentType);
            
            var accessToken = await GetOrFetchTokenAsync(httpClient, cancellationToken);
            var requestToken = BuildRequestToken(input.UserId);

            var frontHash = await UploadFromStorageAsync(
                httpClient, accessToken, input.FrontImageObjectKey, "front", cancellationToken);
            var backHash = await UploadFromStorageAsync(
                httpClient, accessToken, input.BackImageObjectKey, "back", cancellationToken);
            var selfieHash = await UploadFromStorageAsync(
                httpClient, accessToken, input.SelfieImageObjectKey, "selfie", cancellationToken);

            _logger.LogInformation("All images uploaded successfully. SessionId={SessionId}, UploadElapsedMs={ElapsedMs}", 
                clientSession, sw.ElapsedMilliseconds);

            var ocr = await CallOcrAsync(httpClient, accessToken, requestToken, frontHash, backHash, clientSession, cancellationToken);
            if (ocr.IsProviderFailure)
            {
                _logger.LogWarning("OCR provider failure. SessionId={SessionId}, ErrorCode={ErrorCode}, ElapsedMs={ElapsedMs}", 
                    clientSession, ocr.Result?.ErrorCode, sw.ElapsedMilliseconds);
                return ocr.Result!;
            }

            if (ocr.Result!.IsDocumentUnreadable)
            {
                _logger.LogWarning("Document unreadable from OCR. SessionId={SessionId}, DocumentCheckResult={DocumentCheckResult}, ElapsedMs={ElapsedMs}", 
                    clientSession, ocr.Result.DocumentCheckResult, sw.ElapsedMilliseconds);
                return ocr.Result;
            }

            var compare = await CallFaceCompareAsync(httpClient, accessToken, requestToken, frontHash, selfieHash, clientSession, cancellationToken);
            if (compare.IsProviderFailure)
            {
                _logger.LogWarning("Face compare provider failure. SessionId={SessionId}, ErrorCode={ErrorCode}, ElapsedMs={ElapsedMs}", 
                    clientSession, compare.Result?.ErrorCode, sw.ElapsedMilliseconds);
                return compare.Result!;
            }

            var liveness = await CallFaceLivenessAsync(httpClient, accessToken, requestToken, selfieHash, clientSession, cancellationToken);
            if (liveness.IsProviderFailure)
            {
                _logger.LogWarning("Face liveness provider failure. SessionId={SessionId}, ErrorCode={ErrorCode}, ElapsedMs={ElapsedMs}", 
                    clientSession, liveness.Result?.ErrorCode, sw.ElapsedMilliseconds);
                return liveness.Result!;
            }

            var result = BuildFinalResult(
                clientSession,
                ocr.Result!,
                compare.Result!,
                liveness.Result!);
                
            _logger.LogInformation("VNPT verification completed. SessionId={SessionId}, EkycResult={EkycResult}, RiskLevel={RiskLevel}, DocumentCheckResult={DocumentCheckResult}, FaceMatchScore={FaceMatchScore:F2}, FaceMatchResult={FaceMatchResult}, LivenessResult={LivenessResult}, TotalElapsedMs={ElapsedMs}", 
                clientSession, result.EkycResult, result.RiskLevel, result.DocumentCheckResult, 
                result.FaceMatchScore ?? 0m, result.FaceMatchResult, result.LivenessResult, sw.ElapsedMilliseconds);
                
            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "VNPT eKYC request timed out. SessionId={SessionId}, ElapsedMs={ElapsedMs}", clientSession, sw.ElapsedMilliseconds);
            return ProviderFailure("VNPT_TIMEOUT", "VNPT provider request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "VNPT eKYC HTTP failure. SessionId={SessionId}, ElapsedMs={ElapsedMs}", clientSession, sw.ElapsedMilliseconds);
            return ProviderFailure("VNPT_HTTP_ERROR", "VNPT provider connection failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VNPT eKYC unexpected failure. SessionId={SessionId}, ElapsedMs={ElapsedMs}", clientSession, sw.ElapsedMilliseconds);
            return ProviderFailure("VNPT_UNEXPECTED_ERROR", "VNPT provider returned an unexpected error.");
        }
    }

    private VnptEkycClientResult BuildFinalResult(
        string clientSession,
        VnptEkycClientResult ocr,
        VnptEkycClientResult compare,
        VnptEkycClientResult liveness)
    {
        var faceScore = compare.FaceMatchScore ?? 0m;
        var faceMsg = compare.FaceMatchResult ?? string.Empty;
        var livenessValue = liveness.LivenessResult ?? string.Empty;

        var highRisk = ocr.DocumentCheckResult == "Tampered"
            || string.Equals(faceMsg, "NotMatched", StringComparison.OrdinalIgnoreCase)
            || string.Equals(livenessValue, "Failed", StringComparison.OrdinalIgnoreCase);

        var mediumRisk = string.Equals(faceMsg, "LowConfidence", StringComparison.OrdinalIgnoreCase)
            || faceScore < NormalizeThresholdAsDecimal();

        var ekycResult = highRisk || mediumRisk ? "NeedReview" : "Passed";
        var riskLevel = highRisk
            ? KycRiskLevel.High
            : (mediumRisk ? KycRiskLevel.Medium : KycRiskLevel.Low);
        
        if (highRisk)
        {
            var reasons = new System.Text.StringBuilder();
            if (ocr.DocumentCheckResult == "Tampered")
                reasons.Append("DocumentTampered ");
            if (string.Equals(faceMsg, "NotMatched", StringComparison.OrdinalIgnoreCase))
                reasons.Append("FaceNotMatched ");
            if (string.Equals(livenessValue, "Failed", StringComparison.OrdinalIgnoreCase))
                reasons.Append("LivenessFailed");
                
            _logger.LogWarning("High risk detected. SessionId={SessionId}, Reasons={Reasons}", clientSession, reasons.ToString());
        }
        else if (mediumRisk)
        {
            var reasons = new System.Text.StringBuilder();
            if (string.Equals(faceMsg, "LowConfidence", StringComparison.OrdinalIgnoreCase))
                reasons.Append("FaceLowConfidence ");
            if (faceScore < NormalizeThresholdAsDecimal())
                reasons.Append($"ScoreBelowThreshold({faceScore:F2}vs{NormalizeThresholdAsDecimal():F2})");
                
            _logger.LogWarning("Medium risk detected. SessionId={SessionId}, Reasons={Reasons}", clientSession, reasons.ToString());
        }

        return new VnptEkycClientResult
        {
            SessionId = clientSession,
            EkycResult = ekycResult,
            
            OcrFullName = ocr.OcrFullName,
            OcrCitizenId = ocr.OcrCitizenId,
            OcrDateOfBirth = ocr.OcrDateOfBirth,
            OcrGender = ocr.OcrGender,
            OcrAddress = ocr.OcrAddress,
            OcrConfidence = ocr.OcrConfidence,
            DocumentCheckResult = ocr.DocumentCheckResult,
            FaceMatchScore = faceScore,
            FaceMatchResult = faceMsg,
            LivenessResult = livenessValue,
            RiskLevel = riskLevel
        };
    }

    private async Task<(bool IsProviderFailure, VnptEkycClientResult? Result)> CallOcrAsync(
        HttpClient httpClient,
        string accessToken,
        string requestToken,
        string frontHash,
        string backHash,
        string clientSession,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var payload = new VnptOcrRequest
        {
            ImgFront = frontHash,
            ImgBack = backHash,
            ClientSession = clientSession,
            Type = _options.OcrType,
            CropParam = _options.CropParam,
            ValidatePostcode = _options.ValidatePostcode,
            Token = requestToken
        };

        _logger.LogDebug("OCR request initiated. SessionId={SessionId}", clientSession);
        
        using var request = CreateJsonRequest(HttpMethod.Post, "ai/v1/ocr/id", payload, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT OCR HTTP {StatusCode}. SessionId={SessionId}, ElapsedMs={ElapsedMs}", 
                (int)response.StatusCode, clientSession, sw.ElapsedMilliseconds);
            var failure = BuildOcrHttpFailure(clientSession, response, body);
            return (failure.IsProviderFailure, failure);
        }

        var parsed = JsonSerializer.Deserialize<VnptGenericResponse>(body, JsonOptions);
        var obj = parsed?.Object;

        if (obj == null)
        {
            _logger.LogWarning("OCR response parse failed. SessionId={SessionId}, ElapsedMs={ElapsedMs}", 
                clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderFailure("VNPT_OCR_PARSE", "VNPT OCR response was invalid."));
        }

        var msg = obj.Msg?.Trim();
        var msgBack = obj.MsgBack?.Trim();
        if (!string.Equals(msg, "OK", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(msgBack, "OK", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("OCR document validation failed. SessionId={SessionId}, MsgFront={MsgFront}, MsgBack={MsgBack}, ElapsedMs={ElapsedMs}", 
                clientSession, msg, msgBack, sw.ElapsedMilliseconds);
            return (false, new VnptEkycClientResult
            {
                SessionId = clientSession,
                EkycResult = "Failed",
                DocumentCheckResult = "Unreadable",
                IsDocumentUnreadable = true,
                ErrorCode = ErrorCodes.EkycDocumentFailed,
                ErrorMessage = "Document verification failed."
            });
        }

        var documentCheck = "Valid";
        if (string.Equals(obj.IdFakeWarning, "yes", StringComparison.OrdinalIgnoreCase)
            || (obj.Tampering != null
                && !string.Equals(obj.Tampering.IsLegal, "yes", StringComparison.OrdinalIgnoreCase)))
        {
            documentCheck = "Tampered";
            _logger.LogWarning("Fake ID or tampering detected. SessionId={SessionId}, IdFakeWarning={IdFakeWarning}, ElapsedMs={ElapsedMs}", 
                clientSession, obj.IdFakeWarning, sw.ElapsedMilliseconds);
        }

        DateTime? dob = null;
        if (!string.IsNullOrWhiteSpace(obj.BirthDay)
            && DateTime.TryParse(obj.BirthDay, out var parsedDob))
        {
            dob = parsedDob;
        }

        var result = new VnptEkycClientResult
        {
            SessionId = clientSession,
            EkycResult = documentCheck == "Tampered" ? "NeedReview" : "Passed",
            OcrFullName = obj.Name,
            OcrCitizenId = obj.Id,
            OcrDateOfBirth = dob,
            OcrGender = obj.Gender,
            OcrAddress = obj.RecentLocation,
            OcrConfidence = 0.95m,
            DocumentCheckResult = documentCheck
        };
        
        _logger.LogInformation("OCR completed. SessionId={SessionId}, DocumentCheckResult={DocumentCheckResult}, OcrConfidence={OcrConfidence:F2}, ElapsedMs={ElapsedMs}", 
            clientSession, documentCheck, 0.95m, sw.ElapsedMilliseconds);
        
        return (false, result);
    }

    private async Task<(bool IsProviderFailure, VnptEkycClientResult? Result)> CallFaceCompareAsync(
        HttpClient httpClient,
        string accessToken,
        string requestToken,
        string frontHash,
        string selfieHash,
        string clientSession,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var payload = new VnptFaceCompareRequest
        {
            ImgFront = frontHash,
            ImgFace = selfieHash,
            ClientSession = clientSession,
            Token = requestToken
        };

        _logger.LogDebug("Face compare request initiated. SessionId={SessionId}", clientSession);
        
        using var request = CreateJsonRequest(HttpMethod.Post, "ai/v1/face/compare", payload, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT face compare HTTP {StatusCode}. SessionId={SessionId}, ElapsedMs={ElapsedMs}", 
                (int)response.StatusCode, clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderHttpFailure("VNPT_FACE_COMPARE_HTTP", response, body));
        }

        var parsed = JsonSerializer.Deserialize<VnptGenericResponse>(body, JsonOptions);
        var obj = parsed?.Object;
        if (obj == null)
        {
            _logger.LogWarning("Face compare response parse failed. SessionId={SessionId}, ElapsedMs={ElapsedMs}", 
                clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderFailure("VNPT_FACE_COMPARE_PARSE", "VNPT face compare response was invalid."));
        }

        var rawProb = obj.Prob ?? 0m;
        var normalizedScore = NormalizeFaceScore(rawProb);
        var msg = parsed?.Message ?? obj.Msg ?? string.Empty;

        string faceMatchResult;
        if (string.Equals(msg, "NOMATCH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(obj.Msg, "NOMATCH", StringComparison.OrdinalIgnoreCase))
        {
            faceMatchResult = "NotMatched";
            _logger.LogWarning("Face not matched. SessionId={SessionId}, RawProb={RawProb:F2}, ElapsedMs={ElapsedMs}", 
                clientSession, rawProb, sw.ElapsedMilliseconds);
        }
        else if (rawProb < (decimal)_options.FaceMatchThreshold)
        {
            faceMatchResult = "LowConfidence";
            _logger.LogWarning("Face confidence low. SessionId={SessionId}, RawProb={RawProb:F2}, Threshold={Threshold:F2}, ElapsedMs={ElapsedMs}", 
                clientSession, rawProb, (decimal)_options.FaceMatchThreshold, sw.ElapsedMilliseconds);
        }
        else
        {
            faceMatchResult = "Matched";
            _logger.LogInformation("Face matched. SessionId={SessionId}, NormalizedScore={Score:F4}, ElapsedMs={ElapsedMs}", 
                clientSession, normalizedScore, sw.ElapsedMilliseconds);
        }

        return (false, new VnptEkycClientResult
        {
            SessionId = clientSession,
            FaceMatchScore = normalizedScore,
            FaceMatchResult = faceMatchResult
        });
    }

    private async Task<(bool IsProviderFailure, VnptEkycClientResult? Result)> CallFaceLivenessAsync(
        HttpClient httpClient,
        string accessToken,
        string requestToken,
        string selfieHash,
        string clientSession,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var payload = new VnptFaceLivenessRequest
        {
            Img = selfieHash,
            ClientSession = clientSession,
            Token = requestToken
        };

        _logger.LogDebug("Face liveness request initiated. SessionId={SessionId}", clientSession);
        
        using var request = CreateJsonRequest(HttpMethod.Post, "ai/v1/face/liveness", payload, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT face liveness HTTP {StatusCode}. SessionId={SessionId}, ElapsedMs={ElapsedMs}", 
                (int)response.StatusCode, clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderHttpFailure("VNPT_LIVENESS_HTTP", response, body));
        }

        var parsed = JsonSerializer.Deserialize<VnptGenericResponse>(body, JsonOptions);
        var liveness = parsed?.Object?.Liveness ?? parsed?.Message;

        var passed = string.Equals(liveness, "success", StringComparison.OrdinalIgnoreCase);
        
        if (!passed)
        {
            _logger.LogWarning("Face liveness check failed. SessionId={SessionId}, LivenessValue={LivenessValue}, ElapsedMs={ElapsedMs}", 
                clientSession, liveness, sw.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogInformation("Face liveness check passed. SessionId={SessionId}, ElapsedMs={ElapsedMs}", 
                clientSession, sw.ElapsedMilliseconds);
        }

        return (false, new VnptEkycClientResult
        {
            SessionId = clientSession,
            LivenessResult = passed ? "Passed" : "Failed"
        });
    }

    private async Task<string> UploadFromStorageAsync(
        HttpClient httpClient,
        string token,
        string objectKey,
        string label,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogDebug("Uploading image from storage. ObjectKey={ObjectKey}, Label={Label}", objectKey, label);
        
        await using var stream = await _storage.OpenReadAsync(objectKey, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        
        _logger.LogDebug("Image read from storage. Label={Label}, FileSizeBytes={FileSize}, ReadElapsedMs={ElapsedMs}", 
            label, memory.Length, sw.ElapsedMilliseconds);

        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(memory);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", $"{label}.jpg");
        content.Add(new StringContent($"{label}-image"), "title");
        content.Add(new StringContent($"KYC {label} upload"), "description");

        using var request = CreateRequest(HttpMethod.Post, "file-service/v1/addFile", token);
        request.Content = content;

        var uploadSw = System.Diagnostics.Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT file upload HTTP {StatusCode} for {Label}, UploadElapsedMs={ElapsedMs}", 
                (int)response.StatusCode, label, uploadSw.ElapsedMilliseconds);
            throw new HttpRequestException($"VNPT file upload failed for {label}.");
        }

        var parsed = JsonSerializer.Deserialize<VnptUploadFileResponse>(body, JsonOptions);
        if (string.IsNullOrWhiteSpace(parsed?.Object?.Hash))
        {
            _logger.LogWarning("VNPT file upload returned no hash for {Label}, UploadElapsedMs={ElapsedMs}", 
                label, uploadSw.ElapsedMilliseconds);
            throw new InvalidOperationException($"VNPT file upload returned no hash for {label}.");
        }
        
        _logger.LogInformation("Image uploaded successfully. Label={Label}, Hash={Hash:N}, UploadElapsedMs={ElapsedMs}, TotalElapsedMs={TotalMs}", 
            label, parsed.Object.Hash.GetHashCode(), uploadSw.ElapsedMilliseconds, sw.ElapsedMilliseconds);

        return parsed.Object.Hash;
    }

    private async Task<string> GetOrFetchTokenAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool isStaticToken = string.Equals(_options.AuthMode, "StaticToken", StringComparison.OrdinalIgnoreCase)
                             && !string.IsNullOrWhiteSpace(_options.AccessToken);

        if (isStaticToken)
        {
            _logger.LogDebug("Using static auth token. ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
            return NormalizeBearerToken(_options.AccessToken);
        }

        var cacheKey = $"vnpt_ekyc_token_{_options.TokenId}_{_options.TokenKey}";
        if (_cache.TryGetValue<string>(cacheKey, out var cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            _logger.LogDebug("Token cache hit. CacheKey={CacheKey}, ElapsedMs={ElapsedMs}", cacheKey, sw.ElapsedMilliseconds);
            return cachedToken;
        }

        _logger.LogInformation("Fetching new OAuth token from VNPT. CacheKey={CacheKey}", cacheKey);
        
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token");
        request.Headers.TryAddWithoutValidation("Token-id", _options.TokenId);
        request.Headers.TryAddWithoutValidation("Token-key", _options.TokenKey);
        request.Headers.TryAddWithoutValidation("mac-address", _options.MacAddress);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT OAuth token retrieval failed. HTTP {StatusCode}, ElapsedMs={ElapsedMs}", 
                (int)response.StatusCode, sw.ElapsedMilliseconds);
            throw new HttpRequestException($"VNPT OAuth token retrieval failed. HTTP status {(int)response.StatusCode}");
        }

        var parsed = JsonSerializer.Deserialize<VnptTokenResponse>(body, JsonOptions);
        var token = NormalizeBearerToken(parsed?.Object?.Token);

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("VNPT OAuth token response was empty or invalid. ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
            throw new InvalidOperationException("VNPT OAuth token response was empty or invalid.");
        }

        var cacheDuration = TimeSpan.FromMinutes(_options.TokenCacheDurationMinutes > 0 ? _options.TokenCacheDurationMinutes : 50);
        _cache.Set(cacheKey, token, cacheDuration);
        
        _logger.LogInformation("New OAuth token cached. CacheKey={CacheKey}, CacheDurationMinutes={CacheDurationMinutes}, ElapsedMs={ElapsedMs}", 
            cacheKey, cacheDuration.TotalMinutes, sw.ElapsedMilliseconds);

        return token;
    }

    private HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string relativeUrl, T payload, string token)
    {
        var request = CreateRequest(method, relativeUrl, token);
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        return request;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string token)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.TryAddWithoutValidation("Token-id", _options.TokenId);
        request.Headers.TryAddWithoutValidation("Token-key", _options.TokenKey);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        request.Headers.TryAddWithoutValidation("mac-address", _options.MacAddress);
        return request;
    }

    private bool HasCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.TokenId) || string.IsNullOrWhiteSpace(_options.TokenKey))
        {
            return false;
        }

        if (string.Equals(_options.AuthMode, "StaticToken", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return true;
        }

        if (string.Equals(_options.AuthMode, "OAuth", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_options.AccessToken);
    }

    private static string BuildClientSession(Guid userId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"ANDROID_srp_api_0_Server_1_0_{userId:N}_{timestamp}";
    }

    private static string BuildRequestToken(Guid userId) =>
        $"srp{userId:N}{Guid.NewGuid():N}";

    private decimal NormalizeFaceScore(decimal rawProb) =>
        rawProb > 1m ? Math.Round(rawProb / 100m, 4) : rawProb;

    private decimal NormalizeThresholdAsDecimal() =>
        (decimal)_options.FaceMatchThreshold > 1m
            ? (decimal)_options.FaceMatchThreshold / 100m
            : (decimal)_options.FaceMatchThreshold;

    private static string NormalizeBearerToken(string? token)
    {
        var normalized = token?.Trim().Trim('"', '\'') ?? string.Empty;
        if (normalized.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Bearer ".Length..].Trim();
        }

        var whitespaceIndex = normalized.IndexOfAny([' ', '\r', '\n', '\t']);
        return whitespaceIndex > 0
            ? normalized[..whitespaceIndex]
            : normalized;
    }

    private static VnptEkycClientResult ProviderFailure(string errorCode, string message) =>
        new()
        {
            EkycResult = "ProviderError",
            IsProviderFailure = true,
            ErrorCode = errorCode,
            ErrorMessage = message
        };

    private static VnptEkycClientResult ProviderHttpFailure(
        string errorCode,
        HttpResponseMessage response,
        string body)
    {
        var statusCode = (int)response.StatusCode;
        var detail = string.IsNullOrWhiteSpace(body)
            ? "<empty body>"
            : body.Length > 1000 ? body[..1000] : body;

        return ProviderFailure(
            errorCode,
            $"VNPT HTTP {statusCode} {response.ReasonPhrase}. Body: {detail}");
    }

    private static VnptEkycClientResult BuildOcrHttpFailure(
        string clientSession,
        HttpResponseMessage response,
        string body)
    {
        if ((int)response.StatusCode == StatusCodes.Status400BadRequest
            && IsDocumentInputError(body))
        {
            return new VnptEkycClientResult
            {
                SessionId = clientSession,
                EkycResult = "Failed",
                DocumentCheckResult = "Unreadable",
                IsDocumentUnreadable = true,
                ErrorCode = ErrorCodes.EkycDocumentFailed,
                ErrorMessage = ExtractVnptErrorMessage(body)
                    ?? "Document image quality is not acceptable."
            };
        }

        return ProviderHttpFailure("VNPT_OCR_HTTP", response, body);
    }

    private static bool IsDocumentInputError(string body) =>
        body.Contains("IDG-00010003", StringComparison.OrdinalIgnoreCase)
        || body.Contains("input_khong_hop_le", StringComparison.OrdinalIgnoreCase)
        || body.Contains("Chất lượng ảnh", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractVnptErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                var values = errors
                    .EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                if (values.Length > 0)
                {
                    return string.Join(" ", values);
                }
            }

            if (root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
