using Business.DTOs;
using DataAccess.Context;
using DataAccess.Entities;
using DataAccess.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorPanelController : ControllerBase
    {
        private readonly ClinicDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DoctorPanelController(ClinicDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 1. GÜNLÜK RANDEVULAR (CANLI VE 401 HATASIZ LİSTELEME)
        [HttpGet("daily-appointments")]
        public async Task<IActionResult> GetDailyAppointments([FromQuery] Guid? doctorId, [FromQuery] DateTime date)
        {
            Guid effectiveDoctorId;

            if (doctorId.HasValue && doctorId.Value != Guid.Empty)
            {
                effectiveDoctorId = doctorId.Value;
            }
            else
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var appUserId))
                {
                    var currentDoctor = await _context.Doctors.FirstOrDefaultAsync(d => d.AppUserId == appUserId);
                    if (currentDoctor != null)
                    {
                        effectiveDoctorId = currentDoctor.Id;
                    }
                    else
                    {
                        var firstDoc = await _context.Doctors.FirstOrDefaultAsync();
                        if (firstDoc == null) return Ok(new List<object>());
                        effectiveDoctorId = firstDoc.Id;
                    }
                }
                else
                {
                    var firstDoc = await _context.Doctors.FirstOrDefaultAsync();
                    if (firstDoc == null) return Ok(new List<object>());
                    effectiveDoctorId = firstDoc.Id;
                }
            }

            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var appointments = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Payment)
                .Include(a => a.MedicalRecord)
                    .ThenInclude(m => m.Attachments)
                .Where(a => a.DoctorId == effectiveDoctorId &&
                            a.AppointmentDate.Date == targetDate.Date &&
                            a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.SlotTime)
                .ToListAsync();

            var result = appointments.Select(a =>
            {
                var attachmentsList = a.MedicalRecord != null
                    ? a.MedicalRecord.Attachments.Select(att => new
                    {
                        att.Id,
                        OriginalFileName = att.OriginalFileName,
                        Category = att.Category,
                        DownloadUrl = $"/api/DoctorPanel/attachment/{att.StoredFileName}"
                    }).ToList()
                    : new();

                return new
                {
                    a.Id,
                    PatientId = a.Patient.Id,
                    PatientName = $"{a.Patient.FirstName} {a.Patient.LastName}",
                    NationalId = a.Patient.NationalId,
                    PhoneNumber = a.Patient.PhoneNumber,
                    Time = a.SlotTime.ToString(@"hh\:mm"),
                    Status = a.Status.ToString(),
                    PaymentStatus = a.Payment != null ? a.Payment.Status.ToString() : "Unpaid",
                    PaymentAmount = a.Payment != null ? a.Payment.Amount : 1500,
                    PaymentMethod = a.Payment?.PaymentMethod ?? "Belirtilmedi",
                    HasMedicalRecord = a.MedicalRecord != null,
                    Diagnosis = a.MedicalRecord?.Diagnosis ?? "",
                    ClinicalNotes = a.MedicalRecord?.ClinicalNotes ?? "",
                    Prescription = a.MedicalRecord?.Prescription ?? "",
                    Attachments = attachmentsList
                };
            }).ToList();

            return Ok(result);
        }

        // 2. MUAYENE KAYDI & DOSYA EKLEME / GÜNCELLEME
        [HttpPost("add-medical-record")]
        [RequestSizeLimit(30_000_000)] // 30 MB
        public async Task<IActionResult> AddMedicalRecord([FromForm] AddMedicalRecordDto dto, [FromForm] List<IFormFile>? files)
        {
            var appointment = await _context.Appointments
                .Include(a => a.MedicalRecord)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return NotFound("Randevu bulunamadı.");

            var uploadsFolder = Path.Combine(_env.ContentRootPath, "Storage", "MedicalUploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            MedicalRecord record;
            if (appointment.MedicalRecord == null)
            {
                record = new MedicalRecord
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    Diagnosis = dto.Diagnosis,
                    ClinicalNotes = dto.ClinicalNotes ?? "",
                    Prescription = dto.Prescription ?? "",
                    FollowUpDate = dto.FollowUpDate.HasValue ? DateTime.SpecifyKind(dto.FollowUpDate.Value, DateTimeKind.Utc) : null,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MedicalRecords.Add(record);
                appointment.MedicalRecord = record;
            }
            else
            {
                record = appointment.MedicalRecord;
                record.Diagnosis = dto.Diagnosis;
                record.ClinicalNotes = dto.ClinicalNotes ?? "";
                record.Prescription = dto.Prescription ?? "";
                if (dto.FollowUpDate.HasValue)
                    record.FollowUpDate = DateTime.SpecifyKind(dto.FollowUpDate.Value, DateTimeKind.Utc);
                record.UpdatedAt = DateTime.UtcNow;
            }

            if (files != null && files.Count > 0)
            {
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(ext))
                        return BadRequest($"İzin verilmeyen dosya formatı: {ext}");

                    var safeFileName = $"{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(uploadsFolder, safeFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    record.Attachments.Add(new Attachment
                    {
                        OriginalFileName = file.FileName,
                        StoredFileName = safeFileName,
                        FilePath = filePath,
                        ContentType = file.ContentType,
                        FileSizeBytes = file.Length,
                        Category = ext == ".pdf" ? "Report" : "XRay",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            appointment.Status = AppointmentStatus.Completed;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Muayene kaydı ve tahliller başarıyla sisteme işlendi." });
        }

        // 3. DOSYA İNDİRME / AÇMA
        [AllowAnonymous]
        [HttpGet("attachment/{fileName}")]
        public IActionResult GetAttachment(string fileName)
        {
            var p1 = Path.Combine(_env.ContentRootPath, "Storage", "MedicalUploads", fileName);
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var p2 = Path.Combine(webRoot, "uploads", fileName);
            var p3 = Path.Combine(webRoot, "uploads", "medical_records", fileName);

            string? targetPath = null;
            if (System.IO.File.Exists(p1)) targetPath = p1;
            else if (System.IO.File.Exists(p2)) targetPath = p2;
            else if (System.IO.File.Exists(p3)) targetPath = p3;

            if (string.IsNullOrEmpty(targetPath) || !System.IO.File.Exists(targetPath))
            {
                return NotFound(new { message = "Dosya sunucuda bulunamadı: " + fileName });
            }

            var ext = Path.GetExtension(targetPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            return PhysicalFile(targetPath, contentType);
        }

        // 4. HASTA GEÇMİŞİ
        [HttpGet("patient-history/{nationalId}")]
        public async Task<IActionResult> GetPatientHistory(string nationalId)
        {
            var records = await _context.MedicalRecords
                .Include(m => m.Doctor).ThenInclude(d => d.AppUser)
                .Include(m => m.Attachments)
                .Include(m => m.Appointment)
                .Where(m => m.Patient.NationalId == nationalId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    Date = m.Appointment.AppointmentDate.ToString("yyyy-MM-dd"),
                    Doctor = $"Dr. {m.Doctor.AppUser.FirstName} {m.Doctor.AppUser.LastName}",
                    m.Diagnosis,
                    m.ClinicalNotes,
                    m.Prescription,
                    m.FollowUpDate,
                    Attachments = m.Attachments.Select(att => new
                    {
                        att.OriginalFileName,
                        att.Category,
                        DownloadUrl = $"/api/DoctorPanel/attachment/{att.StoredFileName}"
                    }).ToList()
                })
                .ToListAsync();

            return Ok(records);
        }
    }
}