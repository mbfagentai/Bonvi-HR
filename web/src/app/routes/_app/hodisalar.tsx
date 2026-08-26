import { createFileRoute } from '@tanstack/react-router'

import { IncidentsPage } from '@/features/safety/components/incidents-page'

/**
 * Baxtsiz hodisalar jurnali — faqat `safety.view` ruxsati bilan.
 *
 * `foydalanuvchilar.tsx` dagi bilan bir xil naqsh: marshrutda `beforeLoad`
 * yo'q, sahifaning o'zi ruxsat holatini ko'rsatadi — chunki server
 * `GET /api/safety/incidents` ni ham xuddi shu ruxsat bilan himoyalagan.
 */
export const Route = createFileRoute('/_app/hodisalar')({
  component: IncidentsPage,
})
