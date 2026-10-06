// DataAccess/Context/DbInitializer.cs
using DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Context
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(ClinicDbContext context, UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
        {
            // 1. Rolleri oluştur
            string[] roles = { AppRole.Admin, AppRole.Doctor, AppRole.Receptionist, AppRole.Patient };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new AppRole(role));
                }
            }

            // 2. Doktor kullanıcısı var mı kontrol et, yoksa ekle
            var doctorEmail = "drahmet@eklinik.com";
            var doctorUser = await userManager.FindByEmailAsync(doctorEmail);

            if (doctorUser == null)
            {
                doctorUser = new AppUser
                {
                    UserName = doctorEmail,
                    Email = doctorEmail,
                    FirstName = "Ahmet",
                    LastName = "Yılmaz",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(doctorUser, "Doktor123!");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(doctorUser, AppRole.Doctor);

                    // Doktor profilini ekle
                    var doctor = new Doctor
                    {
                        AppUserId = doctorUser.Id,
                        Specialty = "Göz Hastalıkları Uzmanı",
                        Title = "Op. Dr.",
                        Biography = "Katarakt, lazer ve retina cerrahisi uzmanı.",
                        ConsultationFee = 1500m
                    };

                    context.Doctors.Add(doctor);
                    await context.SaveChangesAsync();

                    // Pazartesi - Cuma arası 09:00 - 17:00, 30'ar dakikalık çalışma takvimi oluştur
                    var workDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
                    foreach (var day in workDays)
                    {
                        context.DoctorSchedules.Add(new DoctorSchedule
                        {
                            DoctorId = doctor.Id,
                            DayOfWeek = day,
                            StartTime = new TimeSpan(9, 0, 0),
                            EndTime = new TimeSpan(17, 0, 0),
                            SlotDurationMinutes = 30,
                            IsDayOff = false
                        });
                    }

                    await context.SaveChangesAsync();
                }
            }
        }
    }
}