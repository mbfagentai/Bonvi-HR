using FluentValidation;

namespace Bonvi.Application.Catalog.Common;

/// <summary>
/// Ma'lumotnoma yozuvini saqlash uchun tekshiruvlar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Xabar emas, KOD.</b> Har bir qoidaga <c>WithErrorCode</c> beriladi.
/// Interfeys to'rt tilda ishlaydi (uz-Latn, uz-Cyrl, ru, en), shu sababli
/// matnni backend tanlay olmaydi — u faqat «nima buzildi»ni kod bilan aytadi.
/// Xabar zaxira sifatida qoladi.
/// </para>
/// <para>
/// <b>Nega domen tekshiruvi bilan takrorlanadi.</b> Daraja va sinov muddati
/// chegaralari <c>Position</c> ichida ham tekshiriladi. Bu takror emas,
/// ikki qatlamli himoya: domen — buzilmas qoida (har qanday chaqiruvchi uchun),
/// validator — foydalanuvchiga TEZ va aniq javob (bazaga bormasdan).
/// </para>
/// </remarks>
public sealed class SaveReferenceCommandValidator : AbstractValidator<SaveReferenceCommand>
{
    /// <summary>Nom uchun eng katta uzunlik — baza ustuni bilan bir xil.</summary>
    private const int MaxNameLength = 255;

    public SaveReferenceCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
                .WithErrorCode("malumotnoma.nom_bosh")
                .WithMessage("Nom kiritilishi shart.")
            .MaximumLength(MaxNameLength)
                .WithErrorCode("malumotnoma.nom_uzun")
                .WithMessage("Nom 255 belgidan oshmasligi kerak.");

        // Quyidagi qoidalar «null bo'lsa o'tadi» tarzida yozilgan: buyruq
        // sakkizta ma'lumotnomaga umumiy, ya'ni maydonlarning ko'pi
        // aksariyat holatda umuman yuborilmaydi.
        RuleFor(x => x.Grade)
            .Must(grade => grade is null or (>= 1 and <= 5))
            .WithErrorCode("lavozim.daraja")
            .WithMessage("Daraja 1 dan 5 gacha bo'lishi kerak.");

        RuleFor(x => x.ProbationDays)
            .Must(days => days is null or (>= 0 and <= 180))
            .WithErrorCode("lavozim.sinov_muddati")
            .WithMessage("Sinov muddati 0 dan 180 kungacha bo'lishi kerak (mehnat kodeksi chegarasi).");

        RuleFor(x => x.Severity)
            .Must(severity => severity is null or (>= 1 and <= 3))
            .WithErrorCode("jarima.oglirlik")
            .WithMessage("Og'irlik darajasi 1 dan 3 gacha bo'lishi kerak.");
    }
}

/// <summary>Qayta tartiblash so'rovining tekshiruvi.</summary>
public sealed class ReorderReferenceCommandValidator : AbstractValidator<ReorderReferenceCommand>
{
    public ReorderReferenceCommandValidator()
    {
        RuleFor(x => x.OrderedIds)
            .NotEmpty()
            .WithErrorCode("malumotnoma.tartib_bosh")
            .WithMessage("Qayta tartiblash uchun kamida bitta yozuv kerak.");

        // Takroriy identifikator — frontend xatosi belgisi. Uni jimgina
        // qabul qilsak, yozuvlarning bir qismi tartibsiz qolib ketadi.
        RuleFor(x => x.OrderedIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithErrorCode("malumotnoma.tartib_takror")
            .WithMessage("Ro'yxatda takroriy yozuv bor.");
    }
}
