namespace Bonvi.Domain.Common;

/// <summary>
/// Agregat ildizi — tranzaksiya chegarasi.
/// Repozitoriy FAQAT agregat ildizi bilan ishlaydi:
/// masalan <c>Employee</c> agregat, uning ichidagi <c>EmployeeDocument</c> emas.
/// Bu qoida ma'lumot yaxlitligini kafolatlaydi.
/// </summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot(Guid id) : base(id) { }

    protected AggregateRoot() { }
}
