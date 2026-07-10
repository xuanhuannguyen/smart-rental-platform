using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Api.Controllers.Properties;

[ApiController]
[Route("api/favorite-rooming-houses")]
[Authorize]
public class FavoriteRoomingHousesController : ControllerBase
{
    private readonly IFavoriteRoomingHouseService _favoriteService;
    private readonly ICurrentUserService _currentUserService;

    public FavoriteRoomingHousesController(
        IFavoriteRoomingHouseService favoriteService,
        ICurrentUserService currentUserService)
    {
        _favoriteService = favoriteService;
        _currentUserService = currentUserService;
    }

    [HttpPost("{roomingHouseId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleFavorite(Guid roomingHouseId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var isFavorited = await _favoriteService.ToggleFavoriteAsync(roomingHouseId, userId, cancellationToken);
        
        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = isFavorited ? "Đã thêm vào danh sách yêu thích." : "Đã gỡ khỏi danh sách yêu thích.",
            Data = isFavorited
        });
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RoomingHouseListingResponse>>>> GetMyFavorites(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _favoriteService.GetMyFavoritesAsync(userId, pageNumber, pageSize, cancellationToken);
        
        return Ok(new ApiResponse<PagedResult<RoomingHouseListingResponse>>
        {
            Success = true,
            Message = "Tải danh sách yêu thích thành công.",
            Data = result
        });
    }

    [HttpGet("ids")]
    public async Task<ActionResult<ApiResponse<List<Guid>>>> GetMyFavoriteIds(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _favoriteService.GetMyFavoriteIdsAsync(userId, cancellationToken);
        
        return Ok(new ApiResponse<List<Guid>>
        {
            Success = true,
            Message = "Thành công.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
        => _currentUserService.UserId ?? throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");
}
