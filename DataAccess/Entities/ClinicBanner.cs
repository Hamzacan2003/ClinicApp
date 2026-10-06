using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class ClinicBanner : BaseEntity
    {
        public string ImageUrl { get; set; } = null!;
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public int DisplayOrder { get; set; } = 0;
    }
}