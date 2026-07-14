using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.Ekyc;

public class RealVnptEkycClient : IVnptEkycClient
{
    public const string HttpClientName = "VnptEkyc";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VnptEkycOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RealVnptEkycClient> _logger;

    public RealVnptEkycClient(
        IHttpClientFactory httpClientFactory,
        IOptions<VnptEkycOptions> options,
        IMemoryCache cache,
        ILogger<RealVnptEkycClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
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

            var requestToken = BuildRequestToken(input.UserId);

            var frontHash = await UploadImageAsync(
                httpClient, input.FrontImage, "front", cancellationToken);
            var backHash = await UploadImageAsync(
                httpClient, input.BackImage, "back", cancellationToken);

            _logger.LogInformation("Document images uploaded successfully. SessionId={SessionId}, UploadElapsedMs={ElapsedMs}",
                clientSession, sw.ElapsedMilliseconds);

            var ocr = await CallOcrAsync(httpClient, requestToken, frontHash, backHash, clientSession, cancellationToken);
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

            if (string.IsNullOrWhiteSpace(ocr.Result.OcrAddress))
            {
                var frontOcr = await CallFrontOcrAsync(httpClient, requestToken, frontHash, clientSession, cancellationToken);
                if (!frontOcr.IsProviderFailure && frontOcr.Result is not null && !frontOcr.Result.IsDocumentUnreadable)
                {
                    ocr.Result = MergeOcrFallback(ocr.Result, frontOcr.Result);
                }
            }

            if (input.DocumentOnly || !_options.EnableFaceVerification)
            {
                var documentOnlyResult = BuildDocumentOnlyResult(clientSession, ocr.Result!);

                _logger.LogInformation("VNPT document-only verification completed. SessionId={SessionId}, EkycResult={EkycResult}, RiskLevel={RiskLevel}, DocumentCheckResult={DocumentCheckResult}, TotalElapsedMs={ElapsedMs}",
                    clientSession,
                    documentOnlyResult.EkycResult,
                    documentOnlyResult.RiskLevel,
                    documentOnlyResult.DocumentCheckResult,
                    sw.ElapsedMilliseconds);

                return documentOnlyResult;
            }

            if (input.SelfieImage is null)
            {
                throw new InvalidOperationException("Selfie image is required when face verification is enabled.");
            }

            var selfieHash = await UploadImageAsync(
                httpClient, input.SelfieImage, "selfie", cancellationToken);

            var compare = await CallFaceCompareAsync(httpClient, requestToken, frontHash, selfieHash, clientSession, cancellationToken);
            if (compare.IsProviderFailure)
            {
                _logger.LogWarning("Face compare provider failure. SessionId={SessionId}, ErrorCode={ErrorCode}, ElapsedMs={ElapsedMs}",
                    clientSession, compare.Result?.ErrorCode, sw.ElapsedMilliseconds);
                return compare.Result!;
            }

            var liveness = await CallFaceLivenessAsync(httpClient, requestToken, selfieHash, clientSession, cancellationToken);
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

    private static VnptEkycClientResult BuildDocumentOnlyResult(
        string clientSession,
        VnptEkycClientResult ocr)
    {
        var documentValid = string.Equals(ocr.DocumentCheckResult, "Valid", StringComparison.OrdinalIgnoreCase);
        var documentTampered = string.Equals(ocr.DocumentCheckResult, "Tampered", StringComparison.OrdinalIgnoreCase);
        var lowOcrConfidence = (ocr.OcrConfidence ?? 1m) < 0.85m;

        var riskLevel = documentTampered
            ? KycRiskLevel.High
            : (!documentValid || lowOcrConfidence ? KycRiskLevel.Medium : KycRiskLevel.Low);

        return new VnptEkycClientResult
        {
            SessionId = clientSession,
            EkycResult = riskLevel == KycRiskLevel.High ? "NeedReview" : (documentValid && !lowOcrConfidence ? "Passed" : "NeedReview"),
            OcrFullName = ocr.OcrFullName,
            OcrCitizenId = ocr.OcrCitizenId,
            OcrDateOfBirth = ocr.OcrDateOfBirth,
            OcrGender = ocr.OcrGender,
            OcrAddress = ocr.OcrAddress,
            OcrConfidence = ocr.OcrConfidence,
            DocumentCheckResult = ocr.DocumentCheckResult,
            FaceMatchScore = null,
            FaceMatchResult = FaceMatchResult.NotSupported.ToString(),
            LivenessResult = LivenessResult.NotSupported.ToString(),
            RiskLevel = riskLevel
        };
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

        using var request = await CreateJsonRequestAsync(httpClient, HttpMethod.Post, "ai/v1/ocr/id", payload, cancellationToken);
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

        if (!IsOk(obj.Msg) || !IsOk(obj.MsgBack))
        {
            _logger.LogWarning("OCR document validation failed. SessionId={SessionId}, MsgFront={MsgFront}, MsgBack={MsgBack}, ElapsedMs={ElapsedMs}",
                clientSession, obj.Msg?.Trim(), obj.MsgBack?.Trim(), sw.ElapsedMilliseconds);
            return (false, BuildUnreadableOcrResult(clientSession, "Document verification failed."));
        }

        var documentCheck = ResolveDocumentCheck(obj);
        if (IsTampered(documentCheck))
        {
            _logger.LogWarning("Fake ID or tampering detected. SessionId={SessionId}, IdFakeWarning={IdFakeWarning}, ElapsedMs={ElapsedMs}",
                clientSession, obj.IdFakeWarning, sw.ElapsedMilliseconds);
        }

        var result = MapOcrResult(clientSession, obj, documentCheck);
        if (string.IsNullOrWhiteSpace(result.OcrAddress))
        {
            _logger.LogInformation(
                "VNPT OCR did not include a mapped address field. SessionId={SessionId}, ObjectKeys={ObjectKeys}",
                clientSession,
                BuildOcrObjectKeyList(obj));
        }

        _logger.LogInformation("OCR completed. SessionId={SessionId}, DocumentCheckResult={DocumentCheckResult}, OcrConfidence={OcrConfidence:F2}, ElapsedMs={ElapsedMs}",
            clientSession, documentCheck, 0.95m, sw.ElapsedMilliseconds);

        return (false, result);
    }

    private async Task<(bool IsProviderFailure, VnptEkycClientResult? Result)> CallFrontOcrAsync(
        HttpClient httpClient,
        string requestToken,
        string frontHash,
        string clientSession,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var payload = new VnptOcrFrontRequest
        {
            ImgFront = frontHash,
            ClientSession = clientSession,
            Type = _options.OcrType,
            ValidatePostcode = _options.ValidatePostcode,
            Token = requestToken
        };

        using var request = await CreateJsonRequestAsync(httpClient, HttpMethod.Post, "ai/v1/ocr/id/front", payload, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT front OCR HTTP {StatusCode}. SessionId={SessionId}, ElapsedMs={ElapsedMs}",
                (int)response.StatusCode, clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderHttpFailure("VNPT_FRONT_OCR_HTTP", response));
        }

        var parsed = JsonSerializer.Deserialize<VnptGenericResponse>(body, JsonOptions);
        var obj = parsed?.Object;

        if (obj is null)
        {
            _logger.LogWarning("VNPT front OCR parse failed. SessionId={SessionId}, ElapsedMs={ElapsedMs}",
                clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderFailure("VNPT_FRONT_OCR_PARSE", "VNPT front OCR response was invalid."));
        }

        if (!IsOk(obj.Msg))
        {
            _logger.LogWarning("VNPT front OCR validation failed. SessionId={SessionId}, MsgFront={MsgFront}, ElapsedMs={ElapsedMs}",
                clientSession, obj.Msg?.Trim(), sw.ElapsedMilliseconds);
            return (false, BuildUnreadableOcrResult(clientSession, "Front document OCR failed."));
        }

        var documentCheck = ResolveDocumentCheck(obj);
        var result = MapOcrResult(clientSession, obj, documentCheck);

        _logger.LogInformation(
            "VNPT front OCR completed. SessionId={SessionId}, HasAddress={HasAddress}, DocumentCheckResult={DocumentCheckResult}, ElapsedMs={ElapsedMs}",
            clientSession,
            !string.IsNullOrWhiteSpace(result.OcrAddress),
            documentCheck,
            sw.ElapsedMilliseconds);

        return (false, result);
    }

    private static VnptEkycClientResult MergeOcrFallback(
        VnptEkycClientResult primary,
        VnptEkycClientResult fallback)
    {
        var fallbackTampered = string.Equals(fallback.DocumentCheckResult, "Tampered", StringComparison.OrdinalIgnoreCase);

        return new VnptEkycClientResult
        {
            SessionId = primary.SessionId ?? fallback.SessionId,
            EkycResult = fallbackTampered ? "NeedReview" : primary.EkycResult,
            OcrFullName = FirstNonEmpty(primary.OcrFullName, fallback.OcrFullName),
            OcrCitizenId = FirstNonEmpty(primary.OcrCitizenId, fallback.OcrCitizenId),
            OcrDateOfBirth = primary.OcrDateOfBirth ?? fallback.OcrDateOfBirth,
            OcrGender = FirstNonEmpty(primary.OcrGender, fallback.OcrGender),
            OcrAddress = FirstNonEmpty(primary.OcrAddress, fallback.OcrAddress),
            OcrConfidence = primary.OcrConfidence ?? fallback.OcrConfidence,
            DocumentCheckResult = fallbackTampered
                ? fallback.DocumentCheckResult
                : (primary.DocumentCheckResult ?? fallback.DocumentCheckResult),
            FaceMatchScore = primary.FaceMatchScore,
            FaceMatchResult = primary.FaceMatchResult,
            LivenessResult = primary.LivenessResult,
            RiskLevel = primary.RiskLevel,
            ErrorCode = primary.ErrorCode,
            ErrorMessage = primary.ErrorMessage,
            IsProviderFailure = primary.IsProviderFailure,
            IsDocumentUnreadable = primary.IsDocumentUnreadable
        };
    }

    private static VnptEkycClientResult MapOcrResult(
        string clientSession,
        VnptOcrObject obj,
        string documentCheck)
    {
        return new VnptEkycClientResult
        {
            SessionId = clientSession,
            EkycResult = IsTampered(documentCheck) ? "NeedReview" : "Passed",
            OcrFullName = obj.Name,
            OcrCitizenId = obj.Id,
            OcrDateOfBirth = ParseVnptDate(obj.BirthDay ?? obj.BirthdayAlternative ?? obj.Dob),
            OcrGender = obj.Gender,
            OcrAddress = ResolveOcrAddress(obj),
            OcrConfidence = 0.95m,
            DocumentCheckResult = documentCheck
        };
    }

    private static VnptEkycClientResult BuildUnreadableOcrResult(
        string clientSession,
        string message)
    {
        return new VnptEkycClientResult
        {
            SessionId = clientSession,
            EkycResult = "Failed",
            DocumentCheckResult = "Unreadable",
            IsDocumentUnreadable = true,
            ErrorCode = ErrorCodes.EkycDocumentFailed,
            ErrorMessage = message
        };
    }

    private static string ResolveDocumentCheck(VnptOcrObject obj)
    {
        return string.Equals(obj.IdFakeWarning, "yes", StringComparison.OrdinalIgnoreCase)
            || (obj.Tampering != null && !string.Equals(obj.Tampering.IsLegal, "yes", StringComparison.OrdinalIgnoreCase))
            ? "Tampered"
            : "Valid";
    }

    private static bool IsTampered(string? documentCheck)
    {
        return string.Equals(documentCheck, "Tampered", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOk(string? value)
    {
        return string.Equals(value?.Trim(), "OK", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? ParseVnptDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleanValue = value.Trim();

        // 1. Try parsing with known VNPT formats using InvariantCulture
        var formats = new[] 
        { 
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", 
            "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d", 
            "dd.MM.yyyy", "d.M.yyyy", "ddMMyyyy", "dMyyyy" 
        };
        if (DateTime.TryParseExact(cleanValue, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        // 2. Try parsing with InvariantCulture fallback
        if (DateTime.TryParse(cleanValue, System.Globalization.CultureInfo.InvariantCulture, out var fallbackParsed))
        {
            return fallbackParsed;
        }

        // 3. Try parsing with Vietnamese culture
        try
        {
            var viCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            if (DateTime.TryParse(cleanValue, viCulture, out var viParsed))
            {
                return viParsed;
            }
        }
        catch
        {
            // Ignore if culture is not available on the operating system
        }

        // 4. Try standard DateTime.TryParse as final fallback
        if (DateTime.TryParse(cleanValue, out var finalParsed))
        {
            return finalParsed;
        }

        return null;
    }

    private static string? ResolveOcrAddress(VnptOcrObject obj)
    {
        var knownAddress = FirstNonEmpty(
            obj.RecentLocation,
            obj.Address,
            obj.PermanentAddress,
            obj.Residence,
            obj.PlaceOfResidence,
            obj.Home,
            obj.OriginLocation);

        if (!string.IsNullOrWhiteSpace(knownAddress))
        {
            return knownAddress;
        }

        if (obj.ExtraFields is null)
        {
            return null;
        }

        foreach (var (key, value) in obj.ExtraFields)
        {
            if (!LooksLikeAddressKey(key))
            {
                continue;
            }

            var text = ExtractFirstString(value);
            var normalizedText = NormalizeMeaningfulText(text);
            if (normalizedText is not null)
            {
                return normalizedText;
            }
        }

        return null;
    }

    private static bool LooksLikeAddressKey(string key)
    {
        return key.Contains("address", StringComparison.OrdinalIgnoreCase)
            || key.Contains("location", StringComparison.OrdinalIgnoreCase)
            || key.Contains("residence", StringComparison.OrdinalIgnoreCase)
            || key.Contains("permanent", StringComparison.OrdinalIgnoreCase)
            || key.Contains("origin", StringComparison.OrdinalIgnoreCase)
            || key.Contains("home", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractFirstString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var value = ExtractFirstString(property.Value);
                if (IsMeaningfulText(value))
                {
                    return value;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = ExtractFirstString(item);
                if (IsMeaningfulText(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string BuildOcrObjectKeyList(VnptOcrObject obj)
    {
        var knownKeys = new[]
        {
            "name",
            "id",
            "birth_day",
            "gender",
            "recent_location",
            "address",
            "home",
            "origin_location",
            "permanent_address",
            "residence",
            "place_of_residence",
            "msg",
            "msg_back",
            "id_fake_warning",
            "tampering",
            "prob",
            "liveness"
        };

        if (obj.ExtraFields is null || obj.ExtraFields.Count == 0)
        {
            return string.Join(", ", knownKeys);
        }

        return string.Join(", ", knownKeys.Concat(obj.ExtraFields.Keys).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var normalizedValue = NormalizeMeaningfulText(value);
            if (normalizedValue is not null)
            {
                return normalizedValue;
            }
        }

        return null;
    }

    private static bool IsMeaningfulText(string? value)
    {
        return NormalizeMeaningfulText(value) is not null;
    }

    private static string? NormalizeMeaningfulText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed != "-"
            && !trimmed.Equals("--", StringComparison.Ordinal)
            && !trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Equals("NA", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : null;
    }

    private async Task<(bool IsProviderFailure, VnptEkycClientResult? Result)> CallFaceCompareAsync(
        HttpClient httpClient,
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

        using var request = await CreateJsonRequestAsync(httpClient, HttpMethod.Post, "ai/v1/face/compare", payload, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT face compare HTTP {StatusCode}. SessionId={SessionId}, ElapsedMs={ElapsedMs}",
                (int)response.StatusCode, clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderHttpFailure("VNPT_FACE_COMPARE_HTTP", response));
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

        using var request = await CreateJsonRequestAsync(httpClient, HttpMethod.Post, "ai/v1/face/liveness", payload, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VNPT face liveness HTTP {StatusCode}. SessionId={SessionId}, ElapsedMs={ElapsedMs}",
                (int)response.StatusCode, clientSession, sw.ElapsedMilliseconds);
            return (true, ProviderHttpFailure("VNPT_LIVENESS_HTTP", response));
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

    private async Task<string> UploadImageAsync(
        HttpClient httpClient,
        VnptEkycFileInput file,
        string label,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var memory = new MemoryStream();
        await file.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        _logger.LogDebug("Image prepared for VNPT upload. Label={Label}, FileName={FileName}, FileSizeBytes={FileSize}, ReadElapsedMs={ElapsedMs}",
            label, file.FileName, memory.Length, sw.ElapsedMilliseconds);

        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(memory);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(NormalizeContentType(file.ContentType));
        content.Add(fileContent, "file", ResolveUploadFileName(file.FileName, label));
        content.Add(new StringContent($"{label}-image"), "title");
        content.Add(new StringContent($"KYC {label} upload"), "description");

        // Upload endpoint does NOT use mac-address — only file-service endpoints
        using var request = new HttpRequestMessage(HttpMethod.Post, "file-service/v1/addFile");
        await AddAuthHeadersAsync(request, httpClient, cancellationToken);
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

    private static string ResolveUploadFileName(string? fileName, string label)
    {
        var trimmed = fileName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return Path.GetFileName(trimmed);
        }

        return $"{label}.jpg";
    }

    private static string NormalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
    }

    private Task AddAuthHeadersAsync(HttpRequestMessage request, HttpClient httpClient, CancellationToken ct)
    {
        var bearerToken = GetValidToken();
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken}");
        request.Headers.TryAddWithoutValidation("Token-id", _options.TokenId);
        request.Headers.TryAddWithoutValidation("Token-key", _options.TokenKey);
        return Task.CompletedTask;
    }

    private string GetValidToken()
    {
        var cacheKey = $"vnpt_ekyc_token_{_options.TokenId}";

        if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        var token = NormalizeBearerToken(_options.AccessToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("VNPT AccessToken is not configured. " +
                             "Update AccessToken in appsettings from the VNPT eKYC portal (Quản lý token).");
            return string.Empty;
        }

        var expiry = GetJwtExpiry(token);
        if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
        {
            // Token expired — log clearly so developer knows to update it
            _logger.LogError(
                "VNPT AccessToken expired at {Expiry:u}. " +
                "Please log in to the VNPT eKYC portal, get a new token from \"Quản lý token\", " +
                "and update AccessToken in appsettings.",
                expiry.Value);
            // Return expired token anyway — VNPT will return 401, which surfaces as ProviderFailure
            return token;
        }

        // Warn when token will expire within 2 hours
        if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow.AddHours(2))
        {
            _logger.LogWarning("VNPT AccessToken expires soon at {Expiry:u}. Update it before then.", expiry.Value);
        }
        else
        {
            _logger.LogDebug("VNPT token valid, expires at {Expiry:u}", expiry);
        }

        var cacheDuration = expiry.HasValue
            ? expiry.Value.AddMinutes(-5) - DateTimeOffset.UtcNow
            : TimeSpan.FromHours(23);

        if (cacheDuration > TimeSpan.Zero)
            _cache.Set(cacheKey, token, cacheDuration);

        return token;
    }


    private static string NormalizeBearerToken(string token)
    {
        var normalized = token?.Trim() ?? string.Empty;
        if (normalized.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["Bearer ".Length..].Trim();
        return normalized;
    }

    private static DateTimeOffset? GetJwtExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

            return doc.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64())
                : null;
        }
        catch
        {
            return null;
        }
    }


    private async Task<HttpRequestMessage> CreateJsonRequestAsync<T>(HttpClient httpClient, HttpMethod method, string relativeUrl, T payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        await AddAuthHeadersAsync(request, httpClient, ct);
        request.Headers.TryAddWithoutValidation("mac-address", _options.MacAddress);
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        return request;
    }

    private bool HasCredentials() =>
        !string.IsNullOrWhiteSpace(_options.AccessToken)
        && !string.IsNullOrWhiteSpace(_options.TokenId)
        && !string.IsNullOrWhiteSpace(_options.TokenKey);

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
        HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;

        return ProviderFailure(
            errorCode,
            $"VNPT HTTP {statusCode} {response.ReasonPhrase}.");
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

        return ProviderHttpFailure("VNPT_OCR_HTTP", response);
    }

    private static bool IsDocumentInputError(string body) =>
        body.Contains("IDG-00010003", StringComparison.OrdinalIgnoreCase)
        || body.Contains("input_khong_hop_le", StringComparison.OrdinalIgnoreCase)
        || body.Contains("Chất lượng ảnh", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractVnptErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

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
                    return string.Join(" ", values);
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

