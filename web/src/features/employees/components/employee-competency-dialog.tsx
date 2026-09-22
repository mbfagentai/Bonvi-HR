import { LoaderCircle, Plus, X } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import {
  LANGUAGE_CODES,
  LANGUAGE_LEVELS,
  type EmployeeCompetency,
  type LanguageCode,
  type LanguageLevel,
  type LanguageSkill,
} from '../api/employee-competency-api'
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

  const [languageSkills, setLanguageSkills] = useState<LanguageSkill[]>([])
  const [computerLiteracyPercent, setComputerLiteracyPercent] = useState('')
  const [specialization, setSpecialization] = useState('')
  const [otherAbilities, setOtherAbilities] = useState('')
  const [interest, setInterest] = useState('')
  const [educationPlace, setEducationPlace] = useState('')
  const [candidateNote, setCandidateNote] = useState('')

  useEffect(() => {
    if (!open) return

    setLanguageSkills(profile?.languageSkills ?? [])
    setComputerLiteracyPercent(
      profile?.computerLiteracyPercent === null || profile?.computerLiteracyPercent === undefined
        ? ''
        : String(profile.computerLiteracyPercent),
    )
    setSpecialization(profile?.specialization ?? '')
    setOtherAbilities(profile?.otherAbilities ?? '')
    setInterest(profile?.interest ?? '')
    setEducationPlace(profile?.educationPlace ?? '')
    setCandidateNote(profile?.candidateNote ?? '')
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

  /*
   * Tanlanmagan tillar — yangi qator va boshqa qatorlarning ro'yxati
   * uchun. Bitta til IKKI qatorda tanlana olmaydi (backend qoidasi:
   * «qobiliyat.til_takror») — band tilni ro'yxatda ko'rsatib, keyin 400
   * qaytarish o'rniga u umuman tanlab bo'lmaydigan qilinadi.
   */
  const usedLanguages = new Set(languageSkills.map((skill) => skill.language))

  const addLanguage = () => {
    const free = LANGUAGE_CODES.find((code) => !usedLanguages.has(code))
    if (free === undefined) return
    setLanguageSkills([...languageSkills, { language: free, level: 'elementary' }])
  }

  const changeLanguage = (index: number, patch: Partial<LanguageSkill>) => {
    setLanguageSkills(languageSkills.map((skill, i) => (i === index ? { ...skill, ...patch } : skill)))
  }

  const removeLanguage = (index: number) => {
    setLanguageSkills(languageSkills.filter((_, i) => i !== index))
  }

  const submit = () => {
    if (percentInvalid) return

    save.mutate(
      {
        /*
         * Eski erkin matn O'ZGARISHSIZ qaytariladi, tahrirlanmaydi:
         * `PUT` to'liq almashtirish, shu sababli maydon yuborilmasa u
         * BO'SHATILARDI — Excel'dan ko'chgan yozuv esa yo'qolmasligi
         * kerak (uni ajratish taxmin bo'lardi, saqlab turish esa arzon).
         */
        languages: profile?.languages ?? null,
        languageSkills,
        computerLiteracyPercent: percentValue,
        specialization: orNull(specialization),
        otherAbilities: orNull(otherAbilities),
        interest: orNull(interest),
        educationPlace: orNull(educationPlace),
        candidateNote: orNull(candidateNote),
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

        <div className="space-y-2">
          <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
            {t('competency.languages')}
          </span>

          {languageSkills.map((skill, index) => (
            <div key={skill.language} className="flex items-center gap-2">
              <Select
                aria-label={t('competency.languageLabel')}
                className="flex-1"
                options={LANGUAGE_CODES.map((code) => ({
                  value: code,
                  label: t(`competency.language.${code}`),
                  // Boshqa qatorda band til — tanlab bo'lmaydi (o'zi turgan qator emas).
                  disabled: code !== skill.language && usedLanguages.has(code),
                }))}
                value={skill.language}
                onChange={(event) =>
                  changeLanguage(index, { language: event.target.value as LanguageCode })
                }
              />
              <Select
                aria-label={t('competency.levelLabel')}
                className="flex-1"
                options={LANGUAGE_LEVELS.map((level) => ({
                  value: level,
                  label: t(`competency.languageLevel.${level}`),
                }))}
                value={skill.level}
                onChange={(event) =>
                  changeLanguage(index, { level: event.target.value as LanguageLevel })
                }
              />
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label={t('competency.removeLanguage')}
                onClick={() => removeLanguage(index)}
              >
                <X />
              </Button>
            </div>
          ))}

          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={addLanguage}
            disabled={usedLanguages.size >= LANGUAGE_CODES.length}
          >
            <Plus />
            {t('competency.addLanguage')}
          </Button>

          {/*
            Excel'dan ko'chgan eski erkin matn — FAQAT ko'rsatiladi.
            Tahrirlanmaydi: HR yangi ma'lumotni strukturaviy ro'yxatga
            kiritadi, eski matn esa hujjat sifatida saqlanib turadi.
          */}
          {profile?.languages !== null && profile?.languages !== undefined && (
            <p className="text-caption1 text-[var(--fg-tertiary)]">
              {profile.languages} ({t('competency.legacyLanguages')})
            </p>
          )}
        </div>

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

        {/*
          Nomzodlik anketasidan ko'chgan matn — TAHRIRLANADI: HR uni
          tuzatishi yoki butunlay o'chirishi mumkin. Qatorlar ko'p
          bo'lgani uchun boshqalardan balandroq.
        */}
        <TextArea
          label={t('competency.candidateNote')}
          hint={t('competency.candidateNoteHint')}
          rows={5}
          value={candidateNote}
          onChange={(event) => setCandidateNote(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
