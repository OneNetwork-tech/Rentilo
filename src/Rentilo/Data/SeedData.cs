using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rentilo.Models;

namespace Rentilo.Data;

/// <summary>
/// Creates demo users and listings so a fresh development database isn't empty.
/// Enabled with <c>Database:SeedDemoData=true</c>; never runs if any item already exists.
/// All demo accounts use the password <see cref="DemoPassword"/>.
/// </summary>
public static class SeedData
{
    public const string DemoPassword = "Rentilo123!";

    public static async Task SeedDemoDataAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (await db.Items.AnyAsync()) return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedData));

        var anna = await EnsureUserAsync(users, "anna@rentilo.dev", "Anna Lindqvist", "Stockholm");
        var erik = await EnsureUserAsync(users, "erik@rentilo.dev", "Erik Johansson", "Göteborg");
        var sara = await EnsureUserAsync(users, "sara@rentilo.dev", "Sara Ahmed", "Malmö");

        db.Items.AddRange(
            Item(anna, "Bosch cordless drill set", "18V drill with two batteries, charger and a 40-piece bit set. Perfect for hanging shelves or assembling furniture.", ItemCategory.Tools, 89, "Stockholm, Södermalm"),
            Item(anna, "4-person camping tent", "Lightweight dome tent, easy to pitch in ten minutes. Includes footprint and pegs. Waterproof to 3000 mm.", ItemCategory.Outdoor, 149, "Stockholm, Södermalm"),
            Item(erik, "Sony A7 III camera body", "Full-frame mirrorless camera with two batteries and a 64 GB card. Lenses available on request.", ItemCategory.Photography, 450, "Göteborg, Haga"),
            Item(erik, "Electric cargo bike", "Long-tail e-bike that fits two kids or a week of groceries. Range about 60 km. Helmet and lock included.", ItemCategory.Vehicles, 390, "Göteborg, Majorna"),
            Item(erik, "Party speaker with lights", "Portable Bluetooth speaker, 12 h battery, two wireless microphones for karaoke.", ItemCategory.Party, 199, "Göteborg, Haga"),
            Item(sara, "Stand-up paddle board", "Inflatable SUP, 10'6\", with pump, paddle and leash. Great for beginners.", ItemCategory.Sports, 249, "Malmö, Västra Hamnen"),
            Item(sara, "Pressure washer", "Kärcher K5, ideal for decks, patios and cars. Comes with a surface cleaner attachment.", ItemCategory.Home, 159, "Malmö, Limhamn"),
            Item(sara, "Acoustic guitar", "Yamaha FG800 with soft case, tuner and spare strings.", ItemCategory.Music, 79, "Malmö, Möllevången"));

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo data. Demo login: anna@rentilo.dev / {Password}", DemoPassword);
    }

    private static Item Item(ApplicationUser owner, string title, string description, ItemCategory category, decimal price, string location) =>
        new() { OwnerId = owner.Id, Title = title, Description = description, Category = category, PricePerDay = price, Location = location };

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> users, string email, string name, string city)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name, City = city };
        var result = await users.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not create demo user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }
}
