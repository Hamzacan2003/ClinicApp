using DataAccess.Entities.Common;

namespace DataAccess.Entities
{
    public class PasswordResetToken : BaseEntity
    {
        public string Email { get; set; } = null!;
        public string Code { get; set; } = null!; // 6 haneli kod
        public DateTime ExpirationDate { get; set; }
        public bool IsUsed { get; set; } = false;
    }
}