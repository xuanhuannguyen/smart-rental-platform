using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RentalPolicies.Requests;
using SmartRentalPlatform.Contracts.RentalPolicies.Responses;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Responses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/rooming-houses")]
public class RoomingHousesController : ControllerBase
{
    private readonly IRoomingHouseQueryService roomingHouseQueryService;
    private readonly IRoomingHouseDraftService roomingHouseDraftService;
    private readonly IRoomingHouseMediaService roomingHouseMediaService;
    private readonly IRoomingHouseSubmissionService roomingHouseSubmissionService;
    private readonly IRoomingHouseRentalPolicyService roomingHouseRentalPolicyService;
    private readonly IRoomingHouseRuleService roomingHouseRuleService;
    private readonly IRoomingHouseServicePriceService roomingHouseServicePriceService;
    private readonly ICurrentUserService currentUserService;

    public RoomingHousesController(
        IRoomingHouseQueryService roomingHouseQueryService,
        IRoomingHouseDraftService roomingHouseDraftService,
        IRoomingHouseMediaService roomingHouseMediaService,
        IRoomingHouseSubmissionService roomingHouseSubmissionService,
        IRoomingHouseRentalPolicyService roomingHouseRentalPolicyService,
        IRoomingHouseRuleService roomingHouseRuleService,
        IRoomingHouseServicePriceService roomingHouseServicePriceService,
        ICurrentUserService currentUserService)
    {
        this.roomingHouseQueryService = roomingHouseQueryService;
        this.roomingHouseDraftService = roomingHouseDraftService;
        this.roomingHouseMediaService = roomingHouseMediaService;
        this.roomingHouseSubmissionService = roomingHouseSubmissionService;
        this.roomingHouseRentalPolicyService = roomingHouseRentalPolicyService;
        this.roomingHouseRuleService = roomingHouseRuleService;
        this.roomingHouseServicePriceService = roomingHouseServicePriceService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpGet("my/onboarding")]
    public async Task<ActionResult<ApiResponse<RoomingHouseOnboardingResponse>>> GetMyOnboarding(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseQueryService.GetOnboardingAsync(userId, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseOnboardingResponse>
        {
            Success = true,
            Message = "Tải trạng thái đăng ký chủ trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("draft")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> CreateDraft(
        CreateRoomingHouseDraftRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseDraftService.CreateDraftAsync(userId, request, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = "Bản nháp khu trọ đã sẵn sàng.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<List<RoomingHouseResponse>>>> GetMyRoomingHouses(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseQueryService.GetByLandlordAsync(userId, cancellationToken);

        return Ok(new ApiResponse<List<RoomingHouseResponse>>
        {
            Success = true,
            Message = "Tải danh sách khu trọ của tôi thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseQueryService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return RoomingHouseNotFound();
        }

        if (!CanAccess(result))
        {
            return Forbidden(ErrorCodes.Forbidden, "Bạn không có quyền truy cập khu trọ này.");
        }

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = "Tải thông tin khu trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> Update(
        Guid id,
        UpdateRoomingHouseRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseDraftService.UpdateAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu thông tin cơ bản của khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/amenities")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateAmenities(
        Guid id,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseMediaService.UpdateAmenitiesAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu tiện ích khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/images")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateImages(
        Guid id,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseMediaService.UpdateImagesAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu ảnh khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/legal-document")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateLegalDocument(
        Guid id,
        UpdateRoomingHouseLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseMediaService.UpdateLegalDocumentAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Lưu giấy tờ pháp lý của khu trọ thành công.");
    }

    [Authorize]
    [HttpPut("{id:guid}/visibility")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> UpdateVisibility(
        Guid id,
        UpdateRoomingHouseVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var ownership = await EnsureOwnerAsync(id, cancellationToken);
        if (ownership is not null)
        {
            return ownership;
        }

        var result = await roomingHouseDraftService.UpdateVisibilityAsync(id, request, cancellationToken);
        return RoomingHouseDetailResult(result, "Cập nhật trạng thái hiển thị khu trọ thành công.");
    }

    [Authorize]
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> Submit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseSubmissionService.SubmitAsync(id, userId, cancellationToken);
        if (result is null)
        {
            return NotFound(RoomingHouseNotFoundError());
        }

        return RoomingHouseDetailResult(result, "Gửi khu trọ để xét duyệt thành công.");
    }

    [Authorize]
    [HttpGet("{id:guid}/rental-policy")]
    public async Task<ActionResult<ApiResponse<RentalPolicyResponse>>> GetRentalPolicy(
        Guid id,
        CancellationToken cancellationToken)
    {
        var roomingHouse = await roomingHouseQueryService.GetByIdAsync(id, cancellationToken);
        if (roomingHouse is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.HouseNotFound,
                Message = "Không tìm thấy khu trọ."
            });
        }

        if (roomingHouse.LandlordUserId != GetCurrentUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.Forbidden,
                Message = "Bạn không có quyền truy cập khu trọ này."
            });
        }

        var result = await roomingHouseRentalPolicyService.GetRentalPolicyAsync(id, cancellationToken);

        return Ok(new ApiResponse<RentalPolicyResponse>
        {
            Success = true,
            Message = result is null
                ? "Khu trọ chưa có chính sách thuê."
                : "Tải chính sách thuê thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("{id:guid}/rental-policy")]
    public async Task<ActionResult<ApiResponse<RentalPolicyResponse>>> UpdateRentalPolicy(
        Guid id,
        UpdateRentalPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseRentalPolicyService.UpdateRentalPolicyAsync(id, userId, request, cancellationToken);

        return Ok(new ApiResponse<RentalPolicyResponse>
        {
            Success = true,
            Message = "Lưu chính sách thuê thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/rule")]
    public async Task<ActionResult<ApiResponse<RoomingHouseRuleResponse>>> GetRule(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseRuleService.GetRuleAsync(id, userId, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseRuleResponse>
        {
            Success = true,
            Message = result is null
                ? "Khu trọ chưa có luật khu trọ."
                : "Tải luật khu trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("{id:guid}/rule")]
    public async Task<ActionResult<ApiResponse<RoomingHouseRuleResponse>>> UpsertRule(
        Guid id,
        UpsertRoomingHouseRuleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomingHouseRuleService.UpsertRuleAsync(id, userId, request, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseRuleResponse>
        {
            Success = true,
            Message = "Lưu luật khu trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("{id:guid}/service-prices")]
    public async Task<ActionResult<ApiResponse<List<ServicePriceResponse>>>> GetServicePrices(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseServicePriceService.GetServicePricesAsync(GetCurrentUserId(), id, cancellationToken);

        return Ok(new ApiResponse<List<ServicePriceResponse>>
        {
            Success = true,
            Message = "Tải bảng giá dịch vụ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/service-prices")]
    public async Task<ActionResult<ApiResponse<ServicePriceResponse>>> CreateServicePrice(
        Guid id,
        CreateServicePriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseServicePriceService.CreateServicePriceAsync(GetCurrentUserId(), id, request, cancellationToken);

        return Ok(new ApiResponse<ServicePriceResponse>
        {
            Success = true,
            Message = "Tạo bảng giá dịch vụ mới thành công.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedAccessException("Không tìm thấy mã người dùng đã đăng nhập.");
        }

        return currentUserService.UserId.Value;
    }

    private async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>?> EnsureOwnerAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var roomingHouse = await roomingHouseQueryService.GetByIdAsync(roomingHouseId, cancellationToken);
        if (roomingHouse is null)
        {
            return RoomingHouseNotFound();
        }

        if (roomingHouse.LandlordUserId != GetCurrentUserId())
        {
            return Forbidden(ErrorCodes.Forbidden, "Bạn không có quyền cập nhật khu trọ này.");
        }

        return null;
    }

    private bool CanAccess(RoomingHouseDetailResponse roomingHouse)
    {
        return currentUserService.Roles.Contains("Admin") ||
               roomingHouse.LandlordUserId == GetCurrentUserId();
    }

    private ActionResult<ApiResponse<RoomingHouseDetailResponse>> RoomingHouseDetailResult(
        RoomingHouseDetailResponse? result,
        string message)
    {
        if (result is null)
        {
            return RoomingHouseNotFound();
        }

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = message,
            Data = result
        });
    }

    private ActionResult<ApiResponse<RoomingHouseDetailResponse>> RoomingHouseNotFound()
    {
        return NotFound(RoomingHouseNotFoundError());
    }

    private static ApiErrorResponse RoomingHouseNotFoundError()
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = ErrorCodes.HouseNotFound,
            Message = "Không tìm thấy khu trọ."
        };
    }

    private ActionResult<ApiResponse<RoomingHouseDetailResponse>> Forbidden(
        string errorCode,
        string message)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        });
    }
}
