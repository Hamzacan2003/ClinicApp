
using DataAccess.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Context
{
    public class ClinicDbContext : IdentityDbContext<AppUser, AppRole, Guid>
    {
        public ClinicDbContext(DbContextOptions<ClinicDbContext> options) : base(options) { }

        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
        public DbSet<Attachment> Attachments => Set<Attachment>();
        public DbSet<ClinicSetting> ClinicSettings => Set<ClinicSetting>();
        public DbSet<DoctorTimeOff> DoctorTimeOffs => Set<DoctorTimeOff>();
        public DbSet<ClinicBanner> ClinicBanners => Set<ClinicBanner>();
        public DbSet<AppointmentTreatment> AppointmentTreatments => Set<AppointmentTreatment>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. Concurrency & Çakışma Önleyici: Aynı doktora aynı gün ve saatte tek randevu (Cancelled hariç)
            builder.Entity<Appointment>()
                .HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.SlotTime })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"Status\" != 3"); // PostgreSQL için (Status != Cancelled)

            // 2. T.C. Kimlik Numarası Hızlı Arama & Tekillik
            builder.Entity<Patient>()
                .HasIndex(p => p.NationalId)
                .IsUnique();

            // 3. Para Hassasiyetleri
            builder.Entity<Doctor>().Property(d => d.ConsultationFee).HasPrecision(18, 2);
            builder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);

            // 4. Birebir İlişkiler
            builder.Entity<Appointment>()
                .HasOne(a => a.Payment)
                .WithOne(p => p.Appointment)
                .HasForeignKey<Payment>(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Appointment>()
                .HasOne(a => a.MedicalRecord)
                .WithOne(m => m.Appointment)
                .HasForeignKey<MedicalRecord>(m => m.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. Global Soft Delete Filtresi
            builder.Entity<Appointment>().HasQueryFilter(a => !a.IsDeleted);
            builder.Entity<Patient>().HasQueryFilter(p => !p.IsDeleted);
            builder.Entity<Doctor>().HasQueryFilter(d => !d.IsDeleted);
            builder.Entity<MedicalRecord>().HasQueryFilter(m => !m.IsDeleted);
        }
    }
}