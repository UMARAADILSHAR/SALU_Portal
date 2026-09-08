using Microsoft.AspNetCore.Identity;
using SaluExamPortal.Domain.Entities;
using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Infrastructure.Persistence
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? FatherName { get; set; }
        public string? Cnic { get; set; }
        public bool IsVerified { get; set; } = true;
        public AdmissionType AdmissionType { get; set; } = AdmissionType.AffiliatedCollege;
        public bool MustChangePassword { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public Guid? CollegeId { get; set; }
        public College? College { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

        // RFC 6238 TOTP Secret (base32 encoded, encrypted with Data Protection API)
        public string? TotpSecretEncrypted { get; set; }
        public bool TotpEnabled { get; set; } = false;
        public DateTime? TotpEnabledAt { get; set; }
    }

}
