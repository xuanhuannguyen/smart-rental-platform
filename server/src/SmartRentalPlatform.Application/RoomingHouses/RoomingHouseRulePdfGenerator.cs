using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

internal static class RoomingHouseRulePdfGenerator
{
    public static Stream Generate(RoomingHouse house, UpsertRoomingHouseRuleRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var stream = new MemoryStream();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));
                page.Header().Column(column =>
                {
                    column.Item().Text("LUAT KHU TRO").FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                    column.Item().Text(house.Name).FontSize(14).SemiBold();
                    column.Item().Text(house.AddressDisplay).FontSize(10).FontColor(Colors.Grey.Darken2);
                });
                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Spacing(12);
                    AddSection(column, "Quy dinh chung", request.GeneralRules);
                    AddSection(column, "Gio giac yen tinh", request.QuietHours);
                    AddSection(column, "An ninh", request.SecurityPolicy);
                    AddSection(column, "Ve sinh", request.CleaningPolicy);
                    AddSection(column, "Khach ra vao", request.GuestPolicy);
                    AddSection(column, "Gui xe", request.ParkingPolicy);
                    AddSection(column, "Dien, nuoc va tien ich", request.UtilityPolicy);
                    AddSection(column, "Boi thuong hu hong", request.DamageCompensationPolicy);
                    AddSection(column, "Ghi chu bo sung", request.AdditionalNotes);
                });
                page.Footer()
                    .AlignRight()
                    .Text($"Tao luc {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm 'UTC'}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(stream);

        stream.Position = 0;
        return stream;
    }

    private static void AddSection(ColumnDescriptor column, string title, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        column.Item().Column(section =>
        {
            section.Item().Text(title).FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
            section.Item().PaddingTop(3).Text(content.Trim()).LineHeight(1.35f);
        });
    }
}
