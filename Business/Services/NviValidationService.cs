using Business.Interfaces;
using DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Business.Services
{
    public class NviValidationService : INviValidationService
    {
        private readonly ClinicDbContext _context;
        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public NviValidationService(ClinicDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ValidateTcAsync(long tcNo, string ad, string soyad, int dogumYili)
        {
            // 1. Resmi 11 Haneli TCKN Algoritması
            if (!ValidateTcAlgorithm(tcNo))
            {
                Console.WriteLine($"[KİMLİK HATA] {tcNo} algoritma kuralına uymuyor.");
                return false;
            }

            // 2. Temel Kontroller
            if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad) || dogumYili < 1900 || dogumYili > DateTime.UtcNow.Year)
                return false;

            // 3. İsim Temizliği (Türkçe Karakter Uyumlu Büyük Harf)
            string cleanAd = string.Join(" ", ad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);
            string cleanSoyad = string.Join(" ", soyad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);

            // 4. CANLI DEVLET NVI (KPSPublic) SOAP SORGUSU
            bool nviSonuc = await CallNviSoapAsync(tcNo, cleanAd, cleanSoyad, dogumYili);

            if (!nviSonuc)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[NVI REDDİ] T.C: {tcNo}, Ad: '{cleanAd}', Soyad: '{cleanSoyad}', Yıl: {dogumYili} Nüfus Müdürlüğü kayıtlarıyla EŞLEŞMEDİ!");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[NVI ONAYLANDI] {cleanAd} {cleanSoyad} ({tcNo}) devlet kayıtlarında doğrulandı.");
            Console.ResetColor();

            return true;
        }

        private static async Task<bool> CallNviSoapAsync(long tcNo, string ad, string soyad, int dogumYili)
        {
            try
            {
                string soapXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>
    <TCKimlikNoDogrula xmlns=""http://tckimlik.nvi.gov.tr/WS"">
      <TCKimlikNo>{tcNo}</TCKimlikNo>
      <Ad>{ad}</Ad>
      <Soyad>{soyad}</Soyad>
      <DogumYili>{dogumYili}</DogumYili>
    </TCKimlikNoDogrula>
  </soap12:Body>
</soap12:Envelope>";

                var request = new HttpRequestMessage(HttpMethod.Post, "https://tckimlik.nvi.gov.tr/Service/KPSPublic.asmx");
                request.Content = new StringContent(soapXml, Encoding.UTF8, "application/soap+xml");

                var response = await HttpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[NVI HTTP HATA] Kod: {response.StatusCode}, Yanıt: {responseContent}");
                    return false;
                }

                var xDoc = XDocument.Parse(responseContent);
                XNamespace ns = "http://tckimlik.nvi.gov.tr/WS";
                var resultEl = xDoc.Descendants(ns + "TCKimlikNoDogrulaResult").FirstOrDefault();

                if (resultEl != null && bool.TryParse(resultEl.Value, out bool isValid))
                {
                    return isValid;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NVI İSTİSNA HATA] {ex.Message}");
                return false;
            }
        }

        private static bool ValidateTcAlgorithm(long tc)
        {
            string tcStr = tc.ToString();
            if (tcStr.Length != 11 || tcStr.StartsWith("0"))
                return false;

            int[] digits = tcStr.Select(c => c - '0').ToArray();

            int oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
            int evenSum = digits[1] + digits[3] + digits[5] + digits[7];

            int digit10 = ((oddSum * 7) - evenSum) % 10;
            if (digit10 < 0) digit10 += 10;

            if (digits[9] != digit10)
                return false;

            int totalSum = digits.Take(10).Sum();
            if (totalSum % 10 != digits[10])
                return false;

            return true;
        }
    }
}