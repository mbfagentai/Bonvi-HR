import { createFileRoute } from '@tanstack/react-router'

import { CouncilAppPage } from '@/features/council-app/council-app-page'

/** The council's Telegram Mini App (`docs/SPEC-kengash-guruhi.md` §5). Outside `_app`: no panel login, no panel shell. */
export const Route = createFileRoute('/tg/kengash')({
  component: CouncilAppPage,
})
