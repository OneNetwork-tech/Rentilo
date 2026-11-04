using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Tests;

/// <summary>A clock fixed at a given local date so date rules are deterministic.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>SQLite in-memory database with the real model, plus a few seeded users and items.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDb()
    {
        _connection.Open();
        Context = NewContext();
        Context.Database.EnsureCreated();

        Owner = AddUser("owner", "Olga Owner");
        Renter = AddUser("renter", "Rick Renter");
        Other = AddUser("other", "Oscar Other");
        Item = new Item { Title = "Drill", Description = "A good drill.", PricePerDay = 100, Location = "Stockholm", OwnerId = Owner.Id };
        Context.Items.Add(Item);
        Context.SaveChanges();
    }

    public ApplicationDbContext Context { get; }
    public ApplicationUser Owner { get; }
    public ApplicationUser Renter { get; }
    public ApplicationUser Other { get; }
    public Item Item { get; }

    public ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

    private ApplicationUser AddUser(string id, string name)
    {
        var user = new ApplicationUser { Id = id, UserName = id, Email = $"{id}@test", FullName = name };
        Context.Users.Add(user);
        Context.SaveChanges();
        return user;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
