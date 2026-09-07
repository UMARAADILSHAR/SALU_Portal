using System.Collections.Generic;

namespace SaluExamPortal.Domain.Constants;

public static class Permissions
{
    public static class Enrollment
    {
        public const string View = "Permissions.Enrollment.View";
        public const string Create = "Permissions.Enrollment.Create";
        public const string Edit = "Permissions.Enrollment.Edit";
        public const string Delete = "Permissions.Enrollment.Delete";
        public const string Verify = "Permissions.Enrollment.Verify";
        public const string Approve = "Permissions.Enrollment.Approve";
        public const string Print = "Permissions.Enrollment.Print";
    }

    public static class Examination
    {
        public const string View = "Permissions.Examination.View";
        public const string Manage = "Permissions.Examination.Manage";
        public const string GenerateAdmitCards = "Permissions.Examination.GenerateAdmitCards";
        public const string PublishResults = "Permissions.Examination.PublishResults";
        public const string Print = "Permissions.Examination.Print";
    }

    public static class Fees
    {
        public const string View = "Permissions.Fees.View";
        public const string Reconcile = "Permissions.Fees.Reconcile";
        public const string Configure = "Permissions.Fees.Configure";
    }

    public static class Colleges
    {
        public const string View = "Permissions.Colleges.View";
        public const string Manage = "Permissions.Colleges.Manage";
    }

    public static class Users
    {
        public const string View = "Permissions.Users.View";
        public const string Manage = "Permissions.Users.Manage";
    }

    public static class Settings
    {
        public const string View = "Permissions.Settings.View";
        public const string Manage = "Permissions.Settings.Manage";
    }

    public static class Audit
    {
        public const string View = "Permissions.Audit.View";
    }

    public static IReadOnlyList<string> GetAllPermissions()
    {
        return
        [
            Enrollment.View, Enrollment.Create, Enrollment.Edit, Enrollment.Delete, Enrollment.Verify, Enrollment.Approve, Enrollment.Print,
            Examination.View, Examination.Manage, Examination.GenerateAdmitCards, Examination.PublishResults, Examination.Print,
            Fees.View, Fees.Reconcile, Fees.Configure,
            Colleges.View, Colleges.Manage,
            Users.View, Users.Manage,
            Settings.View, Settings.Manage,
            Audit.View
        ];
    }
}
