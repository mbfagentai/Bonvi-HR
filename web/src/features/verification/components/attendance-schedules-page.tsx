import {
  CalendarDays,
  CalendarPlus,
  CalendarRange,
  CircleCheck,
  Clock,
  LoaderCircle,
  MoonStar,
  Pencil,
  Plus,
  Trash2,
  UserRoundSearch,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  Dialog,
  EmptyState,
  IconTile,
  PageHeader,
  SegmentedControl,
  Skeleton,
  Switch,
  TextField,
  type BadgeTone,
  type SegmentOption,
} from '@/shared/ui'

import type {
  AssignmentFilters,
  ScheduleAssignment,
  ScheduleResolution,
  ScheduleScope,
  WorkSchedule,
} from '../api/attendance-api'
import {
  useDeleteAssignment,
  useDeleteSchedule,
  useScheduleAssignments,
  useScheduleResolution,
  useSetScheduleActive,
  useWorkSchedules,
} from '../api/attendance-queries'
import {
  ATTENDANCE_MANAGE,
  ATTENDANCE_VIEW,
  SCOPE_LOOK,
  SCOPE_ORDER,
  WEEKDAY_LABEL_KEY,
  breakWindowLabel,
  crossesMidnight,
  formatDuration,
  isoWeekday,
  weekSummary,
} from '../lib/attendance-presentation'
import {
  assignmentInForce,
  inheritedFromParent,
  plannedDayMinutes,
  scheduleOutcome,
} from '../lib/attendance-schedule-rules'
import { AttendanceAssignmentDialog } from './attendance-assignment-dialog'
import { AttendanceEmployeePicker, type PickedEmployee } from './attendance-employee-picker'
import {
  AttendanceLoadError,
  AttendanceNoAccess,
  AttendancePageTitle,
} from './attendance-manage-states'
import { AttendanceScheduleFormDialog } from './attendance-schedule-form-dialog'
import { AttendanceScheduleYearDialog } from './attendance-schedule-year-dialog'

/** Filtrsiz — barcha tirik biriktirmalar. Modul darajasida: kalit barqaror bo'lsin. */
const ALL_ASSIGNMENTS: AssignmentFilters = {}
const NO_SCHEDULES: WorkSchedule[] = []
const NO_ASSIGNMENTS: ScheduleAssignment[] = []

type AssignmentView = 'all' | 'inForce'
type AssignmentState = 'inForce' | 'future' | 'ended'

const ASSIGNMENT_STATE_LOOK: Readonly<Record<AssignmentState, { tone: BadgeTone; labelKey: string }>> = {
  inForce: { tone: 'success', labelKey: 'attendance.assignmentInForce' },
  future: { tone: 'info', labelKey: 'attendance.assignmentFuture' },
  ended: { tone: 'neutral', labelKey: 'attendance.assignmentEnded' },
}

/**
 * Ish jadvallari — jadvallar, ularning biriktirmalari va «kim uchun amal
 * qiladi» javobi bitta sahifada.
 *
 * NEGA uchalasi birga: HR savoli hech qachon «jadvallar ro'yxati qanaqa?»
 * emas, «nega bu odam 9–18 da hisoblanyapti?». Javob uch qadamda —
 * qaysi jadval, qaysi biriktirma orqali, qaysi daraja yutdi — va uchala
 * qadam bir ekranda ko'rinsa, murojaat dasturchiga yetib bormaydi (§6.4).
 *
 * Ruxsat (SPEC §7.3): O'QISH — `attendance.view` (jadval tabelning
 * asosi, uni ko'radigan odam nima bo'yicha hisoblanganini ham ko'rishi
 * kerak); YOZISH — `attendance.manage`. Ruxsati yo'q tugma umuman
 * chizilmaydi.
 */
export function AttendanceSchedulesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canView = hasPermission(ATTENDANCE_VIEW)
  // O'qiy olmagan odam tahrirlay ham olmaydi: forma o'qilgan ro'yxat ustida ishlaydi
  const canManage = canView && hasPermission(ATTENDANCE_MANAGE)

  const schedules = useWorkSchedules(canView)
  const assignments = useScheduleAssignments(ALL_ASSIGNMENTS, canView)

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<WorkSchedule | null>(null)

  /** Yillik to'r oynasi — qaysi jadval uchun ochilgan. */
  const [yearOf, setYearOf] = useState<WorkSchedule | null>(null)
  const [deleting, setDeleting] = useState<WorkSchedule | null>(null)
  const [deactivating, setDeactivating] = useState<WorkSchedule | null>(null)

  const setActive = useSetScheduleActive()

  const [assignOpen, setAssignOpen] = useState(false)
  const [assignScheduleId, setAssignScheduleId] = useState<string | null>(null)
  const [removing, setRemoving] = useState<ScheduleAssignment | null>(null)

  const scheduleList = schedules.data ?? NO_SCHEDULES
  const assignmentList = assignments.data ?? NO_ASSIGNMENTS

  /*
    Biriktirish faqat FAOL jadvalga: server faolsizini rad etadi
    (`davomat.jadval_faolsiz`, 409), ya'ni uni ro'yxatda taklif qilish
    operatorni aniq rad javobiga yetaklardi.
  */
  const hasActiveSchedule = scheduleList.some((schedule) => schedule.isActive)

  /** Jadval → unga ishora qilayotgan biriktirmalar soni (o'chirishdan oldingi ogohlantirish). */
  const assignmentCount = useMemo(() => {
    const counts = new Map<string, number>()
    for (const item of assignmentList) {
      counts.set(item.workScheduleId, (counts.get(item.workScheduleId) ?? 0) + 1)
    }
    return counts
  }, [assignmentList])

  if (!canView) {
    return <AttendanceNoAccess icon={Clock} title={t('attendance.schedulesTitle')} />
  }

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (schedule: WorkSchedule) => {
    setEditing(schedule)
    setFormOpen(true)
  }

  const openAssign = (scheduleId: string | null) => {
    setAssignScheduleId(scheduleId)
    setAssignOpen(true)
  }

  /*
    Yoqish — darhol (xavfsiz amal: jadval yana tanlovga qaytadi), o'chirish —
    tasdiq bilan, chunki biriktirmasi bor jadvalni server rad etadi va oyna
    buni bosishdan OLDIN aytadi (qurilmalar sahifasidagi bilan bir xil naqsh).
  */
  const toggleActive = (schedule: WorkSchedule, next: boolean) => {
    if (next) {
      setActive.mutate({ id: schedule.id, active: true })
    } else {
      setDeactivating(schedule)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={<AttendancePageTitle icon={Clock} title={t('attendance.schedulesTitle')} />}
        subtitle={
          schedules.isPending
            ? t('common.loading')
            : t('attendance.schedulesSubtitle', { n: num(scheduleList.length) })
        }
        actions={
          canManage ? (
            <Button variant="create" onClick={openCreate}>
              <Plus />
              {t('attendance.scheduleCreate')}
            </Button>
          ) : undefined
        }
      />

      {/*
        Faollashtirish tasdiqsiz ketadi, ya'ni xatosi oynada emas — shu yerda,
        ro'yxat tepasida ko'rinadi. Faolsizlantirish xatosi o'z oynasida.
      */}
      {setActive.isError && setActive.variables.active && (
        <Alert tone="danger">{errorMessage(problemCode(setActive.error))}</Alert>
      )}

      {/* ─────────────────────────── jadvallar */}
      {schedules.isPending ? (
        <div className="space-y-4" aria-busy>
          {Array.from({ length: 2 }, (_, index) => (
            <Card key={`skeleton-${index}`} className="space-y-4 p-5">
              <Skeleton className="h-5 w-48" />
              <Skeleton className="h-3.5 w-64" />
              <div className="grid gap-1.5 md:grid-cols-7">
                {Array.from({ length: 7 }, (_, day) => (
                  <Skeleton key={`day-${day}`} className="h-16" />
                ))}
              </div>
            </Card>
          ))}
        </div>
      ) : schedules.isError ? (
        <Card>
          <AttendanceLoadError
            error={schedules.error}
            onRetry={() => void schedules.refetch()}
            retrying={schedules.isFetching}
          />
        </Card>
      ) : scheduleList.length === 0 ? (
        <Card>
          <EmptyState
            icon={Clock}
            title={t('attendance.schedulesEmptyTitle')}
            description={t('attendance.schedulesEmptyText')}
            action={
              canManage ? (
                <Button variant="create" onClick={openCreate}>
                  <Plus />
                  {t('attendance.scheduleCreate')}
                </Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <div className="space-y-4">
          {scheduleList.map((schedule) => (
            <ScheduleCard
              key={schedule.id}
              schedule={schedule}
              assignmentCount={assignmentCount.get(schedule.id) ?? 0}
              canManage={canManage}
              toggling={setActive.isPending && setActive.variables.id === schedule.id}
              onToggleActive={(next) => toggleActive(schedule, next)}
              onEdit={() => openEdit(schedule)}
              onDelete={() => setDeleting(schedule)}
              onAssign={() => openAssign(schedule.id)}
              onYear={() => setYearOf(schedule)}
            />
          ))}
        </div>
      )}

      {/* ─────────────────────────── biriktirmalar */}
      <AssignmentsCard
        query={assignments}
        items={assignmentList}
        canManage={canManage}
        canAssign={canManage && hasActiveSchedule}
        onAssign={() => openAssign(null)}
        onRemove={setRemoving}
      />

      {/* ─────────────────────────── kim uchun amal qiladi */}
      <ResolvePreviewCard />

      {canManage && (
        <>
          <AttendanceScheduleYearDialog schedule={yearOf} onClose={() => setYearOf(null)} />

          <AttendanceScheduleFormDialog
            open={formOpen}
            onClose={() => setFormOpen(false)}
            schedule={editing}
          />
          <AttendanceAssignmentDialog
            open={assignOpen}
            onClose={() => setAssignOpen(false)}
            schedules={scheduleList}
            defaultScheduleId={assignScheduleId}
          />
          <DeactivateScheduleDialog
            schedule={deactivating}
            assignmentCount={
              deactivating === null || assignments.isError
                ? null
                : (assignmentCount.get(deactivating.id) ?? 0)
            }
            onClose={() => setDeactivating(null)}
          />
          <DeleteScheduleDialog
            schedule={deleting}
            assignmentCount={
              deleting === null || assignments.isError
                ? null
                : (assignmentCount.get(deleting.id) ?? 0)
            }
            onClose={() => setDeleting(null)}
          />
          <RemoveAssignmentDialog item={removing} onClose={() => setRemoving(null)} />
        </>
      )}
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ jadval kartasi */

function ScheduleCard({
  schedule,
  assignmentCount,
  canManage,
  toggling,
  onToggleActive,
  onEdit,
  onDelete,
  onAssign,
  onYear,
}: {
  schedule: WorkSchedule
  assignmentCount: number
  canManage: boolean
  /** Shu jadvalning yoqish/o'chirish so'rovi ketmoqda. */
  toggling: boolean
  onToggleActive: (next: boolean) => void
  onEdit: () => void
  onYear: () => void
  onDelete: () => void
  onAssign: () => void
}) {
  const { t, num } = useLocale()
  const summary = weekSummary(schedule.days)
  const hours = Math.round((summary.plannedMinutes / 60) * 10) / 10

  return (
    <Card className="p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="truncate-1 text-callout font-semibold">{schedule.name}</h2>
            {!schedule.isActive && (
              <Badge size="sm">{t('attendance.scheduleInactive')}</Badge>
            )}
          </div>
          <p className="tabular mt-1 text-footnote text-[var(--fg-secondary)]">
            {t('attendance.weekSummary', { days: num(summary.workingDays), hours: num(hours) })}
            {' · '}
            {t('attendance.graceSummary', {
              in: num(schedule.graceInMinutes),
              out: num(schedule.graceOutMinutes),
            })}
          </p>
        </div>

        {canManage && (
          <div className="flex shrink-0 flex-wrap items-center gap-1.5">
            <Switch
              checked={schedule.isActive}
              disabled={toggling}
              onChange={onToggleActive}
              label={t('attendance.active')}
              aria-label={`${schedule.name} — ${t('attendance.active')}`}
              className="mr-2"
            />
            {/* Faolsiz jadvalni server biriktirmaydi (`davomat.jadval_faolsiz`) — tugma ham yo'q */}
            {schedule.isActive && (
              <Button type="button" variant="soft" size="sm" onClick={onAssign}>
                <CalendarPlus />
                {t('attendance.assignmentCreate')}
              </Button>
            )}
            {/*
              Yillik to'r — TAHRIRLASHDAN alohida tugma: naqsh (haftalik
              qoida) va istisnolar (bayram, qisqa kun) ikki xil ish va
              ular bitta oynaga sig'masdi (SPEC-jadval-to'ri §D).
            */}
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title={t('attendance.scheduleYearOpen')}
              aria-label={`${schedule.name} — ${t('attendance.scheduleYear')}`}
              onClick={onYear}
            >
              <CalendarRange />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title={t('common.edit')}
              aria-label={t('common.edit')}
              onClick={onEdit}
            >
              <Pencil />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title={t('common.delete')}
              aria-label={t('common.delete')}
              onClick={onDelete}
            >
              <Trash2 className="text-[var(--danger-text)]" />
            </Button>
          </div>
        )}
      </div>

      {/*
        Haftalik yettita quti OLIB TASHLANDI (egasi, 2026-09-20). Sabab:
        jadvalning kunlari endi ikki joyda ko'rinadi — tahrirlash
        oynasida (naqsh) va YILLIK TO'RDA (haqiqiy kunlar). Ro'yxatda
        ular uchinchi nusxa edi va har bir jadval ekranda 150 px joy
        egallab, ro'yxatni varaqlab bo'lmaydigan qilib qo'yardi
        (Verifix'da ham ro'yxat faqat nomdan iborat).
      */}
      <p className="tabular mt-3 text-caption1 text-[var(--fg-tertiary)]">
        {assignmentCount === 0
          ? t('attendance.scheduleNotAssigned')
          : t('attendance.assignmentCount', { n: num(assignmentCount) })}
        {schedule.note !== null && ` · ${schedule.note}`}
      </p>
    </Card>
  )
}

/* ═══════════════════════════════════════════════════════════ biriktirmalar */

function AssignmentsCard({
  query,
  items,
  canManage,
  canAssign,
  onAssign,
  onRemove,
}: {
  query: ReturnType<typeof useScheduleAssignments>
  items: readonly ScheduleAssignment[]
  canManage: boolean
  canAssign: boolean
  onAssign: () => void
  onRemove: (item: ScheduleAssignment) => void
}) {
  const { t, num } = useLocale()
  const [view, setView] = useState<AssignmentView>('all')

  const today = todayIso()
  const visible = view === 'inForce' ? items.filter((item) => assignmentInForce(item, today)) : items

  /*
    Guruhlar USTUNLIK tartibida (§5.2): ro'yxatning o'zi qoidani takrorlaydi,
    ya'ni «nega bu odamda shu jadval?» degan savolga qarashning o'ziyoq
    javob beradi. Guruh ichida — nishon nomi, keyin eng yangi sana.
  */
  const groups = SCOPE_ORDER.map((scope) => ({
    scope,
    items: visible
      .filter((item) => item.scope === scope)
      .sort(
        (left, right) =>
          targetName(left, t).localeCompare(targetName(right, t)) ||
          right.effectiveFrom.localeCompare(left.effectiveFrom),
      ),
  })).filter((group) => group.items.length > 0)

  const viewOptions: Array<SegmentOption<AssignmentView>> = [
    { value: 'all', label: t('attendance.assignmentsAll') },
    { value: 'inForce', label: t('attendance.assignmentsInForce') },
  ]

  return (
    <Card className="overflow-hidden">
      <CardHeader
        title={t('attendance.assignmentsTitle')}
        subtitle={t('attendance.scopePrecedenceHint')}
        action={
          canAssign ? (
            <Button type="button" variant="soft" size="sm" onClick={onAssign}>
              <CalendarPlus />
              {t('attendance.assignmentCreate')}
            </Button>
          ) : undefined
        }
      />

      {query.isPending ? (
        <div className="space-y-3 px-5 pb-5" aria-busy>
          {Array.from({ length: 3 }, (_, index) => (
            <div key={`skeleton-${index}`} className="flex items-center gap-3">
              <Skeleton className="size-10 rounded-full" />
              <Skeleton className="h-4 flex-1" />
              <Skeleton className="h-5 w-20 rounded-full" />
            </div>
          ))}
        </div>
      ) : query.isError ? (
        <AttendanceLoadError
          error={query.error}
          onRetry={() => void query.refetch()}
          retrying={query.isFetching}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={CalendarPlus}
          title={t('attendance.assignmentsEmptyTitle')}
          description={t('attendance.assignmentsEmptyText')}
          action={
            canAssign ? (
              <Button type="button" variant="create" onClick={onAssign}>
                <CalendarPlus />
                {t('attendance.assignmentCreate')}
              </Button>
            ) : undefined
          }
        />
      ) : (
        <>
          <div className="border-y border-[var(--border)] px-4 py-3 sm:px-5">
            <SegmentedControl
              options={viewOptions}
              value={view}
              onChange={setView}
              size="sm"
              aria-label={t('common.filters')}
            />
          </div>

          {groups.length === 0 ? (
            <p className="px-5 py-10 text-center text-subhead text-[var(--fg-secondary)]">
              {t('attendance.assignmentsNoneInForce')}
            </p>
          ) : (
            groups.map((group) => {
              const look = SCOPE_LOOK[group.scope]
              const Icon = look.icon
              return (
                <section key={group.scope}>
                  <div className="flex items-center gap-2 bg-[var(--bg-sunken)] px-4 py-2 sm:px-5">
                    <Icon className="size-4 text-[var(--fg-secondary)]" />
                    <h3 className="text-footnote font-semibold">{t(look.labelKey)}</h3>
                    <span className="tabular text-footnote text-[var(--fg-tertiary)]">
                      {num(group.items.length)}
                    </span>
                  </div>
                  <ul className="divide-y divide-[var(--border)]">
                    {group.items.map((item) => (
                      <AssignmentRow
                        key={item.id}
                        item={item}
                        today={today}
                        canManage={canManage}
                        onRemove={() => onRemove(item)}
                      />
                    ))}
                  </ul>
                </section>
              )
            })
          )}
        </>
      )}
    </Card>
  )
}

function AssignmentRow({
  item,
  today,
  canManage,
  onRemove,
}: {
  item: ScheduleAssignment
  today: string
  canManage: boolean
  onRemove: () => void
}) {
  const { t, date } = useLocale()
  const state = assignmentState(item, today)
  const stateLook = ASSIGNMENT_STATE_LOOK[state]

  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3 sm:flex-nowrap sm:px-5">
      <AssignmentTarget item={item} />

      <div className="min-w-0 sm:w-64">
        <p className="truncate-1 text-subhead font-medium">{item.workScheduleName ?? '—'}</p>
        <p className="tabular text-caption1 text-[var(--fg-secondary)]">
          {date(item.effectiveFrom)}
          {' — '}
          {item.effectiveTo === null ? t('attendance.openEnded') : date(item.effectiveTo)}
        </p>
      </div>

      <Badge tone={stateLook.tone} size="sm" className="shrink-0">
        {t(stateLook.labelKey)}
      </Badge>

      {canManage && (
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          className="ml-auto sm:ml-0"
          title={t('common.delete')}
          aria-label={t('common.delete')}
          onClick={onRemove}
        >
          <Trash2 className="text-[var(--danger-text)]" />
        </Button>
      )}
    </li>
  )
}

/** Biriktirma kimga: xodim — YUZI bilan, qolganlari — daraja ikonkasi bilan. */
function AssignmentTarget({ item }: { item: ScheduleAssignment }) {
  const { t } = useLocale()
  const look = SCOPE_LOOK[item.scope]

  return (
    <div className="flex min-w-0 flex-1 basis-full items-center gap-3 sm:basis-auto">
      {item.scope === 'Employee' && item.employeeId !== null ? (
        <EmployeePhoto
          employeeId={item.employeeId}
          // Xodim pog'onasida server rasm kalitini beradi; `null` — rasm yo'q, bosh harflar
          photoKey={item.photoKey}
          name={item.employeeName ?? ''}
          size="md"
        />
      ) : (
        <IconTile icon={look.icon} tone="gray" />
      )}
      <p className="truncate-1 text-subhead font-medium">{targetName(item, t)}</p>
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ kim uchun amal qiladi */

/**
 * «Bu xodimga shu sanada qaysi jadval tushadi?» — `GET /schedules/resolve`.
 *
 * ⭐ Javob faqat jadval nomi emas, YUTGAN DARAJA: to'rt pog'onali narvon
 * chiziladi va yutgan pog'ona belgilanadi. Bo'lim yutganda, jadval OTA
 * bo'limdan kelgan bo'lsa, bu alohida aytiladi — «nega sexdagi odamda
 * zavodning jadvali?» degan savolning butun javobi shu (§6.4).
 *
 * Natija FAQAT ikki `null` dan o'qiladi (`scheduleOutcome`).
 */
function ResolvePreviewCard() {
  const { t, date } = useLocale()

  const [employee, setEmployee] = useState<PickedEmployee | null>(null)
  const [onDate, setOnDate] = useState(todayIso)

  const resolution = useScheduleResolution(employee?.id ?? '', onDate, employee !== null)

  return (
    <Card>
      <CardHeader
        title={t('attendance.resolvePreview')}
        subtitle={t('attendance.resolvePreviewSubtitle')}
      />
      <div className="grid gap-5 px-5 pb-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <div className="space-y-4">
          <AttendanceEmployeePicker value={employee} onChange={setEmployee} />
          <TextField
            label={t('attendance.resolveDate')}
            type="date"
            value={onDate}
            onChange={(event) => setOnDate(event.target.value)}
          />
        </div>

        <div className="min-w-0">
          {employee === null || onDate === '' ? (
            <div className="flex h-full min-h-40 flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-[var(--border)] px-6 py-8 text-center">
              <UserRoundSearch className="size-7 text-[var(--fg-tertiary)]" strokeWidth={1.6} />
              <p className="text-subhead text-[var(--fg-secondary)]">
                {t('attendance.resolvePickEmployee')}
              </p>
            </div>
          ) : resolution.isPending ? (
            <div className="space-y-3 rounded-xl bg-[var(--bg-sunken)] p-4" aria-busy>
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-4 w-56" />
              <Skeleton className="h-24 w-full" />
            </div>
          ) : resolution.isError ? (
            <div className="rounded-xl bg-[var(--bg-sunken)]">
              <AttendanceLoadError
                error={resolution.error}
                onRetry={() => void resolution.refetch()}
                retrying={resolution.isFetching}
              />
            </div>
          ) : (
            <ResolutionView resolution={resolution.data} dateLabel={date(onDate)} />
          )}
        </div>
      </div>
    </Card>
  )
}

function ResolutionView({
  resolution,
  dateLabel,
}: {
  resolution: ScheduleResolution
  dateLabel: string
}) {
  const { t, date } = useLocale()
  const outcome = scheduleOutcome(resolution)

  if (outcome === 'notEmployed') {
    return (
      <Alert tone="info" title={t('attendance.resolveNotEmployedTitle')}>
        {t('attendance.resolveNotEmployed', { date: dateLabel })}
      </Alert>
    )
  }

  const context = [resolution.legalEntityName, resolution.departmentName, resolution.positionName]
    .filter(Boolean)
    .join(' · ')

  return (
    <div className="space-y-4">
      {outcome === 'noSchedule' ? (
        <Alert tone="warning" title={t('attendance.resolveNoScheduleTitle')}>
          {t('attendance.resolveNoSchedule', { date: dateLabel })}
        </Alert>
      ) : (
        <div className="rounded-xl bg-[var(--bg-sunken)] px-4 py-3">
          <p className="text-caption1 text-[var(--fg-tertiary)]">
            {t('attendance.resolvedSchedule', { date: dateLabel })}
          </p>
          <p className="truncate-1 text-title3 font-semibold">{resolution.workScheduleName ?? '—'}</p>
          {resolution.effectiveFrom !== null && (
            <p className="tabular text-footnote text-[var(--fg-secondary)]">
              {date(resolution.effectiveFrom)}
              {' — '}
              {resolution.effectiveTo === null
                ? t('attendance.openEnded')
                : date(resolution.effectiveTo)}
            </p>
          )}
        </div>
      )}

      <PrecedenceLadder resolution={resolution} />

      {inheritedFromParent(resolution) && (
        <Alert tone="info" title={t('attendance.resolveInheritedTitle')}>
          {t('attendance.resolveInherited', {
            matched: resolution.matchedDepartmentName ?? '—',
            own: resolution.departmentName ?? '—',
          })}
        </Alert>
      )}

      {context !== '' && (
        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('attendance.resolveEmployment', { context })}
        </p>
      )}

      {outcome === 'resolved' && <ResolvedDay resolution={resolution} />}
    </div>
  )
}

/**
 * To'rt pog'ona — ustunlik tartibida. Yutgani to'ldirilgan, undan
 * yuqoridagilari «mos kelmadi», pastdagilari «ko'rilmadi» (birinchi
 * mos kelgan pog'ona yutadi, §5.2 — pastkilari umuman tekshirilmaydi).
 */
function PrecedenceLadder({ resolution }: { resolution: ScheduleResolution }) {
  const { t } = useLocale()
  const winner = resolution.scope
  const winnerIndex = winner === null ? SCOPE_ORDER.length : SCOPE_ORDER.indexOf(winner)

  return (
    <ol className="space-y-1.5" aria-label={t('attendance.resolvedBy', { scope: winner === null ? '—' : t(SCOPE_LOOK[winner].labelKey) })}>
      {SCOPE_ORDER.map((scope, index) => {
        const look = SCOPE_LOOK[scope]
        const Icon = look.icon
        const won = scope === winner
        const detail = won ? ladderDetail(scope, resolution, t) : null

        return (
          <li
            key={scope}
            className={cn(
              'flex items-center gap-3 rounded-lg px-3 py-2',
              won
                ? 'bg-[var(--accent-soft)] text-[var(--accent-on-soft)]'
                : 'text-[var(--fg-tertiary)]',
            )}
          >
            <Icon className="size-4 shrink-0" />
            <span className={cn('text-footnote', won && 'font-semibold')}>{t(look.labelKey)}</span>
            {detail !== null && <span className="truncate-1 min-w-0 text-footnote">· {detail}</span>}
            <span className="ml-auto shrink-0 text-caption1">
              {won ? (
                <CircleCheck className="size-4" aria-label={t('attendance.tierWon')} />
              ) : index < winnerIndex ? (
                t('attendance.tierNoMatch')
              ) : (
                t('attendance.tierNotChecked')
              )}
            </span>
          </li>
        )
      })}
    </ol>
  )
}

/** Kun rejasining manbai — `GET /schedules/resolve` ning `daySource` i. */
const DAY_SOURCE_LABEL_KEY = {
  Date: 'attendance.resolveDayFromDate',
  Pattern: 'attendance.resolveDayFromPattern',
} as const

/**
 * Shu SANANING rejasi — tanaffus oynasi bilan (§6.4 `day`).
 *
 * Server kunni tabel hisoblagan manbadan beradi: yillik to'rda shu sanaga katak
 * yozilgan bo'lsa — o'sha katak, aks holda hafta kunining naqshi (`daySource`).
 * Manba ko'rsatiladi: «nega bugun dam olish?» degan savolning javobi aynan shu.
 */
function ResolvedDay({ resolution }: { resolution: ScheduleResolution }) {
  const { t } = useLocale()
  const day = resolution.day
  const weekday = isoWeekday(resolution.date)

  if (day === null) return null

  return (
    <div className="flex items-start gap-3 rounded-xl border border-[var(--border)] px-4 py-3">
      <CalendarDays className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" />
      <div className="min-w-0 space-y-0.5">
        <p className="flex flex-wrap items-center gap-1.5 text-footnote font-semibold">
          {weekday === null ? '—' : t(WEEKDAY_LABEL_KEY[weekday])}
          {resolution.daySource !== null && (
            <Badge tone={resolution.daySource === 'Date' ? 'info' : 'neutral'} size="sm">
              {t(DAY_SOURCE_LABEL_KEY[resolution.daySource])}
            </Badge>
          )}
        </p>
        {day.isWorking ? (
          <>
            <p className="tabular flex flex-wrap items-center gap-1.5 text-subhead">
              {day.startTime ?? '—'}–{day.endTime ?? '—'}
              {crossesMidnight(day) && (
                <Badge tone="purple" size="sm">
                  <MoonStar className="size-3" />
                  {t('attendance.nextDay')}
                </Badge>
              )}
            </p>
            <p className="tabular text-footnote text-[var(--fg-secondary)]">
              {t('attendance.dayPlanned', { hours: formatDuration(plannedDayMinutes(day)) })}
              {' · '}
              {breakWindowLabel(day) !== null
                ? t('attendance.breakWindow', { range: breakWindowLabel(day) })
                : day.breakMinutes > 0
                  ? t('attendance.breakWindowNoneHint')
                  : t('attendance.noBreak')}
            </p>
          </>
        ) : (
          <p className="text-subhead text-[var(--fg-secondary)]">{t('attendance.dayOff')}</p>
        )}
      </div>
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ o'chirish oynalari */

/**
 * Jadvalni o'chirish.
 *
 * ⚠️ Tirik biriktirmasi bor jadval o'chirilmaydi va FAOLSIZLANTIRILMAYDI
 * ham (`davomat.jadval_biriktirilgan`, 409 — koordinator qarori): hisob
 * mexanizmi `isActive` ga qaramaydi, ya'ni ruxsat berilsa ekran «nofaol»
 * deb, tabel esa shu jadval bo'yicha hisoblab turardi.
 *
 * Oyna buni OLDINDAN aytadi (biriktirmalar soni ma'lum bo'lsa, tugma
 * o'chiq), server rad etsa ham xuddi shu tushuntirishni ko'rsatadi —
 * haqiqat manbasi baribir server.
 */
function DeleteScheduleDialog({
  schedule,
  assignmentCount,
  onClose,
}: {
  schedule: WorkSchedule | null
  /** `null` — biriktirmalarni o'qib bo'lmadi, oldindan aytib bo'lmaydi. */
  assignmentCount: number | null
  onClose: () => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteSchedule()

  // Oyna har ochilganda oldingi urinishning xatosi tozalanadi (`incident-delete-dialog.tsx` naqshi)
  useEffect(() => {
    if (schedule !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [schedule])

  if (schedule === null) return null

  const code = remove.isError ? problemCode(remove.error) : null
  const refusedAssigned = code === 'davomat.jadval_biriktirilgan'
  const blocked = assignmentCount !== null && assignmentCount > 0

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('attendance.scheduleDeleteTitle')}
      description={schedule.name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={remove.isPending || blocked || refusedAssigned}
            onClick={() => remove.mutate(schedule.id, { onSuccess: onClose })}
          >
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {blocked || refusedAssigned ? (
          <Alert tone="warning" title={t('attendance.scheduleAssignedTitle')}>
            {assignmentCount !== null && assignmentCount > 0
              ? t('attendance.scheduleAssignedText', { n: num(assignmentCount) })
              : errorMessage('davomat.jadval_biriktirilgan')}
          </Alert>
        ) : (
          <p className="text-subhead text-[var(--fg-secondary)]">
            {t('attendance.scheduleDeleteConfirm')}
          </p>
        )}

        {code !== null && !refusedAssigned && <Alert tone="danger">{errorMessage(code)}</Alert>}
      </div>
    </Dialog>
  )
}

/**
 * Jadvalni faolsizlantirish.
 *
 * ⚠️ O'chirish bilan AYNAN bir xil qoida: biriktirmasi bor (muddati
 * o'tganlari ham) jadval faolsizlantirilmaydi (`davomat.jadval_biriktirilgan`,
 * 409). Sabab koordinator qarorida: hisob mexanizmi `isActive` ga qaramaydi,
 * ya'ni ruxsat berilsa ekran «faol emas» deb, tabel esa shu jadval bo'yicha
 * hisoblab turardi.
 *
 * Oyna buni OLDINDAN aytadi (biriktirmalar soni ma'lum bo'lsa tugma o'chiq),
 * server rad etsa ham xuddi shu tushuntirishni ko'rsatadi — haqiqat manbasi
 * baribir server.
 */
function DeactivateScheduleDialog({
  schedule,
  assignmentCount,
  onClose,
}: {
  schedule: WorkSchedule | null
  /** `null` — biriktirmalarni o'qib bo'lmadi, oldindan aytib bo'lmaydi. */
  assignmentCount: number | null
  onClose: () => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const setActive = useSetScheduleActive()

  // Oyna har ochilganda oldingi urinishning xatosi tozalanadi
  useEffect(() => {
    if (schedule !== null) setActive.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [schedule])

  if (schedule === null) return null

  const code = setActive.isError ? problemCode(setActive.error) : null
  const refusedAssigned = code === 'davomat.jadval_biriktirilgan'
  const blocked = assignmentCount !== null && assignmentCount > 0

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!setActive.isPending}
      title={t('attendance.scheduleDeactivateTitle')}
      description={schedule.name}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={setActive.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={setActive.isPending || blocked || refusedAssigned}
            onClick={() =>
              setActive.mutate({ id: schedule.id, active: false }, { onSuccess: onClose })
            }
          >
            {setActive.isPending && <LoaderCircle className="animate-spin" />}
            {t('catalog.deactivate')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {blocked || refusedAssigned ? (
          <Alert tone="warning" title={t('attendance.scheduleAssignedTitle')}>
            {assignmentCount !== null && assignmentCount > 0
              ? t('attendance.scheduleAssignedText', { n: num(assignmentCount) })
              : errorMessage('davomat.jadval_biriktirilgan')}
          </Alert>
        ) : (
          <p className="text-subhead text-[var(--fg-secondary)]">
            {t('attendance.scheduleDeactivateConfirm')}
          </p>
        )}

        {code !== null && !refusedAssigned && <Alert tone="danger">{errorMessage(code)}</Alert>}
      </div>
    </Dialog>
  )
}

function RemoveAssignmentDialog({
  item,
  onClose,
}: {
  item: ScheduleAssignment | null
  onClose: () => void
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteAssignment()

  useEffect(() => {
    if (item !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('attendance.assignmentDeleteTitle')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={remove.isPending}
            onClick={() => remove.mutate(item.id, { onSuccess: onClose })}
          >
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="rounded-xl bg-[var(--bg-sunken)] px-4 py-3">
          <AssignmentTarget item={item} />
          <p className="tabular mt-2 text-footnote text-[var(--fg-secondary)]">
            {item.workScheduleName ?? '—'} · {date(item.effectiveFrom)}
            {' — '}
            {item.effectiveTo === null ? t('attendance.openEnded') : date(item.effectiveTo)}
          </p>
        </div>

        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('attendance.assignmentDeleteConfirm')}
        </p>

        {remove.isError && <Alert tone="danger">{errorMessage(problemCode(remove.error))}</Alert>}
      </div>
    </Dialog>
  )
}

/* ═══════════════════════════════════════════════════════════ yordamchilar */

function targetName(item: ScheduleAssignment, t: (key: string) => string): string {
  switch (item.scope) {
    case 'Employee':
      return item.employeeName ?? '—'
    case 'Position':
      return item.positionName ?? '—'
    case 'Department':
      return item.departmentName ?? '—'
    case 'Default':
      return t('attendance.scopeDefaultTarget')
  }
}

function assignmentState(item: ScheduleAssignment, today: string): AssignmentState {
  if (item.effectiveFrom > today) return 'future'
  return assignmentInForce(item, today) ? 'inForce' : 'ended'
}

/** Yutgan pog'onaning «kim/nima» izohi. */
function ladderDetail(
  scope: ScheduleScope,
  resolution: ScheduleResolution,
  t: (key: string) => string,
): string | null {
  switch (scope) {
    case 'Employee':
      return resolution.employeeName
    case 'Position':
      return resolution.positionName
    case 'Department':
      return resolution.matchedDepartmentName ?? resolution.departmentName
    case 'Default':
      return t('attendance.scopeDefaultTarget')
  }
}
