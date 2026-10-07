/**
 * Rekruting (2-bosqich) modulining tashqi yuzi.
 *
 * Marshrut fayllari komponentni to'g'ridan-to'g'ri yo'l bo'yicha import
 * qiladi (`features/employees` dagi bilan bir xil odat), shu sababli bu
 * yerda faqat MODUL TASHQARISIDA kerak bo'lishi mumkin bo'lgan narsalar:
 * sahifalar va ruxsat kodlari.
 *
 * `recruiting-api.ts` dagi tiplar ATAYLAB chiqarilmaydi: ular
 * vaqtinchalik qo'lda yozilgan klientning ichki shakli va `./bonvi gen`
 * dan keyin generatsiya qilingan DTO'lar bilan almashadi. Ularga
 * tashqaridan bog'lanib qolish o'sha almashtirishni qimmatlashtirardi.
 */
export { CandidateCardPage } from './components/candidate-card-page'
export { CandidatesPage } from './components/candidates-page'
export { ReserveCandidatesPage } from './components/reserve-candidates-page'
export { BlacklistCandidatesPage } from './components/blacklist-candidates-page'
export { VacanciesPage } from './components/vacancies-page'
export { RECRUITING_MANAGE, RECRUITING_VIEW } from './lib/recruiting-presentation'
