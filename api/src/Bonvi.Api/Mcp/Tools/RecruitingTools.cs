using System.ComponentModel;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>Rekruting — vakansiyalar, nomzodlar va voronka (faqat o'qish).</summary>
/// <remarks>
/// <para>
/// Hamma so'rov PROYEKSIYA bilan: <c>candidates</c> jadvalida <c>photo_key</c> va
/// <c>telegram_chat_id</c> ochilmagan, butun entity o'qilsa PostgreSQL rad etadi.
/// <c>candidate_files</c> ga umuman tegilmaydi.
/// </para>
/// <para>
/// Nomzod va anketa matnlari ommaviy Telegram botdan keladi — chiqishda ular
/// faqat MA'LUMOT (server ko'rsatmasi buni AI'ga aytadi).
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class RecruitingTools(McpToolContext t)
{
    private const string Area = "Rekruting";

    /// <summary><c>ILIKE</c> ekranlash belgisi — <see cref="Like"/> shu bilan ekranlaydi.</summary>
    private const string Escape = "\\";

    /// <summary>Matnli qidiruvda ko'pi bilan nechta so'z hisobga olinadi.</summary>
    private const int MaxSearchTokens = 6;

    // ─────────────────────────────────────────────────────────────────
    //  vacancies
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "vacancies", Title = "Vakansiyalar",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Vakansiyalar ro'yxati: nomi, o'rinlar soni, ochilgan/yopilgan sana, izoh, nomzodlar soni holat bo'yicha "
        + "va hozir voronkada turganlar bosqich bo'yicha, ishga olinganlar soni. Sukut bo'yicha faqat ochiq vakansiyalar. "
        + "Vakansiya identifikatori search_candidates va recruiting_funnel uchun kerak bo'ladi. "
        + "Lists vacancies with openings and candidate counts per status and per current pipeline stage.")]
    public async Task<string> Vacancies(
        [Description("Holat: Open (sukut), Closed yoki all.")] string? status = null,
        [Description("Nomi bo'yicha qidiruv (qismi yetarli, katta-kichik harf farqsiz).")] string? title = null,
        [Description("Sahifa raqami (1 dan).")] int? page = null,
        [Description("Sahifa hajmi (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.RecruitingView, Area);

        var db = t.Db;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var query = db.Vacancies.AsQueryable();

        var statusText = string.IsNullOrWhiteSpace(status) ? "Open" : status.Trim();

        if (!statusText.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var wanted = ParseEnum<VacancyStatus>(statusText, "status")!.Value;
            query = query.Where(vacancy => vacancy.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var pattern = Like(title);
            query = query.Where(vacancy => EF.Functions.ILike(vacancy.Title, pattern, Escape));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(vacancy => vacancy.OpenedAt)
            .ThenBy(vacancy => vacancy.Title)
            .ThenBy(vacancy => vacancy.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(vacancy => new
            {
                vacancy.Id,
                vacancy.Title,
                vacancy.Openings,
                vacancy.Status,
                vacancy.OpenedAt,
                vacancy.ClosedAt,
                vacancy.Note,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = rows.Select(row => row.Id).ToList();

        var byStatus = await db.Candidates
            .Where(candidate => ids.Contains(candidate.VacancyId))
            .GroupBy(candidate => new { candidate.VacancyId, candidate.Status })
            .Select(group => new { group.Key.VacancyId, group.Key.Status, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bosqich bo'yicha — faqat voronkada TURGANLAR (InProgress): yopilgan
        // nomzodning «joriy bosqichi» u chiqib ketgan joy, hozirgi ish emas.
        var byStage = await db.Candidates
            .Where(candidate => ids.Contains(candidate.VacancyId) && candidate.Status == CandidateStatus.InProgress)
            .GroupBy(candidate => new { candidate.VacancyId, candidate.CurrentStageId })
            .Select(group => new { group.Key.VacancyId, group.Key.CurrentStageId, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stages = await StagesAsync(cancellationToken).ConfigureAwait(false);

        var items = rows.Select(row =>
        {
            var statuses = byStatus.Where(item => item.VacancyId == row.Id).ToList();

            return new
            {
                id = row.Id,
                title = row.Title,
                status = row.Status,
                openings = row.Openings,
                openedAt = McpToolContext.Iso(row.OpenedAt),
                closedAt = McpToolContext.Iso(row.ClosedAt),
                note = row.Note,
                candidates = statuses.Sum(item => item.Count),
                hired = statuses.Where(item => item.Status == CandidateStatus.Hired).Sum(item => item.Count),
                byStatus = statuses.ToDictionary(item => item.Status.ToString(), item => item.Count),
                inProgressByStage = byStage
                    .Where(item => item.VacancyId == row.Id)
                    .OrderBy(item => StageOrder(stages, item.CurrentStageId))
                    .Select(item => new { stage = StageName(stages, item.CurrentStageId), count = item.Count }),
            };
        }).ToList();

        return t.Json(
            new
            {
                total,
                page = pageNo,
                pageSize = size,
                status = statusText,
                items,
            },
            items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  search_candidates
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "search_candidates", Title = "Nomzodlarni qidirish",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Nomzodlarni qidirish va saralash: vakansiya, holat (masalan Reserve — rezerv), joriy bosqich, matn "
        + "(ism, telefon, mutaxassislik, izoh, manzil, anketadagi ish tajribasi/tillar/ko'nikmalar), kelgan sana, manba, yosh, "
        + "hamda bot anketasi bo'yicha: ma'lumoti, rus tili darajasi, boshqa tillar, haydovchilik guvohnomasi (toifasi), "
        + "kutilayotgan maosh, ish haqida qayerdan bilgani. «Rezervda X vakansiyaga mos / Y tilini biladigan odam bormi?» "
        + "kabi savollar uchun asosiy vosita. Har qatorda anketaning qisqa mazmuni bor; to'liq karta — get_candidate. "
        + "Search candidates by vacancy, status, stage, text, questionnaire answers (education, languages, licence, salary), age; paged.")]
    public async Task<string> SearchCandidates(
        [Description("Vakansiya: identifikator yoki nomining bir qismi.")] string? vacancy = null,
        [Description("Holat: InProgress (jarayonda), Rejected (rad etilgan), Hired (ishga olingan), Withdrawn (o'zi voz kechgan), Reserve (rezerv), Blacklisted (qora ro'yxat — odam qayta olinmaydi).")] string? status = null,
        [Description("Joriy bosqich nomi (qismi yetarli).")] string? stage = null,
        [Description("Matn: ism-familiya, telefon raqami (qismi ham), mutaxassislik, izoh, manzil yoki anketa matnlari. Bir necha so'z — hammasi topilishi kerak.")] string? text = null,
        [Description("Kelgan sana (dan), YYYY-MM-DD.")] string? createdFrom = null,
        [Description("Kelgan sana (gacha), YYYY-MM-DD.")] string? createdTo = null,
        [Description("Manba: Manual (HR kiritgan), TelegramBot (bot anketasi), Excel (2023–2024 kengash arxivi).")] string? source = null,
        [Description("Excel kengash arxivini ham qo'shish (sukut: yo'q; source=Excel bo'lsa avtomatik qo'shiladi).")] bool? includeArchive = null,
        [Description("Anketa bormi: true — faqat anketasi borlar, false — anketasizlar.")] bool? hasQuestionnaire = null,
        [Description("Ma'lumoti (vergul bilan bir nechta): SecondarySchool (o'rta), VocationalSchool (o'rta maxsus), HigherComplete (oliy), HigherIncomplete (oliy, tugallanmagan).")] string? education = null,
        [Description("Rus tili darajasi kamida: Elementary, Intermediate, UpperIntermediate, Advanced, Native.")] string? minRussianLevel = null,
        [Description("Anketadagi «boshqa tillar» matnida qidiruv (masalan «ingliz», «english», «турк»).")] string? otherLanguages = null,
        [Description("Haydovchilik guvohnomasi bormi.")] bool? hasDriverLicense = null,
        [Description("Guvohnoma toifasi (masalan B yoki C).")] string? driverLicenseCategory = null,
        [Description("Kutilayotgan maosh oralig'i (vergul bilan): From1To2Million, From2To3Million, From3To4Million, From5To7Million.")] string? expectedSalary = null,
        [Description("Ish haqida qayerdan bilgan (vergul bilan): Acquaintance (tanishlar), SocialNetwork (Instagram/Facebook), Olx, HeadHunter (hh.uz), Telegram, Other.")] string? referralSource = null,
        [Description("Yoshi kamida (tug'ilgan sanasi yo'q nomzodlar yosh filtrida chiqmaydi).")] int? minAge = null,
        [Description("Yoshi ko'pi bilan.")] int? maxAge = null,
        [Description("Saralash: created (sukut, yangilari birinchi), name, nextContact (keyingi aloqa sanasi), interview (suhbat vaqti).")] string? sort = null,
        [Description("Sahifa raqami (1 dan).")] int? page = null,
        [Description("Sahifa hajmi (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.RecruitingView, Area);

        var db = t.Db;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);
        var notes = new List<string>();

        var query = db.Candidates.AsQueryable();

        var vacancies = await ResolveVacanciesAsync(vacancy, cancellationToken).ConfigureAwait(false);

        if (vacancies is not null)
        {
            var vacancyIds = vacancies.Keys.ToList();
            query = query.Where(candidate => vacancyIds.Contains(candidate.VacancyId));
        }

        if (ParseEnum<CandidateStatus>(status, "status") is { } statusValue)
        {
            query = query.Where(candidate => candidate.Status == statusValue);
        }

        var stages = await StagesAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stage))
        {
            var stageIds = MatchStages(stages, stage);
            query = query.Where(candidate => candidate.CurrentStageId != null && stageIds.Contains(candidate.CurrentStageId.Value));
        }

        var sourceValue = ParseEnum<CandidateSource>(source, "source");

        if (sourceValue is { } wantedSource)
        {
            query = query.Where(candidate => candidate.Source == wantedSource);
        }
        else if (includeArchive != true)
        {
            query = query.Where(candidate => candidate.Source != CandidateSource.Excel);
            notes.Add("Excel kengash arxivi (2023–2024) sukut bo'yicha chiqarilmagan — kerak bo'lsa includeArchive=true.");
        }

        query = ApplyText(query, text);

        if (createdFrom is not null || createdTo is not null)
        {
            var (from, to) = McpToolContext.Range(createdFrom, createdTo, defaultDays: McpToolContext.MaxRangeDays);
            var fromUtc = StartOfDayUtc(from);
            var toUtc = StartOfDayUtc(to.AddDays(1));
            query = query.Where(candidate => candidate.CreatedAt >= fromUtc && candidate.CreatedAt < toUtc);

            if (sourceValue == CandidateSource.Excel || includeArchive == true)
            {
                notes.Add("Excel arxivida «kelgan sana» — ko'chirilgan kun, ariza kuni emas.");
            }
        }

        if (hasQuestionnaire is { } withForm)
        {
            query = withForm
                ? query.Where(candidate => candidate.Questionnaire != null)
                : query.Where(candidate => candidate.Questionnaire == null);
        }

        if (ParseEnumList<EducationLevel>(education, "education") is { } levels)
        {
            query = query.Where(candidate => candidate.Questionnaire != null && levels.Contains(candidate.Questionnaire.Education));
        }

        if (ParseEnum<LanguageLevel>(minRussianLevel, "minRussianLevel") is { } russian)
        {
            query = query.Where(candidate => candidate.Questionnaire != null && candidate.Questionnaire.RussianLevel >= russian);
        }

        if (!string.IsNullOrWhiteSpace(otherLanguages))
        {
            var pattern = Like(otherLanguages);
            query = query.Where(candidate => candidate.Questionnaire != null
                && EF.Functions.ILike(candidate.Questionnaire.OtherLanguages!, pattern, Escape));
        }

        if (hasDriverLicense is { } licence)
        {
            query = query.Where(candidate => candidate.Questionnaire != null && candidate.Questionnaire.HasDriverLicense == licence);
        }

        if (!string.IsNullOrWhiteSpace(driverLicenseCategory))
        {
            var pattern = Like(driverLicenseCategory);
            query = query.Where(candidate => candidate.Questionnaire != null
                && candidate.Questionnaire.HasDriverLicense
                && EF.Functions.ILike(candidate.Questionnaire.DriverLicenseCategories!, pattern, Escape));
        }

        if (ParseEnumList<SalaryExpectation>(expectedSalary, "expectedSalary") is { } salaries)
        {
            query = query.Where(candidate => candidate.Questionnaire != null && salaries.Contains(candidate.Questionnaire.ExpectedSalary));
        }

        if (ParseEnumList<ReferralSource>(referralSource, "referralSource") is { } referrals)
        {
            query = query.Where(candidate => candidate.Questionnaire != null && referrals.Contains(candidate.Questionnaire.ReferralSource));
        }

        // Yosh — tug'ilgan sana chegarasiga aylanadi (indeksga tushadigan oddiy taqqoslash).
        var today = McpToolContext.Today;

        if (minAge is { } youngest)
        {
            var latestBirth = today.AddYears(-youngest);
            query = query.Where(candidate => candidate.BirthDate != null && candidate.BirthDate <= latestBirth);
        }

        if (maxAge is { } oldest)
        {
            var earliestBirth = today.AddYears(-(oldest + 1)).AddDays(1);
            query = query.Where(candidate => candidate.BirthDate != null && candidate.BirthDate >= earliestBirth);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var sortKey = string.IsNullOrWhiteSpace(sort) ? "CREATED" : sort.Trim().ToUpperInvariant();

        var ordered = sortKey switch
        {
            "NAME" => query.OrderBy(candidate => candidate.FullName.LastName).ThenBy(candidate => candidate.FullName.FirstName),

            // Sanasi yo'q qator DOIM oxirida (aks holda PostgreSQL NULL'ni boshiga qo'yishi mumkin).
            "NEXTCONTACT" => query.OrderBy(candidate => candidate.NextContactDate == null).ThenBy(candidate => candidate.NextContactDate),
            "INTERVIEW" => query.OrderBy(candidate => candidate.InterviewAt == null).ThenBy(candidate => candidate.InterviewAt),
            "CREATED" => query.OrderByDescending(candidate => candidate.CreatedAt),
            _ => throw new McpException("«sort» qiymati: created, name, nextContact yoki interview."),
        };

        var rows = await ordered
            .ThenByDescending(candidate => candidate.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.FullName.LastName,
                candidate.FullName.FirstName,
                candidate.FullName.MiddleName,
                candidate.BirthDate,
                candidate.Phone,
                candidate.Speciality,
                candidate.VacancyId,
                candidate.CurrentStageId,
                candidate.Status,
                candidate.Source,
                candidate.CreatedAt,
                candidate.NextContactDate,
                candidate.InterviewAt,
                candidate.ClosedAt,
                Form = candidate.Questionnaire == null
                    ? null
                    : new
                    {
                        candidate.Questionnaire.Education,
                        candidate.Questionnaire.RussianLevel,
                        candidate.Questionnaire.OtherLanguages,
                        candidate.Questionnaire.HasDriverLicense,
                        candidate.Questionnaire.DriverLicenseCategories,
                        candidate.Questionnaire.ExpectedSalary,
                        candidate.Questionnaire.ReferralSource,
                        candidate.Questionnaire.WorkExperience,
                    },
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vacancyTitles = await VacancyTitlesAsync(rows.Select(row => row.VacancyId), cancellationToken).ConfigureAwait(false);

        var items = rows.Select(row => new
        {
            id = row.Id,
            name = FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
            age = McpLookups.Age(row.BirthDate),
            phone = McpLookups.Phone(row.Phone),
            speciality = row.Speciality,
            vacancy = vacancyTitles.Name(row.VacancyId),
            status = row.Status,
            stage = StageName(stages, row.CurrentStageId),
            source = row.Source,
            createdAt = McpToolContext.Iso(row.CreatedAt.TashkentDate()),
            nextContactDate = McpToolContext.Iso(row.NextContactDate),
            interviewAt = McpToolContext.Local(row.InterviewAt),
            closedAt = McpToolContext.Iso(row.ClosedAt),
            questionnaire = row.Form is null
                ? null
                : new
                {
                    education = row.Form.Education,
                    russianLevel = row.Form.RussianLevel,
                    otherLanguages = row.Form.OtherLanguages,
                    driverLicense = row.Form.HasDriverLicense ? row.Form.DriverLicenseCategories ?? "bor" : null,
                    expectedSalary = row.Form.ExpectedSalary,
                    referralSource = row.Form.ReferralSource,
                    workExperience = Shorten(row.Form.WorkExperience, 200),
                },
        }).ToList();

        return t.Json(
            new
            {
                total,
                page = pageNo,
                pageSize = size,
                vacancies = vacancies?.Values,
                notes = notes.Count == 0 ? null : notes,
                items,
            },
            items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  get_candidate
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_candidate", Title = "Nomzod kartasi",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Bitta nomzodning to'liq kartasi: shaxsiy ma'lumot (ism, tug'ilgan sana, yosh, telefon, manzil, mutaxassislik, "
        + "izoh, manba), vakansiya, holat va joriy bosqich, bosqichlar tarixi (sana, xulosa, kim yozgan), amallar izohlari, "
        + "bot anketasining barcha javoblari, suhbat vaqti, keyingi aloqa sanasi, yopilish sanasi va sababi; ishga olingan "
        + "bo'lsa — xodim identifikatori (get_employee uchun); shu telefon bilan kelgan boshqa arizalar. Fayl va rasm berilmaydi. "
        + "Full candidate card: personal data, stage history, notes, questionnaire, hire link.")]
    public async Task<string> GetCandidate(
        [Description("Nomzod identifikatori (search_candidates natijasidagi id).")] string id,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.RecruitingView, Area);

        if (!Guid.TryParse(id?.Trim(), out var candidateId))
        {
            throw new McpException("«id» — nomzod identifikatori (GUID) bo'lishi kerak.");
        }

        var db = t.Db;

        var card = await db.Candidates
            .Where(candidate => candidate.Id == candidateId)
            .Select(candidate => new
            {
                candidate.FullName.LastName,
                candidate.FullName.FirstName,
                candidate.FullName.MiddleName,
                candidate.BirthDate,
                candidate.Phone,
                candidate.Address,
                candidate.Speciality,
                candidate.Note,
                candidate.Source,
                candidate.TelegramLanguage,
                candidate.VacancyId,
                candidate.Status,
                candidate.CurrentStageId,
                candidate.CreatedAt,
                candidate.CreatedBy,
                candidate.InterviewAt,
                candidate.InterviewNoticeSentAt,
                candidate.NextContactDate,
                candidate.ClosedAt,
                candidate.ClosingNote,
                candidate.RejectionNoticeSentAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new McpException("Nomzod topilmadi (o'chirilgan bo'lishi yoki identifikator noto'g'ri bo'lishi mumkin).");

        var vacancy = await db.Vacancies.IgnoreQueryFilters()
            .Where(item => item.Id == card.VacancyId)
            .Select(item => new { item.Id, item.Title, item.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var stages = await StagesAsync(cancellationToken).ConfigureAwait(false);

        var history = await db.CandidateStageEntries
            .Where(entry => entry.CandidateId == candidateId)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.RecordedAt)
            .Select(entry => new { entry.StageId, entry.Date, entry.Conclusion, entry.RecordedBy, entry.RecordedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var notes = await db.CandidateNotes
            .Where(note => note.CandidateId == candidateId)
            .OrderBy(note => note.CreatedAt)
            .Select(note => new { note.Kind, note.Text, note.CreatedAt, note.CreatedBy })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var form = await db.CandidateQuestionnaires
            .Where(item => item.CandidateId == candidateId)
            .Select(item => new
            {
                item.Education,
                item.WorkExperience,
                item.MaritalStatus,
                item.RussianLevel,
                item.OtherLanguages,
                item.OfficeSkills,
                item.LastSalary,
                item.ExtraNote,
                item.HasDriverLicense,
                item.DriverLicenseCategories,
                item.ExpectedSalary,
                item.ReferralSource,
                item.OfferAcceptedAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Nomzod → xodim havolasi ishga qabul HODISASIDA saqlanadi (EmploymentEvent.CandidateId).
        var hire = await (
                from item in db.EmploymentEvents
                join employment in db.Employments.IgnoreQueryFilters() on item.EmploymentId equals employment.Id
                where item.Type == EmploymentEventType.Hire && item.CandidateId == candidateId
                orderby item.EffectiveDate
                select new { employment.EmployeeId, item.EffectiveDate })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Shu odamning boshqa arizalari — telefon AYNAN mos (konvertor parametrga ham qo'llanadi).
        var phone = card.Phone;
        var otherApplications = phone is null
            ? []
            : await db.Candidates
                .Where(other => other.Id != candidateId && other.Phone == phone)
                .OrderByDescending(other => other.CreatedAt)
                .Take(20)
                .Select(other => new { other.Id, other.VacancyId, other.Status, other.CreatedAt, other.ClosedAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var vacancyTitles = await VacancyTitlesAsync(otherApplications.Select(item => item.VacancyId), cancellationToken).ConfigureAwait(false);

        var userIds = history.Select(entry => entry.RecordedBy)
            .Concat(notes.Select(note => note.CreatedBy))
            .Append(card.CreatedBy)
            .OfType<Guid>();

        var users = await t.UserNamesAsync(userIds, cancellationToken).ConfigureAwait(false);

        return t.Json(
            new
            {
                id = candidateId,
                name = FullName.FromTrusted(card.LastName, card.FirstName, card.MiddleName).Full,
                birthDate = McpToolContext.Iso(card.BirthDate),
                age = McpLookups.Age(card.BirthDate),
                phone = McpLookups.Phone(card.Phone),
                address = card.Address,
                speciality = card.Speciality,
                note = card.Note,
                source = card.Source,
                telegramLanguage = card.TelegramLanguage,
                vacancy = vacancy is null ? null : new { id = vacancy.Id, title = vacancy.Title, status = vacancy.Status },
                status = card.Status,
                currentStage = StageName(stages, card.CurrentStageId),
                createdAt = McpToolContext.Local(card.CreatedAt),
                createdBy = users.Name(card.CreatedBy),
                interviewAt = McpToolContext.Local(card.InterviewAt),
                interviewNoticeSentAt = McpToolContext.Local(card.InterviewNoticeSentAt),
                nextContactDate = McpToolContext.Iso(card.NextContactDate),
                closedAt = McpToolContext.Iso(card.ClosedAt),
                closingNote = card.ClosingNote,
                rejectionNoticeSentAt = McpToolContext.Local(card.RejectionNoticeSentAt),
                hiredEmployee = hire is null ? null : new { employeeId = hire.EmployeeId, hireDate = McpToolContext.Iso(hire.EffectiveDate) },
                stageHistory = history.Select(entry => new
                {
                    stage = StageName(stages, entry.StageId),
                    date = McpToolContext.Iso(entry.Date),
                    conclusion = entry.Conclusion,
                    recordedBy = users.Name(entry.RecordedBy),
                    recordedAt = McpToolContext.Local(entry.RecordedAt),
                }),
                notes = notes.Select(note => new
                {
                    kind = note.Kind,
                    text = note.Text,
                    createdAt = McpToolContext.Local(note.CreatedAt),
                    author = users.Name(note.CreatedBy),
                }),
                questionnaire = form is null
                    ? null
                    : new
                    {
                        education = form.Education,
                        workExperience = form.WorkExperience,
                        maritalStatus = form.MaritalStatus,
                        russianLevel = form.RussianLevel,
                        otherLanguages = form.OtherLanguages,
                        officeSkills = form.OfficeSkills,
                        lastSalary = form.LastSalary,
                        extraNote = form.ExtraNote,
                        hasDriverLicense = form.HasDriverLicense,
                        driverLicenseCategories = form.DriverLicenseCategories,
                        expectedSalary = form.ExpectedSalary,
                        referralSource = form.ReferralSource,
                        offerAcceptedAt = McpToolContext.Local(form.OfferAcceptedAt),
                    },
                otherApplications = otherApplications.Count == 0
                    ? null
                    : otherApplications.Select(item => new
                    {
                        id = item.Id,
                        vacancy = vacancyTitles.Name(item.VacancyId),
                        status = item.Status,
                        createdAt = McpToolContext.Iso(item.CreatedAt.TashkentDate()),
                        closedAt = McpToolContext.Iso(item.ClosedAt),
                    }),
            },
            1);
    }

    // ─────────────────────────────────────────────────────────────────
    //  recruiting_funnel
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "recruiting_funnel", Title = "Rekruting voronkasi",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Davrda kelgan arizalar (nomzodlar) bo'yicha voronka: har bosqichga yetganlar va hozir turganlar (bosqich tartibida), "
        + "holatlar, manbalar, botda «qayerdan bildi» javoblari, ishga olish konversiyasi va ishga olishgacha o'tgan kunlar "
        + "(mediana). Vakansiya berilmasa — vakansiyalar kesimi ham. Sukut: oxirgi 90 kun. "
        + "Recruiting funnel for a period/vacancy: stage reach, statuses, sources, conversion, median days to hire.")]
    public async Task<string> RecruitingFunnel(
        [Description("Vakansiya: identifikator yoki nomining bir qismi (ixtiyoriy).")] string? vacancy = null,
        [Description("Ariza kelgan sana (dan), YYYY-MM-DD. Sukut: 90 kun oldin.")] string? from = null,
        [Description("Ariza kelgan sana (gacha), YYYY-MM-DD. Sukut: bugun.")] string? to = null,
        [Description("Excel kengash arxivini qo'shish (sukut: yo'q — arxivda «kelgan sana» ko'chirilgan kun).")] bool? includeArchive = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.RecruitingView, Area);

        var db = t.Db;
        var (start, end) = McpToolContext.Range(from, to, defaultDays: 90);
        var fromUtc = StartOfDayUtc(start);
        var toUtc = StartOfDayUtc(end.AddDays(1));

        var cohort = db.Candidates.Where(candidate => candidate.CreatedAt >= fromUtc && candidate.CreatedAt < toUtc);

        var vacancies = await ResolveVacanciesAsync(vacancy, cancellationToken).ConfigureAwait(false);

        if (vacancies is not null)
        {
            var vacancyIds = vacancies.Keys.ToList();
            cohort = cohort.Where(candidate => vacancyIds.Contains(candidate.VacancyId));
        }

        if (includeArchive != true)
        {
            cohort = cohort.Where(candidate => candidate.Source != CandidateSource.Excel);
        }

        var cohortIds = cohort.Select(candidate => candidate.Id);

        var byStatusAndStage = await cohort
            .GroupBy(candidate => new { candidate.Status, candidate.CurrentStageId })
            .Select(group => new { group.Key.Status, group.Key.CurrentStageId, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bySource = await cohort
            .GroupBy(candidate => candidate.Source)
            .Select(group => new { Source = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byReferral = await db.CandidateQuestionnaires
            .Where(form => cohortIds.Contains(form.CandidateId))
            .GroupBy(form => form.ReferralSource)
            .Select(group => new { Referral = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // «Yetgan» — shu bosqichga kamida bir marta yozuvi bor nomzodlar (takrorlar bitta).
        var reached = await db.CandidateStageEntries
            .Where(entry => cohortIds.Contains(entry.CandidateId))
            .GroupBy(entry => entry.StageId)
            .Select(group => new { StageId = group.Key, Count = group.Select(entry => entry.CandidateId).Distinct().Count() })
            .ToDictionaryAsync(item => item.StageId, item => item.Count, cancellationToken)
            .ConfigureAwait(false);

        var hired = await cohort
            .Where(candidate => candidate.Status == CandidateStatus.Hired)
            .Select(candidate => new { candidate.Id, candidate.CreatedAt, candidate.ClosedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hiredIds = hired.Select(item => item.Id).ToList();

        var hireDates = hiredIds.Count == 0
            ? []
            : (await db.EmploymentEvents
                    .Where(item => item.Type == EmploymentEventType.Hire
                        && item.CandidateId != null
                        && hiredIds.Contains(item.CandidateId.Value))
                    .Select(item => new { CandidateId = item.CandidateId!.Value, item.EffectiveDate })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .GroupBy(item => item.CandidateId)
                .ToDictionary(group => group.Key, group => group.Min(item => item.EffectiveDate));

        var daysToHire = hired
            .Select(item =>
            {
                DateOnly? hiredOn = hireDates.TryGetValue(item.Id, out var effective) ? effective : item.ClosedAt;
                return hiredOn is { } date ? date.DayNumber - item.CreatedAt.TashkentDate().DayNumber : (int?)null;
            })
            .OfType<int>()
            .Where(days => days >= 0)
            .Order()
            .ToList();

        var stages = await StagesAsync(cancellationToken).ConfigureAwait(false);

        var total = byStatusAndStage.Sum(item => item.Count);
        var hiredCount = byStatusAndStage.Where(item => item.Status == CandidateStatus.Hired).Sum(item => item.Count);

        var stageRows = stages.Values
            .Where(item => item.IsActive
                || reached.ContainsKey(item.Id)
                || byStatusAndStage.Any(row => row.CurrentStageId == item.Id))
            .OrderBy(item => item.SortOrder)
            .Select(item => new
            {
                stage = item.Name,
                reached = reached.GetValueOrDefault(item.Id),
                inProgressNow = byStatusAndStage
                    .Where(row => row.CurrentStageId == item.Id && row.Status == CandidateStatus.InProgress)
                    .Sum(row => row.Count),
                closedHere = byStatusAndStage
                    .Where(row => row.CurrentStageId == item.Id && row.Status != CandidateStatus.InProgress)
                    .GroupBy(row => row.Status)
                    .ToDictionary(group => group.Key.ToString(), group => group.Sum(row => row.Count)),
            })
            .ToList();

        var noStage = byStatusAndStage.Where(row => row.CurrentStageId == null).Sum(row => row.Count);

        object? perVacancy = null;

        if (vacancies is null)
        {
            var groups = await cohort
                .GroupBy(candidate => candidate.VacancyId)
                .Select(group => new
                {
                    VacancyId = group.Key,
                    Total = group.Count(),
                    InProgress = group.Count(candidate => candidate.Status == CandidateStatus.InProgress),
                    Hired = group.Count(candidate => candidate.Status == CandidateStatus.Hired),
                    Reserve = group.Count(candidate => candidate.Status == CandidateStatus.Reserve),
                })
                .OrderByDescending(group => group.Total)
                .Take(50)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var titles = await VacancyTitlesAsync(groups.Select(group => group.VacancyId), cancellationToken).ConfigureAwait(false);

            perVacancy = groups.Select(group => new
            {
                vacancyId = group.VacancyId,
                vacancy = titles.Name(group.VacancyId),
                total = group.Total,
                inProgress = group.InProgress,
                hired = group.Hired,
                reserve = group.Reserve,
            });
        }

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                vacancies = vacancies?.Values,
                total,
                hired = hiredCount,
                conversionPercent = total == 0 ? (double?)null : Math.Round(hiredCount * 100.0 / total, 1),
                byStatus = byStatusAndStage
                    .GroupBy(row => row.Status)
                    .ToDictionary(group => group.Key.ToString(), group => group.Sum(row => row.Count)),
                bySource = bySource.ToDictionary(item => item.Source.ToString(), item => item.Count),
                byReferralSource = byReferral.Count == 0 ? null : byReferral.ToDictionary(item => item.Referral.ToString(), item => item.Count),
                stages = stageRows,
                withoutStage = noStage == 0 ? (int?)null : noStage,
                daysToHire = daysToHire.Count == 0
                    ? null
                    : new
                    {
                        count = daysToHire.Count,
                        median = Median(daysToHire),
                        min = daysToHire[0],
                        max = daysToHire[^1],
                    },
                perVacancy,
                notes = new[]
                {
                    "Kogorta — arizasi shu davrda KELGAN nomzodlar (created_at, Toshkent sanasi); holati bugungi.",
                    "reached — shu bosqichga kamida bir marta o'tgan nomzodlar (bosqichlar tarixidan); inProgressNow — hozir shu bosqichda, voronkada; closedHere — shu bosqichda turib yopilganlar (rad etilgan / voz kechgan / rezerv / ishga olingan).",
                    "conversionPercent = ishga olinganlar / jami arizalar.",
                    "daysToHire — ariza kelgan kundan ishga qabul buyrug'idagi kuchga kirish sanasigacha (buyruq topilmasa — nomzod yopilgan sana).",
                    includeArchive == true
                        ? "Excel kengash arxivi qo'shilgan: uning «kelgan sanasi» — ko'chirilgan kun."
                        : "Excel kengash arxivi (2023–2024) qo'shilmagan.",
                },
            },
            total);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ─────────────────────────────────────────────────────────────────

    private sealed record StageInfo(Guid Id, string Name, int SortOrder, bool IsActive);

    /// <summary>Hamma bosqichlar (o'chirilgan/faolsizlari ham — tarixdagi nom «?» bo'lmasin).</summary>
    private Task<Dictionary<Guid, StageInfo>> StagesAsync(CancellationToken cancellationToken) =>
        t.Db.PipelineStages.IgnoreQueryFilters()
            .Select(item => new StageInfo(item.Id, item.Name, item.SortOrder, item.IsActive && item.DeletedAt == null))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

    private static string? StageName(Dictionary<Guid, StageInfo> stages, Guid? id) =>
        id is { } key && stages.TryGetValue(key, out var stage) ? stage.Name : null;

    private static int StageOrder(Dictionary<Guid, StageInfo> stages, Guid? id) =>
        id is { } key && stages.TryGetValue(key, out var stage) ? stage.SortOrder : int.MaxValue;

    private static List<Guid> MatchStages(Dictionary<Guid, StageInfo> stages, string stage)
    {
        var wanted = stage.Trim();

        var exact = stages.Values.Where(item => item.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        var matched = exact.Count > 0
            ? exact
            : [.. stages.Values.Where(item => item.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase))];

        if (matched.Count == 0)
        {
            var names = string.Join(", ", stages.Values.Where(item => item.IsActive).OrderBy(item => item.SortOrder).Select(item => item.Name));
            throw new McpException($"«{wanted}» bosqichi topilmadi. Bosqichlar: {names}.");
        }

        return [.. matched.Select(item => item.Id)];
    }

    /// <summary>
    /// Vakansiya: identifikator yoki nom bo'lagi → <c>{ id: nom }</c>; bo'sh bo'lsa <c>null</c> (filtr yo'q).
    /// </summary>
    private async Task<Dictionary<Guid, string>?> ResolveVacanciesAsync(string? vacancy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vacancy))
        {
            return null;
        }

        var db = t.Db;
        Dictionary<Guid, string> found;

        if (Guid.TryParse(vacancy.Trim(), out var id))
        {
            found = await db.Vacancies.IgnoreQueryFilters()
                .Where(item => item.Id == id)
                .Select(item => new { item.Id, item.Title })
                .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var pattern = Like(vacancy);
            found = await db.Vacancies
                .Where(item => EF.Functions.ILike(item.Title, pattern, Escape))
                .OrderBy(item => item.Title)
                .Take(50)
                .Select(item => new { item.Id, item.Title })
                .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken)
                .ConfigureAwait(false);
        }

        return found.Count > 0
            ? found
            : throw new McpException($"«{vacancy.Trim()}» vakansiyasi topilmadi. Ro'yxat uchun vacancies vositasini chaqiring (status=all).");
    }

    private async Task<Dictionary<Guid, string>> VacancyTitlesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        return await t.Db.Vacancies.IgnoreQueryFilters()
            .Where(item => wanted.Contains(item.Id))
            .Select(item => new { item.Id, item.Title })
            .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Matnli qidiruv: har bir so'z ism, mutaxassislik, izoh, manzil yoki anketa
    /// matnlaridan BIRIDA topilishi kerak; raqamli so'z telefon bo'yicha ham.
    /// </summary>
    private static IQueryable<Candidate> ApplyText(IQueryable<Candidate> query, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return query;
        }

        // Butun matn telefon raqamiga o'xshasa (+998 90 123-45-67) — bitta raqamli so'z.
        var compact = new string([.. text.Where(symbol => !char.IsWhiteSpace(symbol) && symbol is not ('-' or '(' or ')' or '+'))]);
        var tokens = compact.Length > 0 && compact.All(char.IsAsciiDigit)
            ? [compact]
            : text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxSearchTokens).ToArray();

        foreach (var token in tokens)
        {
            var pattern = Like(token);
            var digits = new string([.. token.Where(char.IsAsciiDigit)]);

            if (digits.Length >= 3 && digits.Length == token.TrimStart('+').Length)
            {
                /*
                  Telefon `+998XXXXXXXXX` bo'lib saqlanadi — raqamlar bo'lagi ustun ichida qidiriladi.
                  `(string)(object)` — SQL'da `CAST(phone AS text)`: konvertorli qiymat obyektining
                  `ToString()` / `.Value` i tarjima QILINMAYDI, `EF.Property<string>` esa naqshga
                  konvertorni qo'llab yiqiladi (ikkalasi tekshirilgan). Xom SQL ishlatilmaydi.
                */
                var phonePattern = Like(digits);
                query = query.Where(candidate => candidate.Phone != null
                    && EF.Functions.ILike((string)(object)candidate.Phone, phonePattern, Escape));
                continue;
            }

            query = query.Where(candidate =>
                EF.Functions.ILike(
                    candidate.FullName.LastName + " " + candidate.FullName.FirstName + " " + (candidate.FullName.MiddleName ?? string.Empty),
                    pattern,
                    Escape)
                || EF.Functions.ILike(candidate.Speciality!, pattern, Escape)
                || EF.Functions.ILike(candidate.Note!, pattern, Escape)
                || EF.Functions.ILike(candidate.Address!, pattern, Escape)
                || (candidate.Questionnaire != null
                    && (EF.Functions.ILike(candidate.Questionnaire.WorkExperience!, pattern, Escape)
                        || EF.Functions.ILike(candidate.Questionnaire.OtherLanguages!, pattern, Escape)
                        || EF.Functions.ILike(candidate.Questionnaire.OfficeSkills!, pattern, Escape)
                        || EF.Functions.ILike(candidate.Questionnaire.ExtraNote!, pattern, Escape))));
        }

        return query;
    }

    /// <summary>Toshkent kunining boshi — UTC lahza (Npgsql <c>timestamptz</c> uchun nol siljish shart).</summary>
    private static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();

    /// <summary>Matnni «ichida qidirish» <c>ILIKE</c> naqshiga aylantiradi (<c>\ % _</c> ekranlanadi).</summary>
    private static string Like(string text) =>
        "%" + text.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";

    private static TEnum? ParseEnum<TEnum>(string? value, string name)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new McpException($"«{name}» qiymati noto'g'ri: «{value.Trim()}». Mumkin: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    private static List<TEnum>? ParseEnumList<TEnum>(string? value, string name)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return
        [
            .. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => ParseEnum<TEnum>(item, name)!.Value)
                .Distinct(),
        ];
    }

    private static string? Shorten(string? text, int max) =>
        text is null || text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");

    private static double Median(List<int> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2.0;
}
