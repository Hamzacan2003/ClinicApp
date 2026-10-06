using DataAccess.Context;
using DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorsController : ControllerBase
    {
        private readonly ClinicDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public DoctorsController(ClinicDbContext context, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // 1. Doktorları Listele
        [HttpGet]
        public async Task<IActionResult> GetDoctors()
        {
            var doctors = await _context.Doctors
                .Include(d => d.AppUser)
                .Where(d => d.AppUser != null) 
                .Select(d => new
                {
                    d.Id,
                    FullName = $"{d.Title} {d.AppUser.FirstName} {d.AppUser.LastName}",
                    d.Specialty,
                    d.Biography,
                    d.ConsultationFee
                })
                .ToListAsync();

            return Ok(doctors);
        }

        // 2. Personel Girişi (Doktor / Sekreter Login)
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email)
                       ?? await _userManager.FindByNameAsync(model.Email);

            if (user == null)
                return Unauthorized(new { message = "Kullanıcı bulunamadı." });

            // Hesap kilitli mi?
            if (await _userManager.IsLockedOutAsync(user))
            {
                return BadRequest(new
                {
                    isLocked = true,
                    message = "Hesabınız 3 kez hatalı giriş nedeniyle bloke edilmiştir. Lütfen 'Şifremi Unuttum' üzerinden sıfırlayınız."
                });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                return BadRequest(new
                {
                    isLocked = true,
                    message = "3 kez hatalı şifre girdiniz! Hesabınız güvenliğiniz için kilitlendi. Şifre sıfırlama sayfasına yönlendiriliyorsunuz."
                });
            }

            if (!result.Succeeded)
            {
                int failedAttempts = await _userManager.GetAccessFailedCountAsync(user);
                int remaining = 3 - failedAttempts;
                return Unauthorized(new
                {
                    message = $"Hatalı şifre! Kalan deneme hakkınız: {Math.Max(remaining, 0)}"
                });
            }

            // Giriş başarılıysa hatalı sayacı sıfırla
            await _userManager.ResetAccessFailedCountAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.AppUserId == user.Id);

            return Ok(new
            {
                message = "Giriş başarılı",
                userId = user.Id,
                email = user.Email,
                fullName = $"{user.FirstName} {user.LastName}",
                role = roles.FirstOrDefault() ?? "Staff",
                doctorId = doctor?.Id
            });
        }

        // 3. Doktorun Kendi Çalışma Takvimini Getir
        [HttpGet("{doctorId}/schedule")]
        public async Task<IActionResult> GetSchedule(Guid doctorId)
        {
            var schedules = await _context.DoctorSchedules
                .Where(s => s.DoctorId == doctorId)
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new
                {
                    s.Id,
                    s.DayOfWeek,
                    DayName = s.DayOfWeek.ToString(),
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm"),
                    s.SlotDurationMinutes,
                    s.IsDayOff
                })
                .ToListAsync();

            return Ok(schedules);
        }

        // 4. Doktorun Çalışma Saatlerini / Günlerini Güncellemesi
        [HttpPost("{doctorId}/schedule/update")]
        public async Task<IActionResult> UpdateSchedule(Guid doctorId, [FromBody] List<ScheduleUpdateDto> schedules)
        {
            var existing = await _context.DoctorSchedules.Where(s => s.DoctorId == doctorId).ToListAsync();
            _context.DoctorSchedules.RemoveRange(existing);

            foreach (var item in schedules)
            {
                _context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctorId,
                    DayOfWeek = item.DayOfWeek,
                    StartTime = TimeSpan.Parse(item.StartTime),
                    EndTime = TimeSpan.Parse(item.EndTime),
                    SlotDurationMinutes = item.SlotDurationMinutes > 0 ? item.SlotDurationMinutes : 30,
                    IsDayOff = item.IsDayOff
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Çalışma takvimi başarıyla güncellendi!" });
        }
        // Belirli bir gün veya saat aralığını kapatma / izin verme
        [HttpPost("{doctorId}/time-off")]
        public async Task<IActionResult> AddTimeOff(Guid doctorId, [FromBody] AddTimeOffDto dto)
        {
            var timeOff = new DoctorTimeOff
            {
                DoctorId = doctorId,
                Date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc),
                StartTime = string.IsNullOrEmpty(dto.StartTime) ? null : TimeSpan.Parse(dto.StartTime),
                EndTime = string.IsNullOrEmpty(dto.EndTime) ? null : TimeSpan.Parse(dto.EndTime),
                Reason = dto.Reason
            };

            _context.DoctorTimeOffs.Add(timeOff);
            await _context.SaveChangesAsync();

            return Ok(new { message = "İzin / kapalı saat başarıyla tanımlandı!" });
        }

        // Doktorun kapalı günlerini listeleme
        [HttpGet("{doctorId}/time-offs")]
        public async Task<IActionResult> GetTimeOffs(Guid doctorId)
        {
            var list = await _context.DoctorTimeOffs
                .Where(t => t.DoctorId == doctorId)
                .OrderByDescending(t => t.Date)
                .Select(t => new
                {
                    t.Id,
                    Date = t.Date.ToString("yyyy-MM-dd"),
                    StartTime = t.StartTime.HasValue ? t.StartTime.Value.ToString(@"hh\:mm") : "Tüm Gün",
                    EndTime = t.EndTime.HasValue ? t.EndTime.Value.ToString(@"hh\:mm") : "Tüm Gün",
                    t.Reason
                })
                .ToListAsync();

            return Ok(list);
        }

        // İzni/Kapatmayı silme (Günü tekrar randevuya açma)
        [HttpDelete("time-off/{id}")]
        public async Task<IActionResult> DeleteTimeOff(Guid id)
        {
            var item = await _context.DoctorTimeOffs.FindAsync(id);
            if (item == null) return NotFound();

            _context.DoctorTimeOffs.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { message = "İzin kaldırıldı, saatler tekrar açıldı." });
        }

        public record AddTimeOffDto(DateTime Date, string? StartTime, string? EndTime, string? Reason);
    }

    public record LoginDto(string Email, string Password);
    public record ScheduleUpdateDto(DayOfWeek DayOfWeek, string StartTime, string EndTime, int SlotDurationMinutes, bool IsDayOff);
}