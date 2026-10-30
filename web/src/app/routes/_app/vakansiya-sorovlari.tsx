import { createFileRoute } from '@tanstack/react-router'

import { VacancyRequestsPage } from '@/features/recruiting/components/vacancy-requests-page'
import { validateVacancyRequestsSearch } from '@/features/recruiting/lib/vacancy-requests-search'

/**
 * Vakansiya so'rovlari — bo'lim boshliqlarining yangi vakansiya so'rovlari
 * (`docs/SPEC-vakansiya-sorovi.md`). Filtrlar va tab manzil satrida (`vakansiyalar.tsx` sababi).
 */
export const Route = createFileRoute('/_app/vakansiya-sorovlari')({
  validateSearch: validateVacancyRequestsSearch,
  component: VacancyRequestsPage,
})
