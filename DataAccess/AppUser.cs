using Microsoft.AspNetCore.Identity;

namespace DataAccess.Entities
{
   
    public class AppUser : IdentityUser<Guid>
    {
        public AppUser()
        {
            Id = Guid.NewGuid();
        }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
    }

    public class AppRole : IdentityRole<Guid>
    {
        public AppRole() : base()
        {
            Id = Guid.NewGuid();
        }

        public AppRole(string roleName) : base(roleName)
        {
            Id = Guid.NewGuid();
        }

        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Receptionist = "Receptionist";
        public const string Patient = "Patient";
    }
}