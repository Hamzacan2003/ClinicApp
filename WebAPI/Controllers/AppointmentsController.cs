using Business.DTOs;
using Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppointmentService _appointmentService;

        public AppointmentsController(AppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        [HttpGet("slots")]
        public async Task<IActionResult> GetSlots([FromQuery] Guid doctorId, [FromQuery] DateTime date)
        {
            var slots = await _appointmentService.GetAvailableSlotsAsync(doctorId, date);
            return Ok(slots);
        }

        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] AppointmentBookDto dto)
        {
            var result = await _appointmentService.CreateAppointmentAsync(dto);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message, appointmentId = result.AppointmentId });
        }

        [HttpGet("search-tc/{nationalId}")]
        public async Task<IActionResult> SearchByTc(string nationalId)
        {
            var result = await _appointmentService.SearchByNationalIdAsync(nationalId);
            if (result == null) return NotFound(new { message = "Bu T.C. numarasına ait kayıt bulunamadı." });
            return Ok(result);
        }

        [HttpPost("confirm-payment/{appointmentId}")]
        public async Task<IActionResult> ConfirmPayment(Guid appointmentId, [FromQuery] string method = "Nakit")
        {
            var success = await _appointmentService.ConfirmPaymentAsync(appointmentId, method);
            if (!success) return BadRequest(new { message = "Ödeme onaylanamadı veya randevu bulunamadı." });
            return Ok(new { message = "Ödeme başarıyla kaydedildi ve randevu onaylandı." });
        }
    }
}