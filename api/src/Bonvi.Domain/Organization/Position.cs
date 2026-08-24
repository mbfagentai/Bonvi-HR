using Bonvi.Domain.Common;

namespace Bonvi.Domain.Organization;

/// <summary>
/// Lavozim va uning vazifalari.
/// </summary>
/// <remarks>
/// <para>
/// HR aniq aytdi: «Bizning strukturamizda hammasining lavozimi bor» va
/// «lavozimni tanlaganda avtomatik vazifasi ham yuklanadi». Shu sababli
/// vazifalar ro'yxati lavozimning bir qismi.
/// </para>
/// <para>
/// Excel'da 228 xil lavozim nomi bor, lekin ularning bir qismi bir narsaning
/// turli yozilishi: «Коровул» va «Қоровул», «Ҳисобчи» va «Хисобчи».
/// Tozalangandan keyin ~150 ta qoladi.
/// </para>
/// </remarks>
public sealed class Position : ReferenceEntity
{
    private readonly List<PositionDuty> _duties = [];

    private Position(Guid id, string name, string? code) : base(id, name, code) { }

    private Position() { }

    /// <summary>Lavozim qaysi bo'limga tegishli (ixtiyoriy: ba'zilari umumiy).</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>
    /// Daraja: 1 — ishchi, 2 — usta, 3 — bo'lim boshlig'i, 4 — rahbariyat.
    /// Hisobotlarda guruhlash uchun.
    /// </summary>
    public int Grade { get; private set; } = 1;

    /// <summary>Standart sinov muddati (kun). <c>null</c> — umumiy qoida (90 kun).</summary>
    public int? ProbationDays { get; private set; }

    /// <summary>Lavozim vazifalari — tartiblangan ro'yxat.</summary>
    public IReadOnlyList<PositionDuty> Duties => _duties.AsReadOnly();

    public static Result<Position> Create(string? name, Guid? departmentId = null, string? code = null)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("lavozim.nom_bosh", "Lavozim nomi kiritilishi shart.");
        }

        var position = new Position(NewId(), normalized, code);
        position.DepartmentId = departmentId;
        return position;
    }

    public void AssignDepartment(Guid? departmentId) => DepartmentId = departmentId;

    public Result SetGrade(int grade)
    {
        if (grade is < 1 or > 5)
        {
            return Error.Validation("lavozim.daraja", "Daraja 1 dan 5 gacha bo'lishi kerak.");
        }

        Grade = grade;
        return Result.Success();
    }

    public Result SetProbationDays(int? days)
    {
        if (days is not null && days is < 0 or > 180)
        {
            return Error.Validation(
                "lavozim.sinov_muddati",
                "Sinov muddati 0 dan 180 kungacha bo'lishi kerak (mehnat kodeksi chegarasi).");
        }

        ProbationDays = days;
        return Result.Success();
    }

    public Result AddDuty(string? text)
    {
        var normalized = Normalize(text);
        if (normalized is null)
        {
            return Error.Validation("lavozim.vazifa_bosh", "Vazifa matni kiritilishi shart.");
        }

        if (_duties.Any(d => string.Equals(d.Text, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("lavozim.vazifa_takror", "Bunday vazifa allaqachon qo'shilgan.");
        }

        _duties.Add(PositionDuty.Create(Id, normalized, _duties.Count));
        return Result.Success();
    }

    public void RemoveDuty(Guid dutyId)
    {
        var duty = _duties.Find(d => d.Id == dutyId);
        if (duty is null)
        {
            return;
        }

        _duties.Remove(duty);
        for (var i = 0; i < _duties.Count; i++)
        {
            _duties[i].SetOrder(i);
        }
    }

    public void ReorderDuties(IReadOnlyList<Guid> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            _duties.Find(d => d.Id == orderedIds[i])?.SetOrder(i);
        }

        _duties.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }
}

/// <summary>Lavozim vazifasi — bitta band.</summary>
public sealed class PositionDuty : Entity
{
    private PositionDuty(Guid id, Guid positionId, string text, int sortOrder)
        : base(id)
    {
        PositionId = positionId;
        Text = text;
        SortOrder = sortOrder;
    }

    private PositionDuty() => Text = string.Empty;

    public Guid PositionId { get; private set; }

    public string Text { get; private set; }

    public int SortOrder { get; private set; }

    internal static PositionDuty Create(Guid positionId, string text, int sortOrder)
        => new(NewId(), positionId, text, sortOrder);

    internal void SetOrder(int order) => SortOrder = order;
}
