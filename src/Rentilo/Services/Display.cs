using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Rentilo.Models;

namespace Rentilo.Services;

/// <summary>Formatting helpers shared by the views.</summary>
public static class Display
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    public static string Money(decimal amount) =>
        amount == Math.Round(amount) ? $"{amount.ToString("N0", Swedish)} kr" : $"{amount.ToString("N2", Swedish)} kr";

    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string DateRange(DateOnly start, DateOnly end) =>
        start == end ? Date(start) : $"{start.ToString(start.Year == end.Year ? "d MMM" : "d MMM yyyy", CultureInfo.InvariantCulture)} – {Date(end)}";

    public static string Name(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();

    public static string Icon(this ItemCategory category) => category switch
    {
        ItemCategory.Tools => "🔧",
        ItemCategory.Electronics => "🎮",
        ItemCategory.Outdoor => "⛺",
        ItemCategory.Sports => "🚴",
        ItemCategory.Party => "🎉",
        ItemCategory.Vehicles => "🛵",
        ItemCategory.Home => "🪴",
        ItemCategory.Photography => "📷",
        ItemCategory.Music => "🎸",
        _ => "📦",
    };

    public static string BadgeClass(this BookingStatus status) => status switch
    {
        BookingStatus.Pending => "status-pending",
        BookingStatus.Accepted => "status-accepted",
        BookingStatus.Completed => "status-completed",
        _ => "status-closed",
    };

    public static string Days(int days) => days == 1 ? "1 day" : $"{days} days";
}
