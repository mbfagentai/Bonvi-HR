import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'

import { useConcludeIncident } from '../api/incidents-queries'
import type { Incident } from '../api/incidents-api'
import { safetyErrorCode } from '../lib/safety-error'

/**
 * Komissiya xulosasini yozish — alohida oyna.
 *
 * `incidents-api.ts` boshidagi izohga qarang: xulosa YARATISH formasida
 * ATAYLAB yo'q, tekshiruv tugagach shu yerdan qo'shiladi. `concludedAt`
 * bo'sh qoldirilsa — server bugungi sanani qo'yadi, shu sababli bu yerda
 * ham bo'sh qoldirish mumkin (majburiy emas).
 */
export function IncidentConcludeDialog({
  item,
  onClose,
}: {
  /** `null` — oyna yopiq. */
  item: Incident | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const conclude = useConcludeIncident()

  const [conclusion, setConclusion] = useState('')
  const [concludedAt, setConcludedAt] = useState('')

  useEffect(() => {
    if (item === null) return
    setConclusion(item.commissionConclusion ?? '')
    setConcludedAt(item.concludedAt ?? '')
    conclude.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const incomplete = conclusion.trim() === ''

  const submit = () => {
    if (incomplete) return
    conclude.mutate(
      {
        id: item.id,
        body: { conclusion: conclusion.trim(), concludedAt: concludedAt === '' ? null : concludedAt },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!conclude.isPending}
      title={t('safety.concludeTitle')}
      description={item.employeeName ?? undefined}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={conclude.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={conclude.isPending || incomplete}
          >
            {conclude.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {conclude.isError && <Alert tone="danger">{errorMessage(safetyErrorCode(conclude.error))}</Alert>}

        <TextArea
          label={t('safety.conclusion')}
          hint={t('safety.conclusionHint')}
          rows={4}
          value={conclusion}
          onChange={(event) => setConclusion(event.target.value)}
        />

        <TextField
          label={t('safety.concludedAt')}
          hint={t('safety.concludedAtHint')}
          type="date"
          value={concludedAt}
          onChange={(event) => setConcludedAt(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
