using Business.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private static readonly HttpClient HttpClient = new HttpClient();

        public SmtpEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            // Render Environment'tan veya appsettings'ten API Key'i al
            var apiKey = _config["Resend:ApiKey"] ?? _config["Resend__ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine($"[MAIL UYARI] Resend API Key bulunamadığı için e-posta gönderimi es geçildi.");
                return;
            }

            try
            {
                Console.WriteLine($"[MAIL GÖNDERİLİYOR] Resend HTTPS API üzerinden {toEmail} adresine iletiliyor...");

                var payload = new
                {
                    from = "E-Klinik <onboarding@resend.dev>",
                    to = new[] { toEmail },
                    subject = subject,
                    html = htmlBody
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await HttpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[BAŞARILI] Randevu onay e-postası iletildi -> {toEmail}");
                }
                else
                {
                    Console.WriteLine($"[MAIL HTTP HATA] Kod: {response.StatusCode}, Detay: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIL HATA DETAYI] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}