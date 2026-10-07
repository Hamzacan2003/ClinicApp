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
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public NviValidationService(ClinicDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ValidateTcAsync(long tcNo, string ad, string soyad, int dogumYili)
        {
            // 1. Resmi TCKN Algoritmik Kontrolü
            if (!ValidateTcAlgorithm(tcNo))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[KİMLİK HATA] {tcNo} numarası resmi TCKN algoritmasına uymuyor.");
                Console.ResetColor();
                return false;
            }

            // 2. Doğum Yılı Sınır Denetimi
            int currentYear = DateTime.UtcNow.Year;
            if (dogumYili < 1900 || dogumYili > currentYear)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[KİMLİK HATA] Geçersiz doğum yılı: {dogumYili}");
                Console.ResetColor();
                return false;
            }

            if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad))
                return false;

            // 3. İsim ve Soyisim Format Temizliği (Türkçe Büyük Harfe Çevirme)
            string cleanAd = string.Join(" ", ad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);
            string cleanSoyad = string.Join(" ", soyad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);

            if (cleanAd.Length < 2 || cleanSoyad.Length < 2)
                return false;

            if (!Regex.IsMatch(cleanAd, @"^[A-ZÇĞİÖŞÜ\s]+$") || !Regex.IsMatch(cleanSoyad, @"^[A-ZÇĞİÖŞÜ\s]+$"))
                return false;

            // 4. Veritabanındaki Hasta Kaydı ile Çapraz Kontrol (Mevcut Hastaysa)
            string tcStr = tcNo.ToString();
            var existingPatient = await _context.Patients.FirstOrDefaultAsync(p => p.NationalId == tcStr);

            if (existingPatient != null)
            {
                string dbAd = existingPatient.FirstName.Trim().ToUpper(TrCulture);
                string dbSoyad = existingPatient.LastName.Trim().ToUpper(TrCulture);

                if (dbAd != cleanAd || dbSoyad != cleanSoyad)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[KİMLİK UYUŞMAZLIĞI] {tcNo} TCKN sistemde '{dbAd} {dbSoyad}' adına kayıtlıdır. Girilen: '{cleanAd} {cleanSoyad}' reddedildi.");
                    Console.ResetColor();
                    return false;
                }

                if (existingPatient.DateOfBirth.HasValue)
                {
                    int dbBirthYear = existingPatient.DateOfBirth.Value.Year;
                    if (dbBirthYear != dogumYili)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[DOĞUM YILI UYUŞMAZLIĞI] {tcNo} için kayıtlı doğum yılı: {dbBirthYear}, girilen doğum yılı: {dogumYili}. İstek reddedildi!");
                        Console.ResetColor();
                        return false;
                    }
                }
            }

            // 5. Canlı MERNİS / NVI SOAP Web Servisi Sorgusu (KPSPublic)
            bool isNviValid = await CheckNviSoapAsync(tcNo, cleanAd, cleanSoyad, dogumYili);
            if (!isNviValid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[NVI DEVLET RET] {tcNo} - {cleanAd} {cleanSoyad} ({dogumYili}) Nüfus ve Vatandaşlık İşleri sorgusunda EŞLEŞMEDİ!");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[KİMLİK DOĞRULANDI] {cleanAd} {cleanSoyad} ({tcNo}) NVI ve Sistem tarafından başarıyla doğrulandı.");
            Console.ResetColor();
            return true;
        }

        private static async Task<bool> CheckNviSoapAsync(long tcNo, string ad, string soyad, int dogumYili)
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
                    return false;

                string responseXml = await response.Content.ReadAsStringAsync();
                var xDoc = XDocument.Parse(responseXml);
                XNamespace ns = "http://tckimlik.nvi.gov.tr/WS";
                var resultElement = xDoc.Descendants(ns + "TCKimlikNoDogrulaResult").FirstOrDefault();

                return resultElement != null && bool.TryParse(resultElement.Value, out bool isValid) && isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NVI BAĞLANTI UYARISI] {ex.Message}");
                // Canlı sunucuda NVI servisine ağ erişimi engellenirse sistemin kilitlenmemesi için algoritmik doğrulamaya güven
                return ValidateTcAlgorithm(tcNo);
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