using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractPdfRenderer : IContractPdfRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public byte[] RenderSignedRentalContract(RentalContract contract, ContractRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(11).LineHeight(1.35f));

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    ComposeHeader(column, contract);
                    ComposePartyInfo(column, contract, options);
                    ComposeRentalTerms(column, contract);
                    ComposeOccupants(column, contract, options);
                    ComposeGeneralTerms(column);
                    ComposeSignatures(column, contract);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] RenderSignedContractAppendix(ContractAppendix appendix, ContractRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(appendix);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(11).LineHeight(1.35f));

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    ComposeAppendixHeader(column, appendix);
                    ComposeAppendixParties(column, appendix.RentalContract, options);
                    ComposeAppendixChanges(column, appendix, options);
                    ComposeAppendixSignatures(column, appendix);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
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

    private static void ComposeAppendixHeader(ColumnDescriptor column, ContractAppendix appendix)
    {
        var now = DateTimeOffset.UtcNow;
        var contract = appendix.RentalContract;

        column.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold();
        column.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc").Italic();
        column.Item().AlignCenter().Text("---o0o---");
        column.Item().AlignRight().Text($"Ngày {now:dd} tháng {now:MM} năm {now:yyyy}").Italic();

        column.Item()
            .PaddingTop(10)
            .AlignCenter()
            .Text("PHỤ LỤC HỢP ĐỒNG THUÊ NHÀ TRỌ")
            .Bold()
            .FontSize(16);

        column.Item().AlignCenter().Text($"Số {appendix.AppendixNumber}").Bold();
        column.Item()
            .PaddingTop(6)
            .Text($"Căn cứ theo Hợp đồng cho thuê phòng trọ số {contract.ContractNumber} đã ký ngày {FormatContractSignedDate(contract)}; Căn cứ nhu cầu thực tế 2 bên; Chúng tôi gồm có:");
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

        column.Item().PaddingTop(8).Text("BÊN CHO THUÊ (BÊN A):").Bold();
        column.Item().Text($"Ông/Bà: {landlordProfile?.FullName ?? landlord.DisplayName}    Sinh năm: {FormatBirthYear(landlordProfile?.DateOfBirth)}");
        column.Item().Text($"CCCD/Passport số: {ResolveUserDocumentNumber(landlord.Id, landlordProfile?.VerifiedCitizenIdMasked, options)}    Cấp ngày: -    Nơi cấp: -");
        column.Item().Text($"Địa chỉ thường trú: {landlordProfile?.AddressLine ?? "-"}");
        column.Item().Text($"Số điện thoại: {landlord.PhoneNumber ?? "-"}");

        column.Item().PaddingTop(8).Text("BÊN THUÊ (BÊN B):").Bold();
        column.Item().Text($"Ông/Bà: {tenantProfile?.FullName ?? tenant.DisplayName}    Sinh năm: {FormatBirthYear(tenantProfile?.DateOfBirth)}");
        column.Item().Text($"CCCD/Passport số: {ResolveUserDocumentNumber(tenant.Id, tenantProfile?.VerifiedCitizenIdMasked, options)}    Cấp ngày: -    Nơi cấp: -");
        column.Item().Text($"Địa chỉ thường trú: {tenantProfile?.AddressLine ?? "-"}");
        column.Item().Text($"Số điện thoại: {tenant.PhoneNumber ?? "-"}");
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
        column.Item().PaddingTop(16).Row(row =>
        {
            row.RelativeItem().AlignCenter().Column(left =>
            {
                left.Item().Text("ĐẠI DIỆN BÊN A").Bold();
                left.Item().Text("(Ký, ghi rõ họ tên)").Italic();
                left.Item().PaddingTop(28).Text(FormatAppendixSignatureStatus(appendix, ContractSignerRole.Landlord));
                left.Item().Text(GetSignerName(appendix.RentalContract, ContractSignerRole.Landlord)).Bold();
            });

            row.RelativeItem().AlignCenter().Column(right =>
            {
                right.Item().Text("ĐẠI DIỆN BÊN B").Bold();
                right.Item().Text("(Ký, ghi rõ họ tên)").Italic();
                right.Item().PaddingTop(28).Text(FormatAppendixSignatureStatus(appendix, ContractSignerRole.Tenant));
                right.Item().Text(GetSignerName(appendix.RentalContract, ContractSignerRole.Tenant)).Bold();
            });
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

        var oldValue = string.IsNullOrWhiteSpace(change.OldValue)
            ? string.Empty
            : $" từ {TrimJsonString(change.OldValue)}";

        var newValue = string.IsNullOrWhiteSpace(change.NewValue)
            ? string.Empty
            : $" thành {TrimJsonString(change.NewValue)}";

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
        return contract.Signatures.Any(x => x.SignerRole == signerRole)
            ? "Đã ký"
            : "Chưa ký";
    }

    private static string FormatAppendixSignatureStatus(ContractAppendix appendix, ContractSignerRole signerRole)
    {
        return appendix.Signatures.Any(x => x.SignerRole == signerRole)
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
        return string.Create(CultureInfo.GetCultureInfo("vi-VN"), $"{value:N0} VND");
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
}
