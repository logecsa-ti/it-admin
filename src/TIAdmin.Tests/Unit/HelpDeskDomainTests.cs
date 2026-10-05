namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Domain.Services;
using Xunit;

public class BusinessHoursTests
{
    // Zona fija UTC-6 sin horario de verano (como America/Managua). 2026-10-05 es lunes.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Test-06", TimeSpan.FromHours(-6), "Test-06", "Test-06");
    private static readonly WorkSchedule OfficeHours = new(true, new TimeOnly(8, 0), new TimeOnly(17, 0), WorkSchedule.ParseDays("1,2,3,4,5"));

    [Theory]
    [InlineData("2026-10-05 10:00", 60, "2026-10-05 11:00")]   // dentro de la jornada
    [InlineData("2026-10-05 16:30", 60, "2026-10-06 08:30")]   // cruza al dia siguiente
    [InlineData("2026-10-09 16:00", 120, "2026-10-12 09:00")]  // viernes → lunes
    [InlineData("2026-10-10 10:00", 30, "2026-10-12 08:30")]   // sabado: arranca el lunes
    [InlineData("2026-10-05 07:00", 30, "2026-10-05 08:30")]   // antes de abrir
    [InlineData("2026-10-05 18:00", 15, "2026-10-06 08:15")]   // despues de cerrar
    [InlineData("2026-10-05 08:00", 1440, "2026-10-07 14:00")] // 24 h laborables = 9 + 9 + 6
    [InlineData("2026-10-05 17:00", 0, "2026-10-05 17:00")]    // sin minutos no se mueve
    public void AddWorkingMinutes_ShouldOnlyCountOfficeHours(string startLocal, int minutes, string expectedLocal)
    {
        var due = BusinessHours.AddWorkingMinutes(ToUtc(startLocal), minutes, OfficeHours, Zone);

        TimeZoneInfo.ConvertTimeFromUtc(due, Zone).Should().Be(DateTime.Parse(expectedLocal, System.Globalization.CultureInfo.InvariantCulture));
        due.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void AddWorkingMinutes_AlwaysOn_ShouldRunAroundTheClock()
    {
        var due = BusinessHours.AddWorkingMinutes(ToUtc("2026-10-09 23:00"), 120, WorkSchedule.AlwaysOn, Zone);

        TimeZoneInfo.ConvertTimeFromUtc(due, Zone).Should().Be(new DateTime(2026, 10, 10, 1, 0, 0));
    }

    [Theory]
    [InlineData("1,2,3,4,5", new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })]
    [InlineData("6, 7", new[] { DayOfWeek.Saturday, DayOfWeek.Sunday })]
    [InlineData("0,8,x,1", new[] { DayOfWeek.Monday })]
    public void ParseDays_ShouldMapIsoWeekdays(string days, DayOfWeek[] expected)
    {
        WorkSchedule.ParseDays(days).Should().BeEquivalentTo(expected);
    }

    private static DateTime ToUtc(string local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture), Zone);
}

public class SlaPolicySelectorTests
{
    [Fact]
    public void Select_ShouldPreferMostSpecificMatchAndFallBackToDefault()
    {
        var fallback = Policy(1, isDefault: true);
        var byPriority = Policy(2, priority: TicketPriority.High);
        var byCategoryAndPriority = Policy(3, categoryId: 10, priority: TicketPriority.High);
        var otherCategory = Policy(4, categoryId: 99);
        var inactive = Policy(5, categoryId: 10, priority: TicketPriority.High, departmentId: 7);
        inactive.IsActive = false;
        var policies = new[] { fallback, byPriority, byCategoryAndPriority, otherCategory, inactive };

        SlaPolicySelector.Select(policies, 10, TicketPriority.High, TicketType.Incident, 7).Should().BeSameAs(byCategoryAndPriority);
        SlaPolicySelector.Select(policies, 20, TicketPriority.High, TicketType.Incident, null).Should().BeSameAs(byPriority);
        SlaPolicySelector.Select(policies, 20, TicketPriority.Low, TicketType.Incident, null).Should().BeSameAs(fallback);
        SlaPolicySelector.Select([], 20, TicketPriority.Low, TicketType.Incident, null).Should().BeNull();
    }

    private static SlaPolicy Policy(int id, bool isDefault = false, int? categoryId = null, TicketPriority? priority = null, int? departmentId = null)
    {
        var policy = new SlaPolicy
        {
            Name = $"P{id}", IsDefault = isDefault, CategoryId = categoryId, Priority = priority, DepartmentId = departmentId,
            ResponseTimeMinutes = 60, ResolutionTimeMinutes = 120
        };
        typeof(TIAdmin.Domain.Common.Entity).GetProperty("Id")!.SetValue(policy, id);
        return policy;
    }
}

public class TicketTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Workflow_ShouldFollowSpecFlowAndRecordHistory()
    {
        var ticket = NewTicket();

        var opened = ticket.ChangeStatus(TicketStatus.Open, 2, "agente", Now, null);
        ticket.ChangeStatus(TicketStatus.InProgress, 2, "agente", Now, null);
        ticket.ChangeStatus(TicketStatus.WaitingUser, 2, "agente", Now, "Esperando datos");
        ticket.ChangeStatus(TicketStatus.InProgress, 2, "agente", Now, null);
        ticket.ChangeStatus(TicketStatus.Resolved, 2, "agente", Now.AddHours(1), null, "Se reinstalo el driver");
        var closed = ticket.ChangeStatus(TicketStatus.Closed, 1, "usuario", Now.AddHours(2), null);

        opened.FromStatus.Should().Be(TicketStatus.New);
        opened.ToStatus.Should().Be(TicketStatus.Open);
        ticket.FirstResponseAt.Should().Be(Now, "salir de New es la primera respuesta");
        ticket.ResolutionNotes.Should().Be("Se reinstalo el driver");
        ticket.ResolvedAt.Should().Be(Now.AddHours(1));
        closed.ToStatus.Should().Be(TicketStatus.Closed);
        ticket.ClosedAt.Should().Be(Now.AddHours(2));
        ticket.Invoking(t => t.ChangeStatus(TicketStatus.InProgress, 2, "agente", Now, null))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_TICKET_TRANSITION");
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.WaitingUser)]
    public void New_ShouldNotJumpToLaterStates(TicketStatus target)
    {
        NewTicket().Invoking(t => t.ChangeStatus(target, 2, "agente", Now, null, "notas"))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_TICKET_TRANSITION");
    }

    [Fact]
    public void Resolve_WithoutNotes_ShouldThrow()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.InProgress, 2, "agente", Now, null);

        ticket.Invoking(t => t.ChangeStatus(TicketStatus.Resolved, 2, "agente", Now, null, "  "))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("RESOLUTION_NOTES_REQUIRED");
    }

    [Fact]
    public void Reopen_ShouldClearResolutionTimestamp()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.InProgress, 2, "agente", Now, null);
        ticket.ChangeStatus(TicketStatus.Resolved, 2, "agente", Now, null, "Listo");

        ticket.ChangeStatus(TicketStatus.InProgress, 1, "usuario", Now.AddHours(1), "Sigue fallando");

        ticket.ResolvedAt.Should().BeNull();
        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void Resolve_AfterDeadline_ShouldMarkSlaBreached()
    {
        var ticket = NewTicket();
        ticket.ApplySla(null, Now.AddMinutes(30), Now.AddHours(4));
        ticket.ChangeStatus(TicketStatus.InProgress, 2, "agente", Now.AddMinutes(10), null);

        ticket.ChangeStatus(TicketStatus.Resolved, 2, "agente", Now.AddHours(5), null, "Tarde");

        ticket.IsResponseOverdue(Now.AddHours(5)).Should().BeFalse("respondio a los 10 minutos");
        ticket.IsResolutionOverdue(Now.AddHours(5)).Should().BeTrue();
        ticket.IsSlaBreached.Should().BeTrue();
    }

    [Fact]
    public void Assign_NewTicket_ShouldOpenItAndCountAsFirstResponse()
    {
        var ticket = NewTicket();

        var history = ticket.Assign(assigneeId: 9, actorId: 2, "supervisor", Now);
        var reassign = ticket.Assign(assigneeId: 10, actorId: 2, "supervisor", Now.AddHours(1));

        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.AssignedToId.Should().Be(10);
        ticket.FirstResponseAt.Should().Be(Now);
        history!.ToStatus.Should().Be(TicketStatus.Open);
        reassign.Should().BeNull("reasignar no cambia el estado");
    }

    [Fact]
    public void Approval_ShouldGateWorkAndRejectionShouldCancel()
    {
        var pending = NewTicket(TicketType.ServiceRequest);
        pending.RequireApproval();
        var rejected = NewTicket(TicketType.ServiceRequest);
        rejected.RequireApproval();

        pending.Invoking(t => t.ChangeStatus(TicketStatus.Open, 2, "agente", Now, null))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("TICKET_PENDING_APPROVAL");
        pending.Invoking(t => t.Assign(9, 2, "agente", Now))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("TICKET_PENDING_APPROVAL");

        pending.Approve(5, "jefe", Now, "Presupuesto aprobado");
        pending.ChangeStatus(TicketStatus.Open, 2, "agente", Now, null);
        var rejection = rejected.Reject(5, "jefe", Now, "Sin presupuesto");

        pending.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        pending.Status.Should().Be(TicketStatus.Open);
        rejected.Status.Should().Be(TicketStatus.Cancelled);
        rejected.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
        rejection.Comment.Should().Contain("Sin presupuesto");
        pending.Invoking(t => t.Approve(5, "jefe", Now, null))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("TICKET_NOT_PENDING_APPROVAL");
    }

    [Fact]
    public void PendingApproval_CanStillBeCancelledByRequester()
    {
        var ticket = NewTicket(TicketType.ServiceRequest);
        ticket.RequireApproval();

        ticket.ChangeStatus(TicketStatus.Cancelled, 1, "usuario", Now, "Ya no lo necesito");

        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }

    private static Ticket NewTicket(TicketType type = TicketType.Incident) =>
        new() { TicketNumber = "TKT-2026-000001", Type = type, Title = "No imprime", Description = "Detalle", CategoryId = 1, RequesterId = 1 };
}
