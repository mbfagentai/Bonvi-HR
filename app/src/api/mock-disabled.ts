import type { EmployeeApi } from './client'
import { realApi } from './real'

/**
 * Haqiqiy (`real`) build'da `@/mock` O'RNIGA shu fayl ulanadi (`vite.config.ts`,
 * `resolve.alias`).
 *
 * NEGA: `client.ts` mock'ni `API_MODE` ga qarab tanlaydi va `mockApi` obyektining
 * o'zini Rollup tashlab yuboradi — lekin `src/mock/` dagi modul darajasidagi
 * hisob-kitoblar (soxta jamoa, ismlar ro'yxati, so'rov izohlari, rasm) «yon
 * ta'sirli» deb bundle'da QOLARDI. Production'dagi xodim telefoniga soxta
 * ma'lumot yuklanmasligi kerak: real build `src/mock/` ni umuman o'qimaydi.
 *
 * `API_MODE === 'real'` da `mockApi` hech qachon chaqirilmaydi; bu yerda u
 * faqat import bo'sh qolmasligi uchun.
 */
export const mockApi: EmployeeApi = realApi
