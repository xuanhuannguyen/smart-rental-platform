using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Models.ESign;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Infrastructure.ExternalServices.ESign;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.UnitTests.ESign;

public class ESignProviderClientTests
{
    [Fact]
    public async Task CreateEnvelopeAsync_MissingSignerEmail_FailsBeforeCallingVnpt()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);
        var input = new CreateEnvelopeInput
        {
            FileStream = Stream.Null,
            Signers =
            [
                new ESignSignerInput
                {
                    SignerRole = "Landlord",
                    Email = string.Empty,
                    PhoneNumber = string.Empty
                },
                new ESignSignerInput
                {
                    SignerRole = "Tenant",
                    Email = "tenant@example.com",
                    PhoneNumber = "0900000000"
                }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateEnvelopeAsync(input));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateEnvelopeAsync_MissingSignatureZones_FailsBeforeCallingVnpt()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);
        var input = CreateValidEnvelopeInput();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateEnvelopeAsync(input));

        Assert.Contains("renderer-captured signature zones", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateEnvelopeAsync_InvalidSignatureZone_FailsBeforeCallingVnpt()
    {
        var handler = new RecordingHandler();
        var client = CreateClient(handler);
        var input = CreateValidEnvelopeInput();
        input.SignatureZones = new Dictionary<string, SignatureZone>
        {
            ["Landlord"] = new() { X = 40, Y = 700, Width = 250, Height = 105, Page = 2 },
            ["Tenant"] = new() { X = 320, Y = 800, Width = 250, Height = 105, Page = 2 }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateEnvelopeAsync(input));

        Assert.Contains("outside the A4 page bounds", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateEnvelopeAsync_ValidSignatureZones_AreStoredAsProviderEvidence()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"accessToken":"admin-token"}}"""),
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"hopDongId":1001,"listDetailContract":[{"hopDongCtId":2002}]}}"""),
            Json(HttpStatusCode.OK, """
                {
                  "status": "OK",
                  "object": [
                    {
                      "CHI_TIET_HOP_DONG": {
                        "CHITIET_FILE": [
                          {
                            "CHITIET_NGUOIKY": [
                              { "HDCT_NGUOIKY_ID": 11, "EMAIL": "owner@example.com", "SO_DT": "0900000001", "URL_DINHKEM": "owner-url", "MA_TRUYCAP": "owner-code" },
                              { "HDCT_NGUOIKY_ID": 12, "EMAIL": "tenant@example.com", "SO_DT": "0900000002", "URL_DINHKEM": "tenant-url", "MA_TRUYCAP": "tenant-code" }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
                """));
        var client = CreateClient(handler);
        var input = CreateValidEnvelopeInput();
        input.SignatureZones = new Dictionary<string, SignatureZone>
        {
            ["Landlord"] = new() { X = 40, Y = 620, Width = 250, Height = 105, Page = 3 },
            ["Tenant"] = new() { X = 305, Y = 620, Width = 250, Height = 105, Page = 3 }
        };

        var result = await client.CreateEnvelopeAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("\"p_ht_ky\":\"3\"", handler.Requests[2].Body);
        Assert.DoesNotContain("\"p_ht_ky\":\"2,3\"", handler.Requests[2].Body);
        var landlordEvidence = JsonDocument.Parse(result.Signers.Single(x => x.SignerRole == "Landlord").ProviderEvidenceJson!);
        var tenantEvidence = JsonDocument.Parse(result.Signers.Single(x => x.SignerRole == "Tenant").ProviderEvidenceJson!);
        Assert.Equal(40, landlordEvidence.RootElement.GetProperty("PositionX").GetInt32());
        Assert.Equal(3, landlordEvidence.RootElement.GetProperty("PositionPage").GetInt32());
        Assert.Equal(305, tenantEvidence.RootElement.GetProperty("PositionX").GetInt32());
        Assert.Equal(3, tenantEvidence.RootElement.GetProperty("PositionPage").GetInt32());
    }

    [Fact]
    public async Task SendSignOtpAsync_Email_UsesKtkTokenAndDocumentDetailId()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"accessToken":"signer-token"}}"""),
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"otpId":91,"hdctPhienKyId":92,"email":"owner@example.com","soGiaythoiGianHieuLucConfig":300}}"""));
        var client = CreateClient(handler);

        var result = await client.SendSignOtpAsync(
            "1001", "2002", "owner@example.com", "access-code", ESignOtpMethod.EmailOtp);

        Assert.True(result.IsSuccess);
        Assert.Equal(92, result.HdctPhienKyId);
        Assert.Equal("users-profile-service/auth/login-ktk", handler.Requests[0].Path);
        Assert.Contains("\"maTruyCap\":\"access-code\"", handler.Requests[0].Body);
        Assert.Equal("econtract-integration-service/api/v1/tich-hop-ky/email-otp/khoi-tao", handler.Requests[1].Path);
        Assert.Equal("Bearer signer-token", handler.Requests[1].Authorization);
        Assert.Contains("\"hdChiTietId\":2002", handler.Requests[1].Body);
    }

    [Fact]
    public async Task SubmitSignOtpAsync_Sms_UsesDemoRectangleAndSmsFields()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"accessToken":"signer-token"}}"""),
            Json(HttpStatusCode.OK, """{"status":"OK","object":{}}"""));
        var client = CreateClient(handler);
        var evidence = JsonSerializer.Serialize(new
        {
            PositionX = 100,
            PositionY = 200,
            PositionW = 150,
            PositionH = 60,
            PositionPage = 1
        });

        var result = await client.SubmitSignOtpAsync(
            91, 92, "123456", ValidSignatureImage(),
            evidence, "0900000000", "access-code", ESignOtpMethod.SmsOtp);

        Assert.True(result.IsSuccess);
        Assert.Equal("econtract-integration-service/api/v1/tich-hop-ky/sms-otp/hoan-thanh", handler.Requests[1].Path);
        Assert.Contains("\"RECTANGLE\":\"100,582,250,642\"", handler.Requests[1].Body);
        Assert.Contains("\"signerBy\":true", handler.Requests[1].Body);
        Assert.Contains("\"signerDate\":true", handler.Requests[1].Body);
        Assert.Contains("\"phoneNumberFlag\":false", handler.Requests[1].Body);
        Assert.Contains("\"otpFlag\":false", handler.Requests[1].Body);
        Assert.Contains("\"visibleType\":4", handler.Requests[1].Body);
        Assert.DoesNotContain("emailFlag", handler.Requests[1].Body);
    }

    [Fact]
    public async Task SubmitSignOtpAsync_Email_UsesVisibleTypeAndEmailFields()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"accessToken":"signer-token"}}"""),
            Json(HttpStatusCode.OK, """{"status":"OK","statusCode":200,"object":{}}"""));
        var client = CreateClient(handler);
        var evidence = JsonSerializer.Serialize(new
        {
            PositionX = 57,
            PositionY = 375,
            PositionW = 241,
            PositionH = 105,
            PositionPage = 4
        });

        var result = await client.SubmitSignOtpAsync(
            10890, 1005881, "123456", ValidSignatureImage(),
            evidence, "owner@example.com", "access-code", ESignOtpMethod.EmailOtp);

        Assert.True(result.IsSuccess);
        Assert.Contains("\"RECTANGLE\":\"57,362,298,467\"", handler.Requests[1].Body);
        Assert.Contains("\"PAGE\":4", handler.Requests[1].Body);
        Assert.Contains("\"emailFlag\":false", handler.Requests[1].Body);
        Assert.Contains("\"otpFlag\":false", handler.Requests[1].Body);
        Assert.Contains("\"visibleType\":4", handler.Requests[1].Body);
    }

    [Fact]
    public async Task SubmitSignOtpAsync_ProviderFailure_ReturnsStructuredProviderCode()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"OK","object":{"accessToken":"signer-token"}}"""),
            Json(HttpStatusCode.InternalServerError,
                """{"statusCode":500,"status":"INTERNAL_SERVER_ERROR","message":"ECT-00001126","error":["ECT-00001126"]}"""));
        var client = CreateClient(handler);
        var evidence = JsonSerializer.Serialize(new
        {
            PositionX = 57,
            PositionY = 375,
            PositionW = 241,
            PositionH = 105,
            PositionPage = 4
        });

        var result = await client.SubmitSignOtpAsync(
            10890, 1005881, "123456", ValidSignatureImage(),
            evidence, "owner@example.com", "access-code", ESignOtpMethod.EmailOtp);

        Assert.False(result.IsSuccess);
        Assert.Equal("ECT-00001126", result.ProviderCode);
        Assert.Equal(500, result.ProviderStatusCode);
        Assert.Equal("ECT-00001126", result.ErrorMessage);
    }

    private static ESignProviderClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://vnpt.test/") };
        var factory = new FakeHttpClientFactory(httpClient);
        var options = Options.Create(new ESignOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Username = "partner",
            Password = "password",
            NenTangId = 1,
            LoaiTaiLieuId = "1"
        });
        return new ESignProviderClient(
            factory,
            options,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ESignProviderClient>.Instance);
    }

    private static CreateEnvelopeInput CreateValidEnvelopeInput()
    {
        return new CreateEnvelopeInput
        {
            Title = "Contract test",
            FileName = "contract.pdf",
            FileStream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-test")),
            Signers =
            [
                new ESignSignerInput
                {
                    UserId = Guid.NewGuid(),
                    FullName = "Owner",
                    SignerRole = "Landlord",
                    SigningOrder = 1,
                    Email = "owner@example.com",
                    PhoneNumber = "0900000001"
                },
                new ESignSignerInput
                {
                    UserId = Guid.NewGuid(),
                    FullName = "Tenant",
                    SignerRole = "Tenant",
                    SigningOrder = 2,
                    Email = "tenant@example.com",
                    PhoneNumber = "0900000002"
                }
            ]
        };
    }

    private static ESignSignatureImage ValidSignatureImage() => new()
    {
        Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("signature-image")),
        MediaType = "image/png",
        ByteLength = 128,
        Width = 320,
        Height = 120,
        Sha256Hash = new string('a', 64)
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responseQueue = new(responses);
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return responseQueue.Dequeue();
        }
    }

    private sealed record RecordedRequest(string Path, string Authorization, string Body);
}
