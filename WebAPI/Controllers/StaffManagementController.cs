using DataAccess.Context;
using DataAccess.Entities;
using Business.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffManagementController : ControllerBase
    {
        private readonly ClinicDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public StaffManagementController(
            ClinicDbContext context,
            UserManager<AppUser> userManager,
            IEmailService emailService,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _env = env;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { message = "Lütfen e-posta adresinizi giriniz." });

            var user = await _userManager.FindByEmailAsync(req.Email.Trim())
                       ?? await _userManager.FindByNameAsync(req.Email.Trim());

            if (user == null)
                return BadRequest(new { message = "Bu e-posta adresine ait personel kaydı bulunamadı!" });

            var code = new Random().Next(100000, 999999).ToString();
            var resetToken = new PasswordResetToken
            {
                Email = user.Email!,
                Code = code,
                ExpirationDate = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==================================================");
            Console.WriteLine($"[SIFRE SIFIRLAMA KODU] Kullanıcı: {user.Email} -> KOD: {code}");
            Console.WriteLine("==================================================");
            Console.ResetColor();

            try
            {
                await _emailService.SendEmailAsync(
                    user.Email!,
                    "E-Klinik Şifre Sıfırlama Kodu",
                    $"<h3>Güvenlik Kodunuz: <span style='color:blue;'>{code}</span></h3><p>Bu kod 15 dakika geçerlidir.</p>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UYARI] Mail sunucusuna bağlanılamadı: {ex.Message}");
            }

            return Ok(new { message = $"6 haneli sıfırlama kodu gönderildi! (Konsol Kodu: {code})" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { message = "E-posta, kod ve yeni şifre alanları zorunludur." });

            var email = req.Email.Trim().ToLower();
            var code = req.Code.Trim();

            var token = await _context.PasswordResetTokens
                .Where(t => t.Email.ToLower() == email && t.Code == code && !t.IsUsed)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (token == null)
                return BadRequest(new { message = "Girdiğiniz 6 haneli kod hatalı." });

            if (token.ExpirationDate < DateTime.UtcNow.AddMinutes(-5))
                return BadRequest(new { message = "Girdiğiniz kodun süresi dolmuş. Lütfen tekrar kod isteyiniz." });

            var user = await _userManager.FindByEmailAsync(email)
                       ?? await _userManager.FindByNameAsync(email);

            if (user == null)
                return NotFound(new { message = "Kullanıcı bulunamadı." });

            await _userManager.RemovePasswordAsync(user);
            var addResult = await _userManager.AddPasswordAsync(user, req.NewPassword);

            if (!addResult.Succeeded)
            {
                var errorMsg = string.Join(" ", addResult.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Şifre kurallara uymuyor: {errorMsg}" });
            }

            token.IsUsed = true;
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Şifreniz başarıyla sıfırlandı. Yeni şifrenizle giriş yapabilirsiniz." });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

            var result = await _userManager.ChangePasswordAsync(user, req.OldPassword, req.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { message = "Mevcut eski şifreniz hatalı." });

            return Ok(new { message = "Şifreniz başarıyla güncellendi." });
        }

        [HttpGet("staff-list")]
        public async Task<IActionResult> GetStaffList()
        {
            var doctors = await _context.Doctors
                .Include(d => d.AppUser)
                .Select(d => new
                {
                    d.Id,
                    UserId = d.AppUserId,
                    FullName = $"{d.Title} {d.AppUser.FirstName} {d.AppUser.LastName}",
                    d.AppUser.Email,
                    d.Specialty,
                    d.ConsultationFee,
                    Role = "Doktor"
                }).ToListAsync();

            return Ok(doctors);
        }

        [HttpPost("update-doctor-fee")]
        public async Task<IActionResult> UpdateDoctorFee([FromBody] UpdateFeeRequest req)
        {
            var doc = await _context.Doctors.FindAsync(req.DoctorId);
            if (doc == null) return NotFound();

            doc.ConsultationFee = req.Fee;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Muayene ücreti güncellendi." });
        }

        [HttpDelete("delete-doctor/{id}")]
        public async Task<IActionResult> DeleteDoctor(Guid id)
        {
            var doc = await _context.Doctors
                .Include(d => d.AppUser)
                .Include(d => d.Schedules)
                .Include(d => d.Appointments)
                    .ThenInclude(a => a.MedicalRecord)
                        .ThenInclude(m => m!.Attachments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null)
                return NotFound(new { message = "Hekim bulunamadı." });

            try
            {
                if (doc.Schedules != null && doc.Schedules.Any())
                {
                    _context.DoctorSchedules.RemoveRange(doc.Schedules);
                }

                var timeOffs = await _context.DoctorTimeOffs.Where(t => t.DoctorId == id).ToListAsync();
                if (timeOffs.Any())
                {
                    _context.DoctorTimeOffs.RemoveRange(timeOffs);
                }

                foreach (var app in doc.Appointments)
                {
                    if (app.MedicalRecord != null)
                    {
                        if (app.MedicalRecord.Attachments != null && app.MedicalRecord.Attachments.Any())
                        {
                            _context.Attachments.RemoveRange(app.MedicalRecord.Attachments);
                        }
                        _context.MedicalRecords.Remove(app.MedicalRecord);
                    }
                }

                if (doc.Appointments.Any())
                {
                    _context.Appointments.RemoveRange(doc.Appointments);
                }

                _context.Doctors.Remove(doc);
                await _context.SaveChangesAsync();

                if (doc.AppUser != null)
                {
                    await _userManager.DeleteAsync(doc.AppUser);
                }

                return Ok(new { message = "Hekim ve ilişkili tüm kayıtları başarıyla sistemden silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Hekim silinirken bir hata oluştu: " + ex.Message });
            }
        }

        // TAHSİLAT & ÇOKLU İŞLEM EKLEME (GÜNCELLENDİ)
        [HttpPost("charge-appointment")]
        public async Task<IActionResult> ChargeAppointment([FromBody] ChargeRequest req)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.Id == req.AppointmentId);

            if (appointment == null) return NotFound(new { message = "Randevu bulunamadı." });

            decimal totalAmount = 0;
            var summaryList = new List<string>();

            // Yeni eklenen işlemleri AppointmentTreatments tablosuna ekle
            foreach (var item in req.Treatments)
            {
                _context.AppointmentTreatments.Add(new AppointmentTreatment
                {
                    AppointmentId = req.AppointmentId,
                    ProcedureName = item.ProcedureName,
                    Price = item.Price,
                    CreatedAt = DateTime.UtcNow
                });
                totalAmount += item.Price;
                summaryList.Add($"{item.ProcedureName}: {item.Price} ₺");
            }

            string treatmentSummary = string.Join(" | ", summaryList);

            if (appointment.Payment == null)
            {
                appointment.Payment = new Payment
                {
                    AppointmentId = req.AppointmentId,
                    Amount = totalAmount,
                    PaymentMethod = req.PaymentMethod,
                    Status = DataAccess.Enums.PaymentStatus.Paid,
                    TransactionReference = treatmentSummary,
                    PaidAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(appointment.Payment);
            }
            else
            {
                appointment.Payment.Amount = totalAmount;
                appointment.Payment.PaymentMethod = req.PaymentMethod;
                appointment.Payment.Status = DataAccess.Enums.PaymentStatus.Paid;
                appointment.Payment.TransactionReference = string.IsNullOrEmpty(appointment.Payment.TransactionReference)
                    ? treatmentSummary
                    : $"{appointment.Payment.TransactionReference} | {treatmentSummary}";
                appointment.Payment.PaidAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Tahsilat ve işlemler başarıyla kaydedildi!",
                totalAmount,
                treatmentSummary = appointment.Payment.TransactionReference,
                status = "Paid"
            });
        }

        [HttpGet("daily-revenue")]
        public async Task<IActionResult> GetDailyRevenue([FromQuery] DateTime date)
        {
            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var payments = await _context.Payments
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Patient)
                .Where(p => p.PaidAt.HasValue && p.PaidAt.Value.Date == targetDate.Date && p.Status == DataAccess.Enums.PaymentStatus.Paid)
                .Select(p => new
                {
                    p.Id,
                    PatientName = $"{p.Appointment.Patient.FirstName} {p.Appointment.Patient.LastName}",
                    p.Amount,
                    p.PaymentMethod,
                    TreatmentDetails = p.TransactionReference ?? "Genel Muayene",
                    Time = p.PaidAt.Value.ToString("HH:mm")
                })
                .ToListAsync();

            var totalCash = payments.Where(p => p.PaymentMethod == "Nakit").Sum(p => p.Amount);
            var totalCard = payments.Where(p => p.PaymentMethod == "Kredi Kartı").Sum(p => p.Amount);

            return Ok(new
            {
                Date = targetDate.ToString("yyyy-MM-dd"),
                TotalRevenue = payments.Sum(p => p.Amount),
                TotalCash = totalCash,
                TotalCard = totalCard,
                Transactions = payments
            });
        }

        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners()
        {
            var banners = await _context.ClinicBanners.OrderBy(b => b.DisplayOrder).ToListAsync();
            return Ok(banners);
        }

        [HttpPost("banners/upload")]
        public async Task<IActionResult> UploadBanner([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Dosya seçilmedi.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var base64 = Convert.ToBase64String(memoryStream.ToArray());
            var dataUrl = $"data:{file.ContentType};base64,{base64}";

            var banner = new ClinicBanner
            {
                ImageUrl = dataUrl,
                Title = "Yeni Klinik Hizmeti",
                Subtitle = "Modern tıp teknolojisiyle hizmetinizdeyiz.",
                CreatedAt = DateTime.UtcNow
            };

            _context.ClinicBanners.Add(banner);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Banner başarıyla kaydedildi.", banner });
        }

        [HttpGet("banner-file/{fileName}")]
        public IActionResult GetBannerFile(string fileName)
        {
            var path1 = Path.Combine(_env.ContentRootPath, "Storage", "Banners", fileName);
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var path2 = Path.Combine(webRoot, "uploads", "banners", fileName);

            string? target = null;
            if (System.IO.File.Exists(path1)) target = path1;
            else if (System.IO.File.Exists(path2)) target = path2;

            if (string.IsNullOrEmpty(target) || !System.IO.File.Exists(target))
                return NotFound("Banner bulunamadı: " + fileName);

            var ext = Path.GetExtension(target).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return PhysicalFile(target, contentType);
        }

        [HttpDelete("banners/{id}")]
        public async Task<IActionResult> DeleteBanner(Guid id)
        {
            var banner = await _context.ClinicBanners.FindAsync(id);
            if (banner == null) return NotFound();

            _context.ClinicBanners.Remove(banner);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Resim vitrinden kaldırıldı." });
        }

        [HttpPost("create-staff")]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "E-posta ve şifre zorunludur." });

            var existingUser = await _userManager.FindByEmailAsync(req.Email.Trim());
            if (existingUser != null)
                return BadRequest(new { message = "Bu e-posta adresiyle kayıtlı bir personel zaten var." });

            var user = new AppUser
            {
                UserName = req.Email.Trim(),
                Email = req.Email.Trim(),
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                var err = string.Join(" ", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Kullanıcı oluşturulamadı: {err}" });
            }

            var role = req.Role == "Doktor" ? AppRole.Doctor : "Secretary";
            await _userManager.AddToRoleAsync(user, role);

            if (req.Role == "Doktor")
            {
                var doctor = new Doctor
                {
                    AppUserId = user.Id,
                    Title = req.Title ?? "Uzm. Dr.",
                    Specialty = req.Specialty ?? "Göz Hastalıkları",
                    ConsultationFee = req.ConsultationFee > 0 ? req.ConsultationFee : 1500,
                    Biography = req.Biography ?? "Klinik uzman hekimi."
                };
                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
                foreach (var day in days)
                {
                    _context.DoctorSchedules.Add(new DoctorSchedule
                    {
                        DoctorId = doctor.Id,
                        DayOfWeek = day,
                        StartTime = new TimeSpan(9, 0, 0),
                        EndTime = new TimeSpan(17, 0, 0),
                        SlotDurationMinutes = 30,
                        IsDayOff = false
                    });
                }
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = $"{req.Role} hesabı ({req.FirstName} {req.LastName}) başarıyla oluşturuldu!" });
        }

        public record CreateStaffRequest(
            string FirstName,
            string LastName,
            string Email,
            string Password,
            string Role,
            string? Title,
            string? Specialty,
            decimal ConsultationFee,
            string? Biography
        );
    }

    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Email, string Code, string NewPassword);
    public record ChangePasswordRequest(string Email, string OldPassword, string NewPassword);
    public record UpdateFeeRequest(Guid DoctorId, decimal Fee);
    public record ProcedureItem(string ProcedureName, decimal Price);
    public record ChargeRequest(Guid AppointmentId, string PaymentMethod, List<ProcedureItem> Treatments);
}