import { LoaderCircle, Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  DateField,
  Dialog,
  SegmentedControl,
  Skeleton,
  TextArea,
  TextField,
} from '@/shared/ui'

import type { AttendanceDayPass, AttendancePass, TimesheetCell } from '../api/attendance-api'
import { useAddManualPass, useDayPasses, useRemoveManualPass } from '../api/attendance-queries'
import { DAY_STATUS_LOOK, formatDuration, formatPassTime } from '../lib/attendance-presentation'

/** Izoh chegarasi — serverdagi `AttendanceManualPass.NoteMaxLength`. */
const NOTE_MAX = 500

/** ISO sanaga kun qo'shadi («2026-10-06» + 1 → «2026-10-07»), vaqt mintaqasiga bog'lanmasdan. */
function addDays(iso: string, days: number): string {
  const [year, month, day] = iso.split('-').map(Number)
  const value = new Date(Date.UTC(year ?? 1970, (month ?? 1) - 1, day ?? 1) + days * 86_400_000)
  return value.toISOString().slice(0, 10)
}

/**
 * Kalendar katagining oynasi — Verifix'dagi «Статистика дня / Отметки» (SPEC-qolda-belgi §C).
 *
 * <b>Belgilar</b> — tabel SHU katakka qo'ygan hamma o'tish: tungi smenaning ertalabki chiqishi
 * ham shu yerda. Yo'nalish — hisob-kitobdagi (navbat bilan aniqlangani ham), «Hisobga olingan:
 * Yo'q» — tashlangan takror.
 *
 * <b>Tuzatish faqat QO'SHISH bilan</b> (egasi, 2026-11-03): terminal o'tishi tahrirlanmaydi va
 * o'chirilmaydi. Faqat HR qo'shgan belgini o'chirish mumkin — noto'g'ri yozilgani abadiy
 * qolmasin.
 */
export function AttendanceDayDialog({
  employeeId,
  cell,
  canManage,
  onOpenPass,
  onClose,
}: {
  employeeId: string
  /** `null` — oyna yopiq. */
  cell: TimesheetCell | null
  /** «Belgi qo'shish» va «O'chirish» — `attendance.requests.manage`. */
  canManage: boolean
  onOpenPass: (pass: AttendancePass) => void
  onClose: () => void
}) {
  const { t, date } = useLocale()
  const [tab, setTab] = useState<'marks' | 'stats'>('marks')
  const [adding, setAdding] = useState(false)

  const passes = useDayPasses(employeeId, cell?.date ?? null)

  // Har ochilishda boshidan: oldingi kunning ochiq formasi yangi kunga o'tib ketmasin
  useEffect(() => {
    setTab('marks')
    setAdding(false)
  }, [cell?.date])

  if (cell === null) {
    return null
  }

  const look = DAY_STATUS_LOOK[cell.status]
  const items = passes.data?.items ?? []

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      title={
        <span className="flex flex-wrap items-center gap-2">
          {date(cell.date)}
          <Badge tone={look.tone} size="sm">
            {t(look.labelKey)}
          </Badge>
        </span>
      }
      footer={
        <Button type="button" onClick={onClose}>
          {t('common.close')}
        </Button>
      }
    >
      <div className="space-y-4">
        <SegmentedControl
          value={tab}
          onChange={setTab}
          aria-label={t('attendance.dayDialog')}
          options={[
            { value: 'marks', label: t('attendance.dayMarks') },
            { value: 'stats', label: t('attendance.dayStats') },
          ]}
        />

        {tab === 'stats' ? (
          <DayStats cell={cell} />
        ) : (
          <>
            {/*
              «Hammasi / Faqat hisobga olinganlar» filtri YO'Q (egasi, 2026-11-03): kunda bir
              necha qator bo'ladi, hisobga olinmagani esa «Yo'q» ustuni va xira qator bilan
              allaqachon ajralib turadi.
            */}
            <div className="flex justify-end">
              {canManage && !adding && (
                <Button type="button" size="sm" variant="soft" onClick={() => setAdding(true)}>
                  <Plus />
                  {t('attendance.markAdd')}
                </Button>
              )}
            </div>

            {adding && (
              <ManualPassForm
                employeeId={employeeId}
                cellDate={cell.date}
                onDone={() => setAdding(false)}
              />
            )}

            {passes.isPending ? (
              <div className="space-y-2">
                <Skeleton className="h-10" />
                <Skeleton className="h-10" />
              </div>
            ) : passes.isError ? (
              <Alert tone="danger">{t('attendance.marksLoadFailed')}</Alert>
            ) : items.length === 0 ? (
              <p className="py-6 text-center text-subhead text-[var(--fg-secondary)]">
                {t('attendance.marksEmpty')}
              </p>
            ) : (
              <MarksTable
                items={items}
                cellDate={cell.date}
                canManage={canManage}
                onOpenPass={onOpenPass}
              />
            )}
          </>
        )}
      </div>
    </Dialog>
  )
}

/** «Kun statistikasi» — tabel katagining o'z sonlari, klientda qayta hisoblanmaydi. */
function DayStats({ cell }: { cell: TimesheetCell }) {
  const { t } = useLocale()

  const plan =
    cell.planStartAt === null ? '—' : `${formatPassTime(cell.planStartAt)} – ${formatPassTime(cell.planEndAt)}`
  const actual =
    cell.firstInAt === null && cell.lastOutAt === null
      ? '—'
      : `${formatPassTime(cell.firstInAt)} – ${formatPassTime(cell.lastOutAt)}`

  const rows: [string, string, string?][] = [
    [t('attendance.dayPlan'), plan],
    [t('attendance.dayActual'), actual],
    [t('attendance.worked'), formatDuration(cell.workedMinutes)],
    [t('attendance.lateMinutes'), formatDuration(cell.lateMinutes), cell.lateMinutes > 0 ? 'text-ios-red' : undefined],
    [
      t('attendance.earlyLeaveMinutes'),
      formatDuration(cell.earlyLeaveMinutes),
      cell.earlyLeaveMinutes > 0 ? 'text-ios-red' : undefined,
    ],
    [
      t('attendance.overtimeMinutes'),
      formatDuration(cell.overtimeMinutes ?? 0),
      (cell.overtimeMinutes ?? 0) > 0 ? 'text-ios-green' : undefined,
    ],
    [t('attendance.dayBreak'), formatDuration(cell.breakDeductionMinutes)],
  ]

  return (
    <dl className="divide-y divide-[var(--border)] rounded-md border border-[var(--border)]">
      {rows.map(([label, value, tone]) => (
        <div key={label} className="flex items-center justify-between gap-4 px-3 py-2.5 text-subhead">
          <dt className="text-[var(--fg-secondary)]">{label}</dt>
          <dd className={cn('tabular font-semibold', tone)}>{value}</dd>
        </div>
      ))}
    </dl>
  )
}

/** Belgilar jadvali — Verifix «Отметки» ustunlari, surat «Ko'rish» orqali. */
function MarksTable({
  items,
  cellDate,
  canManage,
  onOpenPass,
}: {
  items: AttendanceDayPass[]
  cellDate: string
  canManage: boolean
  onOpenPass: (pass: AttendancePass) => void
}) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useRemoveManualPass()
  // Ikki bosqichli o'chirish: brauzer `confirm()` o'rniga qatorning o'zida tasdiq
  const [confirming, setConfirming] = useState<string | null>(null)

  return (
    <div className="space-y-2">
      {remove.isError && (
        <Alert tone="danger">{errorMessage(problemCode(remove.error), t('attendance.markDeleteFailed'))}</Alert>
      )}

      <div className="overflow-x-auto rounded-md border border-[var(--border)]">
        <table className="w-full min-w-[560px] text-subhead">
          <thead className="bg-[var(--bg-sunken)] text-left text-caption1 text-[var(--fg-secondary)]">
            <tr>
              <th className="px-3 py-2 font-medium">{t('attendance.markTime')}</th>
              <th className="px-3 py-2 font-medium">{t('attendance.markType')}</th>
              <th className="px-3 py-2 font-medium">{t('attendance.markPlace')}</th>
              <th className="px-3 py-2 font-medium">{t('attendance.markCounted')}</th>
              <th className="px-3 py-2 text-right font-medium">{t('attendance.markActions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border)]">
            {items.map(({ pass, counted }) => {
              const isIn = pass.direction === 'In'
              const manual = pass.manual ?? null

              return (
                <tr key={pass.id} className={cn(!counted && 'opacity-60')}>
                  <td className="tabular px-3 py-2 align-top whitespace-nowrap">
                    {formatPassTime(pass.occurredAt)}
                    {/* Tungi smenaning ertalabki chiqishi — boshqa kalendar kunida */}
                    {pass.localDate !== cellDate && (
                      <span className="block text-caption2 text-[var(--fg-secondary)]">{date(pass.localDate)}</span>
                    )}
                  </td>
                  <td className="px-3 py-2 align-top whitespace-nowrap">
                    <span className="inline-flex items-center gap-1.5">
                      <span
                        aria-hidden
                        className={cn('size-2.5 rounded-full', isIn ? 'bg-ios-blue' : 'bg-ios-red')}
                      />
                      {t(isIn ? 'attendance.directionIn' : 'attendance.directionOut')}
                    </span>
                  </td>
                  <td className="px-3 py-2 align-top">
                    {manual !== null ? (
                      <>
                        <span className="font-medium">{t('attendance.markManual')}</span>
                        <span className="block text-caption1 text-[var(--fg-secondary)]">
                          {manual.createdByName ?? '—'} · {manual.note}
                        </span>
                      </>
                    ) : (
                      [pass.deviceName, pass.deviceLocation].filter(Boolean).join(' · ') || '—'
                    )}
                  </td>
                  <td className="px-3 py-2 align-top">
                    <Badge tone={counted ? 'info' : 'neutral'} size="sm">
                      {t(counted ? 'attendance.markCountedYes' : 'attendance.markCountedNo')}
                    </Badge>
                  </td>
                  <td className="px-3 py-2 text-right align-top whitespace-nowrap">
                    {manual !== null && canManage ? (
                      confirming === pass.id ? (
                        <span className="inline-flex gap-1">
                          <Button type="button" size="sm" onClick={() => setConfirming(null)}>
                            {t('common.cancel')}
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant="danger"
                            disabled={remove.isPending}
                            onClick={() =>
                              remove.mutate(pass.id, { onSuccess: () => setConfirming(null) })
                            }
                          >
                            {remove.isPending && <LoaderCircle className="animate-spin" />}
                            {t('common.delete')}
                          </Button>
                        </span>
                      ) : (
                        <Button
                          type="button"
                          size="sm"
                          variant="ghost"
                          title={t('attendance.markDelete')}
                          aria-label={t('attendance.markDelete')}
                          onClick={() => setConfirming(pass.id)}
                        >
                          <Trash2 />
                        </Button>
                      )
                    ) : (
                      <Button type="button" size="sm" variant="ghost" onClick={() => onOpenPass(pass)}>
                        {t('attendance.markView')}
                      </Button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}

/**
 * «Belgi qo'shish» formasi. Sana sukut bo'yicha katak sanasi; ertangi kun ham ruxsat —
 * tungi smenaning ertalabki chiqishi uchun. Izoh majburiy (server ham tekshiradi).
 */
function ManualPassForm({
  employeeId,
  cellDate,
  onDone,
}: {
  employeeId: string
  cellDate: string
  onDone: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const add = useAddManualPass()

  const [day, setDay] = useState(cellDate)
  const [time, setTime] = useState('')
  const [direction, setDirection] = useState<'In' | 'Out'>('In')
  const [note, setNote] = useState('')

  const noteTrimmed = note.trim()
  const valid = day !== '' && /^\d{2}:\d{2}$/.test(time) && noteTrimmed !== '' && noteTrimmed.length <= NOTE_MAX

  const submit = () => {
    if (!valid) return

    add.mutate(
      { employeeId, date: day, time, direction, note: noteTrimmed },
      { onSuccess: onDone },
    )
  }

  return (
    <div className="space-y-3 rounded-md border border-[var(--border)] bg-[var(--bg-sunken)] p-3">
      <p className="text-footnote text-[var(--fg-secondary)]">{t('attendance.markAddHint')}</p>

      {add.isError && (
        <Alert tone="danger">{errorMessage(problemCode(add.error), t('attendance.markAddFailed'))}</Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-3">
        <DateField
          label={t('attendance.markDate')}
          value={day}
          min={cellDate}
          max={addDays(cellDate, 1)}
          disabled={add.isPending}
          onChange={setDay}
        />
        <TextField
          label={t('attendance.markTime')}
          type="time"
          required
          value={time}
          disabled={add.isPending}
          onChange={(event) => setTime(event.target.value)}
        />
        <div className="space-y-1.5">
          <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
            {t('attendance.markType')}
          </span>
          <SegmentedControl
            value={direction}
            onChange={setDirection}
            aria-label={t('attendance.markType')}
            options={[
              { value: 'In', label: t('attendance.directionIn') },
              { value: 'Out', label: t('attendance.directionOut') },
            ]}
          />
        </div>
      </div>

      <TextArea
        label={t('attendance.markNote')}
        hint={t('attendance.markNoteHint')}
        value={note}
        maxLength={NOTE_MAX}
        disabled={add.isPending}
        onChange={(event) => setNote(event.target.value)}
      />

      <div className="flex justify-end gap-2">
        <Button type="button" size="sm" onClick={onDone} disabled={add.isPending}>
          {t('common.cancel')}
        </Button>
        <Button type="button" size="sm" variant="primary" disabled={!valid || add.isPending} onClick={submit}>
          {add.isPending && <LoaderCircle className="animate-spin" />}
          {t('common.save')}
        </Button>
      </div>
    </div>
  )
}
