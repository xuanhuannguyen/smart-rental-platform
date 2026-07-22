using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Common.Interfaces.Media;

public interface IMediaObjectKeyFactory
{
    MediaObjectKeyResult Create(
        MediaScope scope,
        MediaVisibility visibility,
        string originalFileName);
}
