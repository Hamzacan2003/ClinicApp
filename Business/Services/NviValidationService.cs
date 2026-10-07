using Business.Interfaces;
using DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
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
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        public NviValidationService(ClinicDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ValidateTcAsync(long tcNo, string ad, string soyad, int dogumYili)
        {
            // 1. Resmi TCKN Algoritma Kontrolü (11 hane, ilk hane 0 olamaz, 10. ve 11. hane matematiksel kuralı)
            if (!ValidateTcAlgorithm(tcNo))
            {
                Console.WriteLine($"[KİMLİK HATA] {tcNo} TCKN algoritmasına uymuyor.");
                return false;
            }

            // 2. Doğum Yılı Denetimi
            int currentYear = DateTime.UtcNow.Year;
            if (dogumYili < 1900 || dogumYili > currentYear)
            {
                Console.WriteLine($"[KİMLİK HATA] Geçersiz doğum yılı: {dogumYili}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad))
                return false;

            // 3. İsim ve Soyisim Format Temizliği (Büyük harf ve Türkçe karakter uyumu)
            string cleanAd = string.Join(" ", ad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);
            string cleanSoyad = string.Join(" ", soyad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);

            if (cleanAd.Length < 2 || cleanSoyad.Length < 2)
                return false;

            if (!Regex.IsMatch(cleanAd, @"^[A-ZÇĞİÖŞÜ\s]+$") || !Regex.IsMatch(cleanSoyad, @"^[A-ZÇĞİÖŞÜ\s]+$"))
                return false;

            // 4. Veritabanındaki Hasta ile Çapraz Kontrol (Aynı T.C. ile farklı isim/doğum yılı girilmesini engeller)
            string tcStr = tcNo.ToString();
            var existingPatient = await _context.Patients.FirstOrDefaultAsync(p => p.NationalId == tcStr);

            if (existingPatient != null)
            {
                string dbAd = existingPatient.FirstName.Trim().ToUpper(TrCulture);
                string dbSoyad = existingPatient.LastName.Trim().ToUpper(TrCulture);

                if (dbAd != cleanAd || dbSoyad != cleanSoyad)
                {
                    Console.WriteLine($"[KİMLİK UYUŞMAZLIĞI] {tcNo} kayıtlı isim: '{dbAd} {dbSoyad}', girilen: '{cleanAd} {cleanSoyad}'");
                    return false;
                }

                if (existingPatient.DateOfBirth.HasValue && existingPatient.DateOfBirth.Value.Year != dogumYili)
                {
                    Console.WriteLine($"[DOĞUM YILI UYUŞMAZLIĞI] {tcNo} kayıtlı yıl: {existingPatient.DateOfBirth.Value.Year}, girilen: {dogumYili}");
                    return false;
                }
            }

            // 5. NVI Canlı MERNİS Sorgusu
            // Render yurtdışı IP'sinde olduğu için NVI servisi yanıt vermezse veya 403 dönerse randevuyu engelleme (Fallback)
            try
            {
                var nviResult = await CheckNviSoapAsync(tcNo, cleanAd, cleanSoyad, dogumYili);
                if (nviResult.HasValue)
                {
                    // NVI servisine başarıyla ulaşıldı ve kesin yanıt alındı (True veya False)
                    return nviResult.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NVI ERİŞİM UYARISI] Bulut sunucusundan NVI servisine ulaşılamadı: {ex.Message}");
            }

            // NVI servisi IP kısıtlaması nedeniyle cevap vermediyse algoritmik doğrulamayı geçerli say
            return true;
        }

        private static async Task<bool?> CheckNviSoapAsync(long tcNo, string ad, string soyad, int dogumYili)
        {
            try
            {
                string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <TCKimlikNoDogrula xmlns=""http://tckimlik.nvi.gov.tr/WS"">
      <TCKimlikNo>{tcNo}</TCKimlikNo>
      <Ad>{ad}</Ad>
      <Soyad>{soyad}</Soyad>
      <DogumYili>{dogumYili}</DogumYili>
    </TCKimlikNoDogrula>
  </soap:Body>
</soap:Envelope>";

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://tckimlik.nvi.gov.tr/Service/KPSPublic.asmx");
                request.Headers.Add("SOAPAction", "http://tckimlik.nvi.gov.tr/WS/TCKimlikNoDogrula");
                request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

                using var response = await HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    // Render/Cloud IP engeli veya 403/500 durumu
                    return null;
                }

                string responseXml = await response.Content.ReadAsStringAsync();
                var xDoc = XDocument.Parse(responseXml);
                XNamespace ns = "http://tckimlik.nvi.gov.tr/WS";
                var resultElement = xDoc.Descendants(ns + "TCKimlikNoDogrulaResult").FirstOrDefault();

                if (resultElement != null && bool.TryParse(resultElement.Value, out bool isValid))
                {
                    return isValid;
                }

                return null;
            }
            catch
            {
                // Bağlantı zaman aşımı veya DNS hatası
                return null;
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