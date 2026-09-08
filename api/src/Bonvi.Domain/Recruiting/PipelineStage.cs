using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// Rekruting voronkasining bitta bosqichi: «Анкета», «Телефон суҳбат»,
/// «Кенгаш», «Job Offer» va h.k.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bosqichlar KODDA emas, MA'LUMOTNOMADA.</b> Boshlang'ich rejada
/// voronka qat'iy ketma-ketlik edi (<c>enum</c> bo'lib yozilishi ham
/// mumkin edi). Foydalanuvchi qarori boshqacha: HR bosqich qo'sha, olib
/// tashlay va tartibini o'zgartira olishi kerak. <c>enum</c> da bu har
/// safar kod o'zgarishi va migratsiya demak edi — ya'ni HR o'z jarayonini
/// o'zi boshqara olmasdi. Shu sababli bosqich boshqa sakkizta ma'lumotnoma
/// bilan bir xil tabiatga ega va <see cref="ReferenceEntity"/> dan meros
/// oladi: nomi unikal, o'chirilmaydi — faqat faolsizlantiriladi (eski
/// nomzodlarda unga havola qolgan).
/// </para>
/// <para>
/// <b>Nega «Кенгаш» uchun alohida mexanizm YO'Q.</b> Excel'da «91.1
/// Кенгаш хулосаси» alohida varaq va u ovoz berish jadvaliga o'xshaydi.
/// Foydalanuvchi aniq aytdi: ovoz berish emas, oddiy xulosa — kengash
/// yig'ilgach HR bitta natija, bitta izoh va sana yozadi. Bu esa aynan
/// <see cref="CandidateStageEntry"/> ning ta'rifi. Ya'ni kengash — YANA
/// BIR BOSQICH, unga maxsus agregat ham, maxsus jadval ham kerak emas.
/// </para>
/// <para>
/// <b>Nega bu ma'lumotnomada TARTIB bor, boshqalarida yo'q.</b> Loyihada
/// qo'lda qo'yiladigan tartib raqami ataylab olib tashlangan va barcha
/// ma'lumotnomalar alifbo bo'yicha chiziladi (sababi
/// <c>ReferenceRepository.ListAsync</c> izohida): «Бухгалтерия» ning
/// «Ғазна» dan oldin turishi hech qanday savolga javob bermaydi. Voronkada
/// esa tartib — MA'LUMOTNING O'ZI: «Телефон суҳбат» «Раҳбар суҳбати» dan
/// oldin keladi, bu biznes fakti. Alifbo bo'yicha chizilsa
/// («Job Offer», «Кенгаш», «Телефон суҳбат») voronka ma'nosini butunlay
/// yo'qotardi. Aynan shu sabab <c>PositionDuty.SortOrder</c> da ham
/// saqlangan: vazifalar ketma-ket bajariladi, ularni alifboga tizib
/// bo'lmaydi.
/// </para>
/// </remarks>
public sealed class PipelineStage : ReferenceEntity
{
    private PipelineStage(Guid id, string name, int sortOrder)
        : base(id, name) => SortOrder = sortOrder;

    /// <summary>EF Core uchun.</summary>
    private PipelineStage() { }

    /// <summary>
    /// Voronkadagi o'rin: kichik qiymat — oldinroq.
    /// </summary>
    /// <remarks>
    /// Qiymatlar ketma-ket bo'lishi SHART EMAS (0, 5, 12 ham to'g'ri
    /// voronka): muhimi — nisbiy tartib. Qayta tartiblashda ular baribir
    /// 0 dan boshlab qayta raqamlanadi (<see cref="MoveTo"/> chaqiruvchisi
    /// — <c>ReorderPipelineStagesCommandHandler</c>).
    /// </remarks>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Shu bosqichga o'tkazilgan nomzodga SUHBAT TAKLIFI yuboriladimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega bu bosqichning XUSUSIYATI, kodda yozilgan nom emas.</b>
    /// Ekranda «Кенгашга чақирилди» tugmasi bosilganda HR dan suhbat sanasi
    /// va vaqti so'ralishi, keyin nomzodga Telegram xabari ketishi kerak
    /// (foydalanuvchi talabi, 2026-09-08). Klient qaysi tugma shunday
    /// ishlashini BILISHI kerak — va uni bosqich NOMIGA qarab bilishi
    /// mumkin emas edi: nom ma'lumotnomada, HR uni istagan payt
    /// o'zgartiradi va o'shanda tugma jimgina oddiy tugmaga aylanardi.
    /// </para>
    /// <para>
    /// <b>Nega BAYROQ, yagona sozlama emas.</b> «Qaysi bosqich taklif
    /// yuboradi» degan bitta sozlama ham yetardi, lekin amalda taklif
    /// bittadan ko'p: kengashga chaqirish ham, rahbar suhbatiga chaqirish
    /// ham aynan shu xabar. Bayroq ikkalasiga ham qo'yiladi va HR o'z
    /// jarayonini o'zi ta'riflaydi.
    /// </para>
    /// <para>
    /// Bayroq YOQILGAN bo'lishi xabarni majburlamaydi: nomzodning Telegram
    /// suhbati bo'lmasa (HR uni qo'lda kiritgan) yuboradigan manzil yo'q va
    /// bosqich oddiygina yoziladi.
    /// </para>
    /// </remarks>
    public bool InvitesToInterview { get; private set; }

    /// <summary>Suhbat taklifi bayrog'ini qo'yadi yoki oladi.</summary>
    public void SetInvitesToInterview(bool value) => InvitesToInterview = value;

    /// <summary>Yangi bosqich yaratadi.</summary>
    /// <remarks>
    /// <paramref name="sortOrder"/> ni chaqiruvchi hisoblaydi: yangi bosqich
    /// DOIM oxiriga qo'shiladi (mavjud eng katta tartib + 1). Nega bu yerda
    /// emas: domen bazadagi boshqa yozuvlarni ko'rmaydi va «eng katta
    /// tartib» ni bila olmaydi.
    /// </remarks>
    public static Result<PipelineStage> Create(
        string? name,
        int sortOrder = 0,
        bool invitesToInterview = false)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("bosqich.nom_bosh", "Bosqich nomi kiritilishi shart.");
        }

        return sortOrder < 0
            ? Error.Validation("bosqich.tartib", "Bosqich tartibi manfiy bo'lishi mumkin emas.")
            : new PipelineStage(NewId(), normalized, sortOrder) { InvitesToInterview = invitesToInterview };
    }

    /// <summary>Voronkadagi o'rnini o'zgartiradi.</summary>
    public Result MoveTo(int sortOrder)
    {
        if (sortOrder < 0)
        {
            return Error.Validation("bosqich.tartib", "Bosqich tartibi manfiy bo'lishi mumkin emas.");
        }

        SortOrder = sortOrder;
        return Result.Success();
    }
}
