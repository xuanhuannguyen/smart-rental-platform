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
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Times New Roman").LineHeight(1.4f));

                page.Header().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().AlignCenter().Text("LUẬT KHU TRỌ").FontSize(24).Bold().FontColor("#0e4a8e");
                    column.Item().AlignCenter().Text($"Khu trọ {house.Name}").FontSize(16).Bold().FontColor(Colors.Grey.Darken4);
                    if (!string.IsNullOrWhiteSpace(house.AddressDisplay))
                    {
                        column.Item().AlignCenter().Text(house.AddressDisplay).FontSize(11).Italic().FontColor(Colors.Grey.Darken2);
                    }

                    // Decorative line
                    column.Item().PaddingVertical(8).Row(row =>
                    {
                        row.RelativeItem().PaddingTop(5).LineHorizontal(1).LineColor("#0e4a8e");
                        row.AutoItem().PaddingHorizontal(10).Text("❖").FontSize(10).FontColor("#0e4a8e");
                        row.RelativeItem().PaddingTop(5).LineHorizontal(1).LineColor("#0e4a8e");
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(14);

                    // Intro paragraph
                    column.Item().Text(text =>
                    {
                        text.Justify();

                        text.Span("Nhằm đảm bảo an ninh, trật tự và môi trường sống văn minh, an toàn, tất cả các thành viên khi thuê trọ tại khu trọ ", TextStyle.Default.Italic());

                        text.Span(house.Name, TextStyle.Default.Bold().Italic());

                        text.Span(" cần nghiêm túc ", TextStyle.Default.Italic());

                        text.Span("tuân thủ", TextStyle.Default.Bold().Italic());

                        text.Span(" các quy định sau:", TextStyle.Default.Italic());
                    });

                    int index = 1;
                    if (!string.IsNullOrWhiteSpace(request.GeneralRules))
                        AddSection(column, $"{index++}. QUY ĐỊNH CHUNG", request.GeneralRules);
                    if (!string.IsNullOrWhiteSpace(request.QuietHours))
                        AddSection(column, $"{index++}. GIỜ GIẤC YÊN TĨNH", request.QuietHours);
                    if (!string.IsNullOrWhiteSpace(request.SecurityPolicy))
                        AddSection(column, $"{index++}. AN NINH", request.SecurityPolicy);
                    if (!string.IsNullOrWhiteSpace(request.CleaningPolicy))
                        AddSection(column, $"{index++}. VỆ SINH", request.CleaningPolicy);
                    if (!string.IsNullOrWhiteSpace(request.GuestPolicy))
                        AddSection(column, $"{index++}. KHÁCH RA VÀO", request.GuestPolicy);
                    if (!string.IsNullOrWhiteSpace(request.ParkingPolicy))
                        AddSection(column, $"{index++}. GỬI XE", request.ParkingPolicy);
                    if (!string.IsNullOrWhiteSpace(request.UtilityPolicy))
                        AddSection(column, $"{index++}. ĐIỆN, NƯỚC VÀ TIỆN ÍCH", request.UtilityPolicy);
                    if (!string.IsNullOrWhiteSpace(request.DamageCompensationPolicy))
                        AddSection(column, $"{index++}. BỒI THƯỜNG HƯ HỎNG", request.DamageCompensationPolicy);
                    if (!string.IsNullOrWhiteSpace(request.AdditionalNotes))
                        AddSection(column, $"{index++}. GHI CHÚ BỔ SUNG", request.AdditionalNotes);

                    // Concluding paragraph
                    column.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span("Rất mong các bạn hợp tác và tuân thủ nội quy để cùng xây dựng khu trọ ", TextStyle.Default.Italic());

                        text.Span(house.Name, TextStyle.Default.Bold().Italic());

                        text.Span(" trở thành nơi an toàn – văn minh – thân thiện.", TextStyle.Default.Italic());
                    });

                    column.Item().Text("Xin cảm ơn!").Bold().Italic();

                    // Signature block
                    string location = "..., ngày ...... tháng ...... năm .........";
                    if (!string.IsNullOrWhiteSpace(house.AddressDisplay))
                    {
                        var parts = house.AddressDisplay.Split(',');
                        if (parts.Length > 0)
                        {
                            var lastPart = parts[^1].Trim();
                            lastPart = lastPart.Replace("Thành phố", "").Replace("TP.", "").Replace("Tỉnh", "").Trim();
                            if (!string.IsNullOrWhiteSpace(lastPart))
                            {
                                location = $"{lastPart}, ngày ...... tháng ...... năm .........";
                            }
                        }
                    }

                    column.Item().PaddingTop(20).AlignRight().Column(sig =>
                    {
                        sig.Item().Text(location).Italic().FontSize(11).AlignCenter();
                        sig.Item().PaddingTop(2).Text("CHỦ TRỌ").Bold().FontSize(11).AlignCenter();
                    });
                });

                page.Footer()
                    .AlignLeft()
                    .Text($"Tạo lúc {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm 'UTC'}")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(stream);

        stream.Position = 0;
        return stream;
    }

    private static void AddSection(ColumnDescriptor column, string title, string content)
    {
        column.Item().Column(section =>
        {
            section.Item().Text(title).FontSize(12).Bold().FontColor("#0e4a8e");
            section.Item().PaddingTop(3).Text(content.Trim()).LineHeight(1.35f).Justify();
            section.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        });
    }
}
