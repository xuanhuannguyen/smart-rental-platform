using System.Text;
using QuestPDF.Infrastructure;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.UnitTests.Contract;

public class ContractPdfRendererTests
{
    [Theory]
    [InlineData(0, "Không đồng")]
    [InlineData(15_000, "Mười lăm nghìn đồng")]
    [InlineData(3_500_000, "Ba triệu năm trăm nghìn đồng")]
    public void VietnameseMoneyFormatter_FormatsExpectedWords(decimal amount, string expected)
    {
        Assert.Equal(expected, VietnameseMoneyFormatter.Format(amount));
    }

    [Fact]
    public void Renderer_GeneratesPreviewAndUnsignedPdf()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        QuestPDF.Settings.License = LicenseType.Community;
        var renderer = new ContractPdfRenderer();
        var document = CreateDocument();

        var preview = renderer.RenderRentalContractPreview(document, new ContractRenderOptions
        {
            PreviewAudience = ContractPreviewAudience.LandlordReview,
            ViewerMode = ContractFilePurpose.Preview.ToString(),
            ShowFullDocumentNumbers = true
        });
        var unsigned = renderer.RenderSignedRentalContract(document, new ContractRenderOptions
        {
            ViewerMode = ContractFilePurpose.UnsignedForESign.ToString(),
            ShowFullDocumentNumbers = true
        });

        Assert.True(preview.Length > 50_000);
        Assert.True(unsigned.Length > 50_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(preview, 0, 4));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(unsigned, 0, 4));
    }

    [Fact]
    public void Renderer_ESignContract_CapturesBothSignatureZonesOnTheRenderedPage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        QuestPDF.Settings.License = LicenseType.Community;
        var renderer = new ContractPdfRenderer();

        var result = renderer.RenderRentalContractForESign(CreateDocument(), new ContractRenderOptions
        {
            ViewerMode = ContractFilePurpose.UnsignedForESign.ToString(),
            ShowFullDocumentNumbers = true
        });

        Assert.Equal("%PDF", Encoding.ASCII.GetString(result.PdfBytes, 0, 4));
        Assert.Equal(2, result.SignatureZones.Count);
        var landlord = Assert.Contains("Landlord", result.SignatureZones);
        var tenant = Assert.Contains("Tenant", result.SignatureZones);
        AssertValidA4Zone(landlord);
        AssertValidA4Zone(tenant);
        Assert.Equal(landlord.Page, tenant.Page);
        Assert.True(landlord.Page > 1);
        Assert.True(landlord.X < tenant.X);
    }

    [Fact]
    public void Renderer_ESignAppendix_CapturesBothSignatureZones()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        QuestPDF.Settings.License = LicenseType.Community;
        var renderer = new ContractPdfRenderer();

        var result = renderer.RenderContractAppendixForESign(CreateAppendix(), new ContractRenderOptions
        {
            ViewerMode = ContractFilePurpose.UnsignedForESign.ToString(),
            ShowFullDocumentNumbers = true
        });

        Assert.Equal("%PDF", Encoding.ASCII.GetString(result.PdfBytes, 0, 4));
        Assert.Equal(2, result.SignatureZones.Count);
        var landlord = Assert.Contains("Landlord", result.SignatureZones);
        var tenant = Assert.Contains("Tenant", result.SignatureZones);
        AssertValidA4Zone(landlord);
        AssertValidA4Zone(tenant);
        Assert.Equal(landlord.Page, tenant.Page);
        Assert.True(landlord.X < tenant.X);
    }

    private static ContractDocumentModel CreateDocument()
    {
        return new ContractDocumentModel
        {
            PreparedAt = new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.FromHours(7)),
            ContractNumber = "TEST-20260415",
            Landlord = CreateParty(Guid.NewGuid(), "Nguyễn Văn A", "001000000001"),
            Tenant = CreateParty(Guid.NewGuid(), "Trần Văn B", "079000000001"),
            Property = new ContractDocumentProperty
            {
                RoomId = Guid.NewGuid(),
                RoomNumber = "P-101",
                RoomingHouseName = "Khu trọ kiểm thử",
                Address = "01 Đường Mẫu, Phường Mẫu, Thành phố Đà Nẵng",
                Floor = 1,
                AreaM2 = 25,
                MaxOccupants = 2
            },
            FinancialTerms = new ContractDocumentFinancialTerms
            {
                StartDate = new DateOnly(2026, 4, 20),
                EndDate = new DateOnly(2027, 4, 20),
                MonthlyRent = 3_500_000,
                DepositAmount = 3_500_000,
                PaymentDay = 5,
                DepositPaidAt = new DateTimeOffset(2026, 4, 15, 11, 0, 0, TimeSpan.FromHours(7))
            },
            ServicePrices =
            [
                new ContractDocumentServicePrice
                {
                    ServiceName = "Điện",
                    PricingUnit = "kWh",
                    UnitPrice = 3_500,
                    EffectiveFrom = new DateOnly(2026, 4, 1)
                }
            ],
            Occupants =
            [
                new ContractDocumentOccupant
                {
                    OccupantId = Guid.NewGuid(),
                    FullName = "Trần Văn B",
                    DateOfBirth = new DateOnly(1995, 1, 1),
                    DocumentNumber = "079000000001",
                    Relationship = "Người thuê chính",
                    MoveInDate = new DateOnly(2026, 4, 20)
                }
            ]
        };
    }

    private static ContractDocumentParty CreateParty(Guid userId, string name, string documentNumber)
    {
        return new ContractDocumentParty
        {
            UserId = userId,
            FullName = name,
            DateOfBirth = new DateOnly(1995, 1, 1),
            DocumentNumber = documentNumber,
            Address = "01 Đường Mẫu, Phường Mẫu, Thành phố Đà Nẵng",
            PhoneNumber = "0900000000",
            Email = "test@example.com"
        };
    }

    private static ContractAppendix CreateAppendix()
    {
        var landlord = CreateUser("Nguyễn Văn A", "owner@example.com", "0900000001");
        var tenant = CreateUser("Trần Văn B", "tenant@example.com", "0900000002");
        var contract = new RentalContract
        {
            Id = Guid.NewGuid(),
            ContractNumber = "TEST-20260415",
            StartDate = new DateOnly(2026, 4, 20),
            EndDate = new DateOnly(2027, 4, 20),
            MonthlyRent = 3_500_000,
            DepositAmount = 3_500_000,
            PaymentDay = 5,
            MainTenantUser = tenant,
            Room = new Room
            {
                RoomNumber = "P-101",
                RoomingHouse = new RoomingHouse
                {
                    Name = "Khu trọ kiểm thử",
                    Landlord = landlord
                }
            }
        };

        return new ContractAppendix
        {
            Id = Guid.NewGuid(),
            RentalContractId = contract.Id,
            AppendixNumber = "PL-01",
            EffectiveDate = new DateOnly(2026, 5, 1),
            RentalContract = contract
        };
    }

    private static User CreateUser(string fullName, string email, string phoneNumber)
    {
        var user = new User
        {
            Email = email,
            DisplayName = fullName,
            PhoneNumber = phoneNumber
        };
        user.UserProfile = new UserProfile
        {
            UserId = user.Id,
            User = user,
            FullName = fullName,
            DateOfBirth = new DateOnly(1995, 1, 1),
            AddressLine = "01 Đường Mẫu",
            VerifiedCitizenIdMasked = "********0001"
        };
        return user;
    }

    private static void AssertValidA4Zone(SignatureZone zone)
    {
        Assert.True(zone.Page >= 1);
        Assert.True(zone.X >= 0);
        Assert.True(zone.Y >= 0);
        Assert.True(zone.Width > 0);
        Assert.True(zone.Height > 0);
        Assert.True(zone.X + zone.Width <= 596);
        Assert.True(zone.Y + zone.Height <= 842);
    }
}
