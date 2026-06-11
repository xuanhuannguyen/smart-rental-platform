using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Locations;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.ExternalServices.VietMap;

public class VietMapService : IVietMapService
{
    public const string HttpClientName = "VietMap";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly VietMapOptions options;

    public VietMapService(HttpClient httpClient, IOptions<VietMapOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
    }

    public async Task<LocationSearchResponse> SearchAddressAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ValidateSearchText(text);
        ValidateApiKey();

        var searchResults = await GetAsync<List<VietMapSearchItem>>(
            $"/api/search/v4?apikey={Url(options.ApiKey)}&text={Url(text.Trim())}&display_type={options.DisplayType}",
            cancellationToken);

        var selected = searchResults.FirstOrDefault()
            ?? throw new BadRequestException(
                ErrorCodes.ValidationError,
                "VietMap không tìm thấy vị trí phù hợp với địa chỉ đã nhập.",
                new { text });

        var place = await GetAsync<VietMapPlaceResponse>(
            $"/api/place/v4?apikey={Url(options.ApiKey)}&refid={Url(selected.RefId)}",
            cancellationToken);

        return new LocationSearchResponse
        {
            RefId = selected.RefId,
            DisplayAddress = FirstNotEmpty(place.Display, selected.Display, text.Trim()) ?? text.Trim(),
            Name = FirstNotEmpty(place.Name, selected.Name),
            Address = FirstNotEmpty(place.Address, selected.Address),
            Latitude = place.Lat,
            Longitude = place.Lng
        };
    }

    public async Task<List<LocationSuggestionResponse>> SuggestAddressesAsync(
        string text,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        ValidateSearchText(text);
        ValidateApiKey();

        limit = Math.Clamp(limit, 1, 10);

        var searchResults = await GetAsync<List<VietMapSearchItem>>(
            $"/api/search/v4?apikey={Url(options.ApiKey)}&text={Url(text.Trim())}&display_type={options.DisplayType}",
            cancellationToken);

        var suggestions = new List<LocationSuggestionResponse>();
        foreach (var item in searchResults
            .Where(x => !string.IsNullOrWhiteSpace(x.RefId))
            .Take(limit))
        {
            var place = await GetAsync<VietMapPlaceResponse>(
                $"/api/place/v4?apikey={Url(options.ApiKey)}&refid={Url(item.RefId)}",
                cancellationToken);

            suggestions.Add(new LocationSuggestionResponse
            {
                RefId = item.RefId,
                DisplayAddress = FirstNotEmpty(place.Display, item.Display, text.Trim()) ?? text.Trim(),
                Name = FirstNotEmpty(place.Name, item.Name),
                Address = FirstNotEmpty(place.Address, item.Address),
                Latitude = place.Lat,
                Longitude = place.Lng
            });
        }

        return suggestions;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode == HttpStatusCode.TooManyRequests
                ? "VietMap đang giới hạn số lượt gọi. Vui lòng thử lại sau."
                : "Không thể gọi VietMap API.";

            throw new InternalServerException(
                ErrorCodes.InternalServerError,
                $"{message} Status {(int)response.StatusCode}: {body}",
                null);
        }

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return result
            ?? throw new InternalServerException(
                ErrorCodes.InternalServerError,
                "VietMap trả về dữ liệu không hợp lệ.",
                null);
    }

    private static string Url(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private void ValidateSearchText(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        throw new BadRequestException(
            ErrorCodes.ValidationError,
            "Địa chỉ tìm kiếm là bắt buộc.",
            new { field = nameof(text) });
    }

    private void ValidateApiKey()
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return;
        }

        throw new InternalServerException(
            ErrorCodes.InternalServerError,
            "Chưa cấu hình VietMap API key.",
            null);
    }

    private static string? FirstNotEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private sealed class VietMapSearchItem
    {
        [JsonPropertyName("ref_id")]
        public string RefId { get; set; } = string.Empty;

        [JsonPropertyName("display")]
        public string? Display { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }
    }

    private sealed class VietMapPlaceResponse
    {
        [JsonPropertyName("display")]
        public string? Display { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("lat")]
        public decimal Lat { get; set; }

        [JsonPropertyName("lng")]
        public decimal Lng { get; set; }
    }
}
