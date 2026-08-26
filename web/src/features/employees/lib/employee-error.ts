import axios from 'axios'

import { problemCode, type ProblemDetails } from '@/shared/api/problem-details'

/*
 * Kod ajratish mantig'i endi `shared/api/problem-details.ts` da: uchinchi
 * bo'lim (hisob sozlamalari) shu ishni so'raganda nusxa saqlashning ma'nosi
 * qolmadi. Modul nomi esa o'z o'rnida turibdi — xodimlar kodi qolgan
 * joylarda shu nom bilan chaqiriladi va import zanjiri o'zgarmaydi.
 */

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * Bo'sh satr — «kod noma'lum»: `useErrorMessage` uni umumiy
 * «Nimadir noto'g'ri ketdi» ga aylantiradi.
 */
export function employeeErrorCode(error: unknown): string {
  return problemCode(error)
}

/**
 * Server tezlik cheklovi tufayli rad etdimi (429).
 *
 * NEGA xato KODI emas, HOLAT kodi tekshiriladi: 429 ni serverning
 * cheklovchisi ham (`rate_limit.oshib_ketdi`), oldidagi nginx yoki
 * Cloudflare kabi vositachi ham qaytarishi mumkin — ikkinchisida tana
 * umuman ProblemDetails bo'lmaydi va hech qanday kod topilmaydi. Holat
 * kodi esa ikkala holatda ham bir xil va bitta ma'noni bildiradi:
 * «so'rov to'g'ri, shunchaki juda tez-tez». `terminationWarning` dagi
 * bilan bir xil mulohaza.
 */
export function isRateLimited(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 429
}

/**
 * Bo'shatishga to'sqinlik qiladigan ogohlantirish.
 *
 * HR aynan shuni so'ragan: xodim qora ro'yxatda yoki nazoratda bo'lsa,
 * bo'shatish jimgina o'tib ketmasin. Server 409 qaytaradi, foydalanuvchi
 * sababni O'QIYDI va shundan keyingina tasdiqlaydi.
 */
export interface TerminationWarning {
  /** Asosiy sabab kodi — `errors.*` orqali tarjima qilinadi. */
  code: string
  /** Qo'shimcha kodlar (bir vaqtda ham qora ro'yxat, ham nazorat bo'lishi mumkin). */
  extra: string[]
  /** Serverning erkin matni — tarjimasi bo'lmagan tafsilot uchun. */
  detail: string | null
}

/**
 * Server tasdiq so'rayaptimi.
 *
 * Faqat 409 tekshiriladi, kod ro'yxati emas: yangi to'siq qoidasi
 * qo'shilganda (masalan «sinov muddati tugamagan») frontend uni bilmasa
 * ham oyna to'g'ri ishlaydi — tarjimasi yo'q kod o'z holicha ko'rsatiladi.
 * Kodlar ro'yxatiga bog'lanib qolish esa yangi qoidani jimgina yutib
 * yuborardi.
 */
export function terminationWarning(error: unknown): TerminationWarning | null {
  if (!axios.isAxiosError(error)) return null
  if (error.response?.status !== 409) return null

  const problem = error.response.data as ProblemDetails | undefined
  const code = typeof problem?.title === 'string' ? problem.title : ''
  if (code === '') return null

  const rawWarnings = problem?.warnings ?? problem?.extensions?.warnings
  const extra = Array.isArray(rawWarnings)
    ? rawWarnings.filter(
        (item): item is string => typeof item === 'string' && item !== code,
      )
    : []

  return {
    code,
    extra,
    detail: typeof problem?.detail === 'string' && problem.detail !== '' ? problem.detail : null,
  }
}

/**
 * Qaysi xato kodi ishga qabul formasining qaysi maydoniga tegishli.
 *
 * NEGA kerak: «Bunday JSHSHIR band» degan xabar formaning tepasida emas,
 * aynan JSHSHIR maydoni ostida turgani ma'qul — bosqichli formada esa bu
 * yana muhimroq: xato qaysi bosqichda ekanini bilib, o'sha bosqichga
 * qaytariladi. Ro'yxatda yo'q kodlar umumiy ogohlantirishga tushadi.
 */
export const HIRE_ERROR_FIELD: Readonly<Record<string, string>> = {
  'ism.familiya_bosh': 'lastName',
  'ism.ism_bosh': 'firstName',
  'ism.bosh': 'lastName',
  'ism.tolik_emas': 'lastName',
  'xodim.tugilgan_sana': 'birthDate',
  'xodim.jshshir_sana_mos_emas': 'pinfl',
  'xodim.jshshir_band': 'pinfl',
  'pinfl.bosh': 'pinfl',
  'pinfl.uzunlik': 'pinfl',
  'pinfl.birinchi_raqam': 'pinfl',
  'pinfl.sana': 'pinfl',
  'pinfl.nazorat_raqami': 'pinfl',
  'pasport.bosh': 'passport',
  'pasport.uzunlik': 'passport',
  'pasport.seriya': 'passport',
  'pasport.raqam': 'passport',
  'telefon.bosh': 'phone',
  'telefon.uzunlik': 'phone',
  'telefon.mamlakat_kodi': 'phone',
  'telefon.operator': 'phone',
  'xodim.telegram': 'telegram',
  'mehnat.yuridik_shaxs_yoq': 'legalEntityId',
  'mehnat.stavka': 'rate',
  'mehnat.stavka_oshdi': 'rate',
  'mehnat.ketish_sanasi': 'hireDate',
  'pul.manfiy': 'salaryAmount',
  'pul.aniqlik': 'salaryAmount',
  'pul.valyuta': 'salaryAmount',
}

/** Maydon qaysi bosqichda — xato chiqqanda o'sha bosqichga qaytish uchun. */
export const HIRE_FIELD_STEP: Readonly<Record<string, number>> = {
  lastName: 0,
  firstName: 0,
  middleName: 0,
  birthDate: 0,
  pinfl: 0,
  passport: 0,
  phone: 0,
  secondPhone: 0,
  address: 0,
  speciality: 0,
  telegram: 0,
  legalEntityId: 1,
  departmentId: 1,
  positionId: 1,
  hireDate: 1,
  rate: 1,
  salaryAmount: 2,
  orderNo: 2,
  orderDate: 2,
}
