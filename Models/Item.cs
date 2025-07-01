using System;
using Rentilo.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rentilo.Models
{
    /// <summary>
    /// Represents a single item that can be rented out by a user.
    /// This class contains all the descriptive details for an item
    /// and defines its relationship to its owner and any bookings it's part of.
    /// </summary>
    public class Item
    {
        /// <summary>
        /// The unique identifier for the item.
        /// This is the Primary Key for the Items table in the database.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The title or name of the item.
        /// This is a required field.
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        /// <summary>
        /// A detailed description of the item.
        /// This is a required field.
        /// </summary>
        [Required]
        [StringLength(2000)]
        public string Description { get; set; }

        /// <summary>
        /// The rental price for the item per day.
        /// Stored as a decimal for financial accuracy.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Price Per Day")]
        public decimal PricePerDay { get; set; }

        /// <summary>
        /// The URL for the item's primary image.
        /// This property is optional.
        /// </summary>
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// The general location of the item (e.g., "City, State").
        /// This property is optional.
        /// </summary>
        [StringLength(300)]
        public string? Location { get; set; }

        /// <summary>
        /// The date and time when the item was first listed.
        /// Defaults to the current UTC time when a new item is created.
        /// </summary>
        [Display(Name = "Date Listed")]
        public DateTime DateListed { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// A flag indicating if the item is currently available for rent.
        /// Defaults to true for new items.
        /// </summary>
        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;

        // --- Foreign Key Relationship to the Owner (ApplicationUser) ---

        /// <summary>
        /// The foreign key that links this item to its owner in the AspNetUsers table.
        /// </summary>
        [Required]
        public string OwnerId { get; set; }

        /// <summary>
        /// The navigation property that allows access to the full ApplicationUser object
        /// for the owner. EF Core uses this to build the relationship.
        /// </summary>
        [ForeignKey("OwnerId")]
        public virtual ApplicationUser Owner { get; set; }

        // --- Navigation Property for Bookings ---

        /// <summary>
        /// A collection of all the bookings associated with this item.
        /// This represents a one-to-many relationship: one Item can have many Bookings.
        /// </summary>
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
