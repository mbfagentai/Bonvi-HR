import { createFileRoute } from '@tanstack/react-router'

import { HrMentorBlanksPage } from '@/features/hrmentor-blanks/components/blanks-page'

/**
 * Blanka fayllari — HRMentor da yuritiladigan hujjat shakllari (SPEC-blankalar-hrmentor §6).
 *
 * `beforeLoad` yo'q — `sozlamalar.tsx` dagi naqsh: server `GET /api/hrmentor/blanks`
 * ni `employees.view` bilan himoyalagan, sahifa esa ruxsatsiz odamga
 * tushunarli holat ko'rsatadi.
 */
export const Route = createFileRoute('/_app/blanka-fayllari')({
  component: HrMentorBlanksPage,
})
