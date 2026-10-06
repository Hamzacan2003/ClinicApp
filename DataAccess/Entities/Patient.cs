
using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class Patient : BaseEntity
    {
        public string NationalId { get; set; } = string.Empty; // T.C. Kimlik Numarası (Unique Index)
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? BloodType { get; set; }
        public string? Notes { get; set; }

        // İlişkiler
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    }
}