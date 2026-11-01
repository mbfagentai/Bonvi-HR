import { createFileRoute } from '@tanstack/react-router'

import { BlanksPage } from '@/features/blanks/components/blanks-page'

/**
 * Blankalar — HRMentor da yuritiladigan hujjat shakllari (SPEC-blankalar-hrmentor §6).
 *
 * `beforeLoad` yo'q — `sozlamalar.tsx` dagi naqsh: server `GET /api/blanks`
 * ni `employees.view` bilan himoyalagan, sahifa esa ruxsatsiz odamga
 * tushunarli holat ko'rsatadi.
 */
export const Route = createFileRoute('/_app/blankalar')({
  component: BlanksPage,
})
