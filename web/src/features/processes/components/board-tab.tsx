import { Kanban } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'

import { ProcessPlaceholder } from './process-states'

/** Tab «Jarayonlar» (SPEC §11.3). Placeholder until P11. */
export function BoardTab() {
  const { t } = useLocale()

  return (
    <ProcessPlaceholder
      icon={Kanban}
      title={t('processes.tabs.board')}
      description={t('processes.placeholder')}
    />
  )
}
