using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Blanka shabloni — har bir qog'oz blanka kodiga bitta yozuv (<c>HR-F06</c>), SPEC-blankalar §3.1.
/// </summary>
/// <remarks>
/// <para>
/// Shablonning o'zida maydonlar YO'Q — ular versiyada (<see cref="BlankTemplateVersion"/>):
/// blanka matni o'zgarsa yangi versiya chiqadi, eski blankalar esa o'z versiyasi bilan
/// ko'rsatilaveradi. Shablon faqat «qaysi blanka, kim haqida, faolmi» ni saqlaydi.
/// </para>
/// <para>
/// Faolsizlantirilgan shablon bilan yangi blanka ochilmaydi, ochilganlari esa oxirigacha
/// ishlayveradi. O'chirish yo'q — blankalar unga bog'langan.
/// </para>
/// </remarks>
public sealed class BlankTemplate : AggregateRoot, IAuditable
{
    /// <summary>Kod uzunligi (<c>HR-F06</c>).</summary>
    public const int MaxCodeLength = 16;

    /// <summary>Nom uzunligi.</summary>
    public const int MaxTitleLength = 200;

    private BlankTemplate(Guid id, string code, string title, BlankSubjectKind subjectKind, int sortOrder)
        : base(id)
    {
        Code = code;
        Title = title;
        SubjectKind = subjectKind;
        SortOrder = sortOrder;
        IsActive = true;
    }

    private BlankTemplate() { }

    /// <summary>Blanka kodi — qog'ozdagidek (<c>HR-F06</c>), unikal, o'zgarmaydi.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Blanka nomi — qog'ozdagi sarlavha.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Kim haqida to'ldiriladi — o'zgarmaydi (eski blankalarning sub'yekti shunga bog'liq).</summary>
    public BlankSubjectKind SubjectKind { get; private set; }

    /// <summary>Faolsizlantirilsa yangi blanka ochilmaydi.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Ro'yxatdagi tartib.</summary>
    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Yangi shablon (seeder yoki 3-bosqichda konstruktor).</summary>
    /// <param name="code">Blanka kodi.</param>
    /// <param name="title">Nomi.</param>
    /// <param name="subjectKind">Kim haqida.</param>
    /// <param name="sortOrder">Ro'yxatdagi tartib.</param>
    public static Result<BlankTemplate> Create(string? code, string? title, BlankSubjectKind subjectKind, int sortOrder = 0)
    {
        var cleanCode = Clean(code);
        if (cleanCode is null || cleanCode.Length > MaxCodeLength)
        {
            return BlankErrors.Invalid;
        }

        var cleanTitle = Clean(title);
        if (cleanTitle is null || cleanTitle.Length > MaxTitleLength)
        {
            return BlankErrors.Invalid;
        }

        if (!Enum.IsDefined(subjectKind))
        {
            return BlankErrors.Invalid;
        }

        return new BlankTemplate(NewId(), cleanCode, cleanTitle, subjectKind, sortOrder);
    }

    /// <summary>Nom va tartibni yangilaydi (yangi versiya bilan birga keladi).</summary>
    /// <param name="title">Yangi nom.</param>
    /// <param name="sortOrder">Yangi tartib.</param>
    public Result Update(string? title, int sortOrder)
    {
        var cleanTitle = Clean(title);
        if (cleanTitle is null || cleanTitle.Length > MaxTitleLength)
        {
            return BlankErrors.Invalid;
        }

        Title = cleanTitle;
        SortOrder = sortOrder;
        return Result.Success();
    }

    /// <summary>Yangi blankalar ochishga ruxsat beradi.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Yangi blankalar ochishni to'xtatadi; ochilganlari ishlayveradi.</summary>
    public void Deactivate() => IsActive = false;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
