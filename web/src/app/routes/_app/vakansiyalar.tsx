import { createFileRoute } from '@tanstack/react-router'

import { VacanciesPage } from '@/features/recruiting/components/vacancies-page'
import { validateVacanciesSearch } from '@/features/recruiting/lib/vacancies-search'

/**
 * Vakansiyalar — 2-bosqich (rekruting) voronkasining boshlanish nuqtasi.
 *
 * Butun mantiq `VacanciesPage` ichida: filtr, saralash, sahifalash va
 * holat amallari. Marshrut fayli faqat uni ulaydi — shunda komponentni
 * boshqa joyda ham ishlatish mumkin bo'lib qoladi
 * (`xodimlar/index.tsx` dagi bilan bir xil qoida).
 */
export const Route = createFileRoute('/_app/vakansiyalar')({
  /**
   * Filtrlar, saralash va sahifa — manzil satrida.
   *
   * NEGA: HR «ochiq vakansiyalar»ni filtrlab, qatorga bosib nomzodlar
   * ro'yxatiga o'tadi. «Orqaga» bosilganda u o'sha filtrga qaytishi
   * kerak — holat komponent ichida bo'lsa, filtrsiz birinchi sahifaga
   * tushib qolardi.
   *
   * Tekshiruv `validateVacanciesSearch` da: qo'lda yozilgan noto'g'ri
   * qiymat («?page=abc») ekranni buzmaydi, shunchaki tashlab yuboriladi.
   */
  validateSearch: validateVacanciesSearch,

  component: VacanciesPage,
})
