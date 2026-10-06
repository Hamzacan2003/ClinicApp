
namespace Business.Interfaces
{
    public interface INviValidationService
    {
        Task<bool> ValidateTcAsync(long tcNo, string ad, string soyad, int dogumYili);
    }
}
