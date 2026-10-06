using Business.DTOs;
using FluentValidation;

namespace Business.Validation
{
    public class AppointmentBookValidator : AbstractValidator<AppointmentBookDto>
    {
        public AppointmentBookValidator()
        {
            RuleFor(x => x.DoctorId).NotEmpty().WithMessage("Doktor seçilmelidir.");

            RuleFor(x => x.NationalId)
                .NotEmpty().WithMessage("T.C. Kimlik Numarası zorunludur.")
                .Length(11).WithMessage("T.C. Kimlik Numarası 11 haneli olmalıdır.")
                .Matches(@"^[1-9]{1}[0-9]{9}[0,2,4,6,8]{1}$").WithMessage("Geçersiz T.C. Kimlik Numarası formatı.");

            RuleFor(x => x.FirstName).NotEmpty().WithMessage("Ad zorunludur.");
            RuleFor(x => x.LastName).NotEmpty().WithMessage("Soyad zorunludur.");
            RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Geçerli bir e-posta giriniz.");
            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Telefon numarası zorunludur.");
            RuleFor(x => x.BirthYear).InclusiveBetween(1900, DateTime.UtcNow.Year).WithMessage("Geçerli bir doğum yılı giriniz.");
            RuleFor(x => x.AppointmentDate).GreaterThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("Geçmiş bir tarihe randevu alınamaz.");
        }
    }
}