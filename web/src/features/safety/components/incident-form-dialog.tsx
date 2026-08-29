import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useCatalogList } from '@/features/catalog/api/catalog-queries'
import type { PositionItem } from '@/features/catalog/api/catalog-api'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, Switch, TextArea, TextField } from '@/shared/ui'

import {
  useCreateIncident,
  useEmployeeOptions,
  useUpdateIncident,
} from '../api/incidents-queries'
import { INCIDENT_SEVERITIES, type Incident, type IncidentSeverityValue } from '../api/incidents-api'
import { SAFETY_ERROR_FIELD, safetyErrorCode } from '../lib/safety-error'
import { SEVERITY_LOOK } from '../lib/safety-presentation'

/** `YYYY-MM-DD` — server `DateOnly` kutadi. */
function today(): string {
  return new Date().toISOString().slice(0, 10)
}

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/**
 * Baxtsiz hodisani yozish yoki tahrirlash — BITTA komponent
 * (`incentive-dialog.tsx` dagi bilan bir xil naqsh).
 *
 * <b>`employeeId` faqat YARATISHDA tanlanadi.</b> Tahrirlashda jabrlangan
 * xodim o'zgarmaydi — backend'da `UpdateIncidentRequest`da bu maydon
 * umuman yo'q: noto'g'ri xodimga yozilgan bo'lsa, bu tuzatish emas,
 * boshqa fakt — yozuv o'chirilib, yangisi kiritiladi.
 *
 * <b>Komissiya xulosasi bu yerda ATAYLAB YO'Q.</b> U tekshiruv tugagach,
 * alohida `IncidentConcludeDialog` orqali qo'shiladi — sabab
 * `incidents-api.ts` boshidagi izohda.
 */
/**
 * Formani AYNAN shu xodim uchun ochish (xodim kartasidan kelganda).
 *
 * Tanlov o'rniga odamning o'zi ko'rsatiladi: karta ochilganda kim
 * haqidaligi allaqachon ma'lum va uni ro'yxatdan qayta izlatish —
 * noto'g'ri odamni tanlashning eng oson yo'li.
 */
export interface LockedEmployee {
  id: string
  name: string
  photoKey: string | null
}

export function IncidentFormDialog({
  open,
  onClose,
  item,
  lockedEmployee = null,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi hodisa. */
  item: Incident | null
  /** Berilsa — xodim tanlanmaydi, shu odam uchun yoziladi. */
  lockedEmployee?: LockedEmployee | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const isEdit = item !== null

  const [employeeId, setEmployeeId] = useState('')
  const [incidentDate, setIncidentDate] = useState(today)
  const [location, setLocation] = useState('')
  const [description, setDescription] = useState('')
  const [severity, setSeverity] = useState<IncidentSeverityValue>('minor')
  const [shift, setShift] = useState('')
  const [positionId, setPositionId] = useState('')
  const [cause, setCause] = useState('')
  const [correctiveAction, setCorrectiveAction] = useState('')
  const [medicalAidProvided, setMedicalAidProvided] = useState(false)
  const [medicalAidNote, setMedicalAidNote] = useState('')
  const [responsibleEmployeeId, setResponsibleEmployeeId] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  /** Maydonga bog'langan server xatosi — masalan «joy» yoki «tavsif» bo'sh. */
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  const employees = useEmployeeOptions()
  const positions = useCatalogList<PositionItem>('positions', false)

  const create = useCreateIncident()
  const update = useUpdateIncident()
  const mutation = isEdit ? update : create

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)

    if (isEdit && item !== null) {
      setEmployeeId(item.employeeId)
      setIncidentDate(item.incidentDate)
      setLocation(item.location)
      setDescription(item.description)
      setSeverity(item.severity)
      setShift(item.shift ?? '')
      setPositionId(item.positionId ?? '')
      setCause(item.cause ?? '')
      setCorrectiveAction(item.correctiveAction ?? '')
      setMedicalAidProvided(item.medicalAidProvided)
      setMedicalAidNote(item.medicalAidNote ?? '')
      setResponsibleEmployeeId(item.responsibleEmployeeId ?? '')
    } else {
      setEmployeeId(lockedEmployee?.id ?? '')
      setIncidentDate(today())
      setLocation('')
      setDescription('')
      setSeverity('minor')
      setShift('')
      setPositionId('')
      setCause('')
      setCorrectiveAction('')
      setMedicalAidProvided(false)
      setMedicalAidNote('')
      setResponsibleEmployeeId('')
    }

    create.reset()
    update.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item, lockedEmployee?.id])

  if (!open) return null

  const incomplete =
    (!isEdit && employeeId === '') ||
    incidentDate === '' ||
    location.trim() === '' ||
    description.trim() === ''

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    const shared = {
      incidentDate,
      location: location.trim(),
      description: description.trim(),
      severity,
      shift: orNull(shift),
      positionId: positionId === '' ? null : positionId,
      cause: orNull(cause),
      correctiveAction: orNull(correctiveAction),
      medicalAidProvided,
      medicalAidNote: medicalAidProvided ? orNull(medicalAidNote) : null,
      responsibleEmployeeId: responsibleEmployeeId === '' ? null : responsibleEmployeeId,
    }

    const onError = (error: unknown) => {
      const code = safetyErrorCode(error)
      const field = SAFETY_ERROR_FIELD[code]
      if (field === undefined) setFailure(code)
      else setFieldError({ field, code })
    }

    if (isEdit && item !== null) {
      update.mutate({ id: item.id, body: shared }, { onSuccess: onClose, onError })
    } else {
      create.mutate({ employeeId, ...shared }, { onSuccess: onClose, onError })
    }
  }

  const severityOptions = INCIDENT_SEVERITIES.map((value) => ({
    value,
    label: t(SEVERITY_LOOK[value].labelKey),
  }))

  const fieldMessage = (field: string): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  /** Xodim OLDINDAN ma'lum bo'lgan holat — tahrirlash yoki karta. */
  const person: LockedEmployee | null =
    isEdit && item !== null
      ? { id: item.employeeId, name: item.employeeName ?? '', photoKey: item.photoKey }
      : lockedEmployee

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!mutation.isPending}
      title={isEdit ? t('safety.editTitle') : t('safety.newTitle')}
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

        {/*
          Xodim — faqat yaratishda VA faqat kimligi noma'lum bo'lganda
          tanlanadi. Tahrirlashda jabrlangan xodim umuman o'zgarmaydi
          (yuqoridagi izohga qarang), xodim kartasidan kelganda esa u
          allaqachon ma'lum — ikkala holatda ham tanlov o'rniga odamning
          o'zi ko'rsatiladi.
        */}
        {person !== null ? (
          <div className="flex items-center gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
            <EmployeePhoto
              employeeId={person.id}
              photoKey={person.photoKey}
              name={person.name}
              size="md"
            />
            <div className="min-w-0">
              <p className="text-footnote text-[var(--fg-tertiary)]">{t('safety.employee')}</p>
              <p className="truncate-1 font-medium">{person.name}</p>
            </div>
          </div>
        ) : (
          <Select
            label={t('safety.employee')}
            options={employees.options}
            placeholder={employees.isPending ? t('common.loading') : t('safety.chooseEmployee')}
            value={employeeId}
            disabled={employees.isPending}
            error={fieldMessage('employeeId')}
            onChange={(event) => setEmployeeId(event.target.value)}
          />
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('safety.date')}
            type="date"
            value={incidentDate}
            onChange={(event) => setIncidentDate(event.target.value)}
          />
          <Select
            label={t('safety.severity')}
            options={severityOptions}
            value={severity}
            onChange={(event) => setSeverity(event.target.value as IncidentSeverityValue)}
          />
        </div>

        <TextField
          label={t('safety.location')}
          value={location}
          error={fieldMessage('location')}
          onChange={(event) => setLocation(event.target.value)}
        />

        <TextArea
          label={t('safety.description')}
          rows={3}
          value={description}
          error={fieldMessage('description')}
          onChange={(event) => setDescription(event.target.value)}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('safety.shift')}
            hint={t('safety.shiftHint')}
            value={shift}
            onChange={(event) => setShift(event.target.value)}
          />
          <Select
            label={t('safety.position')}
            placeholder={t('safety.noPosition')}
            options={(positions.data ?? []).map((option) => ({
              value: option.id,
              label: option.name,
            }))}
            disabled={positions.isPending}
            value={positionId}
            error={fieldMessage('positionId')}
            onChange={(event) => setPositionId(event.target.value)}
          />
        </div>

        <TextArea
          label={t('safety.cause')}
          rows={2}
          value={cause}
          onChange={(event) => setCause(event.target.value)}
        />

        <TextArea
          label={t('safety.correctiveAction')}
          rows={2}
          value={correctiveAction}
          onChange={(event) => setCorrectiveAction(event.target.value)}
        />

        <Select
          label={t('safety.responsibleEmployee')}
          placeholder={t('safety.noResponsible')}
          options={employees.options}
          disabled={employees.isPending}
          value={responsibleEmployeeId}
          error={fieldMessage('responsibleEmployeeId')}
          onChange={(event) => setResponsibleEmployeeId(event.target.value)}
        />

        <div className="space-y-3 rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
          <Switch
            checked={medicalAidProvided}
            onChange={setMedicalAidProvided}
            label={t('safety.medicalAidProvided')}
          />

          {medicalAidProvided && (
            <TextField
              label={t('safety.medicalAidNote')}
              value={medicalAidNote}
              onChange={(event) => setMedicalAidNote(event.target.value)}
            />
          )}
        </div>
      </div>
    </Dialog>
  )
}
