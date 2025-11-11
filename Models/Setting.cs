using System.ComponentModel.DataAnnotations;

namespace Online_chat.Models
{
    public class Setting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SiteName { get; set; }

        public bool AllowNewRegistrations { get; set; }

        [StringLength(500)]
        public string MaintenanceMessage { get; set; }

        public bool IsMaintenanceMode { get; set; }

        public int MaxUploadSizeMB { get; set; } = 10;

        [StringLength(200)]
        public string AdminEmail { get; set; }
    }
}