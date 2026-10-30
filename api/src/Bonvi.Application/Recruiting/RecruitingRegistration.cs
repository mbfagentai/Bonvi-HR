using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Application.Recruiting.Stages;
using Bonvi.Application.Recruiting.Vacancies;
using Bonvi.Application.Recruiting.VacancyRequests;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Recruiting;

/// <summary>
/// Rekruting (2-bosqich) use-case'larini ro'yxatga oladi.
/// </summary>
/// <remarks>
/// Alohida faylda — <c>DependencyInjection.cs</c> yuzlab qatorga
/// aylanmasligi uchun har bir mavzu o'z ro'yxatini o'zi olib boradi
/// (<c>CatalogRegistration</c> va <c>SafetyRegistration</c> bilan bir xil).
/// </remarks>
internal static class RecruitingRegistration
{
    public static IServiceCollection AddRecruiting(this IServiceCollection services)
    {
        services.AddScoped<RecruitingMapper>();

        // «Qayerdan bilgan» — tanlov qoidasi (HR formasi, bot, onlayn anketa) va
        // rekruting ruxsati bilan o'qiladigan ro'yxat (SPEC-qayerdan-bilgan §5).
        services.AddScoped<ReferralChannelChoice>();
        services.AddScoped<ListReferralChannelOptionsQueryHandler>();

        // Voronka bosqichlari — ma'lumotnoma, lekin O'Z handlerlari bilan
        // (tartib va qayta tartiblash umumiy ma'lumotnoma mexanizmida yo'q).
        services.AddScoped<ListPipelineStagesQueryHandler>();
        services.AddScoped<CreatePipelineStageCommandHandler>();
        services.AddScoped<UpdatePipelineStageCommandHandler>();
        services.AddScoped<ReorderPipelineStagesCommandHandler>();
        services.AddScoped<SetPipelineStageActiveCommandHandler>();

        // Vakansiyalar
        services.AddScoped<ListVacanciesQueryHandler>();
        services.AddScoped<ListVacancyRequestsQueryHandler>();
        services.AddScoped<CreateVacancyRequestCommandHandler>();
        services.AddScoped<CancelVacancyRequestCommandHandler>();
        services.AddScoped<ApproveVacancyRequestCommandHandler>();
        services.AddScoped<RejectVacancyRequestCommandHandler>();
        services.AddScoped<GetVacancyQueryHandler>();
        services.AddScoped<GetGeneralPoolStatusQueryHandler>();
        services.AddScoped<OpenVacancyCommandHandler>();
        services.AddScoped<UpdateVacancyCommandHandler>();
        services.AddScoped<ChangeVacancyStateCommandHandler>();
        services.AddScoped<DeleteVacancyCommandHandler>();

        // Nomzodlar
        services.AddScoped<SearchCandidatesQueryHandler>();
        services.AddScoped<GetCandidateQueryHandler>();
        services.AddScoped<RegisterCandidateCommandHandler>();
        services.AddScoped<ListPendingRejectionNoticesQueryHandler>();
        services.AddScoped<MarkRejectionNoticeSentCommandHandler>();
        services.AddScoped<InviteCandidateToInterviewCommandHandler>();
        services.AddScoped<ListPendingInterviewNoticesQueryHandler>();
        services.AddScoped<MarkInterviewNoticeSentCommandHandler>();
        services.AddScoped<UpdateCandidateCommandHandler>();
        services.AddScoped<ReassignCandidateCommandHandler>();
        services.AddScoped<RecordCandidateStageCommandHandler>();
        services.AddScoped<EditCandidateStageEntryCommandHandler>();
        services.AddScoped<CloseCandidateCommandHandler>();
        services.AddScoped<ReturnCandidateToFunnelCommandHandler>();
        services.AddScoped<CheckApplicationQueryHandler>();
        services.AddScoped<FindCandidatePhoneMatchesQueryHandler>();

        // Nomzodni ishga olish — `HireEmployeeCommandHandler` ga bog'liq
        // (u `EmployeeRegistration` da ro'yxatga olingan): xodimni
        // 1-bosqichning oqimi yaratadi, rekruting o'z yo'lini yozmaydi.
        services.AddScoped<HireCandidateCommandHandler>();

        // Nomzoddan xodimga ko'chirish — ishga qabul oqimi ham, ishga
        // tushishdagi bir martalik yurish ham shu bitta sinfni ishlatadi.
        services.AddScoped<CandidateHandover>();

        // Nomzod fayllari (o'ntagacha) — `ScaledDocumentStore` quvuridan
        // o'tadi va u `Common` da ro'yxatga olingan. Xodim hujjatlarining
        // quvuri ALOHIDA qoladi: u faylni qayta kodlamaydi (sababi
        // `ScaledDocumentStore` izohida).
        //
        // Eski uchlik (`UploadCandidateResume…`, `GetCandidateResume…`,
        // `DeleteCandidateResume…`) O'CHIRILDI: ular endi mavjud bo'lmagan
        // `candidates.resume_*` ustunlarini o'qirdi.
        services.AddScoped<UploadCandidateFileCommandHandler>();
        services.AddScoped<GetCandidateFileQueryHandler>();
        services.AddScoped<DeleteCandidateFileCommandHandler>();

        // Nomzod surati — xodim rasmi bilan bir xil quvur
        // (`SquarePhotoStore` ham `Common` da ro'yxatga olingan).
        services.AddScoped<UploadCandidatePhotoCommandHandler>();
        services.AddScoped<GetCandidatePhotoQueryHandler>();
        services.AddScoped<DeleteCandidatePhotoCommandHandler>();

        // Nomzod anketasi HR-F02 (SPEC-planshet-anketa): ochiq onlayn forma (seans,
        // yuborish) va kartadagi anketalar/PDF. PDF chizuvchisi va jurnal
        // Infrastructure'da ro'yxatga olingan.
        services.AddScoped<GetKioskSessionQueryHandler>();
        services.AddScoped<SubmitQuestionnaireFormCommandHandler>();
        services.AddScoped<ListCandidateFormsQueryHandler>();
        services.AddScoped<GetCandidateFormPdfQueryHandler>();

        return services;
    }
}
