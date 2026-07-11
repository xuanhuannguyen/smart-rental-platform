using SmartRentalPlatform.Application.Billing;

namespace SmartRentalPlatform.UnitTests.Billing;

public sealed class MeterReadingAiServiceTests
{
    private static readonly Guid ElectricityServiceTypeId =
        Guid.Parse("60000000-0000-0000-0000-000000000001");

    private static readonly Guid WaterServiceTypeId =
        Guid.Parse("60000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData(289, 29)]
    [InlineData(284, 28)]
    [InlineData(285, 29)]
    public void NormalizeReading_Electricity_RemovesOneDigitAndRounds(decimal raw, decimal expected)
    {
        var result = MeterReadingAiService.NormalizeReading(ElectricityServiceTypeId, raw);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(278902, 279)]
    [InlineData(278499, 278)]
    [InlineData(278500, 279)]
    public void NormalizeReading_Water_RemovesThreeDigitsAndRounds(decimal raw, decimal expected)
    {
        var result = MeterReadingAiService.NormalizeReading(WaterServiceTypeId, raw);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeReading_OtherService_KeepsRawReading()
    {
        var result = MeterReadingAiService.NormalizeReading(Guid.NewGuid(), 12345m);

        Assert.Equal(12345m, result);
    }
}
