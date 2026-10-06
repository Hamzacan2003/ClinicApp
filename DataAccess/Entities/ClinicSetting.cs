
using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class ClinicSetting : BaseEntity
    {
        public string ClinicName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? GoogleMapsEmbedUrl { get; set; }
        public string? WorkingHoursText { get; set; }
        public string? BannerSlidesJson { get; set; } 
        public string? AnnouncementsJson { get; set; }
    }
}