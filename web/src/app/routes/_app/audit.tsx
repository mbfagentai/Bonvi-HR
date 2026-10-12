import { createFileRoute } from '@tanstack/react-router'

import { AuditPage } from '@/features/audit/components/audit-page'
import { validateAuditSearch } from '@/features/audit/lib/audit-search'

/**
 * Audit jurnali — kim, qachon, nimani o'zgartirdi (SPEC-rollar-va-audit §9.5).
 *
 * `rollar.tsx` va `foydalanuvchilar.tsx` dagi bilan bir xil naqsh: marshrutda
 * `beforeLoad` yo'q, `audit.view` siz odamga sahifaning o'zi tushunarli holat
 * ko'rsatadi — server ham `GET /api/audit` ni shu ruxsat bilan himoyalagan.
 * Menyudagi band ham shu ruxsat bilan chiziladi (`nav-items.ts`).
 */
export const Route = createFileRoute('/_app/audit')({
  /**
   * Davr, filtrlar, sahifa va «Bitta yozuv» — manzil satrida (`/buyruqlar`
   * naqshi). Hech qachon xato tashlamaydi: noto'g'ri qiymat tashlab yuboriladi.
   */
  validateSearch: validateAuditSearch,

  component: AuditPage,
})
