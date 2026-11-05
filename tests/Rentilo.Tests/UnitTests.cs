using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Tests;

public class ImageSignatureTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }, ".png")]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0, 0 }, ".gif")]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, ".webp")]
    [InlineData(new byte[] { (byte)'<', (byte)'s', (byte)'v', (byte)'g', 0, 0, 0, 0, 0, 0, 0, 0 }, null)]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4, (byte)'W', (byte)'A', (byte)'V', (byte)'E' }, null)]
    public void Detects_only_supported_image_types(byte[] header, string? expected) =>
        Assert.Equal(expected, LocalImageStorage.DetectExtension(header));
}

public class DisplayTests
{
    [Theory]
    [InlineData(1350, "1 350 kr")]
    [InlineData(99.5, "99,50 kr")]
    [InlineData(0, "0 kr")]
    public void Money_uses_swedish_formatting(decimal amount, string expected) =>
        Assert.Equal(expected, Display.Money(amount));

    [Fact]
    public void DateRange_collapses_same_day_and_year()
    {
        Assert.Equal("3 Jun 2026", Display.DateRange(new(2026, 6, 3), new(2026, 6, 3)));
        Assert.Equal("3 Jun – 5 Jun 2026", Display.DateRange(new(2026, 6, 3), new(2026, 6, 5)));
        Assert.Equal("30 Dec 2026 – 2 Jan 2027", Display.DateRange(new(2026, 12, 30), new(2027, 1, 2)));
    }

    [Fact]
    public void Enum_display_names() => Assert.Equal("Tools & DIY", ItemCategory.Tools.Name());

    [Theory]
    [InlineData("Anna Lindqvist", "AL")]
    [InlineData("anna", "A")]
    [InlineData("Anna Maria von Svensson", "AS")]
    [InlineData("  ", "?")]
    public void Initials(string name, string expected) => Assert.Equal(expected, ApplicationUser.GetInitials(name));
}
