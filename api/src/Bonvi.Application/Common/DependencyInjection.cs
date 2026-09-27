using System.Reflection;
using Bonvi.Application.Attendance;
using Bonvi.Application.Catalog;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Dashboard.Queries;
using Bonvi.Application.Employees;
using Bonvi.Application.Feedback;
using Bonvi.Application.Identity.Commands.Login;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Application.Identity.EmployeeAuth;
using Bonvi.Application.Identity.Commands.Logout;
using Bonvi.Application.Identity.Commands.RefreshSession;
using Bonvi.Application.Identity.Photos;
using Bonvi.Application.Identity.Queries.GetCurrentUser;
using Bonvi.Application.Identity.Users.Commands.ActivateUser;
using Bonvi.Application.Identity.Users.Commands.CreateUser;
using Bonvi.Application.Identity.Users.Commands.DeactivateUser;
using Bonvi.Application.Identity.Users.Commands.ResetUserPassword;
using Bonvi.Application.Identity.Users.Commands.UpdateUser;
using Bonvi.Application.Identity.Users.Photos;
using Bonvi.Application.Identity.Users.Queries.ListUsers;
using Bonvi.Application.Me;
using Bonvi.Application.Notifications;
using Bonvi.Application.Recruiting;
using Bonvi.Application.Reports.Queries;
using Bonvi.Application.Safety;
using Bonvi.Application.Settings;
using Bonvi.Application.System.Queries.GetSystemInfo;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Common;

/// <summary>
/// Application qatlamini DI konteyneriga ulash.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Use-case'lar va validatorlarni ro'yxatga oladi.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Use-case handler'lari. Yangi handler qo'shilganda shu yerga bitta qator.
        services.AddScoped<GetSystemInfoQueryHandler>();
        services.AddScoped<GetDashboardStatsQueryHandler>();
        services.AddScoped<GetDashboardBreakdownQueryHandler>();
        services.AddScoped<GetBirthdaysTodayQueryHandler>();

        // Hisobotlar
        services.AddScoped<GetTurnoverQueryHandler>();
        services.AddScoped<ExportEmployeesQueryHandler>();

        // Kvadrat surat quvuri — xodim rasmi ham, hisob rasmi ham shundan o'tadi.
        // Umumiy `Common` da, chunki u bironta bo'limga tegishli emas.
        services.AddScoped<SquarePhotoStore>();

        // Hujjat fayli quvuri (S8/M9) — xuddi shu sababdan `Common` da:
        // hozircha faqat xodim hujjatlari ishlatadi, lekin bo'limga
        // bog'liq emas (kelajakda boshqa egasi bo'lishi mumkin).
        services.AddScoped<DocumentFileStore>();

        // Kichraytirib saqlaydigan hujjat quvuri — nomzod fayllari uchun.
        //
        // ⚠️ Ikkalasi YONMA-YON turadi va bu ataylab: yuqoridagi sinf
        // faylni BAYTMA-BAYT saqlaydi, chunki xodim hujjati (shartnoma,
        // buyruq) mazmuni o'zgarmasligi kerak bo'lgan hujjat. Nomzod fayli
        // esa hujjat NUSXASI (telefonda olingan pasport surati) — uni
        // o'qib bo'lsa bas, shu sababli rasm 2000 px ichiga sig'diriladi.
        // Kichraytirishni `DocumentFileStore` ga qo'shish butun
        // tashkilotning shartnoma skanlarini jimgina qayta kodlashni
        // boshlardi (batafsil sabab `ScaledDocumentStore` izohida).
        services.AddScoped<ScaledDocumentStore>();

        // Autentifikatsiya
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<RefreshSessionCommandHandler>();
        services.AddScoped<LogoutCommandHandler>();
        services.AddScoped<GetCurrentUserQueryHandler>();

        // Foydalanuvchilarni boshqarish — «users.manage» (super administrator
        // va bosh rahbar). Kim kimga tega olishi esa ruxsat emas, use-case
        // qoidasi: `UserRoleRules`.
        services.AddScoped<ListUsersQueryHandler>();
        services.AddScoped<CreateUserCommandHandler>();
        services.AddScoped<UpdateUserCommandHandler>();
        services.AddScoped<DeactivateUserCommandHandler>();
        services.AddScoped<ActivateUserCommandHandler>();
        services.AddScoped<ResetUserPasswordCommandHandler>();

        // Boshqa foydalanuvchining rasmi — o'z hisobi rasmidan ALOHIDA
        // (`/api/users/{id}/photo` va `/api/auth/me/photo`): birinchisi
        // «kim kimga tega oladi» qoidasi ostida, ikkinchisi tokendagi
        // identifikator bilan chegaralangan.
        services.AddScoped<GetUserPhotoQueryHandler>();
        services.AddScoped<UploadUserPhotoCommandHandler>();
        services.AddScoped<DeleteUserPhotoCommandHandler>();

        // Hisob rasmi
        services.AddScoped<GetAccountPhotoQueryHandler>();
        services.AddScoped<UploadAccountPhotoCommandHandler>();
        services.AddScoped<DeleteAccountPhotoCommandHandler>();

        /*
          XODIM ILOVASI (PWA) — ikkita guruh, ikki xil xavfsizlik chegarasi.

          1. HR tomoni (`employee_accounts` boshqaruvi) — oddiy ruxsat
             kodlari ostida, HR tokeni bilan chaqiriladi.
          2. `/api/me/*` — XODIM tokeni bilan, `bonvi-employee`
             auditoriyasida, ruxsat da'volarisiz.

          Ular bitta ro'yxatda turadi, chunki ikkalasi ham bitta
          imkoniyatning ikki tomoni; chegara esa marshrutlarda
          (`MeEndpoints` / `EmployeeAccountEndpoints`) qo'yilgan.
        */
        // Hisob HAR BIR xodimga avtomatik ochiladi (fon vazifasi va «Ilova»
        // bo'limi ochilgan lahza), parolni faqat HR beradi va ko'radi.
        services.AddScoped<EmployeeAccountProvisioner>();
        services.AddScoped<EmployeeAccountProvisioningJob>();
        services.AddScoped<EmployeeAppAccessBuilder>();
        services.AddScoped<GetEmployeeAppAccessQueryHandler>();
        services.AddScoped<RevealEmployeePasswordQueryHandler>();
        services.AddScoped<EmployeePasswordCommandHandler>();
        services.AddScoped<SetMobileCheckInAllowedCommandHandler>();

        services.AddScoped<EmployeeLoginCommandHandler>();
        services.AddScoped<EmployeeRefreshCommandHandler>();
        services.AddScoped<EmployeeLogoutCommandHandler>();
        services.AddScoped<EmployeeAccessGate>();

        // Jarayon ichidagi holat — SINGLETON bo'lishi shart: har so'rovda
        // yangi nusxa bo'lsa, urinishlar hisobi ham, yangilash javobining
        // qisqa xotirasi ham so'rov bilan birga yo'qolardi (izohlar sinflarda).
        services.AddSingleton<EmployeeLoginThrottle>();
        services.AddSingleton<EmployeeRefreshReplayCache>();

        services.AddScoped<CurrentEmployeeResolver>();
        services.AddScoped<GetMeQueryHandler>();
        services.AddScoped<GetMyPhotoQueryHandler>();
        services.AddScoped<GetMyAttendanceQueryHandler>();
        services.AddScoped<GetMyRequestsQueryHandler>();
        services.AddScoped<CreateMyRequestCommandHandler>();
        services.AddScoped<CancelMyRequestCommandHandler>();

        // Ma'lumotnomalar (S2) — o'z ro'yxatini o'zi olib boradi
        services.AddCatalog();

        // Xodimlar va mehnat munosabatlari (S4–S5) — shuningdek o'z ro'yxati bilan
        services.AddEmployees();

        // Qayta aloqa — exit interview (S9/M10)
        services.AddFeedback();

        // Baxtsiz hodisalar jurnali (S9/M11)
        services.AddSafety();

        // Davomat (3-bosqich) — qurilmalar, mosliklar, jadvallar, tabel va
        // hisobotlar. Qabul yo'li (`AddAttendanceIngest`) shu metodning ICHIDAN
        // chaqiriladi (SPEC §10.2.1), bu yerda ikkinchi marta yozilmaydi.
        services.AddAttendance();

        // Rekruting — 2-bosqich: vakansiya, voronka, nomzod, ishga olish
        services.AddRecruiting();

        // Tizim sozlamalari — bot matni va bildirishnoma chegaralari.
        // Bildirishnomalardan OLDIN turadi, chunki yig'uvchi shu bo'limning
        // o'quvchisiga (`SystemSettingsReader`) bog'liq. DI uchun tartib
        // ahamiyatsiz, lekin o'qiyotgan odam uchun bog'liqlik yo'nalishi
        // shu tartibda ko'rinib turadi.
        services.AddSettings();

        // Bildirishnomalar — bironta bo'limga tegishli emas: ular sinov
        // muddati, xodim va nomzod ma'lumotlarini BIRGA o'qiydi.
        // Shu sababli o'z guruhida turadi.
        services.AddNotifications();

        return services;
    }

    /// <summary>Arxitektura testlari uchun qatlam yig'malasi (assembly) havolasi.</summary>
    public static Assembly Assembly => typeof(DependencyInjection).Assembly;
}
