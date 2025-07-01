using Rentilo.Models;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Rentilo.Models
{
    /// <summary>
    /// Represents a custom user for the Rentilo application.
    /// This class inherits from the base IdentityUser to leverage ASP.NET Core's
    /// built-in authentication and security features, while also allowing us
    /// to add custom properties specific to our application's needs.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// The full name of the user.
        /// This is a required field.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        /// <summary>
        /// The URL for the user's profile picture.
        /// This property is optional.
        /// </summary>
        public string? ProfilePictureUrl { get; set; }

        // --- Navigation Properties ---
        // These properties define the relationships between this user and other
        // data models in our application. They are used by Entity Framework Core
        // to understand how the database tables are linked.

        /// <summary>
        /// A collection of all the items this user owns and has listed for rent.
        /// This represents a one-to-many relationship: one User can have many Items.
        /// </summary>
        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

        /// <summary>
        /// A collection of all the bookings this user has made as a renter.
        /// This represents a one-to-many relationship: one User can have many Bookings.
        /// </summary>
        public virtual ICollection<Booking> BookingsAsRenter { get; set; } = new List<Booking>();
    }
}