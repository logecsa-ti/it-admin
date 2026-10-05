namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Proveedor (SPECS.md seccion 24). Soft delete obligatorio: conserva el historico de
/// contratos, licencias, compras y mantenimientos.
/// </summary>
public class Vendor : AuditableSoftDeletableEntity
{
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Website { get; set; }

    public VendorStatus Status { get; set; } = VendorStatus.Active;

    public string? Notes { get; set; }

    /// <summary>Calificacion 0-5.</summary>
    public decimal? Rating { get; set; }
}

/// <summary>
/// Contrato con un proveedor (SPECS.md seccion 25).
/// Solo se persisten los estados de ciclo de vida (Draft, Active, Terminated, Renewed);
/// Expiring y Expired se derivan de las fechas con <see cref="EffectiveStatus"/>.
/// </summary>
public class Contract : AuditableSoftDeletableEntity
{
    public string Number { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int VendorId { get; set; }

    public ContractType Type { get; set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public decimal? Value { get; set; }

    public string Currency { get; set; } = "USD";

    public bool AutoRenew { get; set; }

    /// <summary>Dias antes del vencimiento en que el contrato pasa a Expiring.</summary>
    public int RenewalNoticeDays { get; set; } = 30;

    public int? ResponsibleUserId { get; set; }

    public ContractStatus Status { get; private set; } = ContractStatus.Draft;

    public string? Notes { get; set; }

    /// <summary>Contrato del que este es la renovacion.</summary>
    public int? RenewedFromContractId { get; private set; }

    public Vendor? Vendor { get; set; }

    public void SetPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            throw new DomainValidationException("INVALID_CONTRACT_PERIOD",
                "La fecha final del contrato debe ser posterior a la fecha inicial.");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public void Activate()
    {
        if (Status != ContractStatus.Draft)
        {
            throw new ConflictException("INVALID_CONTRACT_TRANSITION", $"Solo un contrato en borrador puede activarse (estado: {Status}).");
        }

        Status = ContractStatus.Active;
    }

    public void Terminate()
    {
        if (Status is not (ContractStatus.Draft or ContractStatus.Active))
        {
            throw new ConflictException("INVALID_CONTRACT_TRANSITION", $"No se puede terminar un contrato en estado {Status}.");
        }

        Status = ContractStatus.Terminated;
    }

    /// <summary>
    /// Renueva el contrato: este queda como Renewed (historico intacto) y se devuelve el
    /// contrato sucesor, activo, con el nuevo periodo.
    /// </summary>
    public Contract Renew(string newNumber, DateOnly startDate, DateOnly endDate, decimal? value)
    {
        if (Status != ContractStatus.Active)
        {
            throw new ConflictException("INVALID_CONTRACT_TRANSITION", $"Solo un contrato activo puede renovarse (estado: {Status}).");
        }

        if (startDate < StartDate)
        {
            throw new DomainValidationException("INVALID_CONTRACT_PERIOD",
                "La renovacion no puede iniciar antes que el contrato original.");
        }

        var successor = new Contract
        {
            Number = newNumber,
            Name = Name,
            VendorId = VendorId,
            Type = Type,
            Value = value ?? Value,
            Currency = Currency,
            AutoRenew = AutoRenew,
            RenewalNoticeDays = RenewalNoticeDays,
            ResponsibleUserId = ResponsibleUserId,
            Notes = Notes,
            RenewedFromContractId = Id,
            Status = ContractStatus.Active
        };
        successor.SetPeriod(startDate, endDate);

        Status = ContractStatus.Renewed;
        return successor;
    }

    /// <summary>Estado visible: Active se refina a Expiring/Expired segun las fechas.</summary>
    public ContractStatus EffectiveStatus(DateOnly today) => ContractStatusRules.Effective(Status, EndDate, RenewalNoticeDays, today);
}

public static class ContractStatusRules
{
    public static ContractStatus Effective(ContractStatus stored, DateOnly endDate, int renewalNoticeDays, DateOnly today)
    {
        if (stored != ContractStatus.Active)
        {
            return stored;
        }

        if (endDate < today)
        {
            return ContractStatus.Expired;
        }

        return endDate.DayNumber - today.DayNumber <= renewalNoticeDays ? ContractStatus.Expiring : ContractStatus.Active;
    }
}
