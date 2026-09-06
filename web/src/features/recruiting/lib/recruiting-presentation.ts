import {
  Bot,
  CircleCheck,
  CircleSlash,
  CircleX,
  Clock,
  DoorOpen,
  FileSpreadsheet,
  Hourglass,
  Keyboard,
  Send,
  UserCheck,
  UserRoundX,
  type LucideIcon,
} from 'lucide-react'

import type { BadgeTone, IconTone } from '@/shared/ui'
import type {
  CandidateSource,
  CandidateStatus,
  JobOfferStatus,
  StageOutcome,
  VacancyStatus,
} from '../api/recruiting-api'

/* ═════════════════════════════════════════════════════════════ RUXSATLAR */

/**
 * Ruxsat kodlari — backend'dagi `Bonvi.Domain.Identity.Permissions` bilan
 * AYNAN bir xil (`employee-presentation.ts` dagi naqsh). Satrni har joyda
 * qo'lda yozish o'rniga konstanta: yozuv xatosi 403 ga aylanmaydi.
 *
 * Butun rekruting IKKITA ruxsat bilan boshqariladi: barcha `GET` lar
 * `recruiting.view`, har qanday yozuv `recruiting.manage`. Ya'ni
 * ekranlarda ham shu ikki savol beriladi — «ko'ra oladimi» va
 * «o'zgartira oladimi», uchinchisi yo'q.
 */
export const RECRUITING_VIEW = 'recruiting.view'
export const RECRUITING_MANAGE = 'recruiting.manage'

/* ═══════════════════════════════════════════════════════════ KO'RINISHLAR */

/**
 * Holat/natija ko'rinishi.
 *
 * Rang bezak emas, BELGI (`catalog-kinds.ts` dagi bilan bir xil qoida):
 * yashil — o'tdi, qizil — o'tmadi, kulrang — hali javob yo'q. HR
 * ro'yxatga qaraganda matnni o'qimasdan ham holatni ajratadi.
 *
 * `key` — tarjima kalitining oxirgi bo'lagi. Sanoq qiymatining O'ZI
 * («InProgress») kalit sifatida ishlatilmaydi: JSON kalitlari butun
 * loyihada camelCase va bitta faylda ikki xil uslub bo'lmasligi kerak.
 */
export interface StatusLook {
  key: string
  tone: BadgeTone
  icon: LucideIcon
}

/*
 * Vakansiya holati — IKKITA yorliq. «To'xtatilgan» (sariq) qatori
 * olib tashlandi: uning ortida turgan holatning O'ZI yo'q endi
 * (sababi `recruiting-api.ts` dagi `VACANCY_STATUSES` izohida).
 *
 * Yopilgan — KULRANG, qizil emas: yopish muvaffaqiyatsizlik emas va
 * qaytariladi. Qizil «xato bo'ldi» deb o'qilardi.
 */
export const VACANCY_LOOK: Readonly<Record<VacancyStatus, StatusLook>> = {
  Open: { key: 'open', tone: 'success', icon: DoorOpen },
  Closed: { key: 'closed', tone: 'neutral', icon: CircleSlash },
}

export const CANDIDATE_LOOK: Readonly<Record<CandidateStatus, StatusLook>> = {
  InProgress: { key: 'inProgress', tone: 'info', icon: Hourglass },
  /* «Biz rad etdik» — qizil: bu korxonaning qarori. */
  Rejected: { key: 'rejected', tone: 'danger', icon: UserRoundX },
  Hired: { key: 'hired', tone: 'success', icon: UserCheck },
  /*
    «O'zi voz kechdi» — SARIQ, qizil emas. Bu muvaffaqiyatsizlik emas,
    boshqa turdagi fakt: nomzod ketdi. Ikkalasi bir xil rangda bo'lsa,
    voronka hisoboti «biz tanlamadik» va «bizni tanlamadi» ni ajrata
    olmasdi — `CandidateStatus` izohidagi butun sabab shu.
  */
  Withdrawn: { key: 'withdrawn', tone: 'warning', icon: DoorOpen },
}

export const OFFER_LOOK: Readonly<Record<JobOfferStatus, StatusLook>> = {
  Sent: { key: 'sent', tone: 'info', icon: Send },
  Accepted: { key: 'accepted', tone: 'success', icon: CircleCheck },
  Declined: { key: 'declined', tone: 'danger', icon: CircleX },
  Expired: { key: 'expired', tone: 'neutral', icon: Clock },
}

/** Bosqich natijasi — lentadagi ikona uchun `IconTone` (Badge emas). */
export const OUTCOME_LOOK: Readonly<
  Record<StageOutcome, { key: string; tone: IconTone; badge: BadgeTone; icon: LucideIcon }>
> = {
  Pending: { key: 'pending', tone: 'gray', badge: 'neutral', icon: Hourglass },
  Passed: { key: 'passed', tone: 'green', badge: 'success', icon: CircleCheck },
  Failed: { key: 'failed', tone: 'red', badge: 'danger', icon: CircleX },
}

/**
 * Manba — nomzod qayerdan kelgan.
 *
 * Ro'yxatda ATAYLAB ko'rinib turadi: keyingi bosqichda Telegram anketa
 * boti ulanadi va «bot qancha nomzod keltirdi» degan savol shu ustundan
 * javob oladi (`CandidateSource` domen izohi). Bugun hamma yozuv
 * «Qo'lda» — va aynan shu sababli farq ko'rinishi kerak: bot ishga
 * tushgan kuni o'zgarish darhol sezilsin.
 */
export const SOURCE_LOOK: Readonly<Record<CandidateSource, StatusLook>> = {
  Manual: { key: 'manual', tone: 'neutral', icon: Keyboard },
  TelegramBot: { key: 'telegramBot', tone: 'info', icon: Bot },
  Excel: { key: 'excel', tone: 'neutral', icon: FileSpreadsheet },
}

/* ═══════════════════════════════════════════════════════════════ SANALAR */

export { todayIso as today } from '@/shared/lib/today'

/** Bo'sh satrni `null` ga aylantiradi — server «kiritilmagan» deb tushunadi. */
export function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * `InProgress` → `inProgress`.
 *
 * Server sanoqni PascalCase yuboradi, tarjima kalitlari esa butun
 * loyihada camelCase (`recruiting-api.ts` dagi izoh). O'girishning
 * O'ZI ilgari chaqiruv joyida qo'lda yozilardi
 * (`candidates-toolbar.tsx`) — anketada esa beshta sanoq bor va
 * beshinchi nusxa yozishning ma'nosi yo'q.
 *
 * ⚠️ `scripts/check-locales.mjs` da AYNAN shu o'girish takrorlanadi:
 * tekshiruv sanoq ro'yxatidan kutilgan kalitlarni hosil qiladi va
 * to'rtala tilda borligini talab qiladi.
 */
export function lowerFirst(value: string): string {
  return value.charAt(0).toLowerCase() + value.slice(1)
}
