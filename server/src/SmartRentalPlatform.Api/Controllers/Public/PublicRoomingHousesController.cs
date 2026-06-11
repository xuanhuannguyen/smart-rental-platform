using Microsoft.AspNetCore.Mvc;
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

    public PublicRoomingHousesController(IRoomingHouseQueryService roomingHouseQueryService)
    {
        this.roomingHouseQueryService = roomingHouseQueryService;
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
}
