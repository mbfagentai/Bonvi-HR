import { useEffect, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Dialog } from '@/shared/ui'

/** Seconds the candidate has to answer «Davom etasizmi?» before the form is cleared. */
const COUNTDOWN = 60

/**
 * Shown after 10 minutes without a touch. Nobody answering means the
 * candidate walked away: the answers are cleared so the next person does not
 * see them. Not dismissible by tapping outside — only an explicit answer.
 */
export function IdleDialog({
  open,
  onContinue,
  onTimeout,
}: {
  open: boolean
  onContinue: () => void
  onTimeout: () => void
}) {
  const { t } = useLocale()
  const [left, setLeft] = useState(COUNTDOWN)

  useEffect(() => {
    if (!open) return

    setLeft(COUNTDOWN)
    const timer = window.setInterval(() => setLeft((value) => Math.max(0, value - 1)), 1000)
    return () => window.clearInterval(timer)
  }, [open])

  useEffect(() => {
    if (open && left === 0) onTimeout()
  }, [open, left, onTimeout])

  return (
    <Dialog
      open={open}
      onClose={onContinue}
      dismissible={false}
      size="sm"
      title={t('kiosk.idle.title')}
      footer={
        <Button type="button" variant="primary" size="lg" pill onClick={onContinue} className="w-full sm:w-auto">
          {t('kiosk.idle.continue')}
        </Button>
      }
    >
      <p className="text-body text-[var(--fg-secondary)]">{t('kiosk.idle.text', { n: left })}</p>
    </Dialog>
  )
}
