using Business.DTOs;
using Business.Hubs;
using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IHubContext<ClinicHub> _hubContext;

        public ContactController(IEmailService emailService, IHubContext<ClinicHub> hubContext)
        {
            _emailService = emailService;
            _hubContext = hubContext;
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] ContactDoctorDto dto)
        {
            // 1. Doktora SignalR ile anlık bildirim fırlat
            await _hubContext.Clients.Group($"doctor_{dto.DoctorId}").SendAsync("ReceivePatientMessage", new
            {
                sender = dto.SenderName,
                subject = dto.Subject,
                message = dto.Message,
                phone = dto.SenderPhone,
                email = dto.SenderEmail
            });

            // 2. Arka planda kliniğe mail gönder
            string mailBody = $@"<h3>Yeni Hasta İletişim Mesajı</h3>
<p><strong>Gönderen:</strong> {dto.SenderName} ({dto.SenderEmail} / {dto.SenderPhone})</p>
<p><strong>Konu:</strong> {dto.Subject}</p>
<p><strong>Mesaj:</strong></p>
<p>{dto.Message}</p>";

            await _emailService.SendEmailAsync("klinikbilgi@ornek.com", $"İletişim Formu: {dto.Subject}", mailBody);

            return Ok(new { message = "Mesajınız doktora iletildi." });
        }
    }
}