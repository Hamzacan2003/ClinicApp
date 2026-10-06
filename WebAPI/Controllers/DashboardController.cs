
using DataAccess.Context;
using DataAccess.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ClinicDbContext _context;

        public DashboardController(ClinicDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalDoctors = await _context.Doctors.CountAsync();
            var totalPatients = await _context.Patients.CountAsync();
            var totalAppointments = await _context.Appointments.CountAsync();

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var newPatientsLast7Days = await _context.Patients
                .CountAsync(p => p.CreatedAt >= sevenDaysAgo);

            var totalPaid = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var totalUnpaid = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Unpaid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            return Ok(new
            {
                TotalDoctors = totalDoctors,
                TotalPatients = totalPatients,
                TotalAppointments = totalAppointments,
                NewPatientsLast7Days = newPatientsLast7Days,
                TotalPaidAmount = totalPaid,
                TotalPendingAmount = totalUnpaid
            });
        }
    }
}