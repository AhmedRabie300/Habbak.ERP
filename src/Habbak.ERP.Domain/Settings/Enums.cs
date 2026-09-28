namespace Habbak.ERP.Domain.Settings;

public enum UserStatus
{
    PendingActivation = 1,
    Active = 2,
    Suspended = 3,
    Locked = 4
}

public enum PreferredLanguage
{
    Arabic = 1,
    English = 2
}

public enum AuditActionType
{
    Create = 1,
    Update = 2,
    Delete = 3,
    View = 4,
    Login = 5,
    Logout = 6,
    Approve = 7,
    Reject = 8,
    Export = 9,
    Print = 10,

    /// <summary>A special screen button (ButtonPermission) was pressed.</summary>
    ButtonPress = 11
}
