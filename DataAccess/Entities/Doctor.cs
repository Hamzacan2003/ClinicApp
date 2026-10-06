
using DataAccess.Entities;
using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class Doctor : BaseEntity
    {
        public Guid AppUserId { get; set; }
        public AppUser AppUser { get; set; } = null!;

        public string Specialty { get; set; } = string.Empty; // Göz Hastalıkları vb.
        public string? Title { get; set; }                     // Uzm. Dr., Doç. Dr., Prof. Dr.
        public string? Biography { get; set; }
        public string? ProfileImageUrl { get; set; }
        public decimal ConsultationFee { get; set; }

        // İlişkiler
        public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }

    public class DoctorSchedule : BaseEntity
    {
        public Guid DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }       // 09:00:00
        public TimeSpan EndTime { get; set; }         // 17:00:00
        public int SlotDurationMinutes { get; set; } = 30;
        public bool IsDayOff { get; set; } = false;
    }
}