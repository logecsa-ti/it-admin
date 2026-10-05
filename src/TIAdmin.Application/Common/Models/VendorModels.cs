namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

public record VendorDto(
    int Id,
    string? Code,
    string Name,
    string? TaxId,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Country,
    string? Website,
    VendorStatus Status,
    string? Notes,
    decimal? Rating);

public record VendorFilter(VendorStatus? Status);

/// <summary>Datos de proveedor (alta y edicion usan el mismo contrato).</summary>
public record VendorRequest(
    string? Code,
    string Name,
    string? TaxId,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Country,
    string? Website,
    VendorStatus Status,
    string? Notes,
    decimal? Rating);

/// <summary>
/// <paramref name="Status"/> es el estado efectivo: Active se refina a Expiring/Expired segun EndDate.
/// </summary>
public record ContractDto(
    int Id,
    string Number,
    string Name,
    int VendorId,
    string VendorName,
    ContractType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Value,
    string Currency,
    bool AutoRenew,
    int RenewalNoticeDays,
    int? ResponsibleUserId,
    string? ResponsibleUserName,
    ContractStatus Status,
    int DaysRemaining,
    string? Notes,
    int? RenewedFromContractId);

/// <summary><paramref name="Status"/> filtra por estado efectivo (incluye Expiring/Expired).</summary>
public record ContractFilter(int? VendorId, ContractType? Type, ContractStatus? Status, DateOnly? EndsBefore);

public interface IContractData
{
    string Name { get; }

    int VendorId { get; }

    ContractType Type { get; }

    DateOnly StartDate { get; }

    DateOnly EndDate { get; }

    decimal? Value { get; }

    string? Currency { get; }

    bool AutoRenew { get; }

    int? RenewalNoticeDays { get; }

    int? ResponsibleUserId { get; }

    string? Notes { get; }
}

/// <summary>El contrato nace en Draft, o Active si <paramref name="Activate"/> es true.</summary>
public record CreateContractRequest(
    string Number,
    string Name,
    int VendorId,
    ContractType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Value,
    string? Currency,
    bool AutoRenew,
    int? RenewalNoticeDays,
    int? ResponsibleUserId,
    string? Notes,
    bool Activate) : IContractData;

/// <summary>El numero es inmutable; el estado cambia con /activate, /terminate y /renew.</summary>
public record UpdateContractRequest(
    string Name,
    int VendorId,
    ContractType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Value,
    string? Currency,
    bool AutoRenew,
    int? RenewalNoticeDays,
    int? ResponsibleUserId,
    string? Notes) : IContractData;

public record RenewContractRequest(string Number, DateOnly StartDate, DateOnly EndDate, decimal? Value);

/// <summary>
/// Contrato que entra en una ventana de alerta. <paramref name="AlertWindowDays"/> es el umbral
/// configurado mas pequeno que cubre los dias restantes (p. ej. 30 de "90,60,30,15,7"); null si ya vencio.
/// </summary>
public record ContractAlertDto(
    int ContractId,
    string Number,
    string Name,
    string VendorName,
    DateOnly EndDate,
    int DaysRemaining,
    int? AlertWindowDays,
    bool IsExpired,
    bool AutoRenew,
    int? ResponsibleUserId);
