using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Api.Controllers.Public;

[ApiController]
[Route("api/public/rooming-houses")]
public class PublicRoomingHousesController : ControllerBase
{
    private readonly IRoomingHouseQueryService roomingHouseQueryService;
    private readonly IRoomingHouseAiChatService aiChatService;

    public PublicRoomingHousesController(
        IRoomingHouseQueryService roomingHouseQueryService,
        IRoomingHouseAiChatService aiChatService)
    {
        this.roomingHouseQueryService = roomingHouseQueryService;
        this.aiChatService = aiChatService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoomingHouseDetailResponse>>>> GetAvailable(
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseQueryService.GetPublicAvailableAsync(cancellationToken);

        return Ok(new ApiResponse<List<RoomingHouseDetailResponse>>
        {
            Success = true,
            Message = "Tải danh sách khu trọ còn phòng trống thành công.",
            Data = result
        });
    }

    [HttpGet("listing")]
    public async Task<ActionResult<ApiResponse<List<RoomingHouseListingResponse>>>> GetListing(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        var result = await roomingHouseQueryService.GetPublicListingAsync(page, pageSize, cancellationToken);

        return Ok(new ApiResponse<List<RoomingHouseListingResponse>>
        {
            Success = true,
            Message = "Tải danh sách khu trọ thành công.",
            Data = result
        });
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<RoomingHouseSearchItemResponse>>>> Search(
        [FromQuery] RoomingHouseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseQueryService.SearchPublicAsync(request, cancellationToken);

        return Ok(new ApiResponse<PagedResult<RoomingHouseSearchItemResponse>>
        {
            Success = true,
            Message = "Tìm kiếm khu trọ thành công.",
            Data = result
        });
    }

    [HttpPost("recommendations/guest")]
    public async Task<ActionResult<ApiResponse<RoomingHouseRecommendationResponse>>> GetGuestRecommendations(
        [FromBody] GuestRoomingHouseRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseQueryService.GetGuestRecommendationsAsync(request, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseRecommendationResponse>
        {
            Success = true,
            Message = "Tải gợi ý khu trọ phù hợp thành công.",
            Data = result
        });
    }

    [HttpPost("ai-chat")]
    [EnableRateLimiting("AiChat")]
    public async Task<ActionResult<ApiResponse<RoomingHouseAiChatResponse>>> Chat(
        [FromBody] RoomingHouseAiChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await aiChatService.ChatAsync(request, cancellationToken);

        return Ok(new ApiResponse<RoomingHouseAiChatResponse>
        {
            Success = true,
            Message = "Chatbot AI đã phản hồi thành công.",
            Data = result
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomingHouseDetailResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await roomingHouseQueryService.GetPublicByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(new ApiResponse<RoomingHouseDetailResponse>
            {
                Success = false,
                Message = "Không tìm thấy khu trọ công khai."
            });
        }

        return Ok(new ApiResponse<RoomingHouseDetailResponse>
        {
            Success = true,
            Message = "Tải chi tiết khu trọ công khai thành công.",
            Data = result
        });
    }
    [HttpGet("{roomingHouseId:guid}/rooms")]
    public async Task<ActionResult<ApiResponse<List<SmartRentalPlatform.Contracts.Rooms.Responses.RoomResponse>>>> GetAvailableRooms(
        Guid roomingHouseId,
        [FromServices] SmartRentalPlatform.Application.Rooms.IRoomQueryService roomQueryService,
        CancellationToken cancellationToken)
    {
        var result = await roomQueryService.GetPublicAvailableRoomsAsync(roomingHouseId, cancellationToken);

        return Ok(new ApiResponse<List<SmartRentalPlatform.Contracts.Rooms.Responses.RoomResponse>>
        {
            Success = true,
            Message = "Tải danh sách phòng trống thành công.",
            Data = result
        });
    }
}
