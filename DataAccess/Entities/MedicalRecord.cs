
using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class MedicalRecord : BaseEntity
    {
        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public Guid PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public Guid DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        public string Diagnosis { get; set; } = string.Empty;     // Teşhis (Örn: Miyopi, Astigmatizm)
        public string ClinicalNotes { get; set; } = string.Empty; // Muayene ve gözlem notu
        public string? Prescription { get; set; }                 // Reçete detayı
        public DateTime? FollowUpDate { get; set; }               // Kontrol/Sonuç randevu tarihi

        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }

    public class Attachment : BaseEntity
    {
        public Guid? MedicalRecordId { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty; // Guid.ext formatı
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string Category { get; set; } = "XRay"; // Röntgen, Göz Dibi Foto, Tahlil Raporu
    }
}