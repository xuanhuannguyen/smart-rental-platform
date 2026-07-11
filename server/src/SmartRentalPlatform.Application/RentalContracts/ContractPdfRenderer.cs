using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Elements;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractPdfRenderer : IContractPdfRenderer
{
    private const int A4PageWidthPoints = 596;
    private const int A4PageHeightPoints = 842;
    private const int SignatureZoneHeightPoints = 155;
    private const string LandlordCaptureId = "sig-landlord";
    private const string TenantCaptureId = "sig-tenant";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    static ContractPdfRenderer()
    {
        QuestPDF.Settings.EnableDebugging = true;
    }

    public byte[] RenderRentalContractPreview(ContractDocumentModel document, ContractRenderOptions options)
    {
        QuestPDF.Settings.EnableDebugging = true;
        return RenderRentalContract(document, options, includeReviewAttachments: true);
    }

    public byte[] RenderSignedRentalContract(ContractDocumentModel document, ContractRenderOptions options)
    {
        return RenderRentalContract(document, options, includeReviewAttachments: false);
    }

    public PdfRenderResult RenderRentalContractForESign(ContractDocumentModel document, ContractRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ContractPdfDesign.EnsureFontsRegistered();

        var captureComponent = new SignatureCaptureComponent();

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(ContractPdfDesign.PageMarginHorizontal);
                page.MarginRight(ContractPdfDesign.PageMarginHorizontal);
                page.MarginTop(ContractPdfDesign.PageMarginTop);
                page.MarginBottom(ContractPdfDesign.PageMarginBottom);
                page.DefaultTextStyle(text => text
                    .FontFamily(ContractPdfDesign.FontFamily)
                    .FontSize(ContractPdfDesign.BodyFontSize)
                    .LineHeight(ContractPdfDesign.BodyLineHeight));

                ComposeContractPageBackground(page, options);


                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Height(0).Dynamic(captureComponent);
                    ComposeContractNationalHeader(column, document);
                    ComposeContractParties(column, document, options);
                    ComposeContractIntroduction(column);
                    ComposeContractTermsV2(column, document, options);
                    ComposeContractSignatureArea(
                        column,
                        capturePositions: true);
                    ComposeReviewAttachments(column, options, false);
                });

                ComposeContractPageFooter(page, document);
            });
        }).GeneratePdf();

        var signatureZones = RequireCompleteSignatureZones(captureComponent.GetSignatureZones());

        return new PdfRenderResult
        {
            PdfBytes = pdfBytes,
            SignatureZones = signatureZones
        };
    }

    private static byte[] RenderRentalContract(
        ContractDocumentModel document,
        ContractRenderOptions options,
        bool includeReviewAttachments)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ContractPdfDesign.EnsureFontsRegistered();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(ContractPdfDesign.PageMarginHorizontal);
                page.MarginRight(ContractPdfDesign.PageMarginHorizontal);
                page.MarginTop(ContractPdfDesign.PageMarginTop);
                page.MarginBottom(ContractPdfDesign.PageMarginBottom);
                page.DefaultTextStyle(text => text
                    .FontFamily(ContractPdfDesign.FontFamily)
                    .FontSize(ContractPdfDesign.BodyFontSize)
                    .LineHeight(ContractPdfDesign.BodyLineHeight));

                ComposeContractPageBackground(page, options);


                page.Content().Column(column =>
                {
                    column.Spacing(4);

                    ComposeContractNationalHeader(column, document);
                    ComposeContractParties(column, document, options);
                    ComposeContractIntroduction(column);
                    ComposeContractTermsV2(column, document, options);
                    ComposeContractSignatureArea(column, capturePositions: false);
                    ComposeReviewAttachments(column, options, includeReviewAttachments);
                });

                ComposeContractPageFooter(page, document);
            });
        }).GeneratePdf();
    }

    public byte[] RenderContractAppendixPreview(ContractAppendix appendix, ContractRenderOptions options)
    {
        return RenderContractAppendix(appendix, options, includeReviewAttachments: true);
    }

    public byte[] RenderSignedContractAppendix(ContractAppendix appendix, ContractRenderOptions options)
    {
        return RenderContractAppendix(appendix, options, includeReviewAttachments: false);
    }

    public PdfRenderResult RenderContractAppendixForESign(ContractAppendix appendix, ContractRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(appendix);
        ContractPdfDesign.EnsureFontsRegistered();

        var contract = appendix.RentalContract;

        var captureComponent = new SignatureCaptureComponent();

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(ContractPdfDesign.PageMarginHorizontal);
                page.MarginRight(ContractPdfDesign.PageMarginHorizontal);
                page.MarginTop(ContractPdfDesign.PageMarginTop);
                page.MarginBottom(ContractPdfDesign.PageMarginBottom);
                page.DefaultTextStyle(text => text
                    .FontFamily(ContractPdfDesign.FontFamily)
                    .FontSize(ContractPdfDesign.BodyFontSize)
                    .LineHeight(ContractPdfDesign.BodyLineHeight));

                ComposeContractPageBackground(page, options);


                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Height(0).Dynamic(captureComponent);
                    ComposeAppendixHeader(column, appendix);
                    ComposeAppendixParties(column, appendix.RentalContract, options);
                    ComposeAppendixChanges(column, appendix, options);
                    ComposeContractSignatureArea(
                        column,
                        capturePositions: true);
                    ComposeReviewAttachments(column, options, false);
                });

                ComposeAppendixPageFooter(page, appendix);
            });
        }).GeneratePdf();

        var signatureZones = RequireCompleteSignatureZones(captureComponent.GetSignatureZones());

        return new PdfRenderResult
        {
            PdfBytes = pdfBytes,
            SignatureZones = signatureZones
        };
    }

    private static IReadOnlyDictionary<string, SignatureZone> RequireCompleteSignatureZones(
        IReadOnlyDictionary<string, SignatureZone> signatureZones)
    {
        if (signatureZones.Count != 2 ||
            !signatureZones.TryGetValue("Landlord", out var landlordZone) ||
            !signatureZones.TryGetValue("Tenant", out var tenantZone))
        {
            throw new InvalidOperationException(
                "PDF renderer did not capture both Landlord and Tenant signature zones.");
        }

        ValidateSignatureZone("Landlord", landlordZone);
        ValidateSignatureZone("Tenant", tenantZone);
        return signatureZones;
    }

    private static void ValidateSignatureZone(string signerRole, SignatureZone zone)
    {
        var right = (long)zone.X + zone.Width;
        var bottom = (long)zone.Y + zone.Height;
        if (zone.Page < 1 ||
            zone.X < 0 ||
            zone.Y < 0 ||
            zone.Width <= 0 ||
            zone.Height <= 0 ||
            right > A4PageWidthPoints ||
            bottom > A4PageHeightPoints)
        {
            throw new InvalidOperationException(
                $"Captured signature zone for {signerRole} is outside the A4 page bounds.");
        }
    }

    private static byte[] RenderContractAppendix(
        ContractAppendix appendix,
        ContractRenderOptions options,
        bool includeReviewAttachments)
    {
        ArgumentNullException.ThrowIfNull(appendix);
        ContractPdfDesign.EnsureFontsRegistered();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(ContractPdfDesign.PageMarginHorizontal);
                page.MarginRight(ContractPdfDesign.PageMarginHorizontal);
                page.MarginTop(ContractPdfDesign.PageMarginTop);
                page.MarginBottom(ContractPdfDesign.PageMarginBottom);
                page.DefaultTextStyle(text => text
                    .FontFamily(ContractPdfDesign.FontFamily)
                    .FontSize(ContractPdfDesign.BodyFontSize)
                    .LineHeight(ContractPdfDesign.BodyLineHeight));

                ComposeContractPageBackground(page, options);


                page.Content().Column(column =>
                {
                    column.Spacing(4);

                    ComposeAppendixHeader(column, appendix);
                    ComposeAppendixParties(column, appendix.RentalContract, options);
                    ComposeAppendixChanges(column, appendix, options);
                    ComposeAppendixSignatures(column, appendix);
                    ComposeReviewAttachments(column, options, includeReviewAttachments);
                });

                ComposeAppendixPageFooter(page, appendix);
            });
        }).GeneratePdf();
    }

    private static void ComposeContractPageBackground(PageDescriptor page, ContractRenderOptions options)
    {
        var watermark = ResolveWatermark(options);
        if (string.IsNullOrWhiteSpace(watermark))
        {
            return;
        }

        page.Background()
            .AlignCenter()
            .AlignMiddle()
            .Rotate(34)
            .Text(watermark)
            .FontFamily(ContractPdfDesign.FontFamily)
            .Bold()
            .FontSize(44)
            .FontColor("#EEF2F7");
    }

    private static void ComposeContractPageFooter(PageDescriptor page, ContractDocumentModel document)
    {
        page.Footer().Column(column =>
        {
            column.Item().BorderTop(0.5f).BorderColor(ContractPdfDesign.LightBorderColor);
            column.Item().PaddingTop(5).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style
                    .FontFamily(ContractPdfDesign.FontFamily)
                    .FontSize(8)
                    .FontColor(ContractPdfDesign.MutedTextColor));
                text.Span("Trang ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void ComposeContractNationalHeader(ColumnDescriptor column, ContractDocumentModel document)
    {
        column.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
            .Bold().FontSize(11.5f);
        column.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc")
            .Bold().FontSize(11);
        column.Item().AlignCenter().Text("--------------------").FontSize(9);
        column.Item().PaddingTop(2).AlignRight()
            .Text($"{ResolveDocumentLocation(document.Property.Address)}, ngày {document.PreparedAt:dd} tháng {document.PreparedAt:MM} năm {document.PreparedAt:yyyy}")
            .Italic().FontSize(10);

        column.Item().PaddingTop(8).AlignCenter().Text("HỢP ĐỒNG THUÊ PHÒNG TRỌ")
            .Bold().FontSize(17).FontColor("#0F172A");
        column.Item().AlignCenter().Text($"Số: {document.ContractNumber}")
            .Bold().FontSize(10.5f).FontColor("#334155");
        column.Item().PaddingTop(5).Text(
            "Căn cứ Bộ luật Dân sự năm 2015; Luật Nhà ở năm 2023; Luật Giao dịch điện tử năm 2023; căn cứ nhu cầu thuê và sự tự nguyện thỏa thuận của các bên.");
        column.Item().Text("Hôm nay, các bên gồm:");
    }

    private static void ComposeContractParties(
        ColumnDescriptor column,
        ContractDocumentModel document,
        ContractRenderOptions options)
    {
        column.Item().PaddingTop(3).Element(container =>
            ComposePartyBox(container, "BÊN CHO THUÊ (BÊN A)", document.Landlord, options));
        column.Item().PaddingTop(4).Element(container =>
            ComposePartyBox(container, "BÊN THUÊ (BÊN B)", document.Tenant, options));
    }

    private static void ComposePartyBox(
        IContainer container,
        string title,
        ContractDocumentParty party,
        ContractRenderOptions options)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(122);
                columns.RelativeColumn();
            });

            table.Cell().ColumnSpan(2).Element(ContractTableHeaderCell).Text(title).Bold().FontSize(9.5f);
            PartyRow(table, "Họ và tên", party.FullName);
            PartyRow(table, "Ngày sinh", FormatDate(party.DateOfBirth));
            PartyRow(table, "CCCD/Passport", ResolvePrintableDocumentNumber(party.DocumentNumber, options));
            PartyRow(table, "Địa chỉ", party.Address);
            PartyRow(table, "Điện thoại / Email", $"{party.PhoneNumber} / {party.Email}");
        });
    }

    private static void PartyRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(ContractTableCell).Text(label).Bold().FontSize(9);
        table.Cell().Element(ContractTableCell).Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(9);
    }

    private static void ComposeContractIntroduction(ColumnDescriptor column)
    {
        column.Item().PaddingTop(3).Text(
            "Bên A và Bên B sau đây gọi riêng là “Bên”, gọi chung là “Các Bên”. Các Bên thống nhất giao kết Hợp đồng với nội dung sau:");
    }

    private static void ComposeContractTermsV2(
        ColumnDescriptor column,
        ContractDocumentModel document,
        ContractRenderOptions options)
    {
        var property = document.Property;
        var terms = document.FinancialTerms;

        ContractSection(column, 1, "Đối tượng thuê, mục đích sử dụng và bàn giao");
        ContractClause(column, "1.1.", $"Bên A đồng ý cho Bên B thuê phòng {property.RoomNumber} thuộc {property.RoomingHouseName}, địa chỉ {property.Address}.");
        ContractClause(column, "1.2.", $"Đặc điểm phòng: tầng {property.Floor}; diện tích sử dụng khoảng {FormatArea(property.AreaM2)}; sức chứa tối đa {property.MaxOccupants:00} người. Mục đích thuê là để ở, không sử dụng làm địa điểm kinh doanh hoặc cho thuê lại nếu chưa được Bên A đồng ý bằng văn bản điện tử.");
        ContractClause(column, "1.3.", $"Bên A bàn giao phòng, chìa khóa và tài sản đi kèm vào ngày {terms.StartDate:dd/MM/yyyy}. Tình trạng phòng, chỉ số công tơ và tài sản bàn giao được xác nhận bằng Biên bản bàn giao điện tử; biên bản là một phần của Hợp đồng.");
        ContractClause(column, "1.4.", "Bên B có trách nhiệm kiểm tra khi nhận bàn giao và thông báo sai lệch trong vòng 24 giờ. Hư hỏng ẩn hoặc sự cố ảnh hưởng an toàn được thông báo ngay khi phát hiện.");

        ContractSection(column, 2, "Thời hạn thuê và hiệu lực hợp đồng");
        ContractClause(column, "2.1.", $"Thời hạn thuê từ ngày {terms.StartDate:dd/MM/yyyy} đến hết ngày {terms.EndDate:dd/MM/yyyy}.");
        ContractClause(column, "2.2.", "Hợp đồng có hiệu lực kể từ thời điểm người ký cuối cùng hoàn tất ký điện tử trên hệ thống VNPT eContract. Thời điểm ký của từng Bên được xác định theo chứng cứ và dấu thời gian do hệ thống ký điện tử ghi nhận.");
        ContractClause(column, "2.3.", "Nếu Hợp đồng được ký sau ngày dự kiến bàn giao, Các Bên phải xác nhận lại ngày bàn giao và kỳ tính tiền đầu tiên trước khi Bên B nhận phòng.");

        ContractSection(column, 3, "Giá thuê, tiền đặt cọc, dịch vụ và thanh toán");
        ContractClause(column, "3.1.", $"Giá thuê là {FormatMoney(terms.MonthlyRent)}/tháng (Bằng chữ: {VietnameseMoneyFormatter.Format(terms.MonthlyRent)} một tháng). Giá thuê được xác định cho {Math.Max(1, document.Occupants.Count):00} người cư trú có tên tại Điều 4.");
        ContractClause(column, "3.2.", BuildDepositClause(terms));
        ContractClause(column, "3.3.", $"Tiền thuê và dịch vụ được lập hóa đơn theo tháng. Bên B thanh toán chậm nhất ngày {terms.PaymentDay:00} của tháng kế tiếp qua phương thức thanh toán được nền tảng hỗ trợ. Kỳ đầu được tính theo số ngày sử dụng thực tế từ ngày bàn giao đến cuối tháng.");
        column.Item().ShowEntire().Text("3.4. Bảng giá dịch vụ tại thời điểm lập Hợp đồng:").Bold();
        ComposeServicePriceTable(column, document.ServicePrices);
        ContractClause(column, "3.5.", "Mọi thay đổi giá thuê phải được Các Bên thống nhất bằng phụ lục ký điện tử. Thay đổi đơn giá dịch vụ phải được thông báo theo cơ chế đã thỏa thuận và chỉ áp dụng từ kỳ tiếp theo sau ngày có hiệu lực.");
        ContractClause(column, "3.6.", "Khoản tiền thanh toán được coi là hoàn tất khi giao dịch trên nền tảng ghi nhận thành công. Lịch sử giao dịch và hóa đơn điện tử là căn cứ đối soát giữa Các Bên.");

        ContractSection(column, 4, "Người cư trú, khách và nội quy khu trọ");
        ContractClause(column, "4.1.", "Những người sau đây được cư trú tại phòng trong thời hạn Hợp đồng:");
        ComposeContractOccupantTable(column, document, options);
        ContractClause(column, "4.2.", "Việc thêm, rời đi hoặc thay đổi người thuê chính phải được lập thành phụ lục. Người ở cùng không mặc nhiên trở thành người ký hoặc chịu nghĩa vụ thanh toán thay Bên B, trừ khi có thỏa thuận khác.");
        ContractClause(column, "4.3.", "Khách qua đêm phải được Bên B thông báo cho Bên A theo nội quy. Bên B chịu trách nhiệm về hành vi của khách trong thời gian lưu trú.");
        ContractClause(column, "4.4.", BuildHouseRuleClause(document.HouseRules));

        ContractSection(column, 5, "Quyền và nghĩa vụ của Bên A");
        ContractClause(column, "5.1.", "Bàn giao phòng đúng thời gian, đặc điểm và tình trạng đã thỏa thuận; bảo đảm Bên B được sử dụng ổn định trong thời hạn thuê.");
        ContractClause(column, "5.2.", "Thực hiện sửa chữa hư hỏng thuộc trách nhiệm của Bên A trong thời gian hợp lý sau khi nhận thông báo, trừ trường hợp hư hỏng do lỗi của Bên B hoặc người do Bên B quản lý.");
        ContractClause(column, "5.3.", "Hỗ trợ cung cấp thông tin cần thiết để Bên B thực hiện đăng ký cư trú theo quy định.");
        ContractClause(column, "5.4.", "Có quyền kiểm tra tình trạng phòng khi đã thông báo trước tối thiểu 24 giờ và trong khung giờ hợp lý. Trường hợp khẩn cấp liên quan đến cháy, ngập, rò rỉ điện nước hoặc nguy cơ gây thiệt hại, Bên A được tiếp cận ngay và phải thông báo cho Bên B sớm nhất có thể.");
        ContractClause(column, "5.5.", "Không tự ý tăng giá thuê, hạn chế trái thỏa thuận quyền sử dụng phòng hoặc di chuyển tài sản của Bên B.");

        ContractSection(column, 6, "Quyền và nghĩa vụ của Bên B");
        ContractClause(column, "6.1.", "Nhận và sử dụng phòng đúng mục đích; được yêu cầu sửa chữa, giảm giá hoặc thực hiện quyền khác theo thỏa thuận và pháp luật khi phòng không bảo đảm điều kiện sử dụng.");
        ContractClause(column, "6.2.", "Thanh toán đầy đủ, đúng hạn; bảo quản phòng, chìa khóa, thiết bị và tài sản bàn giao; chịu chi phí khắc phục thiệt hại do lỗi của mình, người cư trú hoặc khách gây ra.");
        ContractClause(column, "6.3.", "Không tự ý đục phá, cơi nới, thay đổi kết cấu, cho thuê lại, chuyển giao phòng hoặc sử dụng phòng cho hoạt động trái pháp luật.");
        ContractClause(column, "6.4.", "Thông báo kịp thời về sự cố điện, nước, cháy nổ, hư hỏng nghiêm trọng hoặc nguy cơ mất an toàn. Bên B không chịu trách nhiệm đối với hao mòn tự nhiên do sử dụng đúng mục đích.");
        ContractClause(column, "6.5.", "Hoàn trả phòng, chìa khóa và tài sản đi kèm khi Hợp đồng chấm dứt; phối hợp chốt chỉ số dịch vụ, công nợ và Biên bản trả phòng.");

        ContractSection(column, 7, "Sửa chữa, hư hỏng và quyết toán công nợ");
        ContractClause(column, "7.1.", "Chi phí sửa chữa hư hỏng do hao mòn tự nhiên hoặc khuyết tật không do Bên B gây ra thuộc trách nhiệm Bên A. Hư hỏng do lỗi của Bên B, người cư trú hoặc khách do Bên B quản lý được bồi thường theo chi phí hợp lý có chứng từ hoặc biên bản xác nhận.");
        ContractClause(column, "7.2.", "Khi trả phòng, Các Bên lập biên bản xác nhận tình trạng phòng, tài sản, công nợ và khoản khấu trừ cọc. Bên A phải cung cấp căn cứ cho từng khoản khấu trừ.");
        ContractClause(column, "7.3.", "Phần tiền cọc còn lại được hoàn qua nền tảng sau khi hoàn tất biên bản trả phòng và nghĩa vụ cuối cùng. Trường hợp có tranh chấp, phần không tranh chấp vẫn được xử lý trước.");

        ContractSection(column, 8, "Chấm dứt hợp đồng và xử lý tiền cọc");
        ContractClause(column, "8.1.", "Hợp đồng chấm dứt khi hết thời hạn; theo thỏa thuận của Các Bên; phòng không còn hoặc không thể tiếp tục sử dụng; hoặc thuộc trường hợp khác theo Hợp đồng và pháp luật.");
        ContractClause(column, "8.2.", "Bên đơn phương chấm dứt phải thông báo bằng văn bản điện tử cho Bên kia trước ít nhất 30 ngày, trừ trường hợp Các Bên có thỏa thuận khác hoặc pháp luật cho phép xử lý ngay do vi phạm, nguy hiểm hay bất khả kháng.");
        ContractClause(column, "8.3.", "Bên A có quyền chấm dứt theo điều kiện pháp luật và Hợp đồng, bao gồm trường hợp Bên B không thanh toán đủ tiền thuê từ 03 tháng trở lên; sử dụng sai mục đích; tự ý cải tạo, cho thuê lại; hoặc vi phạm nghiêm trọng nội quy đã được lập biên bản nhưng không khắc phục.");
        ContractClause(column, "8.4.", "Bên B có quyền chấm dứt khi Bên A không sửa chữa hư hỏng nặng; tăng giá trái thỏa thuận; hoặc quyền sử dụng phòng bị hạn chế nghiêm trọng không do lỗi Bên B.");
        ContractClause(column, "8.5.", "Nếu Bên B tự ý chấm dứt trước hạn không thuộc trường hợp được phép và không được Bên A chấp thuận, tiền cọc được dùng để bù trừ nghĩa vụ, thiệt hại và khoản bồi thường theo thỏa thuận; phần xử lý phải được thể hiện trong quyết toán.");
        ContractClause(column, "8.6.", "Nếu Bên A tự ý chấm dứt trước hạn không thuộc trường hợp được phép và không được Bên B chấp thuận, Bên A hoàn lại toàn bộ tiền cọc và bồi thường cho Bên B một khoản tương đương tiền cọc, không loại trừ nghĩa vụ bồi thường thiệt hại khác nếu có căn cứ.");

        ContractSection(column, 9, "Thông báo, giải quyết tranh chấp và sự kiện bất khả kháng");
        ContractClause(column, "9.1.", "Thông báo liên quan Hợp đồng được gửi qua tài khoản nền tảng và email đã đăng ký. Thay đổi thông tin liên hệ phải được cập nhật; Bên không cập nhật tự chịu rủi ro không nhận được thông báo.");
        ContractClause(column, "9.2.", "Tranh chấp trước hết được giải quyết bằng thương lượng và đối soát dữ liệu trong 15 ngày kể từ khi một Bên gửi yêu cầu. Nếu không giải quyết được, mỗi Bên có quyền yêu cầu cơ quan có thẩm quyền giải quyết.");
        ContractClause(column, "9.3.", "Bên bị ảnh hưởng bởi bất khả kháng phải thông báo sớm nhất có thể, áp dụng biện pháp hạn chế thiệt hại và cung cấp tài liệu chứng minh hợp lý. Nghĩa vụ thanh toán đã phát sinh trước sự kiện không tự động được miễn trừ.");

        ContractSection(column, 10, "Phụ lục, giao kết và lưu trữ điện tử");
        ContractClause(column, "10.1.", "Phụ lục được Các Bên ký điện tử là bộ phận không tách rời của Hợp đồng. Nội dung phụ lục được ưu tiên áp dụng đối với phần được sửa đổi kể từ ngày hiệu lực ghi tại phụ lục; các nội dung khác giữ nguyên.");
        ContractClause(column, "10.2.", "Các Bên xác nhận đã được xem toàn bộ Hợp đồng, phụ lục và nội quy trước khi ký; thông tin cung cấp là đúng sự thật; việc ký được thực hiện tự nguyện bằng phương thức ký do VNPT eContract cung cấp.");
        ContractClause(column, "10.3.", "Tệp PDF nguyên bản do VNPT eContract trả về sau khi hoàn tất ký là bản hợp đồng điện tử chính thức. Bản xem trước, bản đã che thông tin và hồ sơ đối chiếu giấy tờ không thay thế bản hợp đồng điện tử chính thức.");
        ContractClause(column, "10.4.", "Hợp đồng điện tử được lưu để Các Bên truy cập, tải xuống và đối chiếu khi cần thiết. Tính toàn vẹn và thời điểm ký được xác định theo tệp ký, mã băm, nhật ký và chứng cứ của nhà cung cấp dịch vụ ký điện tử.");
        ContractClause(column, "10.5.", "Hợp đồng gồm 10 Điều và các tài liệu ký kèm được Các Bên đọc, hiểu và thống nhất toàn bộ nội dung.");
    }

    private static void ComposeServicePriceTable(
        ColumnDescriptor column,
        IReadOnlyList<ContractDocumentServicePrice> servicePrices)
    {
        column.Item().PaddingTop(2).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(2.1f);
            });

            table.Header(header =>
            {
                ContractHeaderCell(header.Cell(), "Dịch vụ");
                ContractHeaderCell(header.Cell(), "Đơn vị tính");
                ContractHeaderCell(header.Cell(), "Đơn giá");
                ContractHeaderCell(header.Cell(), "Ghi chú");
            });

            if (servicePrices.Count == 0)
            {
                ContractBodyCell(table.Cell(), "-");
                ContractBodyCell(table.Cell(), "-");
                ContractBodyCell(table.Cell(), "-");
                ContractBodyCell(table.Cell(), "Chưa cấu hình bảng giá dịch vụ tại thời điểm lập hợp đồng.");
                return;
            }

            foreach (var service in servicePrices)
            {
                ContractBodyCell(table.Cell(), service.ServiceName);
                ContractBodyCell(table.Cell(), service.PricingUnit);
                ContractBodyCell(table.Cell(), FormatMoney(service.UnitPrice));
                ContractBodyCell(table.Cell(), "Chưa bao gồm trong giá thuê");
            }
        });
    }

    private static void ComposeContractOccupantTable(
        ColumnDescriptor column,
        ContractDocumentModel document,
        ContractRenderOptions options)
    {
        var occupants = document.Occupants
            .Where(x => options.VisibleOccupantIds is null || options.VisibleOccupantIds.Contains(x.OccupantId))
            .ToList();

        column.Item().PaddingTop(2).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(30);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.05f);
                columns.RelativeColumn(1.55f);
                columns.RelativeColumn(1.65f);
            });

            table.Header(header =>
            {
                ContractHeaderCell(header.Cell(), "STT");
                ContractHeaderCell(header.Cell(), "Họ và tên");
                ContractHeaderCell(header.Cell(), "Ngày sinh");
                ContractHeaderCell(header.Cell(), "Số giấy tờ");
                ContractHeaderCell(header.Cell(), "Quan hệ");
            });

            if (occupants.Count == 0)
            {
                ContractBodyCell(table.Cell(), "-");
                ContractBodyCell(table.Cell(), "Chưa có thông tin người cư trú.");
                ContractBodyCell(table.Cell(), "-");
                ContractBodyCell(table.Cell(), "-");
                ContractBodyCell(table.Cell(), "-");
                return;
            }

            for (var index = 0; index < occupants.Count; index++)
            {
                var occupant = occupants[index];
                ContractBodyCell(table.Cell(), (index + 1).ToString(CultureInfo.InvariantCulture));
                ContractBodyCell(table.Cell(), occupant.FullName);
                ContractBodyCell(table.Cell(), FormatDate(occupant.DateOfBirth));
                ContractBodyCell(table.Cell(), ResolvePrintableDocumentNumber(occupant.DocumentNumber, options));
                ContractBodyCell(table.Cell(), occupant.Relationship);
            }
        });
    }

    private static void ComposeContractSignatureArea(
        ColumnDescriptor column,
        bool capturePositions)
    {
        column.Item().EnsureSpace(250).Column(signatureColumn =>
        {
            signatureColumn.Item().PaddingTop(10).AlignCenter().Text("ĐẠI DIỆN CÁC BÊN").Bold().FontSize(11.5f);
            signatureColumn.Item().Border(0.65f).BorderColor(ContractPdfDesign.BorderColor).Row(row =>
            {
                row.RelativeItem().BorderRight(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
                    .Element(container => ComposeContractSignatureBox(
                        container,
                        "BÊN CHO THUÊ (BÊN A)",
                        capturePositions ? LandlordCaptureId : null));
                row.RelativeItem()
                    .Element(container => ComposeContractSignatureBox(
                        container,
                        "BÊN THUÊ (BÊN B)",
                        capturePositions ? TenantCaptureId : null));
            });
        });
    }

    /// <summary>Renders a signature box without position capture (used by normal rendering paths).</summary>
    private static void ComposeContractSignatureBox(IContainer container, string role)
    {
        ComposeContractSignatureBox(container, role, captureId: null);
    }

    /// <summary>
    /// Renders a signature box, optionally tagging the blank signing area with a
    /// QuestPDF <see cref="IContainer.CaptureContentPosition"/> identifier so that
    /// the layout engine records the element's bounding box after each render pass.
    /// </summary>
    private static void ComposeContractSignatureBox(IContainer container, string role, string? captureId)
    {
        container.Column(column =>
        {
            column.Item().BorderBottom(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
                .PaddingVertical(6).AlignCenter().Text(role).Bold().FontSize(10);
            column.Item().BorderBottom(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
                .PaddingVertical(6).AlignCenter().Text("Ký điện tử qua VNPT eContract").FontSize(9.5f);
            // Signature zone — capture its position when captureId is provided
            if (captureId != null)
                column.Item().CaptureContentPosition(captureId).Height(SignatureZoneHeightPoints);
            else
                column.Item().Height(SignatureZoneHeightPoints);
        });
    }

    private static void ContractSection(ColumnDescriptor column, int number, string title)
    {
        column.Item().EnsureSpace(45).PaddingTop(5)
            .Text($"ĐIỀU {number}. {title.ToUpperInvariant()}")
            .Bold().FontSize(11.2f).FontColor(ContractPdfDesign.PrimaryColor);
    }

    private static void ContractClause(ColumnDescriptor column, string number, string content)
    {
        column.Item().ShowEntire().Text(text =>
        {
            text.Span(number + " ").Bold();
            text.Span(content);
        });
    }

    private static void ContractHeaderCell(IContainer container, string text)
    {
        container.Element(ContractTableHeaderCell).Text(text).Bold().FontSize(9);
    }

    private static void ContractBodyCell(IContainer container, string text)
    {
        container.Element(ContractTableCell).Text(text).FontSize(9);
    }

    private static IContainer ContractTableHeaderCell(IContainer container)
    {
        return container.Border(0.5f).BorderColor(ContractPdfDesign.BorderColor)
            .Background(ContractPdfDesign.HeaderBackground).Padding(5).AlignMiddle();
    }

    private static IContainer ContractTableCell(IContainer container)
    {
        return container.Border(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
            .Padding(5).AlignMiddle();
    }

    private static string BuildDepositClause(ContractDocumentFinancialTerms terms)
    {
        if (terms.DepositPaidAt.HasValue)
        {
            return $"Bên B đã thanh toán tiền đặt cọc {FormatMoney(terms.DepositAmount)} qua nền tảng ngày {terms.DepositPaidAt:dd/MM/yyyy}. Khoản cọc được quản lý theo trạng thái tạm giữ trên nền tảng và dùng để bảo đảm nghĩa vụ thanh toán, bồi thường thiệt hại và hoàn trả phòng.";
        }

        return $"Tiền đặt cọc là {FormatMoney(terms.DepositAmount)}. Khoản cọc được quản lý theo trạng thái tạm giữ trên nền tảng và dùng để bảo đảm nghĩa vụ thanh toán, bồi thường thiệt hại và hoàn trả phòng.";
    }

    private static string BuildHouseRuleClause(IReadOnlyList<string> houseRules)
    {
        if (houseRules.Count == 0)
        {
            return "Bên B và người cư trú tuân thủ quy định về vệ sinh, an ninh trật tự, khách lưu trú, khu vực để xe và phòng cháy, chữa cháy đã được cung cấp trước khi ký.";
        }

        return $"Bên B và người cư trú tuân thủ nội quy đã được cung cấp trước khi ký, gồm: {string.Join("; ", houseRules)}.";
    }

    private static string ResolvePrintableDocumentNumber(string documentNumber, ContractRenderOptions options)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return "-";
        }

        return options.ShowFullDocumentNumbers ? documentNumber.Trim() : MaskDocumentNumber(documentNumber);
    }

    private static string FormatArea(decimal? areaM2)
    {
        return areaM2.HasValue
            ? $"{areaM2.Value.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"))} m²"
            : "chưa xác định";
    }

    private static string ResolveDocumentLocation(string address)
    {
        var location = address
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(location))
        {
            return "Việt Nam";
        }

        return location
            .Replace("Thành phố ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Tỉnh ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string ResolveWatermark(ContractRenderOptions options)
    {
        if (options.PreviewAudience.HasValue)
        {
            return "BẢN XEM TRƯỚC";
        }

        return string.Equals(options.ViewerMode, ContractFilePurpose.MaskedReference.ToString(), StringComparison.OrdinalIgnoreCase)
            ? "BẢN THAM CHIẾU"
            : string.Empty;
    }

    private static string ResolveHeaderLabel(ContractRenderOptions options)
    {
        if (options.PreviewAudience.HasValue)
        {
            return "BẢN XEM TRƯỚC - CHƯA CÓ GIÁ TRỊ KÝ";
        }

        return string.Equals(options.ViewerMode, ContractFilePurpose.MaskedReference.ToString(), StringComparison.OrdinalIgnoreCase)
            ? "BẢN THAM CHIẾU ĐÃ CHE THÔNG TIN"
            : string.Empty;
    }

    private static void ComposeHeader(ColumnDescriptor column, RentalContract contract)
    {
        var now = DateTimeOffset.UtcNow;

        column.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold();
        column.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc").Italic();
        column.Item().AlignCenter().Text("---o0o---");
        column.Item().AlignRight().Text($"Ngày {now:dd} tháng {now:MM} năm {now:yyyy}").Italic();

        column.Item()
            .PaddingTop(10)
            .AlignCenter()
            .Text("HỢP ĐỒNG CHO THUÊ PHÒNG TRỌ")
            .Bold()
            .FontSize(16);

        column.Item().AlignCenter().Text($"Số hợp đồng: {contract.ContractNumber}").Bold();
        column.Item()
            .PaddingTop(6)
            .Text($"Căn cứ vào Bộ luật Dân sự năm 2015; Căn cứ vào nhu cầu và thỏa thuận của hai bên. Hôm nay, ngày {now:dd/MM/yyyy}, chúng tôi gồm:");
    }

    private static void ComposePartyInfo(
        ColumnDescriptor column,
        RentalContract contract,
        ContractRenderOptions options)
    {
        var landlord = contract.Room.RoomingHouse.Landlord;
        var landlordProfile = landlord.UserProfile;
        var tenant = contract.MainTenantUser;
        var tenantProfile = tenant.UserProfile;

        column.Item().PaddingTop(8).Text("BÊN CHO THUÊ (BÊN A):").Bold();
        column.Item().Text($"Ông/Bà: {landlordProfile?.FullName ?? landlord.DisplayName}");
        column.Item().Text($"Ngày sinh: {FormatDate(landlordProfile?.DateOfBirth)}");
        column.Item().Text($"CCCD/Passport số: {ResolveUserDocumentNumber(landlord.Id, landlordProfile?.VerifiedCitizenIdMasked, options)}");
        column.Item().Text($"Địa chỉ thường trú: {landlordProfile?.AddressLine ?? "-"}");
        column.Item().Text($"Số điện thoại: {landlord.PhoneNumber ?? "-"}");

        column.Item().PaddingTop(8).Text("BÊN THUÊ (BÊN B):").Bold();
        column.Item().Text($"Ông/Bà (đại diện thuê): {tenantProfile?.FullName ?? tenant.DisplayName}");
        column.Item().Text($"Ngày sinh: {FormatDate(tenantProfile?.DateOfBirth)}");
        column.Item().Text($"CCCD/Passport số: {ResolveUserDocumentNumber(tenant.Id, tenantProfile?.VerifiedCitizenIdMasked, options)}");
        column.Item().Text($"Địa chỉ thường trú: {tenantProfile?.AddressLine ?? "-"}");
        column.Item().Text($"Số điện thoại: {tenant.PhoneNumber ?? "-"}");

        column.Item()
            .PaddingTop(8)
            .Text("Hai bên thống nhất ký kết hợp đồng thuê phòng trọ với các điều khoản sau:")
            .Italic();
    }

    private static void ComposeRentalTerms(ColumnDescriptor column, RentalContract contract)
    {
        column.Item().PaddingTop(8).Text("ĐIỀU 1: ĐẶC ĐIỂM PHÒNG CHO THUÊ, GIÁ CẢ VÀ THỜI HẠN").Bold();
        column.Item().Text($"1. Bên A đồng ý cho Bên B thuê phòng {contract.Room.RoomNumber} tại khu trọ {contract.Room.RoomingHouse.Name}, địa chỉ {contract.Room.RoomingHouse.AddressDisplay}.");
        column.Item().Text($"2. Giá thuê phòng: {FormatMoney(contract.MonthlyRent)} / tháng.");
        column.Item().Text($"3. Hình thức thanh toán: chuyển khoản hoặc tiền mặt vào ngày {contract.PaymentDay} hằng tháng.");
        column.Item().Text($"4. Tiền cọc: {FormatMoney(contract.DepositAmount)} để đảm bảo thực hiện hợp đồng.");
        column.Item().Text($"5. Thời hạn thuê phòng: từ ngày {contract.StartDate:dd/MM/yyyy} đến ngày {contract.EndDate:dd/MM/yyyy}.");
    }

    private static void ComposeOccupants(
        ColumnDescriptor column,
        RentalContract contract,
        ContractRenderOptions options)
    {
        column.Item().PaddingTop(8).Text("ĐIỀU 2: DANH SÁCH NGƯỜI Ở CÙNG CƯ TRÚ").Bold();
        column.Item().Text("Bên B cam kết những người dưới đây sẽ cùng cư trú tại phòng trọ và chịu trách nhiệm về thông tin khai báo:");

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(32);
                columns.RelativeColumn(2.4f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.7f);
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "STT");
                HeaderCell(header.Cell(), "Họ và tên");
                HeaderCell(header.Cell(), "Ngày sinh");
                HeaderCell(header.Cell(), "CCCD/Passport/Giấy khai sinh");
                HeaderCell(header.Cell(), "Quan hệ");
            });

            var occupants = contract.Occupants
                .Where(x => options.VisibleOccupantIds is null || options.VisibleOccupantIds.Contains(x.Id))
                .OrderBy(x => x.CreatedAt)
                .ToList();

            if (occupants.Count == 0)
            {
                BodyCell(table.Cell(), "-");
                BodyCell(table.Cell(), "Chưa có thông tin người ở.");
                BodyCell(table.Cell(), "-");
                BodyCell(table.Cell(), "-");
                BodyCell(table.Cell(), "-");
                return;
            }

            for (var index = 0; index < occupants.Count; index++)
            {
                var occupant = occupants[index];
                var document = occupant.Documents.OrderBy(x => x.UploadedAt).FirstOrDefault();

                BodyCell(table.Cell(), (index + 1).ToString(CultureInfo.InvariantCulture));
                BodyCell(table.Cell(), occupant.FullName);
                BodyCell(table.Cell(), occupant.DateOfBirth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                BodyCell(table.Cell(), ResolveOccupantDocumentNumber(occupant, document, options));
                BodyCell(table.Cell(), occupant.RelationshipToMainTenant ?? "-");
            }
        });

        var visibleOccupantCount = options.VisibleOccupantIds is null
            ? contract.Occupants.Count
            : contract.Occupants.Count(x => options.VisibleOccupantIds.Contains(x.Id));
        column.Item().Text($"Tổng số thành viên hiển thị: {visibleOccupantCount} người.").Italic();
    }

    private static void ComposeGeneralTerms(ColumnDescriptor column)
    {
        column.Item().PaddingTop(8).Text("ĐIỀU 3: QUYỀN VÀ NGHĨA VỤ CỦA BÊN A").Bold();
        column.Item().Text("1. Bàn giao phòng đúng tình trạng như thỏa thuận.");
        column.Item().Text("2. Tạo điều kiện cho Bên B đăng ký tạm trú, tạm vắng theo quy định.");
        column.Item().Text("3. Có quyền kiểm tra định kỳ hoặc đột xuất việc giữ gìn vệ sinh, an ninh trật tự và số lượng người ở thực tế.");

        column.Item().PaddingTop(8).Text("ĐIỀU 4: QUYỀN VÀ NGHĨA VỤ CỦA BÊN B").Bold();
        column.Item().Text("1. Trả tiền thuê phòng và các chi phí phát sinh đầy đủ, đúng hạn.");
        column.Item().Text("2. Chỉ những người có tên trong Điều 2 mới được phép lưu trú.");
        column.Item().Text("3. Chấp hành nội quy khu trọ và quy định pháp luật về an ninh trật tự, phòng chống cháy nổ.");

        column.Item().PaddingTop(8).Text("ĐIỀU 5: CHẤM DỨT HỢP ĐỒNG VÀ PHẠT VI PHẠM").Bold();
        column.Item().Text("1. Hợp đồng chấm dứt khi hết thời hạn thuê hoặc do hai bên thỏa thuận chấm dứt trước thời hạn.");
        column.Item().Text("2. Nếu Bên B đơn phương chấm dứt hợp đồng trước thời hạn mà không có lý do chính đáng được Bên A đồng ý, Bên B có thể mất toàn bộ số tiền đặt cọc.");
        column.Item().Text("3. Nếu Bên A đơn phương chấm dứt hợp đồng trước thời hạn mà không có lý do chính đáng được Bên B đồng ý, Bên A phải hoàn trả tiền cọc theo thỏa thuận.");

        column.Item().PaddingTop(8).Text("ĐIỀU 6: ĐIỀU KHOẢN CHUNG").Bold();
        column.Item().Text("1. Hợp đồng này được lập thành 02 bản có giá trị pháp lý như nhau, mỗi bên giữ 01 bản.");
        column.Item().Text("2. Hai bên cam kết thực hiện đúng các điều khoản đã ghi. Mọi tranh chấp phát sinh sẽ ưu tiên giải quyết qua thương lượng.");
    }

    private static void ComposeSignatures(ColumnDescriptor column, RentalContract contract)
    {
        column.Item().PaddingTop(16).Row(row =>
        {
            row.RelativeItem().AlignCenter().Column(left =>
            {
                left.Item().Text("ĐẠI DIỆN BÊN A").Bold();
                left.Item().Text("(Ký, ghi rõ họ tên)").Italic();
                left.Item().PaddingTop(28).Text(FormatSignatureStatus(contract, ContractSignerRole.Landlord));
                left.Item().Text(GetSignerName(contract, ContractSignerRole.Landlord)).Bold();
            });

            row.RelativeItem().AlignCenter().Column(right =>
            {
                right.Item().Text("ĐẠI DIỆN BÊN B").Bold();
                right.Item().Text("(Ký, ghi rõ họ tên)").Italic();
                right.Item().PaddingTop(28).Text(FormatSignatureStatus(contract, ContractSignerRole.Tenant));
                right.Item().Text(GetSignerName(contract, ContractSignerRole.Tenant)).Bold();
            });
        });
    }

    private static void ComposeAppendixPageFooter(PageDescriptor page, ContractAppendix appendix)
    {
        page.Footer().Column(column =>
        {
            column.Item().BorderTop(0.5f).BorderColor(ContractPdfDesign.LightBorderColor);
            column.Item().PaddingTop(5).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style
                    .FontFamily(ContractPdfDesign.FontFamily)
                    .FontSize(8)
                    .FontColor(ContractPdfDesign.MutedTextColor));
                text.Span("Trang ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void ComposeAppendixHeader(ColumnDescriptor column, ContractAppendix appendix)
    {
        var contract = appendix.RentalContract;
        var signedDate = FormatContractSignedDate(contract);
        var now = appendix.CreatedAt.ToLocalTime();

        column.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
            .Bold().FontSize(11.5f);
        column.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc")
            .Bold().FontSize(11);
        column.Item().AlignCenter().Text("--------------------").FontSize(9);
        column.Item().PaddingTop(2).AlignRight()
            .Text($"{ResolveDocumentLocation(contract.Room.RoomingHouse.AddressDisplay)}, ngày {now:dd} tháng {now:MM} năm {now:yyyy}")
            .Italic().FontSize(10);

        column.Item().PaddingTop(8).AlignCenter().Text("PHỤ LỤC HỢP ĐỒNG THUÊ PHÒNG TRỌ")
            .Bold().FontSize(17).FontColor("#0F172A");
        column.Item().AlignCenter().Text($"Số: {appendix.AppendixNumber}")
            .Bold().FontSize(10.5f).FontColor("#334155");
        column.Item().PaddingTop(5)
            .Text($"Căn cứ theo Hợp đồng cho thuê phòng trọ số {contract.ContractNumber} đã ký ngày {signedDate}; Căn cứ nhu cầu thực tế và sự tự nguyện thỏa thuận của các bên.");
        column.Item().Text("Hôm nay, các bên gồm có:");
    }

    private static void ComposeAppendixParties(
        ColumnDescriptor column,
        RentalContract contract,
        ContractRenderOptions options)
    {
        var landlord = contract.Room.RoomingHouse.Landlord;
        var landlordProfile = landlord.UserProfile;
        var tenant = contract.MainTenantUser;
        var tenantProfile = tenant.UserProfile;

        column.Item().PaddingTop(3).Element(container =>
            ComposeAppendixPartyBox(
                container,
                "BÊN CHO THUÊ (BÊN A)",
                landlordProfile?.FullName ?? landlord.DisplayName ?? "Bên A",
                FormatDate(landlordProfile?.DateOfBirth),
                ResolveUserDocumentNumber(landlord.Id, landlordProfile?.VerifiedCitizenIdMasked, options),
                landlordProfile?.AddressLine,
                landlord.PhoneNumber,
                landlord.Email));

        column.Item().PaddingTop(4).Element(container =>
            ComposeAppendixPartyBox(
                container,
                "BÊN THUÊ (BÊN B)",
                tenantProfile?.FullName ?? tenant.DisplayName ?? "Bên B",
                FormatDate(tenantProfile?.DateOfBirth),
                ResolveUserDocumentNumber(tenant.Id, tenantProfile?.VerifiedCitizenIdMasked, options),
                tenantProfile?.AddressLine,
                tenant.PhoneNumber,
                tenant.Email));
    }

    private static void ComposeAppendixPartyBox(
        IContainer container,
        string title,
        string fullName,
        string dateOfBirth,
        string documentNumber,
        string? address,
        string? phone,
        string? email)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(122);
                columns.RelativeColumn();
            });

            // Header row – no AlignMiddle to avoid size constraint conflicts
            table.Cell().ColumnSpan(2)
                .Border(0.5f).BorderColor(ContractPdfDesign.BorderColor)
                .Background(ContractPdfDesign.HeaderBackground)
                .Padding(5)
                .Text(title).Bold().FontSize(9.5f);

            AppendixPartyRow(table, "Họ và tên", fullName);
            AppendixPartyRow(table, "Ngày sinh", dateOfBirth);
            AppendixPartyRow(table, "CCCD/Passport", documentNumber);
            AppendixPartyRow(table, "Địa chỉ", address);
            AppendixPartyRow(table, "Điện thoại / Email", $"{phone ?? "-"} / {email ?? "-"}");
        });
    }

    private static void AppendixPartyRow(TableDescriptor table, string label, string? value)
    {
        table.Cell()
            .Border(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
            .Padding(5)
            .Text(label).Bold().FontSize(9);

        table.Cell()
            .Border(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
            .Padding(5)
            .Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(9);
    }


    private static void ComposeAppendixChanges(
        ColumnDescriptor column,
        ContractAppendix appendix,
        ContractRenderOptions options)
    {
        var contract = appendix.RentalContract;
        column.Item()
            .PaddingTop(8)
            .Text($"Sau khi xem xét, thỏa thuận hai bên đã đi đến thống nhất ký Phụ lục hợp đồng số {appendix.AppendixNumber} đối với Hợp đồng đã ký số {contract.ContractNumber}, ngày {FormatContractSignedDate(contract)} cụ thể như sau:");

        var changes = appendix.Changes.OrderBy(x => x.SortOrder).ToList();
        if (changes.Count == 0)
        {
            column.Item().Text("1. Chưa có nội dung thay đổi.");
        }
        else
        {
            for (var index = 0; index < changes.Count; index++)
            {
                column.Item().Text(text =>
                {
                    text.Span($"{index + 1}. ").Bold();
                    text.Span(DescribeAppendixChange(appendix, changes[index], options));
                });
            }
        }

        column.Item().PaddingTop(8).Text("Điều khoản chung:").Bold();
        column.Item().Text("1 Phụ lục hợp đồng được lập thành 02 bản, có nội dung và giá trị pháp lý như nhau, mỗi bên giữ 01 bản.");
        column.Item().Text($"2 Phụ lục này là một phần không thể tách rời của Hợp đồng thuê nhà số {contract.ContractNumber} và có giá trị kể từ ngày {appendix.EffectiveDate:dd/MM/yyyy}.");
    }

    private static void ComposeAppendixSignatures(ColumnDescriptor column, ContractAppendix appendix)
    {
        column.Item().ShowEntire().Column(signatureColumn =>
        {
            signatureColumn.Item().PaddingTop(10).AlignCenter().Text("ĐẠI DIỆN CÁC BÊN").Bold().FontSize(11.5f);
            signatureColumn.Item().Border(0.65f).BorderColor(ContractPdfDesign.BorderColor).Row(row =>
            {
                row.RelativeItem().BorderRight(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
                    .Element(container => ComposeAppendixSignatureBox(
                        container,
                        "BÊN CHO THUÊ (BÊN A)",
                        GetSignerName(appendix.RentalContract, ContractSignerRole.Landlord),
                        FormatAppendixSignatureStatus(appendix, ContractSignerRole.Landlord)));

                row.RelativeItem()
                    .Element(container => ComposeAppendixSignatureBox(
                        container,
                        "BÊN THUÊ (BÊN B)",
                        GetSignerName(appendix.RentalContract, ContractSignerRole.Tenant),
                        FormatAppendixSignatureStatus(appendix, ContractSignerRole.Tenant)));
            });
        });
    }

    private static void ComposeAppendixSignatureBox(
        IContainer container, 
        string role, 
        string signerName, 
        string signatureStatus)
    {
        container.Column(column =>
        {
            column.Item().BorderBottom(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
                .PaddingVertical(6).AlignCenter().Text(role).Bold().FontSize(10);
            
            column.Item().BorderBottom(0.35f).BorderColor(ContractPdfDesign.LightBorderColor)
                .PaddingVertical(6).AlignCenter().Text("Ký xác nhận điện tử").FontSize(9.5f);
            
            column.Item().Height(SignatureZoneHeightPoints).AlignCenter().AlignMiddle()
                .Text(signatureStatus).FontSize(9);
        });
    }


    private static void ComposeReviewAttachments(
        ColumnDescriptor column,
        ContractRenderOptions options,
        bool includeReviewAttachments)
    {
        if (!includeReviewAttachments ||
            options.PreviewAudience != ContractPreviewAudience.LandlordReview ||
            options.ReviewAttachments.Count == 0)
        {
            return;
        }

        foreach (var attachment in options.ReviewAttachments)
        {
            column.Item().PageBreak();
            column.Item()
                .Border(1)
                .BorderColor(Colors.Orange.Darken2)
                .Background(Colors.Orange.Lighten4)
                .Padding(8)
                .AlignCenter()
                .Text("HỒ SƠ PHỤC VỤ ĐỐI CHIẾU - KHÔNG THUỘC NỘI DUNG HỢP ĐỒNG KÝ")
                .Bold()
                .FontColor(Colors.Orange.Darken4)
                .FontSize(11);

            column.Item().PaddingTop(8).Text(text =>
            {
                text.Span("Người được đối chiếu: ").Bold();
                text.Span(attachment.PersonName);
            });
            column.Item().Text(text =>
            {
                text.Span("Vai trò: ").Bold();
                text.Span(attachment.PersonRole);
            });
            column.Item().Text(text =>
            {
                text.Span("Loại giấy tờ: ").Bold();
                text.Span(string.IsNullOrWhiteSpace(attachment.DocumentType) ? "-" : attachment.DocumentType);
            });
            column.Item().Text(text =>
            {
                text.Span("Số giấy tờ: ").Bold();
                text.Span(string.IsNullOrWhiteSpace(attachment.DocumentNumber) ? "-" : attachment.DocumentNumber);
            });

            var images = attachment.Images.Take(3).ToList();
            while (images.Count < 3)
            {
                images.Add(new ContractReviewImage(
                    images.Count == 0 ? "Mặt trước" : images.Count == 1 ? "Mặt sau" : "Ảnh bổ sung",
                    null,
                    "Chưa cung cấp."));
            }

            column.Item().PaddingTop(8).Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(container => ComposeReviewImage(container, images[0], 210));
                row.RelativeItem().Element(container => ComposeReviewImage(container, images[1], 210));
            });

            column.Item().PaddingTop(8).Element(container => ComposeReviewImage(container, images[2], 210));
            column.Item()
                .PaddingTop(8)
                .AlignCenter()
                .Text("CHỈ PHỤC VỤ ĐỐI CHIẾU")
                .Bold()
                .FontColor(Colors.Grey.Darken1)
                .FontSize(9);
        }
    }

    private static void ComposeReviewImage(
        IContainer container,
        ContractReviewImage image,
        float height)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(image.Label).Bold().FontSize(10);
            var frame = column.Item()
                .Height(height)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.Grey.Lighten4)
                .Padding(6);

            if (image.Content is { Length: > 0 })
            {
                frame.AlignCenter().AlignMiddle().Image(image.Content).FitArea();
            }
            else
            {
                frame.AlignCenter()
                    .AlignMiddle()
                    .Text(image.MissingMessage)
                    .FontColor(Colors.Grey.Darken1)
                    .FontSize(9);
            }
        });
    }

    private static string DescribeAppendixChange(
        ContractAppendix appendix,
        ContractAppendixChange change,
        ContractRenderOptions options)
    {
        if (change.TargetType == ContractAppendixTargetType.ContractOccupant)
        {
            if (change.ChangeType == ContractAppendixChangeType.Remove)
            {
                var removedOccupant = appendix.RentalContract.Occupants.FirstOrDefault(x => x.Id == change.TargetId);
                if (removedOccupant is not null)
                {
                    var document = removedOccupant.Documents.OrderBy(x => x.UploadedAt).FirstOrDefault();
                    return $"Ông/bà {removedOccupant.FullName}, CCCD/Passport/Giấy khai sinh số {ResolveOccupantDocumentNumber(removedOccupant, document, options)}, rời khỏi danh sách người ở cùng của Hợp đồng thuê phòng trọ số {appendix.RentalContract.ContractNumber}.";
                }
            }

            if (change.ChangeType == ContractAppendixChangeType.Add)
            {
                var addedOccupant = appendix.RentalContract.Occupants.FirstOrDefault(x => x.Id == change.TargetId);
                if (addedOccupant is not null)
                {
                    var document = addedOccupant.Documents.OrderBy(x => x.UploadedAt).FirstOrDefault();
                    return $"Ông/bà {addedOccupant.FullName}, CCCD/Passport/Giấy khai sinh số {ResolveOccupantDocumentNumber(addedOccupant, document, options)}, được thêm vào danh sách người ở cùng của Hợp đồng thuê phòng trọ số {appendix.RentalContract.ContractNumber}.";
                }

                var occupantRequest = TryParseOccupantRequest(change.NewValue);
                if (occupantRequest is not null)
                {
                    var documentNumber = ResolveOccupantRequestDocumentNumber(occupantRequest, options);
                    var fullName = ResolveOccupantRequestName(occupantRequest);
                    return $"Ông/bà {fullName}, CCCD/Passport/Giấy khai sinh số {documentNumber}, được thêm vào danh sách người ở cùng của Hợp đồng thuê phòng trọ số {appendix.RentalContract.ContractNumber}.";
                }
            }
        }

        var action = change.ChangeType switch
        {
            ContractAppendixChangeType.Add => "Thêm",
            ContractAppendixChangeType.Update => "Cập nhật",
            ContractAppendixChangeType.Remove => "Xóa",
            _ => change.ChangeType.ToString()
        };

        var target = change.TargetType switch
        {
            ContractAppendixTargetType.Contract => "hợp đồng",
            ContractAppendixTargetType.ContractOccupant => "người ở",
            _ => change.TargetType.ToString()
        };

        var field = string.IsNullOrWhiteSpace(change.FieldName)
            ? string.Empty
            : $" {FormatAppendixFieldName(change.FieldName)}";

        string FormatValue(string rawValue)
        {
            var trimmed = TrimJsonString(rawValue);
            if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;

            var normalizedField = string.IsNullOrWhiteSpace(change.FieldName) ? string.Empty : NormalizeFieldName(change.FieldName);

            if (normalizedField == "maintenantuserid" && Guid.TryParse(trimmed, out var userId))
            {
                var occupant = appendix.RentalContract.Occupants.FirstOrDefault(x => x.UserId == userId);
                if (occupant != null)
                {
                    var doc = occupant.Documents.OrderBy(x => x.UploadedAt).FirstOrDefault();
                    return $"{occupant.FullName} - {ResolveOccupantDocumentNumber(occupant, doc, options)}";
                }
            }

            if (normalizedField == "monthlyrent" && decimal.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rent))
            {
                return FormatMoney(rent);
            }

            if (normalizedField == "enddate" && DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            {
                return FormatDate(DateOnly.FromDateTime(date));
            }

            return trimmed;
        }

        var oldValue = string.IsNullOrWhiteSpace(change.OldValue)
            ? string.Empty
            : $" từ {FormatValue(change.OldValue)}";

        var newValue = string.IsNullOrWhiteSpace(change.NewValue)
            ? string.Empty
            : $" thành {FormatValue(change.NewValue)}";

        return $"{action} {target}{field}{oldValue}{newValue}.";
    }

    private static string FormatAppendixFieldName(string fieldName)
    {
        return NormalizeFieldName(fieldName) switch
        {
            "monthlyrent" => "giá thuê hằng tháng",
            "paymentday" => "ngày thanh toán hằng tháng",
            "startdate" => "ngày bắt đầu hợp đồng",
            "enddate" => "ngày kết thúc hợp đồng",
            "maintenantuserid" => "người đại diện thuê chính",
            "relationshiptomaintenant" => "quan hệ với người thuê chính",
            "moveoutdate" => "ngày rời đi",
            "moveindate" => "ngày chuyển vào",
            _ => $"trường {fieldName}"
        };
    }

    private static string NormalizeFieldName(string value)
    {
        return value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    private static string ResolveUserDocumentNumber(
        Guid userId,
        string? maskedDocumentNumber,
        ContractRenderOptions options)
    {
        if (options.ShowFullDocumentNumbers &&
            options.UserDocumentNumbersByUserId.TryGetValue(userId, out var fullDocumentNumber) &&
            !string.IsNullOrWhiteSpace(fullDocumentNumber))
        {
            return fullDocumentNumber;
        }

        return string.IsNullOrWhiteSpace(maskedDocumentNumber)
            ? "-"
            : maskedDocumentNumber;
    }

    private static string ResolveOccupantDocumentNumber(
        ContractOccupant occupant,
        ContractOccupantDocument? document,
        ContractRenderOptions options)
    {
        if (options.ShowFullDocumentNumbers)
        {
            if (occupant.UserId.HasValue &&
                options.UserDocumentNumbersByUserId.TryGetValue(occupant.UserId.Value, out var userDocumentNumber) &&
                !string.IsNullOrWhiteSpace(userDocumentNumber))
            {
                return userDocumentNumber;
            }

            if (document is not null &&
                options.OccupantDocumentNumbersByDocumentId.TryGetValue(document.Id, out var occupantDocumentNumber) &&
                !string.IsNullOrWhiteSpace(occupantDocumentNumber))
            {
                return occupantDocumentNumber;
            }
        }

        if (!string.IsNullOrWhiteSpace(occupant.User?.UserProfile?.VerifiedCitizenIdMasked))
        {
            return occupant.User.UserProfile.VerifiedCitizenIdMasked;
        }

        return string.IsNullOrWhiteSpace(document?.DocumentNumberMasked)
            ? "-"
            : document.DocumentNumberMasked;
    }

    private static ContractOccupantRequest? TryParseOccupantRequest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var json = value.Trim();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                json = document.RootElement.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ContractOccupantRequest>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolveOccupantRequestName(ContractOccupantRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            return request.FullName.Trim();
        }

        return !string.IsNullOrWhiteSpace(request.Email)
            ? $"người dùng {request.Email}"
            : "-";
    }

    private static string ResolveOccupantRequestDocumentNumber(
        ContractOccupantRequest request,
        ContractRenderOptions options)
    {
        var documentNumber = request.Document?.DocumentNumber;
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return !string.IsNullOrWhiteSpace(request.Email) ? "đã xác minh trên hệ thống" : "-";
        }

        return options.ShowFullDocumentNumbers
            ? documentNumber.Trim()
            : MaskDocumentNumber(documentNumber);
    }

    private static string GetSignerName(RentalContract contract, ContractSignerRole signerRole)
    {
        return signerRole == ContractSignerRole.Landlord
            ? contract.Room.RoomingHouse.Landlord.UserProfile?.FullName ?? contract.Room.RoomingHouse.Landlord.DisplayName
            : contract.MainTenantUser.UserProfile?.FullName ?? contract.MainTenantUser.DisplayName;
    }

    private static string FormatSignatureStatus(RentalContract contract, ContractSignerRole signerRole)
    {
        return contract.Signatures.Any(x =>
                x.SignerRole == signerRole &&
                x.Status == ContractSignatureStatus.Signed &&
                x.SignedAt.HasValue)
            ? "Đã ký"
            : "Chưa ký";
    }

    private static string FormatAppendixSignatureStatus(ContractAppendix appendix, ContractSignerRole signerRole)
    {
        return appendix.Signatures.Any(x =>
                x.SignerRole == signerRole &&
                x.Status == ContractSignatureStatus.Signed &&
                x.SignedAt.HasValue)
            ? "Đã ký"
            : "Chưa ký";
    }

    private static string FormatContractSignedDate(RentalContract contract)
    {
        var latestSignedAt = contract.Signatures
            .OrderByDescending(x => x.SignedAt)
            .Select(x => (DateTimeOffset?)x.SignedAt)
            .FirstOrDefault();

        return latestSignedAt?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ?? contract.ActivatedAt?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ?? contract.CreatedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatBirthYear(DateOnly? dateOfBirth)
    {
        return dateOfBirth.HasValue
            ? dateOfBirth.Value.Year.ToString(CultureInfo.InvariantCulture)
            : "-";
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container.Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text(text).Bold().FontSize(9);
    }

    private static void BodyCell(IContainer container, string text)
    {
        container.Border(1).Padding(4).Text(text).FontSize(9);
    }

    private static string TrimJsonString(string value)
    {
        return value.Trim().Trim('"');
    }

    private static string FormatMoney(decimal value)
    {
        return string.Create(CultureInfo.GetCultureInfo("vi-VN"), $"{value:N0} VNĐ");
    }

    private static string FormatDate(DateOnly? value)
    {
        return value.HasValue
            ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "-";
    }

    private static string MaskDocumentNumber(string documentNumber)
    {
        var value = documentNumber.Trim();
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return $"{new string('*', value.Length - 4)}{value[^4..]}";
    }

    /// <summary>
    /// QuestPDF <see cref="IDynamicComponent"/> that reads the positions captured by
    /// the normally paginated signature row.
    ///
    /// QuestPDF performs multiple layout passes; <see cref="Compose"/> is called for each pass.
    /// The component accumulates positions from the final pass so that
    /// <see cref="GetSignatureZones"/> can return accurate coordinates after
    /// <see cref="Document.GeneratePdf"/> completes.
    /// </summary>
    private sealed class SignatureCaptureComponent : IDynamicComponent
    {
        private readonly List<SignaturePosition> landlordPositions = [];
        private readonly List<SignaturePosition> tenantPositions = [];

        private readonly record struct SignaturePosition(
            float X,
            float Y,
            float Width,
            float Height,
            int PageNumber);

        public DynamicComponentComposeResult Compose(DynamicContext context)
        {
            var latestLandlord = context.GetContentCapturedPositions(LandlordCaptureId);
            var latestTenant = context.GetContentCapturedPositions(TenantCaptureId);

            if (latestLandlord.Count > 0)
            {
                landlordPositions.Clear();
                landlordPositions.AddRange(latestLandlord.Select(p =>
                    new SignaturePosition(p.X, p.Y, p.Width, p.Height, p.PageNumber)));
            }

            if (latestTenant.Count > 0)
            {
                tenantPositions.Clear();
                tenantPositions.AddRange(latestTenant.Select(p =>
                    new SignaturePosition(p.X, p.Y, p.Width, p.Height, p.PageNumber)));
            }

            return new DynamicComponentComposeResult
            {
                Content = context.CreateElement(_ => { }),
                HasMoreContent = false
            };
        }

        public IReadOnlyDictionary<string, SignatureZone> GetSignatureZones()
        {
            var zones = new Dictionary<string, SignatureZone>(StringComparer.Ordinal);

            if (landlordPositions.Count > 0)
            {
                var p = landlordPositions[0];
                zones["Landlord"] = new SignatureZone
                {
                    X = (int)Math.Round(p.X),
                    Y = (int)Math.Round(p.Y),
                    Width = (int)Math.Round(p.Width),
                    Height = (int)Math.Round(p.Height),
                    Page = p.PageNumber
                };
            }

            if (tenantPositions.Count > 0)
            {
                var p = tenantPositions[0];
                zones["Tenant"] = new SignatureZone
                {
                    X = (int)Math.Round(p.X),
                    Y = (int)Math.Round(p.Y),
                    Width = (int)Math.Round(p.Width),
                    Height = (int)Math.Round(p.Height),
                    Page = p.PageNumber
                };
            }

            return zones;
        }
    }
}
