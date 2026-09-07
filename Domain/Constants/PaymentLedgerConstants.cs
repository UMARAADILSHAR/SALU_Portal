using SaluExamPortal.Domain.Enums;

namespace SaluExamPortal.Domain.Constants;

public static class PaymentEventTypes
{
    public const string Issued = "Issued";
    public const string StudentReceiptUploaded = "StudentReceiptUploaded";
    public const string BankPosted = "BankPosted";
    public const string Reconciled = "Reconciled";
    public const string Verified = "Verified";
    public const string Expired = "Expired";
    public const string Superseded = "Superseded";
    public const string Reversed = "Reversed";
    public const string UniversityCopyReceived = "UniversityCopyReceived";
}

public static class PaymentChannels
{
    public const string HBL = "HBL";
    public const string NBP = "NBP";
    public const string Manual = "Manual";
    public const string BankScroll = "BankScroll";
    public const string StudentReceipt = "StudentReceipt";
    public const string System = "System";
}

public static class FeeStatusRules
{
    public static bool AllowsAdmitCard(FeeStatus status) => status == FeeStatus.Verified;

    public static bool IsCollected(FeeStatus status) =>
        status is FeeStatus.Paid or FeeStatus.Verified;

    public static bool CanProposePaid(FeeStatus status) =>
        status is FeeStatus.Unpaid or FeeStatus.PendingVerification;

    public static bool CanVerify(FeeStatus status) => status == FeeStatus.Paid;

    public static bool CanUploadReceipt(FeeStatus status) =>
        status is FeeStatus.Unpaid or FeeStatus.PendingVerification;

    public static bool CanReplace(FeeStatus status) => status == FeeStatus.Expired;

    public static bool IsActiveIssue(FeeStatus status) =>
        status is FeeStatus.Unpaid
            or FeeStatus.PendingVerification
            or FeeStatus.Paid
            or FeeStatus.Verified;

    public static string DisplayLabel(FeeStatus status) => status switch
    {
        FeeStatus.Unpaid => "Unpaid — deposit at bank",
        FeeStatus.PendingVerification => "Receipt uploaded — pending bank match",
        FeeStatus.Paid => "Bank posted — awaiting officer verification",
        FeeStatus.Verified => "Verified — eligible for admit card",
        FeeStatus.Expired => "Expired — generate a new challan",
        FeeStatus.Superseded => "Superseded",
        FeeStatus.Reversed => "Reversed",
        _ => status.ToString()
    };
}
