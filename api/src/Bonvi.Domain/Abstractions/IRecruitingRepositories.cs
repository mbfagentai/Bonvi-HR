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
    /// <summary>Shu NOMLI vakansiya bormi (takrorni oldini olish).</summary>
    /// <remarks>
    /// <para>
    /// Nom registrga va bo'shliqlarga sezgir EMAS: «Сотувчи» va
    /// «сотувчи  » — bitta nom (<c>ReferenceRepository.NameExistsAsync</c>
    /// dagi qoida — nom ilgari o'sha ma'lumotnomada yashardi).
    /// </para>
    /// <para>
    /// <paramref name="openOnly"/> — faqat OCHIQ vakansiyalar orasidan
    /// izlash. Ochish va tahrirlashda <c>false</c>: yopilgan vakansiya
    /// ham nomni band qiladi, chunki uni QAYTA OCHISH kerak — yangisi
    /// nomzodlar tarixini ikkiga bo'lardi (<c>Vacancy.Reopen</c> izohi).
    /// Qayta ochishda esa <c>true</c>: ko'chirishdan qolgan bir xil nomli
    /// yopiq vakansiyalar bir-birini to'smasligi kerak, faqat ikkitasi
    /// BIR VAQTDA ochiq turmasin.
    /// </para>
    /// </remarks>
    Task<bool> TitleTakenAsync(
        string title,
        Guid? exceptId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);
}

/// <summary>Nomzodlar uchun saqlash porti.</summary>
public interface ICandidateRepository : IRepository<Candidate>
{
    /// <summary>
    /// Nomzodni BUTUN kartasi bilan yuklaydi: bosqichlar tarixi, fayllari,
    /// izohlari va anketasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tarix DOIM kerak emas: ro'yxat ekrani uni umuman o'qimaydi. Alohida
    /// metod — <c>IEmploymentRepository.GetWithEventsAsync</c> bilan bir xil
    /// sabab: har bir o'qishda o'nlab bola yozuvni tortish ro'yxatni
    /// sekinlashtirardi.
    /// </para>
    /// <para>
    /// Anketa, fayllar va izohlar ham SHU metodda keladi, ularga alohida
    /// yo'l ochilmaydi: hammasi faqat nomzod kartasida ko'rinadi va karta
    /// baribir shu yerdan yuklanadi. Ikkinchi metod bo'lganida chaqiruvchi
    /// hammasini eslab qolishi kerak bo'lardi — va biri unutilganda
    /// ma'lumot XATOSIZ yo'qolib qolardi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Nomi ATAYLAB o'zgartirilmadi</b> (masalan <c>GetFullAsync</c>
    /// bo'lmadi): metod o'ndan ortiq joydan chaqiriladi va qayta nomlash
    /// xatti-harakatga hech narsa qo'shmasdi. Nima yuklanishini shu izoh
    /// aytadi.
    /// </para>
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
