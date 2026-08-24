using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Common;

/// <summary>
/// Bitta ma'lumotnoma turining «shaxsiy hujjati».
/// </summary>
/// <remarks>
/// <para>
/// <b>Muammo.</b> Sakkizta ma'lumotnomaning amallari bir xil (ro'yxat, yaratish,
/// tahrirlash, faolsizlantirish), lekin uchta joyda ular farq qiladi:
/// entity qanday yaratiladi, o'ziga xos maydonlari qaysi va DTO'ga qanday
/// o'giriladi. Agar shu farq handler ichida <c>switch</c> bilan hal qilinsa,
/// yangi ma'lumotnoma qo'shish yettita handlerni tahrirlashni talab qiladi.
/// </para>
/// <para>
/// <b>Yechim.</b> Farqning hammasi shu interfeysga chiqariladi. Handler'lar
/// generik va o'zgarmas bo'lib qoladi, yangi ma'lumotnoma esa bitta ta'rif
/// sinfi + bitta DI qatori bilan qo'shiladi.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
/// <typeparam name="TDto">Tashqariga chiqadigan ko'rinish.</typeparam>
public interface IReferenceDefinition<TEntity, out TDto>
    where TEntity : ReferenceEntity
{
    /// <summary>Entity'ni tashqi ko'rinishga o'giradi.</summary>
    TDto ToDto(TEntity entity);

    /// <summary>Buyruqdan yangi entity yaratadi (faqat konstruktor maydonlari).</summary>
    Result<TEntity> Create(SaveReferenceCommand command);

    /// <summary>
    /// Buyruqdagi tahrirlanadigan maydonlarni entity'ga qo'llaydi:
    /// avval umumiylari (kod, tartib, izoh), keyin turga xoslari.
    /// </summary>
    Result Apply(TEntity entity, SaveReferenceCommand command);
}

/// <summary>
/// Ma'lumotnoma ta'riflari uchun asos — umumiy maydonlarni bir marta qo'llaydi.
/// </summary>
/// <remarks>
/// Kod, tartib va izoh barcha ma'lumotnomalarda bir xil ishlaydi. Ularni
/// har bir ta'rifda qayta yozish — sakkizta bir xil uch qator degani,
/// ya'ni sakkizta xato qilish imkoniyati.
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
/// <typeparam name="TDto">Tashqariga chiqadigan ko'rinish.</typeparam>
public abstract class ReferenceDefinition<TEntity, TDto> : IReferenceDefinition<TEntity, TDto>
    where TEntity : ReferenceEntity
{
    /// <inheritdoc />
    public abstract TDto ToDto(TEntity entity);

    /// <inheritdoc />
    public abstract Result<TEntity> Create(SaveReferenceCommand command);

    /// <inheritdoc />
    public Result Apply(TEntity entity, SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(command);

        entity.SetNote(command.Note);

        return ApplySpecific(entity, command);
    }

    /// <summary>Turga xos maydonlar. <c>null</c> qiymatlar «o'zgartirilmasin» degani.</summary>
    protected abstract Result ApplySpecific(TEntity entity, SaveReferenceCommand command);
}
