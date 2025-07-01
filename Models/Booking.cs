using System;
using Rentilo.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rentilo.Models
{
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Rental Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "Rental End Date")]
        public DateTime EndDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Total Price")]
        public decimal TotalPrice { get; set; }

        [Required]
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        [Display(Name = "Date Booked")]
        public DateTime DateBooked { get; set; } = DateTime.UtcNow;

        [Required]
        public int ItemId { get; set; }

        [ForeignKey("ItemId")]
        public virtual Item Item { get; set; }

        [Required]
        public string RenterId { get; set; }

        [ForeignKey("RenterId")]
        public virtual ApplicationUser Renter { get; set; }
    }

    public enum BookingStatus
    {
        Pending,
        Accepted,
        Declined,
        Completed,
        Cancelled
    }
}
