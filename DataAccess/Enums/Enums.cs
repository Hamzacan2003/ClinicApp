namespace DataAccess.Enums
{
    public enum AppointmentStatus
    {
        Pending = 0,    // Randevu alındı
        Confirmed = 1,  // Onaylandı
        Completed = 2,  // Muayene bitti
        Cancelled = 3,  // İptal
        NoShow = 4      // Randevuya gelmedi
    }

    public enum PaymentStatus
    {
        Unpaid = 0,
        Paid = 1,
        PartiallyPaid = 2,
        Refunded = 3
    }
}