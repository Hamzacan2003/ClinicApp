using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class AppointmentTreatment : BaseEntity
    {
        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;
        public string ProcedureName { get; set; } = null!; // Örn: "20'lik Diş Çekimi"
        public decimal Price { get; set; }
    }
}