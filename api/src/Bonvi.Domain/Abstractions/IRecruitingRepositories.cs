using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Voronka bosqichlari uchun saqlash porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega umumiy <see cref="IReferenceRepository{TEntity}"/> yetmaydi.</b>
/// U ro'yxatni DOIM alifbo bo'yicha qaytaradi va bu barcha ma'lumotnomalar
/// uchun to'g'ri. Voronkada esa tartib — ma'lumotning o'zi
/// (<see cref="PipelineStage.SortOrder"/> izohiga qarang), shu sababli
/// ro'yxat qayta ta'riflanadi. Yangi bosqichni oxiriga qo'shish uchun esa
/// mavjud eng katta tartibni bilish kerak.
/// </para>
/// </remarks>
public interface IPipelineStageRepository : IReferenceRepository<PipelineStage>
{
    /// <summary>Eng katta tartib raqami. Bironta bosqich bo'lmasa — <c>null</c>.</summary>
    /// <remarks>
    /// Faolsizlantirilganlar ham hisobga olinadi: ular ro'yxatda turibdi va
    /// yangi bosqich ularning ustiga tushib qolmasligi kerak.
    /// </remarks>
    Task<int?> MaxSortOrderAsync(CancellationToken cancellationToken = default);

    /// <summary>Berilgan identifikatorlarga mos bosqichlar (qayta tartiblash uchun).</summary>
    Task<IReadOnlyList<PipelineStage>> GetManyAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}

/// <summary>Vakansiyalar uchun saqlash porti.</summary>
/// <remarks>
/// Ro'yxat ekrani bu portdan EMAS, <c>IRecruitingReadModel</c> dan keladi
/// (u nomlar bilan birlashtiradi va sahifalaydi) — <c>IIncentiveRepository</c>
/// va <c>IIncentiveReadModel</c> orasidagi bo'linish bilan bir xil.
/// </remarks>
public interface IVacancyRepository : IRepository<Vacancy>
{
    /// <summary>Shu NOM bo'yicha OCHIQ vakansiya bormi (takrorni oldini olish).</summary>
    /// <remarks>
    /// <para>
    /// Bir xil nom uchun ikkinchi ochiq vakansiya odatda kiritish xatosi:
    /// nomzodlar ikkiga bo'linib ketadi va voronka hisoboti ikki qatorga
    /// yoyiladi. Bu qat'iy TAQIQ emas, use-case darajasidagi ogohlantirish
    /// uchun ishlatiladi.
    /// </para>
    /// <para>
    /// Ilgari tekshiruv «lavozim + firma» juftligi bo'yicha edi. Ikkalasi
    /// ham vakansiyadan olib tashlangach (sababi <see cref="Vacancy"/>
    /// izohida), takrorning yagona belgisi NOM bo'lib qoldi — bu esa
    /// aslida to'g'riroq: bir xil e'lonni ikki marta ochish xato,
    /// e'lonni ikki xil firmaga bog'lash esa endi umuman ma'noga ega emas.
    /// </para>
    /// </remarks>
    Task<bool> HasOpenAsync(
        Guid vacancyTitleId,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Nomzodlar uchun saqlash porti.</summary>
public interface ICandidateRepository : IRepository<Candidate>
{
    /// <summary>Bosqichlar tarixi bilan birga yuklaydi.</summary>
    /// <remarks>
    /// Tarix DOIM kerak emas: ro'yxat ekrani uni umuman o'qimaydi. Alohida
    /// metod — <c>IEmploymentRepository.GetWithEventsAsync</c> bilan bir xil
    /// sabab: har bir o'qishda o'nlab bola yozuvni tortish ro'yxatni
    /// sekinlashtirardi.
    /// </remarks>
    Task<Candidate?> GetWithStagesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Shu bosqichda turgan FAOL nomzodlar soni.</summary>
    /// <remarks>
    /// Bosqichni faolsizlantirishdan oldin ogohlantirish uchun: bosqich
    /// yo'qolmaydi (ma'lumotnoma yozuvi hech qachon o'chirilmaydi), lekin
    /// HR unda odam turganini bilishi kerak.
    /// </remarks>
    Task<int> CountActiveOnStageAsync(Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>Vakansiya bo'yicha nomzodlar soni (jami va ishga olinganlar).</summary>
    Task<(int Total, int Hired)> CountByVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default);
}

/// <summary>Ish takliflari uchun saqlash porti.</summary>
public interface IJobOfferRepository : IRepository<JobOffer>
{
    /// <summary>Nomzodning barcha takliflari — yangidan eskiga.</summary>
    Task<IReadOnlyList<JobOffer>> GetByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default);

    /// <summary>Nomzodning javob kutayotgan taklifi. Yo'q bo'lsa — <c>null</c>.</summary>
    /// <remarks>
    /// Bir vaqtda faqat BITTA faol taklif bo'lishi kerak: ikkita ochiq
    /// taklif ikki xil oklad va’da qilingan degani va qaysi biri kuchda
    /// ekani noaniq bo'lardi.
    /// </remarks>
    Task<JobOffer?> GetPendingByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default);
}
