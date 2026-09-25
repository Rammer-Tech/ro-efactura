using FluentAssertions;
using RoEFactura.Utilities;
using Xunit;

namespace RoEFactura.Tests.Utilities;

public class CifNormalizerTests
{
    [Theory]
    [InlineData("RO12345678", "12345678")]
    [InlineData("ro 12345678", "12345678")]
    [InlineData(" 12345678 ", "12345678")]
    [InlineData("RO 123 456 78", "12345678")]
    public void Normalize_StripsRoPrefixAndWhitespace(string input, string expected)
    {
        CifNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ROABC")]
    [InlineData("12A45")]
    public void Normalize_InvalidValue_ThrowsArgumentException(string input)
    {
        Action act = () => CifNormalizer.Normalize(input);

        act.Should().Throw<ArgumentException>();
    }
}
