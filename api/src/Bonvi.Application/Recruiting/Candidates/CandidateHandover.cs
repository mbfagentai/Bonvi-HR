using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>Nomzoddan xodimga nima ko'chganini sanab beradi — jurnal uchun.</summary>
/// <param name="Photo">Surat ko'chdimi.</param>
/// <param name="Files">Nechta fayl xodim hujjatiga aylandi.</param>
/// <param name="Competency">Anketadan qobiliyatlar profiliga biror narsa tushdimi.</param>
public readonly record struct CandidateHandoverResult(bool Photo, int Files, bool Competency);

/// <summary>
/// ⭐ Nomzod XODIMGA aylanganda uning narsalarini xodim kartasiga topshiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Muammo qayerdan keldi.</b> Foydalanuvchi aytgan holat
/// (2026-09-19): nomzodning anketa suratlari fayllar bo'limiga
/// yuklangan, odam sinov muddatiga ishga olingan va xodim kartasidagi
/// «Hujjatlar» bo'p-bo'sh chiqqan — HR hammasini qaytadan yuklashga
/// majbur bo'lgan. Tekshirganda ma'lum bo'ldiki, ishga qabul FAQAT
/// shaxsiy maydonlarni ko'chiradi (F.I.Sh., tug'ilgan sana, telefon,
/// manzil, mutaxassislik), qolgani nomzodda qolib ketadi. Surat ham
/// ko'chmasdi: yangi xodim kartasida yuz o'rniga bosh harflar turardi.
/// </para>
/// <para>
/// <b>Nega bitta joyda.</b> Ko'chirish IKKI marta kerak bo'ladi: ishga
/// qabul paytida va allaqachon ishga olinganlarni tuzatuvchi bir
/// martalik yurishda. Ikkita amalga oshirish ikkita xulq degani
/// bo'lardi — ertami-kechmi ular bir-biridan ajralardi.
/// </para>
/// <para>
/// ⚠️ <b>Uch narsaning uch xil qoidasi bor</b> va bu ataylab:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Fayl — KO'CHADI.</b> Yozuv nomzoddan chiqib, xodim hujjatiga
///     aylanadi; diskdagi bayt esa joyida qoladi va kalit AYNAN o'sha
///     bo'ladi (nusxa yo'q, disk ikki barobar band bo'lmaydi). Hujjatning
///     egasi endi bitta — xodim kartasi.
///   </item>
///   <item>
///     <b>Surat — NUSXALANADI.</b> Ikkala kartaga ham yuz kerak, nomzod
///     suratini o'chirish esa diskdagi baytni ham o'chiradi — bitta
///     kalitni bo'lishib olish xodimning suratini jimgina yo'qotardi
///     (sababi <see cref="SquarePhotoStore.CopyAsync"/> izohida).
///   </item>
///   <item>
///     <b>Anketa — QISMAN ko'chiriladi.</b> Uning ko'p maydoni xodim
///     tomonida umuman o'rni yo'q (oilaviy holat, kutilgan oylik, qayerdan
///     eshitgan). Mos tushadigani ko'chadi, qolgani nomzod kartasida
///     qoladi — ro'yxati <see cref="HandOverQuestionnaireAsync"/> izohida.
///   </item>
/// </list>
/// <para>
/// <b>Hech narsa USTIGA YOZILMAYDI.</b> Har uchala amal ham faqat BO'SH
/// joyni to'ldiradi: xodimda surat bor bo'lsa tegilmaydi, qobiliyatlar
/// profilida til yozilgan bo'lsa qayta yozilmaydi. Sabab bir martalik
/// yurishda: u Excel'dan ko'chirilgan, HR qo'lda to'ldirgan kartalar
/// ustidan o'tadi va ularning ma'lumoti anketadagidan ishonchliroq.
/// </para>
/// <para>
/// <b>Saqlash BU YERDA emas.</b> Sinf faqat xotiradagi obyektlarni
/// o'zgartiradi va yangi yozuvlarni repozitoriyga qo'shadi;
/// <c>SaveChangesAsync</c> ni chaqiruvchi bajaradi — ishga qabul
/// oqimida bu butun amal bitta tranzaksiyada ketishi uchun muhim.
/// </para>
/// </remarks>
public sealed class CandidateHandover
{
    /// <summary>
    /// Ko'chirilgan fayllar tushadigan hujjat turi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega YANGI tur, mavjudlari emas.</b> Nomzod fayllari
    /// aralash keladi: anketa surati, pasport varag'i, diplom skani —
    /// va qaysi fayl qaysi turdaligini tizim BILMAYDI (nomzod faylida
    /// tur tushunchasi umuman yo'q). Taxmin qilish tur bo'yicha butun
    /// guruhlashni yolg'onga aylantirardi.
    /// </para>
    /// <para>
    /// Shu sababli bu tur — QABULXONA: hammasi shu yerga tushadi, HR
    /// esa keyin bittalab aniqlashtiradi (hujjat oynasida tur ikki
    /// bosishda o'zgaradi). Turning o'zi ma'lumotnomada urug'lanadi
    /// (<c>document-types.json</c>), ya'ni HR uni nomini o'zgartirishi
    /// yoki faolsizlantirishi mumkin — shunda ko'chirish jimgina
    /// to'xtaydi va bu KUTILGAN holat (pastdagi izohga qarang).
    /// </para>
    /// </remarks>
    public const string DocumentTypeName = "Номзодлик ҳужжатлари";

    private readonly IReferenceRepository<DocumentType> _documentTypes;
    private readonly IEmployeeDocumentRepository _documents;
    private readonly IEmployeeCompetencyRepository _competencies;
    private readonly SquarePhotoStore _photos;
    private readonly IClock _clock;

    public CandidateHandover(
        IReferenceRepository<DocumentType> documentTypes,
        IEmployeeDocumentRepository documents,
        IEmployeeCompetencyRepository competencies,
        SquarePhotoStore photos,
        IClock clock)
    {
        _documentTypes = documentTypes;
        _documents = documents;
        _competencies = competencies;
        _photos = photos;
        _clock = clock;
    }

    /// <summary>
    /// Nomzodning surati, fayllari va anketasini xodimga topshiradi.
    /// </summary>
    /// <remarks>
    /// Xato QAYTARMAYDI va ataylab shunday: ko'chirish — ishga qabulning
    /// qo'shimchasi, sharti emas. Surat diskdan topilmasa yoki hujjat
    /// turi ma'lumotnomadan o'chirilgan bo'lsa, odam baribir ishga
    /// olinishi kerak. Nima ko'chgani natijada qaytadi va chaqiruvchi
    /// uni jurnalga yozadi.
    /// </remarks>
    public async Task<CandidateHandoverResult> ApplyAsync(
        Candidate candidate,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(employee);

        var photo = await HandOverPhotoAsync(candidate, employee, cancellationToken).ConfigureAwait(false);
        var files = await HandOverFilesAsync(candidate, employee, cancellationToken).ConfigureAwait(false);
        var competency = await HandOverQuestionnaireAsync(candidate, employee, cancellationToken).ConfigureAwait(false);

        return new CandidateHandoverResult(photo, files, competency);
    }

    /// <summary>Surat — nusxa olinadi, xodimda surat bo'lmasa.</summary>
    private async Task<bool> HandOverPhotoAsync(
        Candidate candidate,
        Employee employee,
        CancellationToken cancellationToken)
    {
        if (candidate.PhotoKey is not { } source || employee.PhotoKey is not null)
        {
            return false;
        }

        var copy = await _photos.CopyAsync(source, cancellationToken).ConfigureAwait(false);
        if (copy is null)
        {
            // Kalit bazada bor, fayl esa diskda yo'q — eski yozuvlarda
            // uchraydi. Ishga qabulni to'xtatish uchun sabab emas.
            return false;
        }

        employee.SetPhoto(copy);

        return true;
    }

    /// <summary>Fayllar — yozuvlari ko'chadi, baytlari joyida qoladi.</summary>
    private async Task<int> HandOverFilesAsync(
        Candidate candidate,
        Employee employee,
        CancellationToken cancellationToken)
    {
        if (candidate.Files.Count == 0)
        {
            return 0;
        }

        var type = await _documentTypes
            .FindByNameAsync(DocumentTypeName, cancellationToken)
            .ConfigureAwait(false);

        /*
          Tur topilmadi — fayllar NOMZODDA QOLADI.

          Bu «jimgina yo'qotish» emas: yozuvlar joyida, nomzod kartasida
          ko'rinib turibdi va keyingi yurishda (tur qaytarilgach)
          ko'chadi. Muqobil yo'l — turni shu yerda yaratib yuborish —
          ma'lumotnomani kod bilan to'ldirish degani bo'lardi va HR
          o'chirgan qatorni tizim qayta tiklab turardi.
        */
        if (type is null)
        {
            return 0;
        }

        var note = $"Nomzodlik davridan ko'chirildi · {_clock.Today:dd.MM.yyyy}";
        var moved = 0;

        foreach (var file in candidate.HandOverFiles())
        {
            var document = EmployeeDocument.Attach(
                employee.Id,
                type.Id,
                file.FileKey,
                file.FileName,
                file.ContentType,
                file.SizeBytes,
                note);

            if (document.IsFailure)
            {
                continue;
            }

            _documents.Add(document.Value);
            moved++;
        }

        return moved;
    }

    /// <summary>
    /// Anketa — «Qobiliyatlar» profiliga MOS tushadigan qismi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Ko'chadigan uchta narsa:</b> rus tili darajasi (tuzilgan
    /// qiymat — anketadagi <c>LanguageLevel</c> xodim tomonidagining
    /// AYNAN o'zi), boshqa tillar (erkin matn) va kompyuter/haydovchilik
    /// ko'nikmalari («Boshqa qobiliyatlar» matniga qo'shiladi).
    /// </para>
    /// <para>
    /// ⚠️ <b>Ko'chmaydiganlar va NEGA:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Ta'lim darajasi</b> (o'rta / oliy / tugallanmagan oliy) —
    ///     xodimda faqat ta'lim JOYI bor (maktab yoki institut nomi).
    ///     Daraja bilan joy boshqa-boshqa narsa; darajani joy maydoniga
    ///     yozish «Oliy» degan «o'quv yurti»ni tug'dirardi.
    ///   </item>
    ///   <item>
    ///     <b>Kompyuter ko'nikmasi FOIZDA emas.</b> Anketada bu erkin
    ///     matn («Word, Excel»), xodimda esa 0-100 foiz. Matnni foizga
    ///     aylantirish — taxmin. Shu sababli matn «Boshqa qobiliyatlar»
    ///     ga tushadi, foiz esa BO'SH qoladi va uni HR qo'yadi.
    ///   </item>
    ///   <item>
    ///     <b>Ish tajribasi</b> — xodimda mehnat munosabatlari va
    ///     hodisalar lentasi bor, ya'ni tajriba TIZIMNING O'ZIDA
    ///     yig'iladi. Anketadagi erkin matnni ularning yoniga qo'yish
    ///     ikkita qarama-qarshi manba yasardi.
    ///   </item>
    ///   <item>
    ///     <b>Oilaviy holat, oxirgi oylik, kutilgan oylik, qayerdan
    ///     eshitgan, qo'shimcha izoh</b> — xodim kartasida bunday
    ///     maydonlar umuman yo'q. Ular ISHGA OLISHDAN OLDINGI
    ///     suhbatning faktlari va nomzod kartasida o'z o'rnida qoladi;
    ///     oylik esa ishga qabulda alohida, kelishilgan qiymat bilan
    ///     kiritiladi.
    ///   </item>
    /// </list>
    /// </remarks>
    private async Task<bool> HandOverQuestionnaireAsync(
        Candidate candidate,
        Employee employee,
        CancellationToken cancellationToken)
    {
        if (candidate.Questionnaire is not { } anketa)
        {
            return false;
        }

        var competency = await _competencies
            .GetByEmployeeAsync(employee.Id, cancellationToken)
            .ConfigureAwait(false);

        if (competency is null)
        {
            var created = EmployeeCompetency.Create(employee.Id);
            if (created.IsFailure)
            {
                return false;
            }

            competency = created.Value;
            _competencies.Add(competency);
        }

        var filled = false;

        // Rus tili — TUZILGAN qiymat, ya'ni ro'yxatga qator bo'lib
        // tushadi va ekranda daraja sifatida ko'rinadi. Profilda til
        // allaqachon yozilgan bo'lsa tegilmaydi.
        if (competency.LanguageSkills.Count == 0)
        {
            var skills = new[] { new LanguageSkill(LanguageCode.Russian, anketa.RussianLevel) };

            if (competency.SetLanguageSkills(skills).IsSuccess)
            {
                filled = true;
            }
        }

        if (string.IsNullOrWhiteSpace(competency.Languages) && !string.IsNullOrWhiteSpace(anketa.OtherLanguages))
        {
            competency.SetLanguages(anketa.OtherLanguages);
            filled = true;
        }

        if (string.IsNullOrWhiteSpace(competency.OtherAbilities))
        {
            var abilities = Abilities(anketa);

            if (abilities is not null)
            {
                competency.SetOtherAbilities(abilities);
                filled = true;
            }
        }

        return filled;
    }

    /// <summary>
    /// Kompyuter va haydovchilik ko'nikmalarini bitta matnga yig'adi.
    /// </summary>
    /// <remarks>
    /// Ikkalasi ham anketada erkin matn va xodim tomonida ular uchun
    /// alohida maydon yo'q — «Boshqa qobiliyatlar» aynan shunday
    /// qoldiqlar uchun. Toifasiz «haydovchilik guvohnomasi bor» ham
    /// yoziladi: guvohnomaning o'zi fakt, toifa esa tafsilot.
    /// </remarks>
    private static string? Abilities(CandidateQuestionnaire anketa)
    {
        var parts = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(anketa.OfficeSkills))
        {
            parts.Add(anketa.OfficeSkills.Trim());
        }

        if (anketa.HasDriverLicense)
        {
            parts.Add(
                string.IsNullOrWhiteSpace(anketa.DriverLicenseCategories)
                    ? "Haydovchilik guvohnomasi bor"
                    : $"Haydovchilik guvohnomasi: {anketa.DriverLicenseCategories.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
