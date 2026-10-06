
using DataAccess.Enums;
using DataAccess.Entities.Common;


namespace DataAccess.Entities
{
    public class Appointment : BaseEntity
    {
        public Guid DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        public Guid PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public DateTime AppointmentDate { get; set; } // YYYY-MM-DD
        public TimeSpan SlotTime { get; set; }        // 14:30:00

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
        public string? PatientComplaint { get; set; }

        public Payment? Payment { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }
    }

    public class Payment : BaseEntity
    {
        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Unpaid;
        public string PaymentMethod { get; set; } = "Nakit"; // Nakit, Kredi Kartı, Havale
        public DateTime? PaidAt { get; set; }
        public string? TransactionReference { get; set; }
    }
}