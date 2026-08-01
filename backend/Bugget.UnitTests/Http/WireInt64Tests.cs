using System.Globalization;
using Bugget.Api.Http;
using FluentAssertions;

namespace Bugget.UnitTests.Http;

/// <summary>
/// Граница <c>long ↔ wire</c> для схемы <c>Int64String</c>: канон описан в
/// <c>specs/contracts/shared.yaml</c>, гейт <c>backend-contracts-int64</c> держит
/// его в контракте, а здесь проверяется реализация — та, через которую значение
/// действительно ходит.
///
/// Значения выбраны по границам: <c>9007199254740993</c> — первое, что клиент
/// потерял бы в double; <c>9223372036854775807</c> и <c>...808</c> — верхняя
/// граница Int64 и первое значение за ней.
/// </summary>
public class WireInt64Tests
{
    private const long UnsafeForDouble = 9007199254740993L;

    [Fact(DisplayName = "ToWire: значение за пределом точности double уходит цифра в цифру")]
    public void ToWire_keeps_every_digit()
    {
        WireInt64.ToWire(UnsafeForDouble).Should().Be("9007199254740993");
        WireInt64.ToWire(0).Should().Be("0");
        WireInt64.ToWire(long.MaxValue).Should().Be("9223372036854775807");
    }

    /// <remarks>
    /// Культура потока подменяется намеренно: у неё свой разделитель групп, и
    /// форматирование «по умолчанию» отдало бы наружу что угодно, кроме канона.
    /// </remarks>
    [Fact(DisplayName = "ToWire: культура потока на провод не влияет")]
    public void ToWire_ignores_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU") { NumberFormat = { NumberGroupSeparator = " " } };
            WireInt64.ToWire(UnsafeForDouble).Should().Be("9007199254740993");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory(DisplayName = "TryParse: канон провода разбирается в исходное значение")]
    [InlineData("0", 0L)]
    [InlineData("1", 1L)]
    [InlineData("9007199254740993", UnsafeForDouble)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void TryParse_accepts_canonical(string wire, long expected)
    {
        WireInt64.TryParse(wire, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory(DisplayName = "TryParse: неканоничное значение до прикладного слоя не доходит")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("00")]
    [InlineData("007")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("1_000")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("abc")]
    [InlineData("0x10")]
    // Не-ASCII цифры: long.TryParse их не берёт, но проверка формы стоит раньше —
    // и не должна зависеть от того, что именно умеет парсер.
    [InlineData("١٢٣")]
    [InlineData("9223372036854775808")]
    [InlineData("99999999999999999999")]
    public void TryParse_rejects_non_canonical(string? wire)
    {
        WireInt64.TryParse(wire, out var value).Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact(DisplayName = "Round-trip: значение переживает long → провод → long")]
    public void Round_trip_keeps_value()
    {
        foreach (var value in new[] { 0L, 1L, UnsafeForDouble, long.MaxValue })
        {
            WireInt64.TryParse(WireInt64.ToWire(value), out var restored).Should().BeTrue();
            restored.Should().Be(value);
        }
    }
}
