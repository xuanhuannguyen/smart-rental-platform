using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure.ExternalServices.PayOS;
using SmartRentalPlatform.Infrastructure.Options;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Payments;

public class PayOSClientTests
{
    private readonly PayOSOptions _options;

    public PayOSClientTests()
    {
        _options = new PayOSOptions
        {
            PayoutClientId = "real-payout-client-id",
            PayoutApiKey = "real-payout-api-key",
            PayoutChecksumKey = "5fe21d9cc8fccc358f4691a77e175bbef7eb55121cdf3a41d51c5a7354766c51",
            BaseUrl = "https://api-merchant.payos.vn"
        };
    }

    [Fact]
    public async Task CreatePayoutAsync_SendsRequestWithCorrectHeadersAndBody()
    {
        // Arrange
        var mockHandler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };
        var factory = new TestHttpClientFactory(httpClient);
        var logger = new TestLogger<PayOSClient>();
        var env = new TestHostEnvironment { EnvironmentName = "Production" };

        var client = new PayOSClient(factory, Options.Create(_options), env, logger);

        var input = new PayOSCreatePayoutInput
        {
            ProviderOrderCode = "order-123",
            IdempotencyKey = "idemp-key-111",
            Amount = 500000,
            Description = "WD order-123",
            BankBin = "970415",
            AccountNumber = "123456"
        };

        // Act
        var result = await client.CreatePayoutAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("00", result.GatewayResponseCode);
        Assert.Equal("payout-12345", result.PayoutId);

        var interceptedRequest = mockHandler.Request;
        Assert.NotNull(interceptedRequest);
        Assert.Equal(HttpMethod.Post, interceptedRequest.Method);
        Assert.Equal("/v1/payouts", interceptedRequest.RequestUri?.AbsolutePath);

        // Assert headers
        Assert.Equal("real-payout-client-id", interceptedRequest.Headers.GetValues("x-client-id").First());
        Assert.Equal("real-payout-api-key", interceptedRequest.Headers.GetValues("x-api-key").First());
        Assert.Equal("idemp-key-111", interceptedRequest.Headers.GetValues("x-idempotency-key").First());
        Assert.True(interceptedRequest.Headers.Contains("x-signature"));
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    code = "00",
                    desc = "success",
                    data = new
                    {
                        id = "payout-12345",
                        referenceId = "order-123",
                        approvalState = "SUCCEEDED",
                        transactions = new[]
                        {
                            new { id = "t-1", state = "SUCCESS" }
                        }
                    }
                })
            };
            return Task.FromResult(response);
        }
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public TestHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
