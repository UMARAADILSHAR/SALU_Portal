namespace SaluExamPortal.Domain.Enums;

public enum PortalRole
{
    SuperAdmin,
    Admin,
    CollegeAdmin,
    Student
}

public enum EnrollmentStatus
{
    Draft,
    Pending,
    Approved,
    Rejected
}

public enum FeeStatus
{
    Unpaid,
    PendingVerification,
    Paid,
    Verified,
    Expired,
    Superseded,
    Reversed
}

public enum Gender
{
    Male,
    Female,
    Other
}

public enum CollegeType
{
    Boys,
    Girls,
    Coed
}

public enum AcademicYearStatus
{
    Draft,
    Active,
    Closed,
    Archived
}
