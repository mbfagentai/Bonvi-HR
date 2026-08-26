import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea, TextField } from '@/shared/ui'

import type { EmployeeCompetency } from '../api/employee-competency-api'
import { useSaveEmployeeCompetency } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Qobiliyatlar profilini tahrirlash.
 *
 * <b>Butun forma yuboriladi — qisman yangilash yo'q.</b> Server
 * `PUT`ni to'liq almashtirish sifatida qabul qiladi, shu sababli
 * bo'sh qoldirilgan maydon «tegilmadi» emas, «bo'shatildi» degani.
 * Boshqa xodim dialoglaridan farqi yo'q — faqat maydonlar boshqacha.
 */
export function EmployeeCompetencyDialog({
  employeeId,
  profile,
  open,
  onClose,
}: {
  employeeId: string
  /** `null` — profil hali yaratilmagan, forma bo'sh boshlanadi. */
  profile: EmployeeCompetency | null
  open: boolean
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveEmployeeCompetency(employeeId)

  const [languages, setLanguages] = useState('')
  const [computerLiteracyPercent, setComputerLiteracyPercent] = useState('')
  const [specialization, setSpecialization] = useState('')
  const [otherAbilities, setOtherAbilities] = useState('')
  const [interest, setInterest] = useState('')
  const [educationPlace, setEducationPlace] = useState('')

  useEffect(() => {
    if (!open) return

    setLanguages(profile?.languages ?? '')
    setComputerLiteracyPercent(
      profile?.computerLiteracyPercent === null || profile?.computerLiteracyPercent === undefined
        ? ''
        : String(profile.computerLiteracyPercent),
    )
    setSpecialization(profile?.specialization ?? '')
    setOtherAbilities(profile?.otherAbilities ?? '')
    setInterest(profile?.interest ?? '')
    setEducationPlace(profile?.educationPlace ?? '')
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  /*
   * Backend qoidasi bilan bir xil (`EmployeeCompetency.SetComputerLiteracy`):
   * 0 dan 100 gacha yoki bo'sh. Mijoz buni oldindan tekshiradi — server
   * baribir tekshiradi, lekin HR xatoni yuborishdan OLDIN ko'rgani ma'qul.
   */
  const percentValue = computerLiteracyPercent.trim() === '' ? null : Number(computerLiteracyPercent)
  const percentInvalid =
    percentValue !== null && (Number.isNaN(percentValue) || percentValue < 0 || percentValue > 100)

  const submit = () => {
    if (percentInvalid) return

    save.mutate(
      {
        languages: orNull(languages),
        computerLiteracyPercent: percentValue,
        specialization: orNull(specialization),
        otherAbilities: orNull(otherAbilities),
        interest: orNull(interest),
        educationPlace: orNull(educationPlace),
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!save.isPending}
      title={t('competency.editTitle')}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={save.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={save.isPending || percentInvalid}
          >
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {save.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(save.error))}</Alert>}

        <TextField
          label={t('competency.languages')}
          value={languages}
          onChange={(event) => setLanguages(event.target.value)}
        />

        <TextField
          label={t('competency.computerLiteracy')}
          hint={t('competency.computerLiteracyHint')}
          type="number"
          inputMode="numeric"
          min={0}
          max={100}
          value={computerLiteracyPercent}
          error={percentInvalid ? errorMessage('qobiliyat.kompyuter_foiz') : undefined}
          onChange={(event) => setComputerLiteracyPercent(event.target.value)}
        />

        <TextField
          label={t('competency.educationPlace')}
          value={educationPlace}
          onChange={(event) => setEducationPlace(event.target.value)}
        />

        <TextField
          label={t('competency.specialization')}
          value={specialization}
          onChange={(event) => setSpecialization(event.target.value)}
        />

        <TextArea
          label={t('competency.interest')}
          rows={2}
          value={interest}
          onChange={(event) => setInterest(event.target.value)}
        />

        <TextArea
          label={t('competency.otherAbilities')}
          rows={2}
          value={otherAbilities}
          onChange={(event) => setOtherAbilities(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
