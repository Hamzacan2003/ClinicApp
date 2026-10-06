using Business.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace Business.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public SmtpEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _config["EmailSettings:Host"] ?? "smtp.gmail.com";
            var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
            var senderEmail = _config["EmailSettings:Email"];
            var password = _config["EmailSettings:Password"];

            // 1. Eğer şifre veya mail girilmemişse / varsayılan test değeri duruyorsa randevuyu patlatma, es geç
            if (string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(password) ||
                password.Contains("xxxx") ||
                senderEmail.Contains("seninmailin"))
            {
                Console.WriteLine($"[BİLGİ] SMTP ayarları yapılmadığı için e-posta gönderimi es geçildi: {toEmail}");
                return;
            }

            // 2. SMTP Bağlantı Hatası Randevunun Kaydedilmesini Engellemesin (Try-Catch Koruması)
            try
            {
                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true,
                    Timeout = 8000 // 8 saniye içinde cevap alamazsa beklemesin
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "E-Klinik Randevu Sistemi"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"[BAŞARILI] Randevu onay e-postası iletildi -> {toEmail}");
            }
            catch (Exception ex)
            {
                // Mail gönderilemese bile hasta randevusunu almış olur, sistem çökmez
                Console.WriteLine($"[UYARI] E-Posta gönderilirken SMTP hatası oluştu: {ex.Message}");
            }
        }
    }
}