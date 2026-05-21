namespace SmartRentalPlatform.Domain.Entities;

public class UserProfile
{
    // UserId vừa là Primary Key, vừa là Foreign Key tới bảng users.
    // Nghĩa là mỗi user chỉ có tối đa 1 profile.
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string? FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }

    public string? AddressLine { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}