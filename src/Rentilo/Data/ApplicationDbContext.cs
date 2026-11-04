using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rentilo.Models;

namespace Rentilo.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Item>(e =>
        {
            e.HasOne(i => i.Owner)
                .WithMany(u => u.Items)
                .HasForeignKey(i => i.OwnerId)
                // Keep listings (and their booking history) if someone tries to delete the owner;
                // account deletion must handle this explicitly.
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => new { i.IsAvailable, i.Category });
            e.HasIndex(i => i.DateListed);
        });

        builder.Entity<Booking>(e =>
        {
            e.HasOne(b => b.Item)
                .WithMany(i => i.Bookings)
                .HasForeignKey(b => b.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Renter)
                .WithMany(u => u.BookingsAsRenter)
                .HasForeignKey(b => b.RenterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(b => new { b.ItemId, b.Status, b.StartDate });
            e.ToTable(t => t.HasCheckConstraint("CK_Booking_Dates", "\"EndDate\" >= \"StartDate\""));
        });

        builder.Entity<Review>(e =>
        {
            e.HasOne(r => r.Booking)
                .WithOne(b => b.Review)
                .HasForeignKey<Review>(r => r.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.BookingId).IsUnique();
            e.HasOne(r => r.Item)
                .WithMany(i => i.Reviews)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Author)
                .WithMany()
                .HasForeignKey(r => r.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        });
    }
}
