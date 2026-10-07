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
            // 1. Resmi TCKN 11 Haneli Matematiksel Algoritma Kontrolü
            if (!ValidateTcAlgorithm(tcNo))
            {
                Console.WriteLine($"[KİMLİK HATA] {tcNo} TCKN algoritmasına uymuyor.");
                return false;
            }

            // 2. Doğum Yılı Sınır Denetimi
            int currentYear = DateTime.UtcNow.AddHours(3).Year;
            if (dogumYili < 1900 || dogumYili > currentYear)
            {
                Console.WriteLine($"[KİMLİK HATA] Geçersiz doğum yılı: {dogumYili}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad))
                return false;

            // 3. İsim ve Soyisim Format Temizliği (Türkçe karakter uyumlu büyük harf)
            string cleanAd = string.Join(" ", ad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);
            string cleanSoyad = string.Join(" ", soyad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);

            if (cleanAd.Length < 2 || cleanSoyad.Length < 2)
                return false;

            if (!Regex.IsMatch(cleanAd, @"^[A-ZÇĞİÖŞÜ\s]+$") || !Regex.IsMatch(cleanSoyad, @"^[A-ZÇĞİÖŞÜ\s]+$"))
                return false;

            // 4. Veritabanındaki Kayıt ile Çapraz Kontrol (T.C. başkasına aitse ad/soyad uydurulmasını engeller)
            string tcStr = tcNo.ToString();
            var existingPatient = await _context.Patients.FirstOrDefaultAsync(p => p.NationalId == tcStr);

            if (existingPatient != null)
            {
                string dbAd = existingPatient.FirstName.Trim().ToUpper(TrCulture);
                string dbSoyad = existingPatient.LastName.Trim().ToUpper(TrCulture);

                if (dbAd != cleanAd || dbSoyad != cleanSoyad)
                {
                    Console.WriteLine($"[KİMLİK UYUŞMAZLIĞI] {tcNo} sistemde '{dbAd} {dbSoyad}' adına kayıtlıdır. Girilen: '{cleanAd} {cleanSoyad}' reddedildi.");
                    return false;
                }

                if (existingPatient.DateOfBirth.HasValue && existingPatient.DateOfBirth.Value.Year != dogumYili)
                {
                    Console.WriteLine($"[DOĞUM YILI UYUŞMAZLIĞI] {tcNo} için kayıtlı yıl: {existingPatient.DateOfBirth.Value.Year}, girilen: {dogumYili}. Reddedildi!");
                    return false;
                }
            }

            // 5. Canlı NVI SOAP Servisi Çağrısı (Render IP'sinden erişilebiliyorsa kesin doğrular)
            var nviSonuc = await TryCallNviSoapAsync(tcNo, cleanAd, cleanSoyad, dogumYili);

            if (nviSonuc.HasValue)
            {
                // NVI servisine ulaşıldı: Devletten gelen kesin cevaba göre onay veya ret ver
                if (!nviSonuc.Value)
                {
                    Console.WriteLine($"[NVI DEVLET RET] {tcNo} - {cleanAd} {cleanSoyad} ({dogumYili}) Nüfus kayıtlarıyla eşleşmedi.");
                    return false;
                }
            }
            else
            {
                // Render yurtdışı IP'si nedeniyle NVI servisi yanıt vermediyse log düş ve algoritma geçerliliğini koru
                Console.WriteLine($"[NVI ERİŞİM KISITI] NVI servisi bulut sunucusuna yanıt vermedi, yerel algoritma ve veritabanı kontrolüyle devam ediliyor.");
            }

            return true;
        }

        private static async Task<bool?> TryCallNviSoapAsync(long tcNo, string ad, string soyad, int dogumYili)
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
                if (!response.IsSuccessStatusCode)
                    return null; // IP engeli / 403 / 500

                string responseContent = await response.Content.ReadAsStringAsync();
                var xDoc = XDocument.Parse(responseContent);
                XNamespace ns = "http://tckimlik.nvi.gov.tr/WS";
                var resultEl = xDoc.Descendants(ns + "TCKimlikNoDogrulaResult").FirstOrDefault();

                if (resultEl != null && bool.TryParse(resultEl.Value, out bool isValid))
                    return isValid;

                return null;
            }
            catch
            {
                return null; // Timeout / Ağ engeli
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