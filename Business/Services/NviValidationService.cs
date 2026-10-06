using Business.Interfaces;
using DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Business.Services
{
    public class NviValidationService : INviValidationService
    {
        private readonly ClinicDbContext _context;
        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");

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

            // 2. Doğum Yılı Denetimi
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

            // 3. İsim ve Soyisim Format Temizliği
            string cleanAd = string.Join(" ", ad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);
            string cleanSoyad = string.Join(" ", soyad.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToUpper(TrCulture);

            if (cleanAd.Length < 2 || cleanSoyad.Length < 2)
                return false;

            if (!Regex.IsMatch(cleanAd, @"^[A-ZÇĞİÖŞÜ\s]+$") || !Regex.IsMatch(cleanSoyad, @"^[A-ZÇĞİÖŞÜ\s]+$"))
                return false;

            // 4. Veritabanındaki Hasta Kaydı ile Çapraz Kontrol (İsim Uyuşmazlığını Engelleme)
            string tcStr = tcNo.ToString();
            var existingPatient = await _context.Patients.FirstOrDefaultAsync(p => p.NationalId == tcStr);

            if (existingPatient != null)
            {
                string dbAd = existingPatient.FirstName.Trim().ToUpper(TrCulture);
                string dbSoyad = existingPatient.LastName.Trim().ToUpper(TrCulture);

                // 1. İsim ve Soyisim Kontrolü (Zaten çalışan kısım)
                if (dbAd != cleanAd || dbSoyad != cleanSoyad)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[KİMLİK UYUŞMAZLIĞI] {tcNo} TCKN sistemde '{dbAd} {dbSoyad}' adına kayıtlıdır. Girilen: '{cleanAd} {cleanSoyad}' reddedildi.");
                    Console.ResetColor();
                    return false;
                }

                // 2. Doğum Yılı Kontrolü (Hatasız ve Katı Hali)
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
                else
                {
                    // Veritabanında DateOfBirth önceden null kaldıysa, ilk seferde girilen yılı güvenle kaydet
                    existingPatient.DateOfBirth = DateTime.SpecifyKind(new DateTime(dogumYili, 1, 1), DateTimeKind.Utc);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[BİLGİ] {tcNo} hastasının eksik doğum yılı {dogumYili} olarak güncellendi.");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[KİMLİK DOĞRULANDI] {cleanAd} {cleanSoyad} ({tcNo}) başarıyla doğrulandı.");
            Console.ResetColor();
            return true;
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