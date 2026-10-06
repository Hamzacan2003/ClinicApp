namespace Business.DTOs
{
    public record AppointmentBookDto(
        Guid DoctorId,
        DateTime AppointmentDate,
        TimeSpan SlotTime,
        string NationalId,
        string FirstName,
        string LastName,
        int BirthYear,
        string Email,
        string PhoneNumber,
        string? PatientComplaint
    );

    public record TimeSlotDto(
        TimeSpan Time,
        string FormattedTime,
        bool IsAvailable
    );

    public record AddMedicalRecordDto(
        Guid AppointmentId,
        string Diagnosis,
        string? ClinicalNotes,
        string? Prescription,
        DateTime? FollowUpDate
    );
    public record ContactDoctorDto(
        Guid DoctorId,
        string SenderName,
        string SenderEmail,
        string SenderPhone,
        string Subject,
        string Message
    );

    public record PatientSearchDto(
        Guid PatientId,
        string NationalId,
        string FullName,
        string PhoneNumber,
        string Email,
        List<PatientAppointmentDto> Appointments
    );

    public record PatientAppointmentDto(
        Guid AppointmentId,
        DateTime AppointmentDate,
        TimeSpan SlotTime,
        string DoctorName,
        string Status,
        decimal Amount,
        string PaymentStatus
    );
}