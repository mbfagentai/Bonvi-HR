import { AlarmClock } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'

import { ProcessPlaceholder } from './process-states'

/** Tab «Kim kechikdi» (SPEC §11.4). Placeholder until P12. */
export function LateTab() {
  const { t } = useLocale()

  return (
    <ProcessPlaceholder
      icon={AlarmClock}
      title={t('processes.tabs.late')}
      description={t('processes.placeholder')}
    />
  )
}
