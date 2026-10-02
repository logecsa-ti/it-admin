namespace TIAdmin.Domain.Enums;

public enum AssetStatus
{
    Available = 0,
    Assigned = 1,
    Maintenance = 2,
    Repair = 3,
    Retired = 4,
    Lost = 5,
    Disposed = 6
}

public enum AssetMovementType
{
    Assignment = 0,
    Return = 1,
    LocationChange = 2,
    DepartmentChange = 3,
    StatusChange = 4,
    Transfer = 5,
    Repair = 6,
    Disposal = 7
}

public enum VendorStatus
{
    Active = 0,
    Inactive = 1,
    Blocked = 2
}

public enum LicenseType
{
    Perpetual = 0,
    Subscription = 1,
    Trial = 2,
    OpenSource = 3,
    Oem = 4
}

public enum ContractType
{
    Purchase = 0,
    Service = 1,
    Maintenance = 2,
    Subscription = 3,
    Lease = 4,
    Support = 5
}

public enum ContractStatus
{
    Draft = 0,
    Active = 1,
    Expiring = 2,
    Expired = 3,
    Terminated = 4,
    Renewed = 5
}
