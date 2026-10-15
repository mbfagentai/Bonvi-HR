/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employments-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Maydon nomlari TAXMIN emas — ular haqiqiy manbadan olingan va jonli
 * endpoint'da tekshirilgan (`GET /api/recruiting/stages` javobi bilan
 * solishtirildi):
 *   api/src/Bonvi.Application/Recruiting/Dtos/RecruitingDtos.cs
 *   api/src/Bonvi.Api/Endpoints/{Vacancy,Candidate,PipelineStage}Endpoints.cs
 *
 * ⚠️ ISTISNO — nomzod izohi, keyingi aloqa sanasi va FAYLLAR
 * (`nextContactDate`, `files`, `notes`, `/files` endpoint'lari,
 * `contactDue`, `rejected`). Ular hali YOZILMAGAN backend'ning
 * shartnomasidan olingan: `docs/SPEC-nomzod-izoh-fayl.md` §4. SPEC shu
 * ish uchun yagona haqiqat manbasi va ikki tomon (server va klient)
 * unga qarab yoziladi. Server tayyor bo'lgach `./bonvi gen` shartnomani
 * tasdiqlaydi; farq chiqsa TUZATILADIGAN joy shu fayl, SPEC emas.
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `recruiting-queries.ts` generatsiya qilingan hook'larga o'tkaziladi.
 *
 * NEGA butun rekruting BITTA faylda (vakansiya, nomzod, bosqich).
 * Uchala endpoint bitta shartnomaning bo'laklari: nomzod kartasi
 * javobida bosqich yozuvlari ham bor (`CandidateDto.stages`), ya'ni
 * tiplar bir-biriga bog'langan. Fayllarga bo'linsa,
 * ular orasida o'zaro import paydo bo'lardi va `unknown` dan o'qish
 * yordamchilari (`text`, `money`, `readPage`) ham nusxalanardi —
 * `employees-api.ts` da aynan shunday bir necha nusxa hosil bo'lgan edi.
 */
import axios from 'axios'

import {
  LANGUAGE_LEVELS,
  type LanguageLevel,
} from '@/features/employees/api/employee-competency-api'
import type { PagedResult } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'

/* ═══════════════════════════════════════════════════════════════ sanoqlar */

/*
 * Sanoqlar JSON'da NOM bo'lib yuradi (`JsonStringEnumConverter`,
 * `Program.cs`) — ya'ni javobda ham, so'rov satrida ham «Open»,
 * «InProgress», «Manual».
 *
 * NEGA klientda camelCase'ga o'girilmaydi (buyruqlar moduli shunday
 * qilgan edi): u yerda javob SON qaytarardi va nomga o'girish baribir
 * kerak edi. Bu yerda esa server nomni o'zi beradi — o'rtada yana bir
 * xarita qo'yish faqat ikkita nomlash tizimi hosil qiladi. Manzil
 * satridagi `?status=InProgress` ham backend hujjatidagi bilan aynan
 * bir xil ko'rinadi, ya'ni havolani qo'lda yozish ham mumkin.
 */

/*
 * Vakansiya holati — IKKITA. «Paused» OLIB TASHLANDI: pauza va yopish
 * tizim uchun ayni natijani berardi (ikkalasida ham yangi nomzod
 * kiritilmasdi), farq faqat HR ning niyatida edi — batafsil sabab
 * backend'dagi `VacancyStatus` domen izohida.
 */
export const VACANCY_STATUSES = ['Open', 'Closed'] as const
export type VacancyStatus = (typeof VACANCY_STATUSES)[number]

/*
 * Nomzodning yakunlari — OLTITA (`Blacklisted` 2026-10-07 da qo'shildi).
 *
 * ⚠️ `Reserve` ro'yxatda BO'LISHI SHART, garchi u faqat «yana bitta
 * holat» bo'lib ko'rinsa ham. Pastdagi `oneOf(...)` tanilmagan qiymatni
 * JIMGINA zaxiraga (`InProgress`) tushiradi — ya'ni ro'yxatdan tushib
 * qolgan holat xato bermaydi, u YOLG'ON ko'rsatadi: rezervdagi nomzod
 * ekranda «Jarayonda» bo'lib turadi va HR uni voronkada faol deb
 * o'qiydi. Aynan shu bo'lgan edi (`docs/PENDING_WIRING.md`).
 *
 * Tartib serverdagi sanoq bilan bir xil (`CandidateStatus` domen
 * izohi): `Reserve` OXIRIDA — u eng keyin qo'shilgan.
 */
export const CANDIDATE_STATUSES = [
  'InProgress',
  'Rejected',
  'Hired',
  'Withdrawn',
  'Reserve',
  'Blacklisted',
] as const
export type CandidateStatus = (typeof CANDIDATE_STATUSES)[number]

export const CANDIDATE_SOURCES = ['Manual', 'TelegramBot', 'Excel'] as const
export type CandidateSource = (typeof CANDIDATE_SOURCES)[number]

/*
 * ⭐ Bosqich yozuvida «natija» sanog'i YO'Q (2026-09-16, foydalanuvchi
 * qarori). Ilgari `Pending | Passed | Failed` bor edi; u olib tashlandi,
 * chunki BOSQICHNING O'ZI holatni aytadi — «Анкета», «Гаплашилди»,
 * «Кенгаш» qatori nomzod qayerda ekanini bildiradi, natija esa o'sha
 * bosqichning xulosa MATNIDA yozilgan. Ikkita joy bir xil savolga javob
 * berardi va ular bir-biriga zid tushardi.
 */


/*
 * Izoh TURI — nomzod izohi qaysi AMALDAN paydo bo'lgani.
 *
 * Bosqichi bo'lmagan amallar (vakansiyani almashtirish, rezervdan
 * qaytarish, suhbatni ko'chirish) bosqich yozuvi hosil qilmaydi — ularning
 * izohi alohida jadvalga tushadi. Tur MATN emas, SANOQ: tarixdagi qatorni
 * to'rt tilda nomlash kerak («Boshqa vakansiyaga o'tkazildi») va matnga
 * qotib qolgan yorliqni tarjima qilib bo'lmasdi.
 *
 * ⚠️ Ro'yxatda `Manual` YO'Q va bu ataylab: erkin «izoh qo'shish» tugmasi
 * shartnomada YO'Q (SPEC §1.2). Izoh faqat amaldan tug'iladi, aks holda
 * bosqich xulosasi bilan ikkita raqib joy paydo bo'lardi.
 *
 * ⚠️ Tartib serverdagi sanoq bilan bir xil bo'lishi SHART
 * (`CandidateNoteKind`, 1-2-3) va yangi qiymat FAQAT oxiriga qo'shiladi.
 * Pastdagi `oneOf(...)` tanilmagan qiymatni JIMGINA birinchisiga
 * (`Reassign`) tushiradi — ya'ni ro'yxatdan tushib qolgan tur xato
 * bermaydi, u YOLG'ON yorliq ko'rsatadi (`CANDIDATE_STATUSES` izohidagi
 * `Reserve` bilan aynan bir xil tuzoq).
 */
export const CANDIDATE_NOTE_KINDS = [
  'Reassign',
  'ReturnToFunnel',
  'InterviewRescheduled',
  'Rejected',
  'Withdrawn',
  'Reserved',
  'Blacklisted',
] as const
export type CandidateNoteKind = (typeof CANDIDATE_NOTE_KINDS)[number]

/* ───────────────────────────────────────────────────── anketa sanoqlari */

/*
 * Anketa javoblari — YOPIQ to'plamlar: nomzod ularni botda tugmadan
 * tanlaydi. Qiymatlar backend'dagi nomlar bilan AYNAN bir xil
 * (`CandidateQuestionnaire.cs`) va tarjima kaliti ulardan
 * `lowerFirst` bilan hosil qilinadi — yuqoridagi holat sanoqlari
 * bilan bir xil qoida.
 */

export const EDUCATION_LEVELS = [
  'SecondarySchool',
  'VocationalSchool',
  'HigherComplete',
  'HigherIncomplete',
] as const
export type EducationLevel = (typeof EDUCATION_LEVELS)[number]

export const MARITAL_STATUSES = ['Married', 'Unmarried', 'Divorced'] as const
export type MaritalStatus = (typeof MARITAL_STATUSES)[number]

/**
 * Kutilayotgan maosh — ORALIQ, aniq son emas.
 *
 * ⚠️ Oraliqlar vaqt o'tishi bilan eskiradi va o'shanda backend YANGI
 * qiymat qo'shadi, eskisini o'chirmaydi (domen izohi). Shu sababli
 * tanilmagan qiymat bu yerda XATO EMAS — u `null` ga aylanadi va qator
 * umuman chizilmaydi (pastdagi `enumOrNull` izohi).
 */
export const SALARY_EXPECTATIONS = [
  'From1To2Million',
  'From2To3Million',
  'From3To4Million',
  'From5To7Million',
] as const
export type SalaryExpectation = (typeof SALARY_EXPECTATIONS)[number]

export const REFERRAL_SOURCES = [
  'Acquaintance',
  'SocialNetwork',
  'Olx',
  'HeadHunter',
  'Telegram',
  'Other',
] as const
export type ReferralSource = (typeof REFERRAL_SOURCES)[number]

/* ═══════════════════════════════════════════════════════════════ tiplar */

/** `PipelineStageDto` — voronka bosqichi. */
export interface PipelineStage {
  id: string
  name: string
  isActive: boolean
  note: string | null
  /**
   * Voronkadagi o'rin: kichik qiymat — oldinroq. Ro'yxat serverdan
   * ALLAQACHON shu tartibda keladi, klientda qayta saralanmaydi
   * (`catalog-api.ts` dagi bilan bir xil qoida: ikki xil tartib
   * bo'lmasin).
   */
  sortOrder: number
  /**
   * ⭐ Shu bosqichga o'tkazilganda nomzoddan SUHBAT VAQTI so'raladimi va
   * unga Telegram taklifi yuboriladimi.
   *
   * Bayroq ma'lumotnomada turadi, kodda emas: «Кенгашга чақирилди» —
   * HR o'zi qo'yadigan nom va uni qayta nomlash tugmaning xatti-harakatini
   * buzmasligi kerak (sabab `PipelineStage.InvitesToInterview` domen
   * izohida).
   */
  invitesToInterview: boolean
}

/**
 * `VacancyDto` — ro'yxat va karta uchun umumiy ko'rinish.
 *
 * ⚠️ Lavozim, bo'lim va firma maydonlari YO'Q va bu ataylab: vakansiya —
 * E'LON, kompaniya tuzilmasidan unda hech narsa saqlanmaydi. Uchalasi
 * nomzod ISHGA OLINAYOTGANDA tanlanadi (`candidate-hire-dialog.tsx`) va
 * o'sha yerdan xodim kartasiga tushadi. Batafsil sabab backend'dagi
 * `Vacancy` domen izohida.
 *
 * Mas'ul xodim ham OLIB TASHLANDI (egasining qarori): vakansiya eng sodda
 * shaklda — nomi, nechta odam, holati, sanasi va izohi.
 */
export interface Vacancy {
  id: string
  /**
   * E'lon nomi — vakansiyaning O'Z matni (ma'lumotnoma havolasi emas).
   *
   * Server uni majburiy qiladi («vakansiya.nom_yoq») va takrorlanishiga
   * yo'l qo'ymaydi («vakansiya.nom_band»), ya'ni ro'yxatda bitta nom
   * bitta qator bo'lib turadi.
   */
  title: string
  openings: number
  status: VacancyStatus
  openedAt: string
  closedAt: string | null
  note: string | null
  candidateCount: number
  /** `openings` bilan birga «2/3 to'ldirildi» ko'rinishini beradi. */
  hiredCount: number
  /**
   * «Umumiy anketa» — tizim vakansiyasi: server uni yopishni ham,
   * o'chirishni ham rad etadi («vakansiya.tizim_vakansiyasi»). Botdagi
   * «Anketa to'ldirish» arizalari shu yerga tushadi — u yopilganda
   * 18 kun davomida har bir bunday ariza rad etilgan (2026-09).
   */
  isSystem: boolean
}

/** `CandidateStageEntryDto` — nomzodning bitta bosqichdagi yozuvi. */
export interface CandidateStageEntry {
  id: string
  stageId: string
  /** Faolsizlantirilgan bosqich uchun ham qaytadi — tarix «noma'lum» bo'lib qolmasin. */
  stage: string | null
  date: string
  conclusion: string | null
  recordedBy: string | null
  /**
   * Yozuv KIRITILGAN lahza — tuzatilganda ham o'zgarmaydi.
   *
   * Lenta aynan shu bo'yicha saralanadi (sabab `candidate-stages-tab.tsx`
   * boshida): tuzatilgan qator tarixning o'rtasidan tepasiga sakrab
   * chiqmasligi kerak.
   */
  recordedAt: string
  /**
   * Qachon tuzatilgan; tegilmagan yozuvda `null` — o'shanda lentada
   * «tahrirlandi» belgisi umuman chizilmaydi.
   */
  updatedAt: string | null
}

/** `CandidateListItemDto` — nomzodlar ro'yxatining bitta qatori. */
export interface CandidateListItem {
  id: string
  fullName: string
  shortName: string
  phone: string | null
  birthDate: string | null
  speciality: string | null
  vacancyId: string
  vacancy: string | null
  currentStageId: string | null
  currentStage: string | null
  status: CandidateStatus
  source: CandidateSource
  /**
   * Kamida BITTA fayl biriktirilganmi.
   *
   * Nomi `hasResume` bo'lib qoldi, garchi nomzodda endi o'ntagacha fayl
   * bo'lsa ham: qayta nomlash serverdagi DTO ni ham, uni o'qiydigan
   * testlarni ham surardi, foydasi esa nol — jadvalda baribir SON emas,
   * qisqich belgisi chiziladi (shartnoma qarori, SPEC §4.8).
   */
  hasResume: boolean
  createdAt: string
  /** Ishga olingan bo'lsa — yaratilgan XODIM kartasi. */
  hiredEmployeeId: string | null
  /**
   * ⭐ HR shu odam bilan keyingi marta qachon gaplashishni rejalashtirgani —
   * kalendar sanasi (`YYYY-MM-DD`, Toshkent), vaqt YO'Q. `null` — reja
   * qo'yilmagan (odatiy hol, ko'pchilik qatorda shunday).
   *
   * Ro'yxatda alohida ustun bo'lib chiziladi va serverda `sort=nextContact`
   * bilan saralanadi. Bugungi yoki o'tgan sana — «bugun kim bilan
   * gaplashish kerak» degan savolning javobi, shu sababli u ogohlantirish
   * rangida ko'rinadi (`contactDue` filtri esa aynan shu qatorlarni
   * qoldiradi).
   */
  nextContactDate: string | null
  /** Voronkadan chiqqan sana (`YYYY-MM-DD`); jarayondagida `null`. */
  closedAt: string | null
  /**
   * Chiqish sababi. «Qora ro'yxat → Nomzodlar» tabida asosiy ustun —
   * «nega bu odam olinmaydi» (2026-10-07).
   */
  closingNote: string | null
}

/**
 * `CandidateQuestionnaireDto` — nomzod botda to'ldirgan anketa.
 *
 * <b>Alohida so'rov YO'Q</b>: anketa nomzod kartasi javobining ichida
 * keladi (server qarori — HR uni har doim nomzod bilan birga ko'radi).
 *
 * ⚠️ <b>Sanoq maydonlari `null` BO'LISHI MUMKIN</b>, garchi serverda
 * ular majburiy bo'lsa ham. Sabab `enumOrNull` da: backend yangi qiymat
 * qo'shishi mumkin va eski klient uni tanimaydi. Bunday paytda qator
 * NOTO'G'RI javob bilan chizilgandan ko'ra umuman chizilmagani ma'qul
 * (`candidate-files-card.tsx` dagi bilan bir xil mulohaza).
 *
 * `hasDriverLicense` esa `false` bo'lsa ham KO'RSATILADI: «yo'q» —
 * bo'sh maydon emas, javobning o'zi.
 */
export interface CandidateQuestionnaire {
  education: EducationLevel | null
  /** Erkin matn — botda o'tkazib yuborilishi mumkin. */
  workExperience: string | null
  maritalStatus: MaritalStatus | null
  /**
   * Rus tili darajasi — xodim kartasidagi til darajasi bilan AYNAN bir
   * xil sanoq (server shuni qaytaradi). Shu sababli tipi ham,
   * tarjimasi ham qayta yozilmaydi: `competency.languageLevel.*`
   * kalitlari qayta ishlatiladi.
   */
  russianLevel: LanguageLevel | null
  otherLanguages: string | null
  officeSkills: string | null
  /** SON EMAS, matn: «kelishilgan», «3 mln» kabi javoblar uchun. */
  lastSalary: string | null
  extraNote: string | null
  hasDriverLicense: boolean
  /** Faqat `hasDriverLicense = true` da: B, BC, BCE, BCED. */
  driverLicenseCategories: string | null
  expectedSalary: SalaryExpectation | null
  referralSource: ReferralSource | null
  /** Ommaviy oferta bilan rozilik VAQTI — huquqiy fakt, bayroq emas. */
  offerAcceptedAt: string | null
}

/**
 * `CandidateFileDto` — nomzodga biriktirilgan BITTA fayl.
 *
 * <b>Alohida so'rov YO'Q</b>: fayllar ro'yxati nomzod kartasi javobining
 * ichida keladi (`CandidateDto.files`), anketa va bosqichlar bilan bir
 * xil qoida — HR ularni har doim karta bilan birga ko'radi, ikkinchi
 * so'rov faqat ekranni sekinlashtirardi.
 *
 * Yuklab olish va o'chirish uchun `id` yetadi; saqlash KALITI ataylab
 * qaytarilmaydi (u ichki manzil — surat bilan bir xil qaror).
 */
export interface CandidateFile {
  /** Yuklab olish va o'chirish AYNAN shu identifikator bo'yicha ketadi. */
  id: string
  /** Asl nom — FAQAT ko'rsatish uchun. Saqlash yo'lida u ishlatilmaydi. */
  fileName: string
  /** MIME turi — server MAZMUNDAN aniqlagan, klient sarlavhasidan emas. */
  contentType: string
  /**
   * SAQLANGAN hajm — kichiklashtirilgandan KEYINGI baytlar, yuklangan
   * fayl hajmi emas: 4 MB telefon surati diskda ~300 KB bo'lib qoladi va
   * kartada 4 MB deb yozish yolg'on bo'lardi.
   *
   * `0` — NOMA'LUM. Eski bitta rezyumeda hajm saqlanmagan bo'lishi mumkin
   * edi va ko'chirish o'shanda `0` yozadi; bunday qatorda hajm o'rniga
   * chiziqcha chiziladi.
   */
  sizeBytes: number
  /** Biriktirilgan VAQT — ISO satr, UTC. Ekranda Toshkent vaqtida. */
  uploadedAt: string
}

/**
 * `CandidateNoteDto` — bosqichi bo'lmagan amaldan qolgan izoh.
 *
 * ⚠️ ICHKI YOZUV: nomzodga hech qachon yuborilmaydi (na Telegram
 * xabarida, na rad etish matnida). Uni faqat HR va rahbar ko'radi.
 *
 * NEGA bosqich yozuvi emas: bosqich yozuvida `stageId` bo'lishi shart, «boshqa vakansiyaga o'tkazildi» esa hech qanday
 * bosqichning NATIJASI emas — ikkisi ham yolg'on qiymat bo'lardi
 * (batafsil sabab SPEC §2.3.1 da).
 */
export interface CandidateNote {
  id: string
  /** Qaysi amaldan tug'ilgani — tarixdagi yorliq shundan hosil bo'ladi. */
  kind: CandidateNoteKind
  text: string
  /** Kim yozgani. `null` — tizimdan tashqarida hosil bo'lgan yozuv. */
  createdBy: string | null
  createdAt: string
}

/** `CandidateDto` — nomzod kartasi: bosqichlar tarixi bilan. */
export interface Candidate {
  id: string
  lastName: string
  firstName: string
  middleName: string | null
  fullName: string
  birthDate: string | null
  phone: string | null
  address: string | null
  speciality: string | null
  vacancyId: string
  vacancy: string | null
  currentStageId: string | null
  currentStage: string | null
  status: CandidateStatus
  source: CandidateSource
  note: string | null
  closedAt: string | null
  closingNote: string | null
  /**
   * ⭐ Keyingi aloqa sanasi — kalendar sanasi (`YYYY-MM-DD`, Toshkent),
   * vaqt YO'Q. `null` — reja qo'yilmagan.
   *
   * O'TGAN sana ham ODATIY hol va xato EMAS: maydonning butun maqsadi
   * «bugun yoki muddati o'tgan» ro'yxatini berish. Shu sababli server ham
   * o'tgan sanani tekshirmaydi (SPEC §2.1.3) — suhbat vaqtidan farqi
   * shunda: suhbat vaqti nomzodga XABAR bo'lib ketadi, bu sana esa faqat
   * HR ning o'z ro'yxatida qator paydo qiladi.
   *
   * Qaysi amal uni qo'yadi, qaysi biri tozalaydi — SPEC §2.1.2 dagi
   * jadval. Qisqasi: rad etish/voz kechish/ishga olish TOZALAYDI,
   * rezervga o'tkazish esa SAQLAYDI (rezerv — chiqish emas, kechiktirish).
   */
  nextContactDate: string | null
  /**
   * Surat bor-yo'qligi. Saqlash KALITI qaytarilmaydi (server qarori: u
   * ichki manzil), shu sababli rasm keshi xodimnikidan farq qiladi —
   * izohi `useCandidatePhoto` da.
   */
  hasPhoto: boolean
  hiredEmployeeId: string | null
  hiredEmploymentId: string | null
  /**
   * Chaqirilgan suhbat vaqti — ISO satr, UTC da. `null` — chaqirilmagan.
   * Ekranda Toshkent vaqtida chiziladi.
   */
  interviewAt: string | null
  /**
   * Taklif Telegram'ga qachon ketgani. Vaqt qo'yilgan, lekin bu bo'sh —
   * xabar hali navbatda (yoki nomzodning Telegram suhbati yo'q).
   */
  interviewNoticeSentAt: string | null
  /**
   * Taklif nomzodga yetib bordimi. `false` — Telegram rad etdi (nomzod botni
   * bloklagan): bot qayta urinmaydi, HR telefon qilishi kerak. `null` — hali
   * urinilmagan yoki eski yozuv (o'shanda faqat yetkazilganlar belgilanardi).
   */
  interviewNoticeDelivered: boolean | null
  /** Yozilgan tartibda. */
  stages: CandidateStageEntry[]
  /**
   * Biriktirilgan fayllar — YUKLANGAN TARTIBDA (eskisidan yangisiga).
   *
   * ⚠️ Tartib server qarori va u xodim hujjatlaridagidan TESKARI (u yerda
   * yangisi tepada). Sabab: pasport uch varaq bo'lib 1-2-3 tartibda
   * yuklanadi va teskari qilinsa karta o'qilmas holga tushadi
   * (SPEC §4.3.4). Klientda QAYTA SARALANMAYDI — ikki xil tartib
   * bo'lmasligi kerak.
   *
   * Bo'sh massiv — fayl yo'q (odatiy hol).
   */
  files: CandidateFile[]
  /**
   * Bosqichi bo'lmagan amallardan qolgan izohlar — yozilgan tartibda.
   *
   * Bosqich xulosalari BU YERDA EMAS, `stages[].conclusion` da: ikkisi
   * boshqa-boshqa narsa va tarixda ham alohida ko'rinadi.
   */
  notes: CandidateNote[]
  /**
   * Botda to'ldirilgan anketa. `null` BO'LISHI ODATIY hol: HR qo'lda
   * kiritgan nomzodda ham, bot ishga tushgunicha yozilgan nomzodlarda
   * ham u yo'q.
   */
  questionnaire: CandidateQuestionnaire | null
}

/**
 * `HrWarning` — ishga qabulning ogohlantirishi (masalan stavka chegarasi).
 *
 * Bu XATO EMAS: amal bajarildi, lekin HR e'tibor berishi kerak bo'lgan
 * narsa bor. `code` — tarjima kaliti (`useErrorMessage`), `message` —
 * serverning o'zbekcha matni (tarjimasi topilmasa zaxira sifatida).
 */
export interface HrWarning {
  code: string
  message: string
}

/**
 * Ishga qabulning natijasi — `HireResultDto` ning ekranga kerak bo'lgan
 * qismi.
 *
 * `DirectHireResult` ichida turadi va ekran shu javobdan darhol yangi
 * xodim kartasiga havola qo'yadi — qo'shimcha so'rovsiz.
 */
export interface HireOutcome {
  /** Yaratilgan mehnat munosabati — bizga faqat `employeeId` kerak. */
  employeeId: string | null
  employmentId: string | null
  /** ⚠️ E'tiborsiz qolmasligi kerak: stavka chegarasi va h.k. */
  warnings: HrWarning[]
}

/** `DirectHireResultDto` — nomzod ishga olinganining natijasi. */
export interface DirectHireResult {
  candidate: Candidate
  hire: HireOutcome
}

/* ═══════════════════════════════════════════════════ so'rov tanalari */

export interface VacancyInput {
  /** Majburiy — bo'sh bo'lsa «vakansiya.nom_yoq» (400). */
  title: string
  openings: number
  openedAt: string | null
  note: string | null
}

export interface CandidateInput {
  lastName: string
  firstName: string
  middleName: string | null
  birthDate: string | null
  phone: string | null
  address: string | null
  speciality: string | null
  note: string | null
}

/** Yaratishda vakansiya, boshlang'ich bosqich va manba ham beriladi. */
export interface RegisterCandidateInput extends CandidateInput {
  vacancyId: string
  /** Bo'sh — voronkaning birinchi faol bosqichi (server tanlaydi). */
  stageId: string | null
}

/**
 * ⭐ Nomzodni ishga olishda so'raladigan qism (`HireCandidateRequest`).
 *
 * Firma, bo'lim va lavozim AYNAN shu yerda: vakansiyada ular umuman
 * saqlanmaydi (`Vacancy` tipidagi izohga qarang), ya'ni «bu odam qaysi
 * firmaga, qaysi bo'limga va qaysi shtat birligiga olinadi» degan qaror
 * shu formada qabul qilinadi.
 *
 * Shaxsiy ma'lumot (F.I.Sh., tug'ilgan sana, telefon, manzil,
 * mutaxassislik) NOMZODDAN ko'chadi va bu yerda umuman yo'q.
 */
export interface HireCandidateInput {
  /** MAJBURIY — «nomzod.yuridik_shaxs_yoq» (400). */
  legalEntityId: string
  /** MAJBURIY — «nomzod.lavozim_yoq» (400). */
  positionId: string
  /** Ixtiyoriy; ko'rsatilsa mavjud bo'lishi shart («nomzod.bolim_topilmadi»). */
  departmentId: string | null
  /** Bo'sh — server bugungi sanani oladi. */
  hireDate: string | null
  orderNo: string | null
  orderDate: string | null
  /** ⭐ Dublikat xodim qidiruvi AYNAN shunga tayanadi. */
  pinfl: string | null
  passport: string | null
  /** Bo'sh — oklad yozuvi umuman ochilmaydi. */
  salaryAmount: number | null
  rate: number | null
  isPrimary: boolean
  responsibleEmployeeId: string | null
  /** Bo'sh — lavozimdagi qiymat, u ham bo'lmasa 90 kun. */
  probationDays: number | null
}

/* ═══════════════════════════════════════════════════════════════ filtrlar */

export interface VacancyFilters {
  status?: VacancyStatus
  /**
   * E'lon nomi bo'yicha qidiruv — nomning ISTALGAN qismi, katta-kichik
   * harf farqsiz (serverda).
   *
   * Lavozim, bo'lim va firma filtrlari OLIB TASHLANDI: server ularni
   * endi umuman qabul qilmaydi, chunki vakansiyada bunday maydonlar
   * yo'q. «Qaysi bo'limga kim olindi» degan savolga xodimlar ro'yxati
   * javob beradi.
   */
  search?: string
  page: number
  pageSize: number
  /** `openedAt` (sukut, yangidan eskiga) yoki `title`; `-` — kamayish. */
  sort?: string
}

export interface CandidateFilters {
  /** F.I.Sh. yoki TELEFON bo'yicha — server ikkalasini ham qidiradi. */
  search?: string
  vacancyId?: string
  stageId?: string
  status?: CandidateStatus
  source?: CandidateSource
  page: number
  pageSize: number
  /** `createdAt` (sukut), `name`, `stage`, `nextContact`; `-` — kamayish. */
  sort?: string
  /**
   * ⭐ FILTR: keyingi aloqa sanasi qo'yilgan VA u bugun yoki undan oldin
   * bo'lgan nomzodlar — «bugun kim bilan gaplashish kerak».
   *
   * «Bugun» ni SERVER hisoblaydi (Toshkent kalendari), klient emas:
   * brauzer sanasi boshqa mintaqada bo'lishi mumkin va u holda ro'yxat
   * bir kunga surilib ketardi.
   */
  contactDue?: boolean
  /**
   * ⭐ BO'LIM bayrog'i: «Rad etilganlar» — `Rejected` VA `Withdrawn`.
   *
   * NEGA holat filtri emas: bitta `<select>` ikkita qiymatni ifodalay
   * olmaydi, `status` esa bitta holat (`?status=Rejected,Withdrawn` uchun
   * butun filtr va manzil sxemasi ko'p qiymatli bo'lib ketardi —
   * SPEC §4.7). Bayroq holat filtri bilan BIRGA ham ishlaydi:
   * `rejected=true&status=Withdrawn` — faqat o'zi voz kechganlar.
   *
   * ⚠️ Bu FILTR EMAS, BO'LIM: bo'sh natija «hech kim rad etilmagan»
   * degani, «qidiruv topilmadi» degani emas — ekrandagi bo'sh holat matni
   * shu farqqa tayanadi.
   */
  rejected?: boolean
  /**
   * Rad etilganlar va o'zi voz kechganlar ham ro'yxatga QO'SHILSINMI.
   *
   * `rejected` dan farqi: u BO'LIM (faqat shu ikki holat), bu esa
   * KENGAYTMA — jarayondagilar ham qoladi. «Nomzodlar» bo'limidagi
   * qidiruv shu bilan so'raydi (egasi, 2026-10-15): HR rad etilgan odamni
   * ham ismi bilan topishi kerak, aks holda uni qayta kiritardi.
   */
  includeRejected?: boolean
  /**
   * Excel'dan ko'chirilgan kengash ARXIVI ham qo'shilsinmi.
   *
   * Sukut bo'yicha server arxivni ro'yxatdan chiqaradi (ekran «hozir kim
   * bor?» degan savolga javob beradi). «Rezerv» bo'limi esa uni SO'RASHI
   * shart: arxivdagi rezerv qatorlari ham o'sha bo'limning bir qismi va
   * bayroqsiz ular hech qayerda ko'rinmasdi — na nomzodlar ro'yxatida
   * (rezerv u yerda yashirin), na rezervda.
   */
  includeArchive?: boolean
}

/**
 * Telefon bo'yicha topilgan nomzod — `GET /candidates/phone-matches`
 * javobining bitta qatori.
 *
 * Maydonlar ataylab OZ: oyna faqat «kim, qaysi holatda, qaysi vakansiyaga,
 * qachon» degan savolga javob beradi va karta havolasini qo'yadi. Telefon
 * raqamining o'zi qaytmaydi — u HR ning o'zi terib turgan raqam.
 */
export interface CandidatePhoneMatch {
  id: string
  fullName: string
  /** Hamma holat keladi, Excel arxivi ham — yumshoq o'chirilganlardan tashqari. */
  status: CandidateStatus
  vacancyTitle: string | null
  /** Kiritilgan lahza — ISO satr, UTC. */
  createdAt: string
}

/* ═════════════════════════════════════════════════ `unknown` dan o'qish */

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function count(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

/**
 * Sanoqni tekshiradi: kutilmagan qiymat zaxiraga tushadi.
 *
 * NEGA zaxira, xato emas: sanoqqa yangi qiymat backend tomonda
 * qo'shilishi mumkin (masalan `CandidateSource` ga «hh.uz» —
 * domen izohida shunday deyilgan). Eski klient uni ko'rganda butun
 * ro'yxatni yiqitmasligi kerak; noma'lum qiymat shunchaki eng
 * zararsiz ko'rinishga tushadi.
 */
export function oneOf<T extends string>(value: unknown, allowed: readonly T[], fallback: T): T {
  return typeof value === 'string' && (allowed as readonly string[]).includes(value)
    ? (value as T)
    : fallback
}

/**
 * `oneOf` ning zaxirasiz varianti — ANKETA sanoqlari uchun.
 *
 * NEGA zaxira qiymat berilmaydi: holat sanoqlarida («Open»,
 * «InProgress») eng zararsiz qiymat bor va noma'lum javob unga tushsa
 * ro'yxat baribir o'qiladi. Anketa esa NOMZODNING JAVOBI: tanilmagan
 * qiymatni «O'rta maktab» yoki «Tanishlar orqali» deb ko'rsatish
 * yolg'on ma'lumot bo'lardi va HR uni haqiqiy javobdan ajrata olmasdi.
 * `null` esa qatorni umuman chizdirmaydi.
 *
 * Server nomni PascalCase yuboradi; ro'yxatdagi qiymat kichik harf
 * bilan bo'lishi ham mumkin (`LANGUAGE_LEVELS`), shu sababli
 * solishtirish REGISTRGA BEFARQ — `employee-competency-api.ts` dagi
 * bilan bir xil kelishuv.
 */
function enumOrNull<T extends string>(value: unknown, allowed: readonly T[]): T | null {
  if (typeof value !== 'string') return null
  const needle = value.toLowerCase()
  return allowed.find((name) => name.toLowerCase() === needle) ?? null
}

/**
 * Sahifalangan javob.
 *
 * NEGA `employments-api.ts` dagi «himoyalangan» varianti KO'CHIRILMADI:
 * u yerda `PagedResult` ning shakli tekshirilmagan edi va o'qish uchta
 * ehtimoliy nomni sinab ko'rardi (`total`/`totalCount`/`totalItems`).
 * Bu yerda shakl ANIQ va jonli endpoint'da tasdiqlangan:
 * `{items, page, pageSize, total, pageCount}`
 * (`EmployeeDtos.cs:22`). Taxminlarni ko'chirish ularni haqiqatga
 * aylantirib qo'yadi — noto'g'ri javob kelsa, u yashirinmasligi kerak.
 */
function readPage<T>(
  data: unknown,
  map: (raw: unknown) => T,
  filters: { page: number; pageSize: number },
): PagedResult<T> {
  const box = (data ?? {}) as Record<string, unknown>
  const items = (Array.isArray(box['items']) ? box['items'] : []).map(map)

  return {
    items,
    total: count(box['total']),
    page: count(box['page']) || filters.page,
    pageSize: count(box['pageSize']) || filters.pageSize,
  }
}

/**
 * Filtrni so'rov satriga o'giradi: bo'sh qiymat UMUMAN yuborilmaydi.
 *
 * NEGA muhim: backend bo'sh satrni ham tekshiradi va `?status=` ni
 * «noto'g'ri holat» deb 400 qaytarishi mumkin edi. Uni sanoq
 * o'qiydigan joyda (`RecruitingQueryParsing`) maxsus qoplab
 * qo'yishgan, lekin klient baribir keraksiz shovqin yubormasligi kerak.
 *
 * ⚠️ `boolean` ham qabul qilinadi, lekin `false` TASHLANMAYDI — bu
 * yordamchi «bo'sh qiymat» deb faqat `undefined` va `''` ni biladi.
 * Ya'ni yoqilmagan bayroqni chaqiruvchi O'ZI `undefined` ga aylantirishi
 * kerak (`filters.contactDue === true ? true : undefined`). Aks holda
 * so'rov satrida `?contactDue=false` bo'lib, server uchun ma'nosiz,
 * keshda esa boshqa kalit hosil qiladigan shovqin paydo bo'lardi.
 */
function params(
  source: Record<string, string | number | boolean | undefined>,
): Record<string, unknown> {
  const result: Record<string, unknown> = {}

  for (const [key, value] of Object.entries(source)) {
    if (value === undefined || value === '') continue
    result[key] = value
  }

  return result
}

/* ═══════════════════════════════════════════════════════════ xaritalash */

function toStage(raw: unknown): PipelineStage {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    name: text(box['name']) ?? '—',
    isActive: box['isActive'] === true,
    note: text(box['note']),
    sortOrder: count(box['sortOrder']),
    invitesToInterview: box['invitesToInterview'] === true,
  }
}

function toVacancy(raw: unknown): Vacancy {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    // Server nomni majburiy qiladi — bo'sh satr faqat buzilgan javobga
    // qarshi sug'urta (formada «—» matni oldindan yozilib qolmasin).
    title: text(box['title']) ?? '',
    openings: count(box['openings']),
    status: oneOf(box['status'], VACANCY_STATUSES, 'Open'),
    openedAt: text(box['openedAt']) ?? '',
    closedAt: text(box['closedAt']),
    note: text(box['note']),
    candidateCount: count(box['candidateCount']),
    hiredCount: count(box['hiredCount']),
    isSystem: box['isSystem'] === true,
  }
}

function toStageEntry(raw: unknown): CandidateStageEntry {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    stageId: text(box['stageId']) ?? '',
    stage: text(box['stage']),
    date: text(box['date']) ?? '',
    conclusion: text(box['conclusion']),
    recordedBy: text(box['recordedBy']),
    recordedAt: text(box['recordedAt']) ?? '',
    updatedAt: text(box['updatedAt']),
  }
}

function toCandidateRow(raw: unknown): CandidateListItem {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    fullName: text(box['fullName']) ?? '—',
    shortName: text(box['shortName']) ?? '—',
    phone: text(box['phone']),
    birthDate: text(box['birthDate']),
    speciality: text(box['speciality']),
    vacancyId: text(box['vacancyId']) ?? '',
    vacancy: text(box['vacancy']),
    currentStageId: text(box['currentStageId']),
    currentStage: text(box['currentStage']),
    status: oneOf(box['status'], CANDIDATE_STATUSES, 'InProgress'),
    source: oneOf(box['source'], CANDIDATE_SOURCES, 'Manual'),
    hasResume: box['hasResume'] === true,
    createdAt: text(box['createdAt']) ?? '',
    hiredEmployeeId: text(box['hiredEmployeeId']),
    nextContactDate: text(box['nextContactDate']),
    closedAt: text(box['closedAt']),
    closingNote: text(box['closingNote']),
  }
}

/**
 * Biriktirilgan fayl.
 *
 * `sizeBytes` uchun `count(...)`: son bo'lmagan qiymat `0` ga tushadi va
 * `0` shartnomada allaqachon «noma'lum hajm» degani — ya'ni bu yerda
 * maxsus zaxira o'ylab topish shart emas.
 */
function toCandidateFile(raw: unknown): CandidateFile {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    // Nom bo'sh kelsa ham qator chizilishi kerak: fayl BOR, faqat nomi
    // yo'q (ko'chirilgan yozuvda shunday bo'lishi mumkin edi).
    fileName: text(box['fileName']) ?? '—',
    // Tur bo'yicha faqat belgi tanlanadi (`image/` bilan boshlanishi),
    // shu sababli tanilmagan qiymat xavfsiz: hujjat belgisi chiziladi.
    contentType: text(box['contentType']) ?? 'application/octet-stream',
    sizeBytes: count(box['sizeBytes']),
    uploadedAt: text(box['uploadedAt']) ?? '',
  }
}

/**
 * Amaldan qolgan izoh.
 *
 * Matni bo'sh yozuv serverda UMUMAN hosil bo'lmaydi («qator bor, chunki
 * matn bor» — SPEC §2.3), shu sababli bu yerda ham bo'sh matn zaxirasi
 * yo'q: kutilmagan holatda qator ko'rinadi, lekin matni chiziqcha bo'ladi
 * va yolg'on ma'lumot chizilmaydi.
 */
function toCandidateNote(raw: unknown): CandidateNote {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    kind: oneOf(box['kind'], CANDIDATE_NOTE_KINDS, 'Reassign'),
    text: text(box['text']) ?? '—',
    createdBy: text(box['createdBy']),
    createdAt: text(box['createdAt']) ?? '',
  }
}

/**
 * Anketa. `null` — nomzodda anketa YO'Q (HR qo'lda kiritgan yoki bot
 * ishga tushgunicha yozilgan yozuv) va bu odatiy hol.
 */
function toQuestionnaire(raw: unknown): CandidateQuestionnaire | null {
  if (raw === null || raw === undefined || typeof raw !== 'object') return null
  const box = raw as Record<string, unknown>

  return {
    education: enumOrNull(box['education'], EDUCATION_LEVELS),
    workExperience: text(box['workExperience']),
    maritalStatus: enumOrNull(box['maritalStatus'], MARITAL_STATUSES),
    russianLevel: enumOrNull(box['russianLevel'], LANGUAGE_LEVELS),
    otherLanguages: text(box['otherLanguages']),
    officeSkills: text(box['officeSkills']),
    lastSalary: text(box['lastSalary']),
    extraNote: text(box['extraNote']),
    hasDriverLicense: box['hasDriverLicense'] === true,
    driverLicenseCategories: text(box['driverLicenseCategories']),
    expectedSalary: enumOrNull(box['expectedSalary'], SALARY_EXPECTATIONS),
    referralSource: enumOrNull(box['referralSource'], REFERRAL_SOURCES),
    offerAcceptedAt: text(box['offerAcceptedAt']),
  }
}

function toCandidate(raw: unknown): Candidate {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    lastName: text(box['lastName']) ?? '',
    firstName: text(box['firstName']) ?? '',
    middleName: text(box['middleName']),
    fullName: text(box['fullName']) ?? '—',
    birthDate: text(box['birthDate']),
    phone: text(box['phone']),
    address: text(box['address']),
    speciality: text(box['speciality']),
    vacancyId: text(box['vacancyId']) ?? '',
    vacancy: text(box['vacancy']),
    currentStageId: text(box['currentStageId']),
    currentStage: text(box['currentStage']),
    status: oneOf(box['status'], CANDIDATE_STATUSES, 'InProgress'),
    source: oneOf(box['source'], CANDIDATE_SOURCES, 'Manual'),
    note: text(box['note']),
    closedAt: text(box['closedAt']),
    closingNote: text(box['closingNote']),
    nextContactDate: text(box['nextContactDate']),
    hasPhoto: box['hasPhoto'] === true,
    hiredEmployeeId: text(box['hiredEmployeeId']),
    hiredEmploymentId: text(box['hiredEmploymentId']),
    interviewAt: text(box['interviewAt']),
    interviewNoticeSentAt: text(box['interviewNoticeSentAt']),
    interviewNoticeDelivered:
      typeof box['interviewNoticeDelivered'] === 'boolean' ? box['interviewNoticeDelivered'] : null,
    stages: (Array.isArray(box['stages']) ? box['stages'] : []).map(toStageEntry),
    // Massiv kelmasa BO'SH ro'yxat: «fayl yo'q» va «javob kutilmagan
    // shaklda» ekranda bir xil ko'rinadi va ikkalasi ham kartani
    // yiqitmaydi (`stages` bilan aynan bir xil qoida).
    files: (Array.isArray(box['files']) ? box['files'] : []).map(toCandidateFile),
    notes: (Array.isArray(box['notes']) ? box['notes'] : []).map(toCandidateNote),
    questionnaire: toQuestionnaire(box['questionnaire']),
  }
}

/**
 * Telefon bo'yicha topilgan nomzod.
 *
 * Holat uchun zaxira `InProgress` — `toCandidateRow` dagi bilan bir xil
 * qoida: noma'lum holat qatorni yiqitmaydi. Bu yerda u eng EHTIYOTKOR
 * qiymat ham: «jarayonda» degan ogohlantirish HR ni tekshirishga undaydi.
 */
function toPhoneMatch(raw: unknown): CandidatePhoneMatch {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    fullName: text(box['fullName']) ?? '—',
    status: oneOf(box['status'], CANDIDATE_STATUSES, 'InProgress'),
    vacancyTitle: text(box['vacancyTitle']),
    createdAt: text(box['createdAt']) ?? '',
  }
}

function toHireOutcome(raw: unknown): HireOutcome {
  const hire = (raw ?? {}) as Record<string, unknown>
  const employment = (hire['employment'] ?? {}) as Record<string, unknown>

  return {
    // Xodim identifikatori mehnat munosabati ICHIDA
    // (`HireResultDto.Employment` → `EmploymentDto`), yassi emas.
    employeeId: text(employment['employeeId']),
    employmentId: text(employment['id']),
    warnings: (Array.isArray(hire['warnings']) ? hire['warnings'] : []).flatMap((item) => {
      const warning = (item ?? {}) as Record<string, unknown>
      const code = text(warning['code'])
      return code === null ? [] : [{ code, message: text(warning['message']) ?? code }]
    }),
  }
}

function toDirectHireResult(raw: unknown): DirectHireResult {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    candidate: toCandidate(box['candidate']),
    hire: toHireOutcome(box['hire']),
  }
}

/* ═══════════════════════════════════════════════════════════ bosqichlar */

const STAGES = '/api/recruiting/stages'

export async function fetchStages(
  onlyActive: boolean,
  signal?: AbortSignal,
): Promise<PipelineStage[]> {
  const { data } = await api.get<unknown>(STAGES, { params: { onlyActive }, signal })
  return Array.isArray(data) ? data.map(toStage) : []
}

/*
  BOSQICHLARNI TAHRIRLASH KLIENTDA YO'Q (2026-08-31, foydalanuvchi
  qarori: «voronka bosqichini menyudan olib tashlayver, bizga umuman
  kerakmas»).

  Voronka uchta bosqichdan iborat va u MBF ning jarayoni — u yiliga
  ham o'zgarmaydi. Uni tahrirlash ekrani HR ning kundalik ishida
  hech qachon ochilmasdi va menyuda faqat joy egallab turardi.

  SERVERDAGI yozish endpoint'lari qoldi (`POST`, `PUT`, `/reorder`,
  `/activate`, `/deactivate`): jarayon o'zgarsa, bosqichlar
  konteynerni qayta yig'masdan o'zgartiriladi. Aynan shu yo'l bilan
  uchta ortiqcha bosqich arxivlangan edi.
*/

/* ══════════════════════════════════════════════════════════ vakansiyalar */

const VACANCIES = '/api/recruiting/vacancies'

export async function fetchVacancies(
  filters: VacancyFilters,
  signal?: AbortSignal,
): Promise<PagedResult<Vacancy>> {
  const { data } = await api.get<unknown>(VACANCIES, {
    params: params({
      status: filters.status,
      search: filters.search,
      page: filters.page,
      pageSize: filters.pageSize,
      sort: filters.sort,
    }),
    signal,
  })

  return readPage(data, toVacancy, filters)
}

export async function saveVacancy(id: string | null, body: VacancyInput): Promise<Vacancy> {
  const { data } =
    id === null
      ? await api.post<unknown>(VACANCIES, body)
      : await api.put<unknown>(`${VACANCIES}/${id}`, body)

  return toVacancy(data)
}

/**
 * Yopish va qayta ochish — bitta funksiya: ikkalasi ham TANASIZ va javobi
 * bir xil (yangilangan vakansiya).
 *
 * Amal nomi to'g'ridan-to'g'ri MANZILGA tushadi va bu ataylab: server
 * marshrutlari ham aynan shunday nomlanadi (`/close`, `/reopen`).
 *
 * NEGA tana yo'q: holat endi BIR BOSISHDA o'zgaradi (egasining qarori —
 * vakansiya eng sodda shaklda). `/close` tanasi serverda ixtiyoriy va
 * u bo'lmasa yopilgan sana — bugun, sababsiz. Qayta ochishda esa tana
 * umuman kutilmaydi.
 *
 * ⚠️ Qayta ochishda «vakansiya.nom_band» (409) kelishi mumkin: shu nomli
 * BOSHQA ochiq vakansiya allaqachon bor.
 */
export async function changeVacancyStatus(
  id: string,
  action: 'close' | 'reopen',
): Promise<Vacancy> {
  const { data } = await api.post<unknown>(`${VACANCIES}/${id}/${action}`)
  return toVacancy(data)
}

/**
 * Yumshoq o'chirish: yozuv bazada qoladi.
 *
 * ⚠️ Server FAQAT nomzodi yo'q vakansiyani o'chiradi
 * («vakansiya.nomzodlari_bor», 409): nomzodi bori — tarix va uni
 * o'chirish nomzodlarning yozuvini yetim qoldirardi. Ro'yxatda bunday
 * qatorda tugma umuman chizilmaydi (`vacancies-page.tsx`), lekin
 * xatoni ushlash baribir kerak — ro'yxat eskirgan bo'lishi mumkin.
 */
export async function deleteVacancy(id: string): Promise<void> {
  await api.delete(`${VACANCIES}/${id}`)
}

/* ═════════════════════════════════════════════════════════════ nomzodlar */

const CANDIDATES = '/api/recruiting/candidates'

export async function fetchCandidates(
  filters: CandidateFilters,
  signal?: AbortSignal,
): Promise<PagedResult<CandidateListItem>> {
  const { data } = await api.get<unknown>(CANDIDATES, {
    params: params({
      search: filters.search,
      vacancyId: filters.vacancyId,
      stageId: filters.stageId,
      status: filters.status,
      source: filters.source,
      page: filters.page,
      pageSize: filters.pageSize,
      sort: filters.sort,
      /*
        Ikkala bayroq FAQAT `true` bo'lganda yuboriladi. `false` ni
        yuborish serverda hech narsani o'zgartirmasdi (u `bool?` ni
        `?? false` bilan o'qiydi), lekin so'rov satrida keraksiz shovqin
        qoldirardi — `params` izohidagi qoida.
      */
      contactDue: filters.contactDue === true ? true : undefined,
      rejected: filters.rejected === true ? true : undefined,
      includeRejected: filters.includeRejected === true ? true : undefined,
      includeArchive: filters.includeArchive === true ? true : undefined,
    }),
    signal,
  })

  return readPage(data, toCandidateRow, filters)
}

/**
 * ⭐ Shu telefon raqamli nomzodlar — takroriy kiritishni OGOHLANTIRISH uchun.
 *
 * Server raqamdan faqat xonalarni oladi va 9 xonali (mahalliy) yoki «998»
 * bilan boshlanadigan 12 xonali raqamni tushunadi. Boshqa har qanday
 * qiymat XATO EMAS — bo'sh ro'yxat (200). Ya'ni chaqiruvchi to'liq
 * bo'lmagan raqamni yuborsa ham ekran buzilmaydi; so'rovni faqat to'liq
 * raqamda yuborish esa uning vazifasi (`useCandidatePhoneMatches`).
 *
 * `exceptId` — tahrirlanayotgan nomzodning o'zi: u o'z raqami bilan
 * «topilib» qolmasligi kerak.
 *
 * Javob beshtagacha qator, jarayondagilar birinchi, keyin yangisidan
 * eskisiga — tartib SERVERNIKI, klientda qayta saralanmaydi.
 */
export async function fetchCandidatePhoneMatches(
  phone: string,
  exceptId?: string,
  signal?: AbortSignal,
): Promise<CandidatePhoneMatch[]> {
  const { data } = await api.get<unknown>(`${CANDIDATES}/phone-matches`, {
    params: params({ phone, exceptId }),
    signal,
  })

  return Array.isArray(data) ? data.map(toPhoneMatch) : []
}

export async function fetchCandidate(id: string, signal?: AbortSignal): Promise<Candidate> {
  const { data } = await api.get<unknown>(`${CANDIDATES}/${id}`, { signal })
  return toCandidate(data)
}

/**
 * Nomzodni voronkaga kiritish.
 *
 * `source` yuborilmaydi — server sukut bo'yicha `Manual` qo'yadi va bu
 * ekrandan kiritilgan har bir nomzod aynan shunday. `TelegramBot` ni
 * faqat botning o'zi yozadi (keyingi agent).
 */
export async function registerCandidate(body: RegisterCandidateInput): Promise<Candidate> {
  const { data } = await api.post<unknown>(CANDIDATES, body)
  return toCandidate(data)
}

/** Tahrirlash — FAQAT shaxsiy maydonlar; vakansiya va manba o'zgarmaydi. */
export async function updateCandidate(id: string, body: CandidateInput): Promise<Candidate> {
  const { data } = await api.put<unknown>(`${CANDIDATES}/${id}`, body)
  return toCandidate(data)
}

/* ────────────────────────── keyingi aloqa sanasi: yozish qoidasi ─────────
 *
 * ⭐ IKKITA maydon, bittasi emas: `nextContactDate` VA `clearNextContact`.
 *
 * NEGA «null — tozalash» QILINMADI. `POST /candidates/{id}/stages` ga
 * formasiz mijoz ham murojaat qilishi mumkin (ilgari voronkadagi SUDRASH
 * shunday edi — `conclusion: null` yuborardi; 2026-09-26 dan u ham oyna
 * ochadi). «null — tozalash» shartnomasida bunday so'rov HR besh daqiqa
 * oldin qo'ygan sanani JIMGINA o'chirib tashlardi.
 *
 * Uchta holat, aniq:
 *   `nextContactDate` bor            → sana QO'YILADI
 *   `clearNextContact: true`         → sana TOZALANADI (`nextContactDate` e'tiborsiz)
 *   ikkalasi ham yo'q (yoki `null`)  → sana O'ZGARMAYDI
 *
 * Shu sababli ikkala maydon ham IXTIYORIY (`?`): ularni umuman
 * yubormaydigan chaqiruv (sudrash) o'zgarmasdan qoldi. Ikkita maydon
 * xunukroq, lekin ANIQ — bu loyihada aniqlik jimlikdan doim ustun
 * (`includeArchive`, `excludedVacancyId` bilan bir xil qaror,
 * SPEC §4.1).
 *
 * `nextContactDate` — `YYYY-MM-DD` (kalendar sanasi, Toshkent). Vaqt va
 * mintaqa siljishi YO'Q: suhbat vaqtidan farqli o'laroq, bu sana
 * nomzodga xabar bo'lib ketmaydi.
 */

/**
 * Vakansiyani almashtirish — alohida amal: nomzod boshqa o'ringa mos
 * kelib qolgan.
 *
 * `note` — bosqichi bo'lmagan izoh: server uni `candidate_notes` ga
 * `Reassign` turi bilan yozadi (bo'sh bo'lsa yozuv umuman hosil
 * bo'lmaydi). U nomzodning `note` maydoni EMAS — u shaxsning uzoq
 * muddatli ta'rifi va tahrirlash formasidan qayta yozilib ketardi
 * (SPEC §2.3.1).
 *
 * `nextContactDate` bo'lsa qo'yiladi, bo'lmasa sana o'zgarmaydi —
 * tozalash bu amalda YO'Q (yuqoridagi qoidaga qarang).
 */
export async function reassignCandidate(
  id: string,
  body: {
    vacancyId: string
    note?: string | null
    nextContactDate?: string | null
  },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/reassign`, body)
  return toCandidate(data)
}

/**
 * ⭐ Voronkaning YAGONA harakati.
 *
 * ⭐ Har chaqiruv TARIXGA YANGI QATOR qo'shadi va mavjud yozuvning
 * ustidan hech qachon yozmaydi: bitta bosqich necha marta
 * takrorlansa, shuncha qator bo'ladi. Ochib qo'yilgan «kutilmoqda»
 * yozuviga natija yozish — boshqa amal
 * (`editCandidateStageEntry`).
 *
 * Oldinga yurish majburlanmaydi: bosqichni o'tkazib yuborish va
 * orqaga qaytish ham mumkin.
 */
/**
 * ⭐ Nomzodni suhbatga chaqirish — bitta so'rovda uchta natija.
 *
 * Server bosqichni yozadi, suhbat vaqtini qo'yadi va Telegram taklifini
 * navbatga tashlaydi. Ularni uch so'rovga bo'lish «bosqich yozildi,
 * xabar ketmadi» degan yarim holatni tug'dirardi.
 *
 * ⚠️ `interviewAt` SILJISH bilan yuboriladi («…+05:00»). Siljishsiz
 * qiymatni server UTC deb o'qiydi va nomzod besh soat oldingi vaqtni
 * ko'rardi.
 *
 * `stageId` bo'sh bo'lsa bosqich o'zgarmaydi — suhbatni KO'CHIRISH
 * aynan shunday ishlaydi.
 *
 * ⭐ `conclusion` ning IKKITA manzili bor va qaysi biriga tushishini
 * SERVER hal qiladi:
 *   `stageId` bor  → yangi bosqich yozuvining xulosasi (bugungi xatti-harakat)
 *   `stageId` bo'sh → `candidate_notes` yozuvi, `InterviewRescheduled` turi
 * Klient DOIM bitta matn maydonini yuboradi. Tanlovni klientga berish
 * server qarorini brauzerga ko'chirardi va yana bitta xato qilish joyi
 * paydo bo'lardi (SPEC §4.2).
 *
 * `nextContactDate` / `clearNextContact` — yuqoridagi uch holatli qoida.
 */
export async function inviteCandidateToInterview(
  id: string,
  body: {
    interviewAt: string
    stageId: string | null
    conclusion: string | null
    nextContactDate?: string | null
    clearNextContact?: boolean
  },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/interview-invite`, body)
  return toCandidate(data)
}

/**
 * Bosqich natijasini yozish.
 *
 * `nextContactDate` / `clearNextContact` IXTIYORIY — voronkadagi sudrash
 * ularni umuman yubormaydi va sana o'zgarmasdan qoladi (yuqoridagi
 * «keyingi aloqa sanasi» qoidasi).
 */
export async function recordCandidateStage(
  id: string,
  body: {
    stageId: string
    date: string | null
    conclusion: string | null
    nextContactDate?: string | null
    clearNextContact?: boolean
  },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/stages`, body)
  return toCandidate(data)
}

/**
 * ⭐ Tarixdagi BITTA yozuvni tuzatish.
 *
 * `recordCandidateStage` dan farqi bitta va u hal qiluvchi: bu yerda
 * YANGI qator qo'shilmaydi — manzilda ko'rsatilgan qator o'zgaradi.
 * Ikkalasi ikki xil ish uchun: «yana bir marta gaplashdim» (yangi
 * qator) va «o'sha qatorga natijani yozdim / xatoni tuzatdim».
 *
 * ⚠️ `conclusion` USTIDAN yozadi. Oyna mavjud matnni to'ldirib
 * ochilishi SHART — bo'sh yuborilsa izoh o'chadi. Bu shartnomaning
 * o'zi ilgari nosozlik manbai bo'lgan: server oxirgi yozuvni jimgina
 * to'ldirar va bo'sh xulosa bilan eski matnni o'chirib yuborardi.
 *
 * Bosqich yuborilmaydi: yozuvning bosqichi o'zgarmaydi (server
 * shartnomasi).
 */
export async function editCandidateStageEntry(
  id: string,
  entryId: string,
  body: {
    date: string | null
    conclusion: string | null
    nextContactDate?: string | null
    clearNextContact?: boolean
  },
): Promise<Candidate> {
  const { data } = await api.put<unknown>(`${CANDIDATES}/${id}/stages/${entryId}`, body)
  return toCandidate(data)
}

/**
 * Voronkadan chiqarish: «rad etdik» (`reject`), «o'zi voz kechdi»
 * (`withdraw`) va «rezervga» (`reserve`). Uchalasi ATAYLAB ajratilgan —
 * voronka hisobotida ular butunlay boshqa ma'no beradi
 * (`CandidateStatus` izohi): birinchisi tanlov sifati, ikkinchisi
 * taklif shartlari, uchinchisi esa «odam yaxshi, o'rin yo'q» haqida
 * gapiradi.
 *
 * Uchalasining SO'ROV TANASI bir xil (`CloseCandidateRequest`: sana va
 * sabab), ya'ni bitta funksiya yetadi. `reserve` dan farqli o'laroq,
 * qolgan ikkitasi qaytarilmaydi — qaytarish faqat rezervda bor
 * (`returnCandidateToFunnel`).
 *
 * ⚠️ `nextContactDate` ni FAQAT `reserve` hisobga oladi. `reject` va
 * `withdraw` uni JIMGINA e'tiborsiz qoldiradi va saqlangan sanani
 * TOZALAYDI — rad etilgan nomzod «bugun qo'ng'iroq qilinadiganlar»
 * ro'yxatida abadiy turib qolsa, HR o'sha ro'yxatga ishonmay qo'yardi.
 * Rezerv esa chiqish emas, KECHIKTIRISH («o'rin bo'shasa chaqiramiz»),
 * ya'ni u yerda sana o'z joyida (SPEC §2.1.2). Ekranda maydon faqat
 * rezerv uchun chiziladi, lekin server xato qaytarmaydi — shu sababli
 * tip ham uchalasi uchun bitta.
 */
export async function closeCandidate(
  id: string,
  action: 'reject' | 'withdraw' | 'reserve' | 'blacklist',
  body: { at: string | null; reason: string | null; nextContactDate?: string | null },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/${action}`, body)
  return toCandidate(data)
}

/**
 * ⭐ Rezervdagi nomzodni voronkaga QAYTARADI — rezervning butun ma'nosi
 * shu amalda.
 *
 * Nomzod qaysi bosqichda rezervga o'tgan bo'lsa, o'sha yerdan davom
 * etadi (server bosqichlar tarixini saqlaydi), chiqish sanasi va sababi
 * esa tozalanadi.
 *
 * So'rov tanasi IXTIYORIY va u ilgari umuman yo'q edi. Tanani
 * yubormaydigan chaqiruv AVVALGIDEK ishlaydi — server `null` tanani ham
 * qabul qiladi (yopish amallaridagi `CloseCandidateRequest?` bilan aynan
 * bir xil naqsh).
 *
 * `note` — QAYTARISH SABABI, `ReturnToFunnel` turidagi izoh. U
 * `closingNote` GA yozilmaydi va yozilishi ham mumkin emas: qaytarish
 * `closingNote` ni o'sha tranzaksiyada TOZALAYDI, ya'ni izoh yozilgan
 * zahoti o'chib ketardi (SPEC §2.3.1). Lentadagi yozuv esa qoladi —
 * qaytarilgan nomzodda «nega rad etilgan edi» degan savolning javobi
 * faqat o'sha yerda.
 *
 * ⚠️ Rad etilgan va voz kechgan nomzodda sabab MAJBURIY
 * («nomzod.qaytarish_sababi_yoq», 400), rezervdan qaytarishda
 * ixtiyoriy. Ishga OLINGAN nomzod qaytarilmaydi —
 * «nomzod.qaytarilmaydi» (409): u bir vaqtda ham xodim, ham faol
 * nomzod bo'lib qolardi.
 */
export async function returnCandidateToFunnel(
  id: string,
  body?: { note?: string | null; nextContactDate?: string | null },
): Promise<Candidate> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/return`, body)
  return toCandidate(data)
}

/**
 * ⚠️ QAYTARIB BO'LMAYDIGAN AMAL: nomzod XODIMGA aylanadi.
 *
 * Bitta tranzaksiyada: nomzod «ishga olingan» bo'lib voronkadan chiqadi,
 * xodim kartasi, mehnat munosabati, ishga qabul buyrug'i va MAJBURIY
 * sinov muddati yaratiladi.
 */
export async function hireCandidate(
  id: string,
  body: HireCandidateInput,
): Promise<DirectHireResult> {
  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/hire`, body)
  return toDirectHireResult(data)
}

/* ─────────────────────────────────────────────────────────────── fayllar */

/*
 * Nomzod fayllari — o'ntagacha, bitta rezyume o'rniga.
 *
 * Manzil bo'lagi `files` (KO'PLIK), xato kodlari esa `nomzod.rezyume_*`
 * bo'lib qoldi. Ziddiyat ataylab: to'plamni birlik nom bilan atash
 * har bir keyingi o'quvchini adashtirardi, xato kodlari esa allaqachon
 * to'rt tilga tarjima qilingan va «rezyume» — HR ning o'z so'zi
 * (SPEC §4.3).
 *
 * Eski `POST|GET|DELETE /{id}/resume` uchligi O'CHIRILDI — ular mavjud
 * bo'lmagan ustunlarni o'qiydi, ya'ni «eskirgan taxallus» sifatida ham
 * qoldirib bo'lmaydi.
 */

/**
 * Bitta faylni yuklash — **multipart**, maydon nomi `file`.
 *
 * ⚠️ BITTA so'rov — BITTA fayl. Bir so'rovda N ta fayl yuborilsa, bitta
 * yaroqsiz fayl yo butun to'plamni yiqitardi, yo «qisman muvaffaqiyat»
 * degan holatni tug'dirardi — uni na `Result<T>`, na `ProblemDetails`
 * ifodalay oladi. Chaqiruvchi tanlangan fayllarni KETMA-KET yuklaydi va
 * xatoni fayl nomi bilan ko'rsatadi (SPEC §4.3.1).
 *
 * Javob — YARATILGAN FAYL, butun nomzod emas: kartani yangilashni
 * `providers.tsx` ning umumiy bekor qilishi bajaradi.
 *
 * `Content-Type` bu yerda QO'YILMAYDI: axios `FormData` ni ko'rib uni
 * chegara (boundary) satri bilan o'zi yozadi — sabab `http-client.ts` da.
 */
export async function uploadCandidateFile(id: string, file: File): Promise<CandidateFile> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<unknown>(`${CANDIDATES}/${id}/files`, form)
  return toCandidateFile(data)
}

/**
 * Fayl baytlari — BLOB, havola emas.
 *
 * NEGA `<a href="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q. Brauzer bu so'rovga sarlavha
 * qo'sha olmaydi va server 401 qaytaradi (`documents-api.ts` dagi
 * `fetchDocumentFile` bilan aynan bir xil sabab).
 *
 * Manzilda IKKALA identifikator ham bor va bu ataylab: server faylni
 * `candidate_id` + `id` juftligi bo'yicha qidiradi, ya'ni chalkash juft
 * 404 qaytaradi va boshqa nomzodning pasport nusxasi berilmaydi.
 */
export async function fetchCandidateFile(
  id: string,
  fileId: string,
  signal?: AbortSignal,
): Promise<{ blob: Blob; disposition: string | null }> {
  const response = await api.get<Blob>(`${CANDIDATES}/${id}/files/${fileId}`, {
    responseType: 'blob',
    signal,
  })

  return {
    blob: response.data,
    disposition: response.headers['content-disposition'] as string | null,
  }
}

/**
 * Faylni o'chirish — yozuv ham, diskdagi baytlar ham HAQIQATAN
 * o'chiriladi (yumshoq o'chirish YO'Q: ko'rinmaydigan yozuv o'nta
 * o'rindan bittasini jimgina band qilib turardi).
 *
 * ⚠️ IDEMPOTENT EMAS: noma'lum `fileId` uchun server 404 qaytaradi
 * (suratni o'chirishdan farqi shunda). Chaqiruvchi aniq bir qatorni
 * nomlagan; jimgina «bajarildi» deyish allaqachon yo'q bo'lgan faylni
 * ko'rsatib turgan eskirgan ekranni YASHIRARDI (SPEC §4.3.3).
 */
export async function deleteCandidateFile(id: string, fileId: string): Promise<void> {
  await api.delete(`${CANDIDATES}/${id}/files/${fileId}`)
}

/* ──────────────────────────────────────────────────────────────── surat */

/**
 * Nomzod surati — BAYTLAR, havola emas.
 *
 * NEGA `<img src="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q — brauzer `<img>` so'roviga
 * sarlavha qo'sha olmaydi va server 401 qaytaradi (`fetchEmployeePhoto`
 * bilan aynan bir xil sabab va aynan bir xil quvur).
 *
 * `404` — XATO EMAS. U «bu nomzodning surati yo'q» degani va `null` ga
 * aylanadi: chaqiruvchi jimgina bosh harflarga qaytadi.
 */
export async function fetchCandidatePhoto(
  id: string,
  signal?: AbortSignal,
): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>(`${CANDIDATES}/${id}/photo`, {
      responseType: 'blob',
      signal,
    })
    return data
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

/**
 * Suratni yuklash — **multipart**, maydon nomi `file`. Yangisi eskisini
 * almashtiradi.
 *
 * Javobdagi `photoKey` ATAYLAB qaytarilmaydi: nomzod kartasi uni umuman
 * ko'rsatmaydi (`CandidateDto` da faqat `hasPhoto` bor), ya'ni undan
 * foydalanadigan joy yo'q. Keshni yangilash `useUploadCandidatePhoto`
 * zimmasida.
 *
 * `Content-Type` bu yerda QO'YILMAYDI: axios `FormData` ni ko'rib uni
 * chegara (boundary) satri bilan o'zi yozadi — sabab `http-client.ts` da.
 */
export async function uploadCandidatePhoto(id: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)

  await api.post(`${CANDIDATES}/${id}/photo`, form)
}

/** Suratni o'chirish. Server idempotent: surat bo'lmasa ham `204` qaytaradi. */
export async function deleteCandidatePhoto(id: string): Promise<void> {
  await api.delete(`${CANDIDATES}/${id}/photo`)
}
