using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses;

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
}
