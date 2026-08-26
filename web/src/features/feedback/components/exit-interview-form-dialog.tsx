import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import {
  useCreateExitInterview,
  useExitInterviewByEmployment,
  useTerminatedEmployeeOptions,
  useTerminatedEmployments,
  useUpdateExitInterview,
} from '../api/exit-interviews-queries'
import {
  RECOMMENDATION_ANSWERS,
  type ExitInterview,
  type RecommendationAnswerValue,
} from '../api/exit-interviews-api'
import { FEEDBACK_ERROR_FIELD, feedbackErrorCode } from '../lib/feedback-error'
import { RECOMMENDATION_LOOK } from '../lib/feedback-presentation'

/** `YYYY-MM-DD` — server `DateOnly` kutadi. */
function today(): string {
  return new Date().toISOString().slice(0, 10)
}

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Qayta aloqa yozuvini yaratish yoki tahrirlash — BITTA komponent
 * (`incident-form-dialog.tsx` dagi bilan bir xil naqsh).
 *
 * <b>Mehnat munosabati FAQAT yaratishda tanlanadi</b> ikki bosqichda:
 * avval xodim (faqat ISHDAN KETGANLAR — `useTerminatedEmployeeOptions`),
 * so'ng — agar shu xodimning bir nechta yakunlangan mehnat munosabati
 * bo'lsa (masalan qayta ishga olinib, yana bo'shatilgan bo'lsa) — aynan
 * qaysi biriga suhbat tegishli ekani. Tahrirlashda bu tanlov o'zgarmaydi
 * (backend'da `UpdateExitInterviewRequest`da `employmentId` umuman yo'q).
 *
 * <b>Tanlangan munosabatda suhbat allaqachon bo'lsa</b> — saqlash
 * bloklanadi (`by-employment` orqali tekshiriladi, `404` esa oddiy «hali
 * yo'q» holati). Backend baribir 409 bilan rad etardi, lekin buni oldindan
 * ko'rsatish HR ni foydasiz xatoga uchratmaydi.
 */
export function ExitInterviewFormDialog({
  open,
  onClose,
  item,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi yozuv. */
  item: ExitInterview | null
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()

  const isEdit = item !== null

  const [employeeId, setEmployeeId] = useState('')
  const [employmentId, setEmploymentId] = useState('')
  const [interviewDate, setInterviewDate] = useState(today)
  const [reason, setReason] = useState('')
  const [wouldRecommend, setWouldRecommend] = useState<RecommendationAnswerValue | ''>('')
  const [impressions, setImpressions] = useState('')
  const [conditionsToReturn, setConditionsToReturn] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  const employeeOptions = useTerminatedEmployeeOptions()
  const { employments, isPending: employmentsPending } = useTerminatedEmployments(employeeId)

  /*
   * Duplikat tekshiruvi FAQAT yaratishda va FAQAT munosabat tanlangach
   * ishga tushadi — tahrirlashda kerak emas (allaqachon shu yozuvning o'zi).
   */
  const existing = useExitInterviewByEmployment(!isEdit && employmentId !== '' ? employmentId : null)
  const hasDuplicate = !isEdit && existing.data !== null && existing.data !== undefined

  const create = useCreateExitInterview()
  const update = useUpdateExitInterview()
  const mutation = isEdit ? update : create

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)

    if (isEdit && item !== null) {
      setEmployeeId('')
      setEmploymentId(item.employmentId)
      setInterviewDate(item.interviewDate)
      setReason(item.reason)
      setWouldRecommend(item.wouldRecommend ?? '')
      setImpressions(item.impressions ?? '')
      setConditionsToReturn(item.conditionsToReturn ?? '')
    } else {
      setEmployeeId('')
      setEmploymentId('')
      setInterviewDate(today())
      setReason('')
      setWouldRecommend('')
      setImpressions('')
      setConditionsToReturn('')
    }

    create.reset()
    update.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  // Xodim almashtirilganda eski tanlov ma'nosiz qoladi — aks holda boshqa
  // odamning munosabat `id`si jimgina yuborilib ketardi.
  const handleEmployeeChange = (nextId: string) => {
    setEmployeeId(nextId)
    setEmploymentId('')
  }

  // Bitta yakunlangan munosabat bo'lsa — tanlov shart emas, o'zi tanlanadi.
  useEffect(() => {
    if (isEdit) return
    if (employmentsPending) return
    const [only] = employments
    if (employments.length === 1 && only !== undefined) setEmploymentId(only.id)
  }, [isEdit, employmentsPending, employments])

  if (!open) return null

  const incomplete =
    (!isEdit && (employeeId === '' || employmentId === '')) ||
    // Duplikat tekshiruvi hali tugamagan bo'lsa ham saqlash bloklanadi —
    // aks holda HR javobni kutmasdan bosishi va 409 ga uchrashi mumkin.
    (!isEdit && employmentId !== '' && existing.isPending) ||
    interviewDate === '' ||
    reason.trim() === '' ||
    hasDuplicate

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    const shared = {
      interviewDate,
      reason: reason.trim(),
      wouldRecommend: wouldRecommend === '' ? null : wouldRecommend,
      impressions: orNull(impressions),
      conditionsToReturn: orNull(conditionsToReturn),
    }

    const onError = (error: unknown) => {
      const code = feedbackErrorCode(error)
      const field = FEEDBACK_ERROR_FIELD[code]
      if (field === undefined) setFailure(code)
      else setFieldError({ field, code })
    }

    if (isEdit && item !== null) {
      update.mutate({ id: item.id, body: shared }, { onSuccess: onClose, onError })
    } else {
      create.mutate({ employmentId, ...shared }, { onSuccess: onClose, onError })
    }
  }

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  const recommendOptions = [
    { value: '', label: t('feedback.recommendUnknown') },
    ...RECOMMENDATION_ANSWERS.map((value) => ({ value, label: t(RECOMMENDATION_LOOK[value].labelKey) })),
  ]

  const employmentOptions = employments.map((row) => ({
    value: row.id,
    label: [
      row.department?.name ?? row.legalEntity?.name ?? '—',
      row.terminationDate !== null ? date(row.terminationDate) : null,
    ]
      .filter((part): part is string => part !== null)
      .join(' — '),
  }))

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!mutation.isPending}
      title={isEdit ? t('feedback.editTitle') : t('feedback.newTitle')}
      description={isEdit && item !== null ? item.employeeName ?? undefined : undefined}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={mutation.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={mutation.isPending || incomplete}
          >
            {mutation.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        {isEdit && item !== null ? (
          <div className="rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
            <p className="font-medium">{item.employeeName}</p>
            <p className="mt-0.5 text-footnote text-[var(--fg-secondary)]">
              {[item.departmentName, item.positionName].filter(Boolean).join(' · ') || '—'}
              {item.terminationDate !== null && ` · ${t('feedback.leftOn', { date: date(item.terminationDate) })}`}
            </p>
          </div>
        ) : (
          <>
            <Select
              label={t('feedback.employee')}
              options={employeeOptions.options}
              placeholder={
                employeeOptions.isPending ? t('common.loading') : t('feedback.chooseEmployee')
              }
              value={employeeId}
              disabled={employeeOptions.isPending}
              error={fieldMessage('employmentId')}
              onChange={(event) => handleEmployeeChange(event.target.value)}
            />

            {employeeId !== '' && !employmentsPending && employments.length === 0 && (
              <Alert tone="warning">{t('feedback.noTerminatedEmployment')}</Alert>
            )}

            {employeeId !== '' && employments.length > 1 && (
              <Select
                label={t('feedback.employment')}
                options={employmentOptions}
                placeholder={t('feedback.chooseEmployment')}
                value={employmentId}
                onChange={(event) => setEmploymentId(event.target.value)}
              />
            )}

            {hasDuplicate && <Alert tone="warning">{t('feedback.alreadyExists')}</Alert>}
          </>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('feedback.interviewDate')}
            type="date"
            value={interviewDate}
            onChange={(event) => setInterviewDate(event.target.value)}
          />
          <Select
            label={t('feedback.wouldRecommend')}
            options={recommendOptions}
            value={wouldRecommend}
            onChange={(event) => setWouldRecommend(event.target.value as RecommendationAnswerValue | '')}
          />
        </div>

        <TextArea
          label={t('feedback.reason')}
          rows={3}
          value={reason}
          error={fieldMessage('reason')}
          onChange={(event) => setReason(event.target.value)}
        />

        <TextArea
          label={t('feedback.impressions')}
          rows={3}
          value={impressions}
          onChange={(event) => setImpressions(event.target.value)}
        />

        <TextArea
          label={t('feedback.conditionsToReturn')}
          rows={2}
          value={conditionsToReturn}
          onChange={(event) => setConditionsToReturn(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
