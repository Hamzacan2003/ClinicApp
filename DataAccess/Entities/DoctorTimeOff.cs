// DataAccess/Entities/DoctorTimeOff.cs
using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class DoctorTimeOff : BaseEntity
    {
        public Guid DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        public DateTime Date { get; set; } // Hangi gün? (Örn: 2026-10-08)
        public TimeSpan? StartTime { get; set; } // Null ise TÜM GÜN İZİNLİ
        public TimeSpan? EndTime { get; set; }
        public string? Reason { get; set; } // Örn: "Kongre", "Ameliyat", "Özel İş"
    }
}