using Business.DTOs;
using Business.Hubs;
using Business.Interfaces;
using DataAccess.Context;
using DataAccess.Entities;
using DataAccess.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Services
{
    public class AppointmentService
    {
        private readonly ClinicDbContext _context;
        private readonly INviValidationService _nviService;
        private readonly IEmailService _emailService;
        private readonly IHubContext<ClinicHub> _hubContext;

        public AppointmentService(
            ClinicDbContext context,
            INviValidationService nviService,
            IEmailService emailService,
            IHubContext<ClinicHub> hubContext)
        {
            _context = context;
            _nviService = nviService;
            _emailService = emailService;
            _hubContext = hubContext;
        }

        public async Task<List<TimeSlotDto>> GetAvailableSlotsAsync(Guid doctorId, DateTime date)
        {
            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var dayOfWeek = date.DayOfWeek;

            // 1. Doktorun o gün çalışma takvimi var mı?
            var schedule = await _context.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && !s.IsDayOff);

            if (schedule == null)
                return new List<TimeSlotDto>();

            // 2. Doktorun özel izin/saat kapatması var mı?
            var timeOffs = await _context.DoctorTimeOffs
                .Where(t => t.DoctorId == doctorId && t.Date.Date == targetDate.Date)
                .ToListAsync();

            if (timeOffs.Any(t => t.StartTime == null))
                return new List<TimeSlotDto>(); // Tüm gün kapalı

            // 3. Alınmış randevular
            var bookedAppointments = await _context.Appointments
                .Where(a => a.DoctorId == doctorId &&
                            a.AppointmentDate.Date == targetDate.Date &&
                            a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.SlotTime)
                .ToListAsync();

            // 4. Türkiye saatine göre geçmiş saat kontrolü (Render UTC olduğu için +3 saat eklenir)
            var turkeyNow = DateTime.UtcNow.AddHours(3);
            bool isToday = (date.Date == turkeyNow.Date);
            var currentTimeOfDay = turkeyNow.TimeOfDay;

            // 5. Slotları üret
            var slots = new List<TimeSlotDto>();
            var current = schedule.StartTime;
            var duration = TimeSpan.FromMinutes(schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : 30);

            while (current + duration <= schedule.EndTime)
            {
                bool isBooked = bookedAppointments.Contains(current);

                bool isBlocked = timeOffs.Any(t => t.StartTime.HasValue && t.EndTime.HasValue &&
                                                   current >= t.StartTime.Value && current < t.EndTime.Value);

                // Bugün seçiliyse ve slot saati şu anki Türkiye saatinden geride kalmışsa kapat
                bool isPastTime = isToday && (current <= currentTimeOfDay);

                bool available = !isBooked && !isBlocked && !isPastTime;

                slots.Add(new TimeSlotDto(
                    current,
                    current.ToString(@"hh\:mm"),
                    available
                ));

                current = current.Add(duration);
            }

            return slots;
        }

        // Randevu Oluşturma
        public async Task<(bool Success, string Message, Guid? AppointmentId)> CreateAppointmentAsync(AppointmentBookDto dto)
        {
            // 1. NVI ile Kimlik Kontrolü
            if (!long.TryParse(dto.NationalId, out long tcNo))
                return (false, "T.C. Kimlik numarası sayısal olmalıdır.", null);

            bool isNviValid = await _nviService.ValidateTcAsync(tcNo, dto.FirstName, dto.LastName, dto.BirthYear);
            if (!isNviValid)
            {
                return (false, "Girilen T.C. Kimlik No, Ad, Soyad veya Doğum Yılı Nüfus Müdürlüğü kayıtlarıyla uyuşmuyor!", null);
            }

            // Tarihleri UTC'ye dönüştürüyoruz
            var utcAppointmentDate = DateTime.SpecifyKind(dto.AppointmentDate.Date, DateTimeKind.Utc);

            // 2. Çakışma Kontrolü
            bool isSlotTaken = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == dto.DoctorId &&
                a.AppointmentDate.Date == utcAppointmentDate.Date &&
                a.SlotTime == dto.SlotTime &&
                a.Status != AppointmentStatus.Cancelled);

            if (isSlotTaken)
                return (false, "Seçtiğiniz randevu saati az önce doldu. Lütfen başka bir saat seçiniz.", null);

            // 3. Hasta Bul veya Yeni Kayıt Aç
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.NationalId == dto.NationalId);
            if (patient == null)
            {
                patient = new Patient
                {
                    NationalId = dto.NationalId,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    Email = dto.Email.Trim(),
                    PhoneNumber = dto.PhoneNumber.Trim(),
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(dto.BirthYear, 1, 1), DateTimeKind.Utc),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }

            var doctor = await _context.Doctors.Include(d => d.AppUser).FirstOrDefaultAsync(d => d.Id == dto.DoctorId);
            if (doctor == null) return (false, "Doktor bulunamadı.", null);

            // 4. Randevu ve Ödeme Kaydı
            var appointment = new Appointment
            {
                DoctorId = dto.DoctorId,
                PatientId = patient.Id,
                AppointmentDate = utcAppointmentDate,
                SlotTime = dto.SlotTime,
                PatientComplaint = dto.PatientComplaint,
                Status = AppointmentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Payment = new Payment
                {
                    Amount = doctor.ConsultationFee,
                    Status = PaymentStatus.Unpaid,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // 5. SignalR ile Doktora Canlı Bildirim
            await _hubContext.Clients.Group($"doctor_{dto.DoctorId}").SendAsync("ReceiveNewAppointment", new
            {
                appointmentId = appointment.Id,
                patientName = $"{patient.FirstName} {patient.LastName}",
                time = appointment.SlotTime.ToString(@"hh\:mm"),
                date = appointment.AppointmentDate.ToString("yyyy-MM-dd")
            });

            // 6. Bilgilendirme E-postası
            string mailHtml = $@"<h3>Sayın {dto.FirstName} {dto.LastName},</h3>
<p>Randevunuz başarıyla oluşturuldu.</p>
<ul>
    <li><strong>Tarih:</strong> {dto.AppointmentDate:dd.MM.yyyy}</li>
    <li><strong>Saat:</strong> {dto.SlotTime:hh\:mm}</li>
    <li><strong>Randevu Takip Kodu:</strong> {appointment.Id}</li>
</ul>
<p>Muayene ücretini klinikte ödeyebilirsiniz.</p>";

            await _emailService.SendEmailAsync(dto.Email, "Randevu Bildirimi - E-Klinik", mailHtml);

            return (true, "Randevunuz başarıyla oluşturuldu.", appointment.Id);
        }

        // T.C. ile Hasta ve Randevu Arama
        public async Task<PatientSearchDto?> SearchByNationalIdAsync(string nationalId)
        {
            var patient = await _context.Patients
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.AppUser)
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.Payment)
                .FirstOrDefaultAsync(p => p.NationalId == nationalId);

            if (patient == null) return null;

            return new PatientSearchDto(
                patient.Id,
                patient.NationalId,
                $"{patient.FirstName} {patient.LastName}",
                patient.PhoneNumber,
                patient.Email ?? "",
                patient.Appointments.OrderByDescending(a => a.AppointmentDate).Select(a => new PatientAppointmentDto(
                    a.Id,
                    a.AppointmentDate,
                    a.SlotTime,
                    $"Dr. {a.Doctor.AppUser.FirstName} {a.Doctor.AppUser.LastName}",
                    a.Status.ToString(),
                    a.Payment?.Amount ?? 0,
                    a.Payment?.Status.ToString() ?? "Unpaid"
                )).ToList()
            );
        }

        // Ödeme Onaylama
        public async Task<bool> ConfirmPaymentAsync(Guid appointmentId, string method)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null || appointment.Payment == null) return false;

            appointment.Payment.Status = PaymentStatus.Paid;
            appointment.Payment.PaidAt = DateTime.UtcNow;
            appointment.Payment.PaymentMethod = method;
            appointment.Status = AppointmentStatus.Confirmed;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}