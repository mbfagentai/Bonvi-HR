import { Inbox } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'

import { ProcessPlaceholder } from './process-states'

/** Tab «Mening ishlarim» (SPEC §11.2). Placeholder until P10. */
export function MyWorkTab() {
  const { t } = useLocale()

  return (
    <ProcessPlaceholder
      icon={Inbox}
      title={t('processes.tabs.my')}
      description={t('processes.placeholder')}
    />
  )
}
