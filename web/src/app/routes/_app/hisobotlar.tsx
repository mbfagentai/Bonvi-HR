import { createFileRoute } from '@tanstack/react-router'

import { ReportsPage } from '@/features/reports/components/reports-page'
import { validateReportsSearch } from '@/features/reports/lib/reports-search'

/**
 * Hisobotlar — kadrlar oqimi, faqat `reports.view` ruxsati bilan.
 *
 * `hodisalar.tsx` va `foydalanuvchilar.tsx` dagi bilan bir xil naqsh:
 * marshrutda `beforeLoad` YO'Q, ruxsat holatini sahifaning o'zi
 * ko'rsatadi — chunki server `GET /api/reports/turnover` ni ham xuddi shu
 * ruxsat bilan himoyalagan (`ReportsEndpoints.cs`). Ya'ni chegara bitta
 * joyda — serverda — turadi, ekran esa uni faqat tushuntirib beradi.
 */
export const Route = createFileRoute('/_app/hisobotlar')({
  /**
   * Tanlangan davr — manzil satrida (`?months=24`).
   *
   * NEGA: hisobotning butun mazmuni shu bitta tanlovga bog'liq, ya'ni
   * «mana shu grafikka qara» degan havola faqat manzilda davr turgandagina
   * ishlaydi — bu esa hisobotning asosiy ishlatilish usuli
   * (`reports-search.ts` dagi to'liq izoh).
   *
   * Tekshiruv `validateReportsSearch` da: `?months=abc` yoki `?months=100`
   * ekranni buzmaydi va serverga ham bormaydi — shunchaki tashlab
   * yuboriladi va standart davr ishlaydi.
   */
  validateSearch: validateReportsSearch,

  component: ReportsPage,
})
