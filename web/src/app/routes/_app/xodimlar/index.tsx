import { createFileRoute } from '@tanstack/react-router'

import { EmployeesPage } from '@/features/employees/components/employees-page'
import { validateEmployeesSearch } from '@/features/employees/lib/employees-search'

/**
 * Xodimlar ro'yxati — HR kuniga soatlab shu ekranda ishlaydi.
 *
 * Butun mantiq `EmployeesPage` ichida: filtr, qidiruv, ikki ko'rinish
 * (jadval va kartochka), sahifalash. Marshrut fayli faqat uni ulaydi —
 * shunda komponentni boshqa joyda ham ishlatish mumkin bo'lib qoladi.
 */
export const Route = createFileRoute('/_app/xodimlar/')({
  /**
   * Sahifa, qidiruv, filtrlar, saralash va ko'rinish — manzil satrida.
   *
   * NEGA: bu komponent xodim kartasiga o'tilganda unmount bo'ladi.
   * Holat uning ichida bo'lsa, «orqaga» bosgan HR uchinchi sahifadan
   * birinchisiga, filtrsiz holatga tushib qolardi. Manzilda esa u
   * brauzer tarixi bilan qaytadi, yangilashga bardosh beradi va havola
   * qilib bo'lishiladi.
   *
   * Tekshiruv `validateEmployeesSearch` da: qo'lda yozilgan noto'g'ri
   * qiymat («?page=abc») ekranni buzmaydi, shunchaki tashlab yuboriladi.
   */
  validateSearch: validateEmployeesSearch,

  component: EmployeesPage,
})
