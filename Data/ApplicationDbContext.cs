// In a new file: /Data/ApplicationDbContext.cs
using Rentilo.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// Inherit from IdentityDbContext to get all the Identity tables for free
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // Add our custom models as DbSet properties
    public DbSet<Item> Items { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}