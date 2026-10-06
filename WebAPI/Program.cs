using System.Text;
using Business.Hubs;
using Business.Services;
using Business.Validation;
using DataAccess.Context;
using DataAccess.Entities;
using Business.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL DB Bağlantısı
builder.Services.AddDbContext<ClinicDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity (Guid Temelli)
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // 3 Hatalı Girişte Kilitleme
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ClinicDbContext>()
.AddDefaultTokenProviders();

// 3. JWT Authentication
var jwtKey = builder.Configuration["JwtSettings:Secret"] ?? "CokGucluVeEnAz32KarakterliGizliKeyDegeri12345678!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Servisler (DI)
builder.Services.AddScoped<INviValidationService, NviValidationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<AppointmentService>();

// 5. Validasyon & Controller
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<AppointmentBookValidator>();

// 6. SignalR & CORS
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// OTOMATİK VERİTABANI OLUŞTURMA (MIGRATION), ROL VE TEST DOKTORU TOHUMLAMA (SEED)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ClinicDbContext>();
        context.Database.Migrate();

        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

        if (!await roleManager.RoleExistsAsync(AppRole.Doctor))
        {
            await roleManager.CreateAsync(new AppRole { Name = AppRole.Doctor });
        }

        if (!await roleManager.RoleExistsAsync("Secretary"))
        {
            await roleManager.CreateAsync(new AppRole { Name = "Secretary" });
        }

        // GEÇİCİ TEST DOKTORU
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var doctorEmail = "doktor@clinic.com";

        var existingDoctor = await userManager.FindByEmailAsync(doctorEmail);
        if (existingDoctor == null)
        {
            var doctorUser = new AppUser
            {
                UserName = doctorEmail,
                Email = doctorEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(doctorUser, "Doktor123!");
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(doctorUser, AppRole.Doctor);

                var doctorEntity = new Doctor
                {
                    AppUserId = doctorUser.Id,
                    Specialty = "Dahiliye"
                };
                context.Doctors.Add(doctorEntity);
                await context.SaveChangesAsync();
                logger.LogInformation(">>> TEST DOKTORU BASARIYLA OLUSTURULDU! E-posta: {Email}", doctorEmail);
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                logger.LogError(">>> DOKTOR KULLANICISI OLUSTURULAMADI! Hatalar: {Errors}", errors);
            }
        }
        else
        {
            logger.LogInformation(">>> Test doktoru zaten veritabaninda mevcut.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, ">>> Migration veya tohumlama sirasinda kritik hata!");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

var storagePath = Path.Combine(app.Environment.ContentRootPath, "Storage", "MedicalUploads");
if (!Directory.Exists(storagePath))
{
    Directory.CreateDirectory(storagePath);
}

app.UseStaticFiles();
app.UseCors("AllowReactApp");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ClinicHub>("/clinichub");

app.Run();