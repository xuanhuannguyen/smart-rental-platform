using System.Text.Json.Serialization;

namespace SmartRentalPlatform.Infrastructure.Ekyc;

internal sealed class VnptUploadFileResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("object")]
    public VnptUploadFileObject? Object { get; set; }
}

internal sealed class VnptUploadFileObject
{
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}

internal sealed class VnptOcrRequest
{
    [JsonPropertyName("img_front")]
    public string ImgFront { get; set; } = default!;

    [JsonPropertyName("img_back")]
    public string ImgBack { get; set; } = default!;

    [JsonPropertyName("client_session")]
    public string ClientSession { get; set; } = default!;

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("crop_param")]
    public string CropParam { get; set; } = default!;

    [JsonPropertyName("validate_postcode")]
    public bool ValidatePostcode { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;
}

internal sealed class VnptFaceCompareRequest
{
    [JsonPropertyName("img_front")]
    public string ImgFront { get; set; } = default!;

    [JsonPropertyName("img_face")]
    public string ImgFace { get; set; } = default!;

    [JsonPropertyName("client_session")]
    public string ClientSession { get; set; } = default!;

    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;
}

internal sealed class VnptFaceLivenessRequest
{
    [JsonPropertyName("img")]
    public string Img { get; set; } = default!;

    [JsonPropertyName("client_session")]
    public string ClientSession { get; set; } = default!;

    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;
}

internal sealed class VnptGenericResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("object")]
    public VnptOcrObject? Object { get; set; }
}

internal sealed class VnptOcrObject
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("birth_day")]
    public string? BirthDay { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("recent_location")]
    public string? RecentLocation { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("msg_back")]
    public string? MsgBack { get; set; }

    [JsonPropertyName("id_fake_warning")]
    public string? IdFakeWarning { get; set; }

    [JsonPropertyName("tampering")]
    public VnptTamperingInfo? Tampering { get; set; }

    [JsonPropertyName("prob")]
    public decimal? Prob { get; set; }

    [JsonPropertyName("liveness")]
    public string? Liveness { get; set; }
}

internal sealed class VnptTamperingInfo
{
    [JsonPropertyName("is_legal")]
    public string? IsLegal { get; set; }
}

internal sealed class VnptTokenResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("object")]
    public VnptTokenObject? Object { get; set; }
}

internal sealed class VnptTokenObject
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
