import { AuditAction } from '@/shared/api/generated/model'

/**
 * Audit jurnalining TARJIMA qilinadigan kalitlari va bo'limlar xaritasi.
 *
 * <b>Nega frontendda ro'yxat bor.</b> Server jurnalda xom nomlarni yuboradi —
 * amal (`Update`), entity (`EmploymentEvent`), maydon (`DepartmentId`), amal
 * nomi (`TransferEmployee`) — matnni esa har tilda frontend tanlaydi.
 * Tarjima statik JSON'da, ya'ni «qaysi kalitlar bo'lishi mumkin» degan
 * ro'yxat kodda ham bo'lishi SHART: `scripts/check-locales.mjs` aynan shu
 * massivlarni o'qib, har biriga to'rt tilda matn borligini tekshiradi
 * (`role-keys.ts` dagi bilan bir xil naqsh).
 *
 * <b>Ro'yxatda yo'q nom</b> (backend yangi entity yoki maydon qo'shgan,
 * frontend hali yangilanmagan) ekranni BUZMAYDI: u xom holida, ikkinchi
 * darajali rangda ko'rinadi. Yashirilmaydi — yashirilgan maydon «bu
 * o'zgarmagan» degan yolg'on taassurot berardi.
 *
 * ⚠️ Massivlar `export const NOM = [...] as const` shaklida qolishi kerak —
 * tekshiruv skripti ularni oddiy muntazam ifoda bilan o'qiydi.
 */

/** Jurnal sahifasining ruxsati — menyu bandi va marshrut ham shu kod bilan. */
export const AUDIT_VIEW = 'audit.view'

/**
 * «Kirish va xavfsizlik» ko'rinishi — server ham aynan shu kod bilan ochadi
 * (`AuditVisibility.Security`). `features/users` dan import QILINMADI:
 * bitta satr uchun boshqa bo'limning ichki fayliga bog'lanish shart emas.
 */
export const AUDIT_SECURITY_PERMISSION = 'users.manage'

/** Bitta sahifadagi yozuvlar — server sukuti bilan bir xil (50). */
export const AUDIT_PAGE_SIZE = 50

/** Davr berilmasa — oxirgi shuncha kun, bugun ham kiradi (server bilan bir xil). */
export const AUDIT_DEFAULT_PERIOD_DAYS = 30

/* ───────────────────────────────────────────────────────────── amallar */

/**
 * Amallar tarjima kalitlari — `audit.actions.<kalit>`. Server nomi
 * (`LoginFailed`) birinchi harfi kichik qilib olinadi (`loginFailed`).
 */
export const AUDIT_ACTION_KEYS = [
  'create',
  'update',
  'delete',
  'login',
  'loginFailed',
  'logout',
  'export',
  'permissionDenied',
  'tokenReuse',
] as const

export type AuditActionKey = (typeof AUDIT_ACTION_KEYS)[number]

/** «O'zgarishlar» ko'rinishidagi amallar — Excel eksporti ham shu yerda (server qoidasi). */
export const AUDIT_CHANGE_ACTIONS: readonly AuditAction[] = [
  AuditAction.Create,
  AuditAction.Update,
  AuditAction.Delete,
  AuditAction.Export,
]

/** «Kirish va xavfsizlik» ko'rinishidagi amallar (`AuditActionGroups.Security`). */
export const AUDIT_SECURITY_ACTIONS: readonly AuditAction[] = [
  AuditAction.Login,
  AuditAction.LoginFailed,
  AuditAction.Logout,
  AuditAction.PermissionDenied,
  AuditAction.TokenReuse,
]

const ACTION_VALUES: ReadonlySet<string> = new Set(Object.values(AuditAction))

export function isAuditAction(value: string): value is AuditAction {
  return ACTION_VALUES.has(value)
}

/** `LoginFailed` → `loginFailed`. Notanish amal bo'lmaydi — tip generatsiyadan. */
export function auditActionKey(action: AuditAction): AuditActionKey {
  return (action.charAt(0).toLowerCase() + action.slice(1)) as AuditActionKey
}

/* ──────────────────────────────────────────────────────────── bo'limlar */

/**
 * Bo'lim kalitlari — server bilan bir xil (`AccessModuleKeys`). Yorliqlari
 * rollar muharririnikidan olinadi (`roles.module.*`, `useRoleLabels`): HR
 * bo'limni bitta nom bilan tanisin.
 */
export const AUDIT_MODULE_KEYS = [
  'employees',
  'employment',
  'lists',
  'incentives',
  'feedback',
  'safety',
  'recruiting',
  'attendance',
  'catalog',
  'reports',
  'users',
  'roles',
  'audit',
  'settings',
] as const

export type AuditModuleKey = (typeof AUDIT_MODULE_KEYS)[number]

const MODULE_SET: ReadonlySet<string> = new Set(AUDIT_MODULE_KEYS)

export function isAuditModule(value: string): value is AuditModuleKey {
  return MODULE_SET.has(value)
}

/**
 * Bo'limning KIRISH kodlari — bo'limning eng quyi darajasi beradigan
 * kodlar (serverdagi `AuditVisibility.ViewerOf` qoidasining ko'zgusi:
 * `PermissionCatalog.Modules[i].Levels[0]`).
 *
 * NEGA kerak: «Bo'lim» filtrida so'rovchi KO'RA OLMAYDIGAN bo'lim
 * chiqmasin. Server buni baribir bo'sh sahifa bilan qaytaradi (xato emas),
 * lekin tanlab bo'ladigan, doim bo'sh qaytadigan band HR ni «jurnal
 * ishlamayapti» degan xulosaga olib borardi.
 *
 * Katalog serverdan olinmadi: `GET /api/roles/catalog` faqat
 * `roles.manage` bilan ochiq, jurnalni esa undan torroq rol ham ko'radi.
 */
export const AUDIT_MODULE_ENTRY: Readonly<Record<AuditModuleKey, readonly string[]>> = {
  employees: ['employees.view'],
  employment: ['employment.view'],
  lists: ['lists.view'],
  incentives: ['incentives.view'],
  feedback: ['feedback.view'],
  safety: ['safety.view'],
  recruiting: ['recruiting.view'],
  attendance: ['attendance.view'],
  catalog: ['catalog.view'],
  reports: ['reports.view'],
  users: ['users.manage'],
  roles: ['roles.manage'],
  audit: ['audit.view'],
  settings: ['settings.manage'],
}

/**
 * Bo'limning yoziladigan entity turlari — serverdagi `AuditCatalog`
 * qoidalarining ko'zgusi. «Yozuv turi» filtri faqat shu ro'yxatni beradi.
 *
 * Hisobotlar va audit bo'limlarida entity yo'q: birinchisida faqat Excel
 * eksporti hodisalari, ikkinchisida hech narsa — filtr o'chiq turadi.
 */
export const AUDIT_MODULE_ENTITIES: Readonly<Record<AuditModuleKey, readonly AuditEntityName[]>> = {
  employees: [
    'Employee',
    'EmployeeCompetency',
    'EmployeeCompetencyLanguage',
    'EmployeeDocument',
    'EmployeeCheckFile',
  ],
  employment: ['Employment', 'EmploymentEvent', 'Probation'],
  lists: ['EmployeeFlag'],
  incentives: ['Incentive'],
  feedback: ['ExitInterview'],
  safety: ['Incident'],
  recruiting: [
    'Vacancy',
    'Candidate',
    'CandidateStageEntry',
    'CandidateNote',
    'CandidateFile',
    'CandidateQuestionnaire',
    'PipelineStage',
  ],
  attendance: [
    'AbsenceRequest',
    'WorkSchedule',
    'WorkScheduleDay',
    'WorkScheduleDate',
    'ScheduleAssignment',
    'AttendanceDevice',
    'AttendanceIdentity',
    'WorkSite',
    'EmployeeAccount',
  ],
  catalog: [
    'LegalEntity',
    'Direction',
    'Department',
    'Position',
    'PositionDuty',
    'TerminationReason',
    'AwardType',
    'PenaltyType',
    'DocumentType',
    'AbsenceType',
  ],
  reports: [],
  users: ['User'],
  roles: ['Role'],
  audit: [],
  settings: ['SystemSetting', 'ApiKey'],
}

/**
 * Xodim kartasidagi «O'zgarishlar tarixi» chiplari — xodimga bog'lanadigan
 * (`subject_employee_id` yoziladigan) bo'limlar. Qolganlari (rekruting,
 * ma'lumotnoma, tizim) xodimning tarixiga tushmaydi, ularning chipi doim
 * bo'sh sahifa berardi.
 */
export const EMPLOYEE_HISTORY_MODULES: readonly AuditModuleKey[] = [
  'employees',
  'employment',
  'lists',
  'incentives',
  'feedback',
  'safety',
  'attendance',
]

/** So'rovchi shu bo'limning qatorlarini ko'radimi (server qoidasining ko'zgusi). */
export function canSeeAuditModule(
  module: AuditModuleKey,
  hasPermission: (code: string) => boolean,
): boolean {
  return AUDIT_MODULE_ENTRY[module].every(hasPermission)
}

/** Entity turi qaysi bo'limda — URL'dagi `entity` dan «Bo'lim» ni tiklash uchun. */
export function auditModuleOfEntity(entity: string): AuditModuleKey | null {
  for (const module of AUDIT_MODULE_KEYS) {
    if ((AUDIT_MODULE_ENTITIES[module] as readonly string[]).includes(entity)) return module
  }
  return null
}

/* ─────────────────────────────────────────────────────────── entity'lar */

/**
 * Entity turlari — `audit.entities.<Nom>`. `Endpoint` — entity emas,
 * «ruxsatsiz urinish» hodisasining obyekti (so'ralgan manzil).
 */
export const AUDIT_ENTITY_NAMES = [
  'User',
  'Role',
  'SystemSetting',
  'ApiKey',
  'LegalEntity',
  'Direction',
  'TerminationReason',
  'AwardType',
  'PenaltyType',
  'DocumentType',
  'AbsenceType',
  'Department',
  'Position',
  'PositionDuty',
  'Employee',
  'Employment',
  'EmploymentEvent',
  'Probation',
  'EmployeeFlag',
  'EmployeeCompetency',
  'EmployeeCompetencyLanguage',
  'EmployeeDocument',
  'EmployeeCheckFile',
  'Incentive',
  'ExitInterview',
  'Incident',
  'PipelineStage',
  'Vacancy',
  'Candidate',
  'CandidateStageEntry',
  'CandidateNote',
  'CandidateFile',
  'CandidateQuestionnaire',
  'AbsenceRequest',
  'WorkSchedule',
  'WorkScheduleDay',
  'WorkScheduleDate',
  'ScheduleAssignment',
  'AttendanceDevice',
  'AttendanceIdentity',
  'WorkSite',
  'EmployeeAccount',
  'Endpoint',
] as const

export type AuditEntityName = (typeof AUDIT_ENTITY_NAMES)[number]

const ENTITY_SET: ReadonlySet<string> = new Set(AUDIT_ENTITY_NAMES)

export function isAuditEntity(value: string): value is AuditEntityName {
  return ENTITY_SET.has(value)
}

/* ────────────────────────────────────────────────────────────── maydonlar */

/**
 * Maydon yorliqlari — `audit.fields.<kalit>`. Kalit — EF xossasi nomi,
 * murakkab xossada nuqta pastki chiziqqa almashadi (`FullName.LastName` →
 * `FullName_LastName`): nuqtali kalit i18next'da ichma-ich yo'l bo'lib
 * o'qilardi.
 *
 * Kirish hodisalari va eksport tafsilotining kalitlari (`reason`, `rows`…)
 * ham shu yerda — server ularni ham «maydon» qilib yuboradi.
 *
 * Ro'yxatda yo'q maydon xom nomi bilan chiqadi (yuqoridagi izoh).
 */
export const AUDIT_FIELD_KEYS = [
  'FullName_LastName',
  'FullName_FirstName',
  'FullName_MiddleName',
  'BirthDate',
  'Gender',
  'Pinfl',
  'Passport',
  'Phone',
  'SecondPhone',
  'Address',
  'Speciality',
  'TelegramUrl',
  'PhotoKey',
  'HasCriminalRecord',
  'CriminalRecordNote',
  'MehnatUzUrl',
  'NeedsReview',
  'ReviewNote',
  'MobileCheckInAllowed',
  'DirectionIds',
  'EmployeeId',
  'LegalEntityId',
  'DepartmentId',
  'PositionId',
  'HireDate',
  'HireOrderNo',
  'HireOrderDate',
  'Salary',
  'Rate',
  'IsPrimary',
  'Status',
  'TerminationDate',
  'TerminationReasonId',
  'TerminationOrderNo',
  'TerminationNote',
  'ResponsibleEmployeeId',
  'EmploymentId',
  'Type',
  'EffectiveDate',
  'OrderNo',
  'OrderDate',
  'FromDepartmentId',
  'ToDepartmentId',
  'FromPositionId',
  'ToPositionId',
  'FromSalary',
  'ToSalary',
  'Note',
  'DutyEndDate',
  'CandidateId',
  'RecordedBy',
  'IsImported',
  'StartDate',
  'EndDate',
  'ResolvedAt',
  'Conclusion',
  'ReviewerEmployeeId',
  'Reason',
  'Login',
  'PasswordHash',
  'IsActive',
  'RoleIds',
  'Name',
  'Code',
  'Permissions',
  'Kind',
  'TypeId',
  'Date',
  'Amount',
  'Percent',
  'GiftDescription',
  'ParentId',
  'HeadEmployeeId',
  'Grade',
  'ProbationDays',
  'Text',
  'SortOrder',
  'IncidentDate',
  'Shift',
  'Location',
  'Description',
  'Cause',
  'CorrectiveAction',
  'Severity',
  'MedicalAidProvided',
  'MedicalAidNote',
  'CommissionConclusion',
  'ConcludedAt',
  'InterviewDate',
  'WouldRecommend',
  'Impressions',
  'ConditionsToReturn',
  'VacancyId',
  'CurrentStageId',
  'StageId',
  'Source',
  'NextContactDate',
  'InterviewAt',
  'ClosedAt',
  'ClosingNote',
  'Title',
  'Openings',
  'OpenedAt',
  'DocumentTypeId',
  'FileName',
  'ContentType',
  'SizeBytes',
  'Key',
  'Value',
  'KeyHash',
  'RevokedAt',
  'RevokedBy',
  'reason',
  'allSessions',
  'method',
  'path',
  'rows',
  'filters',
  'action',
] as const

/* ────────────────────────────────────────────────────────── amal nomlari */

/**
 * Amal nomlari (endpoint nomi, `operation`) — `audit.operations.<Nom>`.
 * Bitta qatordan ko'ra KATTAROQ ma'no: «Ko'chirish» — bitta harakat, uning
 * ichida mehnat munosabati tahriri va yangi buyruq (guruh sarlavhasi).
 *
 * Faqat YOZADIGAN amallar. Ro'yxatda yo'g'i amal turiga tushadi
 * («O'zgartirildi», `audit.actions.*`) — SPEC §9.9 dagi zaxira qoida.
 */
export const AUDIT_OPERATION_NAMES = [
  'HireEmployee',
  'TransferEmployee',
  'AddAdditionalDuty',
  'ChangeSalary',
  'EndAdditionalDuty',
  'EditEmploymentEvent',
  'TerminateEmployment',
  'ResolveProbation',
  'ExtendProbation',
  'ConfirmMissingProbations',
  'CreateEmployee',
  'UpdateEmployee',
  'UploadEmployeePhoto',
  'DeleteEmployeePhoto',
  'UploadEmployeeCheckFile',
  'DeleteEmployeeCheckFile',
  'RaiseEmployeeFlag',
  'CloseEmployeeFlag',
  'SaveEmployeeCompetency',
  'UploadEmployeeDocument',
  'UpdateEmployeeDocument',
  'DeleteEmployeeDocument',
  'GrantIncentive',
  'UpdateIncentive',
  'DeleteIncentive',
  'RecordExitInterview',
  'UpdateExitInterview',
  'DeleteExitInterview',
  'RecordIncident',
  'UpdateIncident',
  'ConcludeIncident',
  'DeleteIncident',
  'RegisterCandidate',
  'UpdateCandidate',
  'ReassignCandidate',
  'RecordCandidateStage',
  'EditCandidateStageEntry',
  'InviteCandidateToInterview',
  'MarkRejectionNoticeSent',
  'MarkInterviewNoticeSent',
  'RejectCandidate',
  'WithdrawCandidate',
  'ReserveCandidate',
  'BlacklistCandidate',
  'ReturnCandidateToFunnel',
  'HireCandidate',
  'UploadCandidateFile',
  'DeleteCandidateFile',
  'UploadCandidatePhoto',
  'DeleteCandidatePhoto',
  'OpenVacancy',
  'UpdateVacancy',
  'ReopenVacancy',
  'CloseVacancy',
  'DeleteVacancy',
  'CreatePipelineStage',
  'UpdatePipelineStage',
  'ReorderPipelineStages',
  'CreateDepartment',
  'UpdateDepartment',
  'MoveDepartment',
  'CreatePosition',
  'UpdatePosition',
  'AddPositionDuty',
  'RemovePositionDuty',
  'ReorderPositionDuties',
  'CreateUser',
  'UpdateUser',
  'DeactivateUser',
  'ActivateUser',
  'ResetUserPassword',
  'UploadUserPhoto',
  'DeleteUserPhoto',
  'UploadMyPhoto',
  'DeleteMyPhoto',
  'CreateRole',
  'UpdateRole',
  'DeleteRole',
  'UpdateSettings',
  'UploadBotLogo',
  'DeleteBotLogo',
  'CreateApiKey',
  'RevokeApiKey',
  'RevealEmployeePassword',
  'RegenerateEmployeePassword',
  'SetEmployeePassword',
  'SetMobileCheckInAllowed',
  'CreateAbsenceRequest',
  'UpdateAbsenceRequest',
  'DecideAbsenceRequest',
  'CancelAbsenceRequest',
  'CreateMyRequest',
  'CancelMyRequest',
  'RegisterAttendanceDevice',
  'UpdateAttendanceDevice',
  'RotateAttendanceDeviceToken',
  'DeleteAttendanceDevice',
  'ApproveDeviceRemovals',
  'CreateAttendanceIdentity',
  'DeleteAttendanceIdentity',
  'ConfirmAttendanceIdentityImport',
  'CreateWorkSchedule',
  'UpdateWorkSchedule',
  'SetWorkScheduleDates',
  'FillWorkScheduleYear',
  'DeleteWorkSchedule',
  'CreateScheduleAssignment',
  'DeleteScheduleAssignment',
  'CreateWorkSite',
  'UpdateWorkSite',
  'Login',
  'Logout',
  'RefreshSession',
  'ExportEmployeesToExcel',
  'ExportIncentivesToExcel',
] as const

/* ──────────────────────────────────────────────── hodisa tafsilotlari */

/** Muvaffaqiyatsiz kirish sabablari — `audit.reasons.<kalit>` (server: `LoginCommand`). */
export const AUDIT_LOGIN_REASONS = ['credentials', 'format', 'locked', 'inactive'] as const
