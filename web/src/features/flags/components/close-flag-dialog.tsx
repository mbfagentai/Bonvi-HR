import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { employeeErrorCode } from '@/features/employees/lib/employee-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'
import { todayIso } from '@/shared/lib/today'

import { useCloseFlag } from '../api/flags-queries'

/**
 * Yopish uchun kerak bo'ladigan eng kichik ma'lumot.
 *
 * ATAYLAB `FlaggedEmployee` emas: bu dialog ikki joyda ishlatiladi —
 * teg ro'yxati sahifasida va xodim profilida. Profildagi belgi yozuvida
 * xodim ismi yo'q (u sahifaning o'zida turadi), shu sababli to'liq tipni
 * talab qilsak, profil uchun ikkinchi nusxa yozishga to'g'ri kelardi.
 */
export interface ClosableFlag {
  id: string
  employeeId: string
  employeeName: string
}

/**
 * Belgini yopish.
 *
 * <b>Yopish — o'chirish emas.</b> Yozuv joyida qoladi, unga tugash sanasi
 * qo'yiladi. Sabab: «bu odam qachondir qora ro'yxatda bo'lganmi?» degan
 * savolga javob kerak bo'ladi, va o'chirilgan yozuv bu savolni javobsiz
 * qoldirardi.
 */
export function CloseFlagDialog({
  item,
  onClose,
}: {
  item: ClosableFlag | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const close = useCloseFlag()

  const [closedAt, setClosedAt] = useState(todayIso)
  const [note, setNote] = useState('')

  // Dialog qayta ochilganda oldingi xodimning izohi qolib ketmasligi kerak.
  useEffect(() => {
    if (item !== null) {
      setClosedAt(todayIso())
      setNote('')
      close.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    if (closedAt === '') return

    close.mutate(
      {
        employeeId: item.employeeId,
        flagId: item.id,
        closedAt,
        note: note.trim() === '' ? null : note.trim(),
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!close.isPending}
      title={t('flags.closeTitle')}
      description={item.employeeName}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={close.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            onClick={submit}
            disabled={close.isPending || closedAt === ''}
          >
            {close.isPending && <LoaderCircle className="animate-spin" />}
            {t('flags.close')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {close.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(close.error))}</Alert>}

        <TextField
          label={t('flags.closedAt')}
          type="date"
          value={closedAt}
          onChange={(event) => setClosedAt(event.target.value)}
        />

        <TextArea
          label={t('flags.note')}
          hint={t('flags.noteHint')}
          rows={3}
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
