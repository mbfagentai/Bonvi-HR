using Bonvi.Domain.Common;

namespace Bonvi.Domain.Organization;

/// <summary>
/// Bo'lim. Daraxt ko'rinishida: bo'lim ichida kichik bo'lim bo'lishi mumkin.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da 73 xil bo'lim nomi bor va ular tekis ro'yxat: «7-Департамент»,
/// «Метан завод», «Плазмада кесиш бўлими» — aslida bular bir-birining ichida.
/// Daraxt tuzilishi shu ierarxiyani tiklaydi.
/// </para>
/// <para>
/// Chuqurlik cheklanmagan, lekin amalda 3 darajadan oshmaydi:
/// departament → zavod/yo'nalish → bo'lim.
/// </para>
/// </remarks>
public sealed class Department : ReferenceEntity
{
    private Department(Guid id, string name, Guid? parentId)
        : base(id, name) => ParentId = parentId;

    private Department() { }

    /// <summary>Ota bo'lim. <c>null</c> — eng yuqori daraja.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Ierarxiyadagi chuqurlik: 0 — eng yuqori.
    /// Saqlanadi, chunki har safar rekursiv so'rov qilish qimmat.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>
    /// To'liq yo'l: <c>«7-Департамент / Метан завод / Плазма бўлими»</c>.
    /// Qidiruv va ro'yxatda ko'rsatish uchun oldindan hisoblab qo'yiladi.
    /// </summary>
    public string? Path { get; private set; }

    /// <summary>Bo'lim qaysi yuridik shaxsga tegishli (ixtiyoriy).</summary>
    public Guid? LegalEntityId { get; private set; }

    /// <summary>Bo'lim boshlig'i — xodim (ixtiyoriy).</summary>
    public Guid? HeadEmployeeId { get; private set; }

    public static Result<Department> Create(string? name, Guid? parentId = null)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("bolim.nom_bosh", "Bo'lim nomi kiritilishi shart.");
        }

        return new Department(NewId(), normalized, parentId);
    }

    /// <summary>
    /// Ota bo'limni almashtiradi.
    /// </summary>
    /// <remarks>
    /// Halqa hosil bo'lmasligini (bo'lim o'zining avlodiga bo'ysunib qolishini)
    /// bu yerda tekshirib bo'lmaydi — buning uchun butun daraxt kerak.
    /// Tekshiruv use-case qatlamida bajariladi.
    /// </remarks>
    public Result MoveTo(Guid? parentId)
    {
        if (parentId == Id)
        {
            return Error.Conflict("bolim.ozi_ota", "Bo'lim o'zining ota bo'limi bo'la olmaydi.");
        }

        ParentId = parentId;
        return Result.Success();
    }

    /// <summary>Daraxtdagi o'rni qayta hisoblanganda chaqiriladi.</summary>
    public void SetHierarchy(int level, string? path)
    {
        Level = level;
        Path = path;
    }

    public void AssignLegalEntity(Guid? legalEntityId) => LegalEntityId = legalEntityId;

    public void AssignHead(Guid? employeeId) => HeadEmployeeId = employeeId;
}
