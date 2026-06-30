using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Rooms.Responses;

namespace SmartRentalPlatform.Api.Controllers.Public;

[ApiController]
[Route("api/public/rooms")]
public class PublicRoomsController : ControllerBase
{
    private readonly IRoomQueryService roomQueryService;

    public PublicRoomsController(IRoomQueryService roomQueryService)
    {
        this.roomQueryService = roomQueryService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await roomQueryService.GetPublicRoomByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RoomNotFound,
                Message = "Không tìm thấy phòng hoặc phòng không khả dụng."
            });
        }

        return Ok(new ApiResponse<RoomResponse>
        {
            Success = true,
            Message = "Tải thông tin phòng thành công.",
            Data = result
        });
    }
}
