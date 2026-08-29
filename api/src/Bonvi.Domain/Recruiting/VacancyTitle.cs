using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// Vakansiya nomi — e'londa turadigan qisqa nom: «Сотувчи-консультант»,
/// «Ҳайдовчи», «Омборчи».
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega VAKANSIYA NOMI va KOMPANIYA LAVOZIMI ataylab IKKI XIL
/// tushuncha.</b> Boshlanishida vakansiya <c>Position</c> ma'lumotnomasidan
/// tanlanardi — «lavozim bitta, uni ikki marta yozishning nima keragi bor»
/// degan mulohaza bilan. Amalda ular bitta narsa emas:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Vakansiya nomi — E'LON.</b> U tashqariga qaraydi va nomzod tushunadigan
/// tilda yoziladi. «Сотувчи-консультант» degan e'longa odam keladi;
/// shtat jadvalidagi «Сотув бўлими катта мутахассиси, 3-тоифа» ga hech kim
/// kelmaydi. E'lonlar soni o'nlab, ular tez paydo bo'lib tez yopiladi.
/// </item>
/// <item>
/// <b>Lavozim — SHTAT BIRLIGI.</b> U ichkariga qaraydi: buyruqda, mehnat
/// shartnomasida va hisobotlarda aynan shu nom turadi, unga vazifalar,
/// daraja va sinov muddati biriktirilgan. Excel'dagi 228 ta lavozim aynan
/// shu ro'yxat.
/// </item>
/// </list>
/// <para>
/// <b>Ular QAYERDA uchrashadi:</b> aynan bitta nuqtada —
/// <c>AcceptJobOfferCommandHandler</c> da, ya'ni taklif QABUL QILINGANDA.
/// O'sha paytgacha nomzod haqida «u qaysi e'longa kelgan» degan faktgina
/// bor; ishga qabul kuni esa HR aniq qaror qabul qiladi: qaysi firmaga,
/// qaysi bo'limga, qaysi shtat birligiga. Bu qaror e'lonning xossasi emas —
/// bitta «Сотувчи-консультант» e'loniga kelgan uch odam uch xil do'konga,
/// hatto holdingning uch xil firmasiga ishga olinishi mumkin.
/// </para>
/// <para>
/// <b>NEGA <c>Position</c> ning O'ZIDAN foydalanilmadi.</b> Foydalanuvchi
/// talabi aniq: «ikkalasi bir-biriga aralashmasin». Aralashganda ikkita
/// zarar bo'lardi. Birinchisi — e'lon uchun ochilgan «Сотувчи-консультант»
/// shtat jadvaliga tushib qolardi va lavozimlar ro'yxati yana kengayib
/// ketardi (Excel'dagi 228 ta yozuvning kelib chiqishi aynan shunday).
/// Ikkinchisi — teskarisi: HR e'lonni chiroyli yozish uchun SHTAT
/// nomini o'zgartirishga majbur bo'lardi va u bilan birga eski buyruqlardagi
/// lavozim nomi ham «o'zgarib» ketardi.
/// </para>
/// <para>
/// <b>NEGA erkin matn emas.</b> Vakansiyada erkin sarlavha bo'lganida
/// «Сотувчи (шошилинч!)», «сотувчи консултант», «Sotuvchi» — bir e'lon uch
/// xil nom bilan ro'yxatda turardi va «shu nom bo'yicha nechta odam keldi»
/// degan savolga javob yo'qolardi. Shu sababli bu ham ma'lumotnoma:
/// nomi unikal, o'chirilmaydi — faqat faolsizlantiriladi (eski
/// vakansiyalarda unga havola qolgan).
/// </para>
/// <para>
/// <b>NEGA tartib raqami YO'Q</b> (<see cref="PipelineStage.SortOrder"/> dan
/// farqli): e'lon nomlari o'rtasida hech qanday tabiiy ketma-ketlik yo'q —
/// «Ҳайдовчи» «Омборчи» dan oldin ham, keyin ham kelishi mumkin. Voronkada
/// esa tartib ma'lumotning O'ZI edi. Loyihaning umumiy qoidasi ham shu:
/// ma'lumotnomalar alifbo bo'yicha chiziladi, qo'lda tartiblash yo'q
/// (<c>ReferenceRepository.ListAsync</c> izohi).
/// </para>
/// <para>
/// <b>NEGA urug' fayli yo'q.</b> Boshqa ma'lumotnomalar Excel'dan
/// ko'chirilgan ma'lumotga ega, bu esa yangi tushuncha — Bonvi'da bunday
/// ro'yxat hech qachon bo'lmagan. Bir necha nom o'ylab topib quyish
/// «tizim o'zi to'ldirdi» degan yolg'on taassurot berardi va HR ularni
/// tozalashdan boshlardi. Ro'yxat BO'SH boshlanadi va birinchi vakansiya
/// ochilayotganda to'ladi.
/// </para>
/// </remarks>
public sealed class VacancyTitle : ReferenceEntity
{
    private VacancyTitle(Guid id, string name) : base(id, name) { }

    /// <summary>EF Core uchun.</summary>
    private VacancyTitle() { }

    /// <summary>Yangi vakansiya nomi yaratadi.</summary>
    /// <remarks>
    /// Faqat NOM: qo'shimcha maydon ataylab yo'q. Daraja, bo'lim va sinov
    /// muddati — <c>Position</c> ning xossalari, e'lonniki emas. Ularni bu
    /// yerga ham qo'shish ikkita yarim lavozim ma'lumotnomasi hosil qilardi
    /// va «qaysi biri haqiqiy?» degan savol tug'ilardi.
    /// </remarks>
    public static Result<VacancyTitle> Create(string? name)
    {
        var normalized = Normalize(name);

        return normalized is null
            ? Error.Validation("vakansiya_nomi.nom_bosh", "Vakansiya nomi kiritilishi shart.")
            : new VacancyTitle(NewId(), normalized);
    }
}
