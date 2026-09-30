import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/ui/button'
import { EmptyState } from '@/ui/feedback'

/**
 * TOPILMADI (UX §A.3).
 *
 * Bitta tugma, bitta yo'l: «Asosiyga qaytish». Noma'lum manzilda
 * turgan odamga tanlov berishning ma'nosi yo'q.
 */
export function NotFoundScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <div className="mx-auto w-full max-w-app px-4 pb-tabbar pt-12">
      <EmptyState
        icon="information-circle"
        title={t('notFound.title')}
        text={t('notFound.text')}
        action={<Button onClick={() => navigate('/', { replace: true })}>{t('notFound.action')}</Button>}
      />
    </div>
  )
}
