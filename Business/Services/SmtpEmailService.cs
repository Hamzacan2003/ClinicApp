using Business.Interfaces;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Threading.Tasks;
// DİKKAT: 'using System.Net.Mail;' kaldırıldı!

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

            var senderEmail = _config["EmailSettings:SenderEmail"]
                           ?? _config["EmailSettings:Email"]
                           ?? _config["EmailSettings__SenderEmail"];

            var password = _config["EmailSettings:SenderPassword"]
                        ?? _config["EmailSettings:Password"]
                        ?? _config["EmailSettings__SenderPassword"];

            Console.WriteLine($"[MAIL TEST] Host: {host}, Port: {port}, Gönderen: {senderEmail}, Alıcı: {toEmail}");

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine($"[MAIL UYARI] SenderEmail veya Password boş geldiği için gönderim iptal edildi!");
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

                // Çakışmayı önlemek için tam adresiyle (MailKit.Net.Smtp.SmtpClient) oluşturuyoruz
                using var client = new MailKit.Net.Smtp.SmtpClient();
                client.Timeout = 10000;

                var secureOption = (port == 465)
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                Console.WriteLine($"[MAIL BAĞLANTI] {host}:{port} ({secureOption}) bağlanılıyor...");
                await client.ConnectAsync(host, port, secureOption);

                Console.WriteLine($"[MAIL DOĞRULAMA] Kimlik doğrulanıyor...");
                await client.AuthenticateAsync(senderEmail, password);

                Console.WriteLine($"[MAIL GÖNDERİLİYOR] Mesaj iletiliyor...");
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                Console.WriteLine($"[BAŞARILI] Randevu onay e-postası başarıyla iletildi -> {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIL HATA DETAYI] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}