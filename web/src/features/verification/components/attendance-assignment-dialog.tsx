import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useFilterOptions } from '@/features/employees/api/employees-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  Dialog,
  SegmentedControl,
  Select,
  TextField,
  type SegmentOption,
} from '@/shared/ui'

import type { ScheduleScope, WorkSchedule } from '../api/attendance-api'
import { useCreateAssignment } from '../api/attendance-queries'
import { SCOPE_LOOK, SCOPE_ORDER } from '../lib/attendance-presentation'
import { AttendanceEmployeePicker, type PickedEmployee } from './attendance-employee-picker'

type Field = 'schedule' | 'target' | 'effectiveFrom' | 'effectiveTo'

/**
 * Server kodi → maydon. Ro'yxatda yo'q kod tepadagi umumiy
 * ogohlantirishga tushadi (`catalog-error.ts` naqshi).
 */
const FIELD_OF_CODE: Readonly<Record<string, Field>> = {
  'davomat.jadval_yoq': 'schedule',
  'davomat.jadval_topilmadi': 'schedule',
  // Oyna ochiq turganda boshqa operator jadvalni faolsizlantirgan bo'lishi mumkin
  'davomat.jadval_faolsiz': 'schedule',
  'davomat.biriktirma_obyekt_yoq': 'target',
  'davomat.xodim_yoq': 'target',
  'davomat.xodim_topilmadi': 'target',
  // «Shu daraja va shu sanadan boshlanadigan biriktirma bor» — tuzatiladigan joy sana
  'davomat.biriktirma_takror': 'effectiveFrom',
  'davomat.davr_notogri': 'effectiveTo',
}

/**
 * Jadvalni biriktirish: xodimga, lavozimga, bo'limga yoki hammaga —
 * amal qilish sanalari bilan.
 *
 * ⭐ Ustunlik tartibi oynaning o'zida yozilgan (SPEC §5.2): xodim →
 * lavozim → bo'lim (OTA bo'lim ham) → hamma uchun. Busiz HR bo'limga
 * jadval biriktirib, lavozim biriktirmasi uni «bosib» qolganini
 * tushunmasdi — «biriktirdim, lekin ishlamayapti» degan murojaat aynan
 * shu yerdan chiqadi.
 *
 * Biriktirma TAHRIRLANMAYDI (shartnomada `PUT` yo'q, §6.4): noto'g'ri
 * yozilgan bo'lsa o'chiriladi va yangisi yaratiladi. Sanani «tuzatish»
 * o'tgan oylarning tabelini jimgina qayta yozgan bo'lardi.
 */
export function AttendanceAssignmentDialog({
  open,
  onClose,
  schedules,
  defaultScheduleId = null,
}: {
  open: boolean
  onClose: () => void
  schedules: readonly WorkSchedule[]
  /** Jadval kartasidan ochilganda — o'sha jadval oldindan tanlangan. */
  defaultScheduleId?: string | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const create = useCreateAssignment()
  const options = useFilterOptions()

  const [scheduleId, setScheduleId] = useState('')
  const [scope, setScope] = useState<ScheduleScope>('Department')
  const [employee, setEmployee] = useState<PickedEmployee | null>(null)
  const [positionId, setPositionId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [effectiveFrom, setEffectiveFrom] = useState(todayIso)
  const [effectiveTo, setEffectiveTo] = useState('')

  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: Field; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setScheduleId(defaultScheduleId ?? '')
    setScope('Department')
    setEmployee(null)
    setPositionId('')
    setDepartmentId('')
    setEffectiveFrom(todayIso())
    setEffectiveTo('')
    setFailure(null)
    setFieldError(null)
    create.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, defaultScheduleId])

  /*
    Lavozim nomi bo'limlarda takrorlanadi («Operator» uch sexda bor) —
    yorliqqa bo'lim nomi qo'shiladi, aks holda ro'yxatda uchta bir xil
    qator turardi va biriktirma tasodifiy sexga tushardi.
  */
  const positionOptions = useMemo(() => {
    const departmentName = new Map(options.departments.map((item) => [item.value, item.label]))
    return options.positions.map((item) => {
      const department = departmentName.get(options.positionDepartment.get(item.value) ?? '')
      return {
        value: item.value,
        label: department === undefined ? item.label : `${item.label} · ${department}`,
      }
    })
  }, [options.departments, options.positions, options.positionDepartment])

  /*
    Faqat FAOL jadvallar: server faolsizini biriktirmaydi
    (`davomat.jadval_faolsiz`, 409). Ro'yxatda «(faol emas)» belgisi bilan
    qoldirish operatorga aniq rad javobi beradigan tanlovni taklif qilardi.
  */
  const scheduleOptions = schedules
    .filter((schedule) => schedule.isActive)
    .map((schedule) => ({ value: schedule.id, label: schedule.name }))

  const scopeOptions: Array<SegmentOption<ScheduleScope>> = SCOPE_ORDER.map((value) => ({
    value,
    label: t(SCOPE_LOOK[value].labelKey),
  }))

  const targetMissing =
    (scope === 'Employee' && employee === null) ||
    (scope === 'Position' && positionId === '') ||
    (scope === 'Department' && departmentId === '')

  // `DateOnly` satrlari leksik tartibda solishtiriladi — mintaqa xatosi yo'q
  const rangeInverted = effectiveTo !== '' && effectiveFrom !== '' && effectiveTo < effectiveFrom

  const incomplete = scheduleId === '' || targetMissing || effectiveFrom === '' || rangeInverted
  const pending = create.isPending

  const changeScope = (next: ScheduleScope) => {
    setScope(next)
    if (fieldError?.field === 'target') setFieldError(null)
  }

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    create.mutate(
      {
        workScheduleId: scheduleId,
        scope,
        // Aynan darajaga mos BITTA maydon to'ldiriladi — server tekshiruvi ham shu (§2.5)
        employeeId: scope === 'Employee' ? (employee?.id ?? null) : null,
        positionId: scope === 'Position' ? positionId : null,
        departmentId: scope === 'Department' ? departmentId : null,
        effectiveFrom,
        effectiveTo: effectiveTo === '' ? null : effectiveTo,
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = problemCode(error)
          const field = FIELD_OF_CODE[code]
          if (field === undefined) setFailure(code)
          else setFieldError({ field, code })
        },
      },
    )
  }

  const fieldMessage = (field: Field): string | undefined =>
    fieldError?.field === field ? errorMessage(fieldError.code) : undefined

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!pending}
      title={t('attendance.assignmentCreateTitle')}
      description={t('attendance.scopePrecedenceHint')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant="primary" onClick={submit} disabled={pending || incomplete}>
            {pending && <LoaderCircle className="animate-spin" />}
            {t('attendance.assignmentCreate')}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        <Select
          label={t('attendance.schedule')}
          options={scheduleOptions}
          placeholder={t('attendance.chooseSchedule')}
          value={scheduleId}
          disabled={pending}
          error={fieldMessage('schedule')}
          onChange={(event) => setScheduleId(event.target.value)}
        />

        <div className="space-y-2">
          <p className="text-footnote font-medium text-[var(--fg-secondary)]">
            {t('attendance.assignTo')}
          </p>
          <SegmentedControl
            options={scopeOptions}
            value={scope}
            onChange={changeScope}
            className="w-full"
            aria-label={t('attendance.assignTo')}
          />
        </div>

        {scope === 'Employee' && (
          <AttendanceEmployeePicker
            value={employee}
            onChange={setEmployee}
            disabled={pending}
            error={fieldMessage('target')}
          />
        )}

        {scope === 'Position' && (
          <Select
            label={t('attendance.scopePosition')}
            options={positionOptions}
            placeholder={options.isPending ? t('common.loading') : t('employees.choosePosition')}
            value={positionId}
            disabled={pending || options.isPending}
            error={fieldMessage('target')}
            hint={t('attendance.scopePositionHint')}
            onChange={(event) => setPositionId(event.target.value)}
          />
        )}

        {scope === 'Department' && (
          <Select
            label={t('attendance.scopeDepartment')}
            options={options.departments}
            placeholder={options.isPending ? t('common.loading') : t('employees.chooseDepartment')}
            value={departmentId}
            disabled={pending || options.isPending}
            error={fieldMessage('target')}
            hint={t('attendance.scopeDepartmentHint')}
            onChange={(event) => setDepartmentId(event.target.value)}
          />
        )}

        {scope === 'Default' && (
          <Alert tone="info">{t('attendance.scopeDefaultHint')}</Alert>
        )}

        {scope === 'Default' && fieldMessage('target') !== undefined && (
          <Alert tone="danger">{fieldMessage('target')}</Alert>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('attendance.effectiveFrom')}
            type="date"
            value={effectiveFrom}
            disabled={pending}
            error={fieldMessage('effectiveFrom')}
            onChange={(event) => {
              setEffectiveFrom(event.target.value)
              if (fieldError?.field === 'effectiveFrom') setFieldError(null)
            }}
          />
          <TextField
            label={t('attendance.effectiveTo')}
            type="date"
            value={effectiveTo}
            min={effectiveFrom === '' ? undefined : effectiveFrom}
            disabled={pending}
            hint={t('attendance.effectiveToHint')}
            error={
              rangeInverted ? errorMessage('davomat.davr_notogri') : fieldMessage('effectiveTo')
            }
            onChange={(event) => setEffectiveTo(event.target.value)}
          />
        </div>
      </div>
    </Dialog>
  )
}
