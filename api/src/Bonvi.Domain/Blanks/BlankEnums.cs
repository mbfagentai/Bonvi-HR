namespace Bonvi.Domain.Blanks;

/*
  Blankalar moduli sanoqlari (docs/SPEC-blankalar.md §3). Hammasi `short` asosida —
  bazada `smallint` (SPEC jadvallari shunday belgilagan). Raqamlar bazada va
  shablon JSON'ida yashaydi: mavjud qiymatning raqami HECH QACHON o'zgartirilmaydi.
*/

/// <summary>Blanka kim haqida to'ldiriladi.</summary>
public enum BlankSubjectKind : short
{
    /// <summary>Xodim (<c>employee_id</c> majburiy).</summary>
    Employee = 1,

    /// <summary>Nomzod (<c>candidate_id</c> majburiy) — 2-bosqich (F03, F05).</summary>
    Candidate = 2,

    /// <summary>Hech kim — blanka bir odamga bog'lanmaydi.</summary>
    None = 3,
}

/// <summary>Shablon versiyasining holati.</summary>
public enum BlankTemplateVersionStatus : short
{
    /// <summary>Qoralama — hali ishlatilmaydi (3-bosqich: konstruktor).</summary>
    Draft = 1,

    /// <summary>E'lon qilingan — yangi blankalar shu versiya bilan ochiladi. Shablonda bittadan ko'p emas.</summary>
    Published = 2,

    /// <summary>Eskirgan — yangisi chiqqan; eski blankalar o'z versiyasi bilan o'qiladi.</summary>
    Retired = 3,
}

/// <summary>Shablon versiyasi qayerdan kelgan.</summary>
public enum BlankTemplateSource : short
{
    /// <summary>Koddagi JSON fayl (seeder).</summary>
    Seed = 1,

    /// <summary>Konstruktor (3-bosqich).</summary>
    Builder = 2,

    /// <summary>Yuklangan Word/PDF'dan (3-bosqich).</summary>
    Imported = 3,
}

/// <summary>To'ldirilgan blankaning holati (SPEC §5).</summary>
public enum BlankSubmissionStatus : short
{
    /// <summary>Qoralama — HR to'ldirmoqda.</summary>
    Draft = 1,

    /// <summary>Kelishuvda — imzo qadamlari ochilgan.</summary>
    InApproval = 2,

    /// <summary>Tasdiqlangan — hamma qadam imzolandi (yakuniy).</summary>
    Approved = 3,

    /// <summary>Rad etilgan (yakuniy).</summary>
    Rejected = 4,

    /// <summary>Bekor qilingan (yakuniy).</summary>
    Cancelled = 5,
}

/// <summary>Qadam qanday imzolanishi mumkin (shablondagi <c>mode</c>).</summary>
public enum BlankStepMode : short
{
    /// <summary>Faqat panelda tugma bilan.</summary>
    Electronic = 1,

    /// <summary>Faqat HR qurilmasida qo'lda chizilgan imzo bilan.</summary>
    InPerson = 2,

    /// <summary>Ikkalasi ham.</summary>
    Either = 3,
}

/// <summary>Qadamning holati.</summary>
public enum BlankStepState : short
{
    /// <summary>Oldingi guruh hali yopilmagan.</summary>
    Waiting = 1,

    /// <summary>Ochiq — imzo kutilmoqda.</summary>
    Open = 2,

    /// <summary>Imzolangan.</summary>
    /// <remarks>
    /// Nom SPEC'dagidek (§3.4) va API/web/AI'da shu so'z bilan yuradi — «signed» bu yerda
    /// imzo, sonning ishorasi emas, ya'ni analizator ogohlantirishi o'rinsiz.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "«Signed» — imzolangan; SPEC-blankalar §3.4 holat nomi.")]
    Signed = 3,

    /// <summary>Shart (<c>when</c>) bajarilmagan — qadam o'tkazib yuborildi.</summary>
    Skipped = 4,

    /// <summary>Shu qadamda rad etildi.</summary>
    Rejected = 5,
}

/// <summary>Qadam AMALDA qanday imzolangan.</summary>
public enum BlankSignMethod : short
{
    /// <summary>Panelda tugma bilan.</summary>
    Electronic = 1,

    /// <summary>HR qurilmasida qo'lda chizilgan imzo bilan.</summary>
    InPerson = 2,
}

/// <summary>Biriktirilgan fayl turi — mijoz aytgani emas, tarkibidan aniqlangani (SPEC §8).</summary>
public enum BlankAttachmentKind : short
{
    Pdf = 1,
    Jpeg = 2,
    Png = 3,
    Webp = 4,
    Docx = 5,
    Xlsx = 6,
    Pptx = 7,
}
