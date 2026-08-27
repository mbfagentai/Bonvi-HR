import { problemCode } from '@/shared/api/problem-details'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * Bo'sh satr — «kod noma'lum»: `useErrorMessage` uni umumiy
 * «Nimadir noto'g'ri ketdi» ga aylantiradi.
 */
export function userErrorCode(error: unknown): string {
  return problemCode(error)
}

/**
 * Qaysi xato kodi qaysi maydonga tegishli.
 *
 * NEGA kerak: «Bu raqam band» degan xabar formaning tepasida emas, aynan
 * telefon maydoni ostida turgani ma'qul (`catalog-error.ts` dagi bilan
 * bir xil sabab). Ro'yxatda yo'q kodlar umumiy ogohlantirishga tushadi.
 */
export const USER_ERROR_FIELD: Readonly<Record<string, string>> = {
  'ism.familiya_bosh': 'lastName',
  'ism.ism_bosh': 'firstName',
  'telefon.bosh': 'phone',
  'telefon.uzunlik': 'phone',
  'telefon.mamlakat_kodi': 'phone',
  'telefon.operator': 'phone',
  'foydalanuvchi.login_band': 'phone',
  'foydalanuvchi.rol_bosh': 'roleCode',
  'foydalanuvchi.rol_topilmadi': 'roleCode',
  // Uch rolli modelning qoidalari — hammasi ROL maydoniga tegishli:
  // «bu rol egasiga huquqing yo'q», «bunday rahbar allaqachon bor»,
  // «o'z rolingni o'zgartira olmaysan». Ularni umumiy blokda ko'rsatish
  // foydalanuvchini «qayerda xato qildim?» degan savol bilan qoldirardi.
  'foydalanuvchi.rolga_haqqi_yoq': 'roleCode',
  'foydalanuvchi.super_admin_mavjud': 'roleCode',
  'foydalanuvchi.bosh_rahbar_mavjud': 'roleCode',
  'foydalanuvchi.oz_rolini_ozgartirish': 'roleCode',
  'foydalanuvchi.oxirgi_administrator': 'roleCode',
  // Faqat parol maydoni bor formalarda uchraydi (yaratish, parol tiklash) —
  // qolganlarida `password` maydonining o'zi yo'q, xato umumiy blokka tushadi.
  'foydalanuvchi.parol_bosh': 'password',
  'foydalanuvchi.parol_qisqa': 'password',
}
