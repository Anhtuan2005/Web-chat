using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReporterId { get; set; }

        [ForeignKey("ReporterId")]
        public virtual User Reporter { get; set; }

        [Required]
        public int ReportedUserId { get; set; }

        [ForeignKey("ReportedUserId")]
        public virtual User ReportedUser { get; set; }

        public int? PostId { get; set; } // Nullable foreign key to Post
        [ForeignKey("PostId")]
        public virtual Post Post { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; }

        public int? MessageId { get; set; }
        public DateTime CreatedAt { get; set; } // Renamed from Timestamp

        public bool IsResolved { get; set; } // New property
    }
}
