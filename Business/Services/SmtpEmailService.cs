using Business.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Net.Mail;
using System.Threading.Tasks;

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
            var portStr = _config["EmailSettings:Port"] ?? "465";
            int port = int.TryParse(portStr, out int p) ? p : 465;

            // Render ortamındaki hem 'SenderEmail' hem de 'Email' anahtarlarını kontrol et
            var senderEmail = _config["EmailSettings:SenderEmail"] ?? _config["EmailSettings:Email"];
            var password = _config["EmailSettings:SenderPassword"] ?? _config["EmailSettings:Password"];

            if (string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(password) ||
                password.Contains("xxxx") ||
                senderEmail.Contains("seninmailin"))
            {
                Console.WriteLine($"[BİLGİ] SMTP ayarları eksik veya geçersiz olduğu için e-posta gönderimi es geçildi: {toEmail}");
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("E-Klinik Randevu Sistemi", senderEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                client.Timeout = 10000; // 10 saniye

                // Port 465 için doğrudan SSL, diğerleri için StartTls
                var secureSocketOption = (port == 465)
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                await client.ConnectAsync(host, port, secureSocketOption);
                await client.AuthenticateAsync(senderEmail, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                Console.WriteLine($"[BAŞARILI] Randevu onay e-postası iletildi -> {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UYARI] E-Posta gönderilirken SMTP hatası oluştu: {ex.Message}");
            }
        }
    }
}