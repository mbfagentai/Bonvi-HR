import { CalendarDays, LoaderCircle, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  Card,
  CardContent,
  CardHeader,
  DateField,
  Dialog,
  EmptyState,
  Select,
  TextField,
} from '@/shared/ui'

import {
  PROCESS_HOLIDAY_KINDS,
  PROCESS_SETTINGS_API_READY,
  type ProcessHoliday,
  type ProcessHolidayKind,
} from '../api/process-admin-api'
import {
  useDeleteProcessHoliday,
  useProcessHolidays,
  useSaveProcessHoliday,
} from '../api/process-admin-queries'
import { sortHolidays } from '../lib/process-settings'
import { ProcessSectionError } from './process-section-error'
import { ProcessListSkeleton } from './process-states'

/** `ProcessHoliday.MaxTitleLength`. */
const MAX_TITLE_LENGTH = 200

/** Adds a date, or replaces the row a date already has: the server keys the calendar by date. */
function HolidayDialog({ onClose }: { onClose: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveProcessHoliday()

  const [date, setDate] = useState('')
  const [kind, setKind] = useState<ProcessHolidayKind>('Holiday')
  const [title, setTitle] = useState('')
  const canSubmit = date !== '' && title.trim() !== ''

  return (
    <Dialog
      open
      size="sm"
      onClose={onClose}
      dismissible={!save.isPending}
      title={t('processes.settings.holidays.addTitle')}
      description={t('processes.settings.holidays.addHint')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={save.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="primary"
            disabled={save.isPending || !canSubmit}
            onClick={() => save.mutate({ date, kind, title: title.trim() }, { onSuccess: onClose })}
          >
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}
        <DateField
          label={t('processes.settings.holidays.date')}
          value={date}
          onChange={setDate}
          disabled={save.isPending}
        />
        <Select
          label={t('processes.settings.holidays.kind')}
          options={PROCESS_HOLIDAY_KINDS.map((value) => ({
            value,
            label: t(`processes.settings.holidays.kinds.${value}`),
          }))}
          value={kind}
          disabled={save.isPending}
          onChange={(event) => setKind(event.target.value === 'WorkingDay' ? 'WorkingDay' : 'Holiday')}
        />
        <TextField
          label={t('processes.settings.holidays.name')}
          value={title}
          maxLength={MAX_TITLE_LENGTH}
          disabled={save.isPending}
          onChange={(event) => setTitle(event.target.value)}
        />
      </div>
    </Dialog>
  )
}

/** Removing a row can move due dates of open steps, so it is asked once more. */
function DeleteHolidayDialog({ holiday, onClose }: { holiday: ProcessHoliday; onClose: () => void }) {
  const { t, date } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteProcessHoliday()

  return (
    <Dialog
      open
      size="sm"
      onClose={onClose}
      dismissible={!remove.isPending}
      title={t('processes.settings.holidays.deleteTitle')}
      description={t('processes.settings.holidays.deleteText', {
        date: date(holiday.date),
        title: holiday.title,
      })}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="danger"
            disabled={remove.isPending}
            onClick={() => remove.mutate(holiday.date, { onSuccess: onClose })}
          >
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      {remove.isError ? (
        <Alert tone="danger">{errorMessage(problemCode(remove.error))}</Alert>
      ) : (
        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('processes.settings.holidays.deleteHint')}
        </p>
      )}
    </Dialog>
  )
}

function HolidayRows() {
  const { t, date } = useLocale()
  const holidays = useProcessHolidays()
  const [deleting, setDeleting] = useState<ProcessHoliday | null>(null)

  if (holidays.isPending) return <ProcessListSkeleton rows={3} />
  if (holidays.isError) {
    return <ProcessSectionError error={holidays.error} onRetry={() => void holidays.refetch()} />
  }
  if (holidays.data.length === 0) {
    return (
      <EmptyState
        icon={CalendarDays}
        title={t('processes.settings.holidays.emptyTitle')}
        description={t('processes.settings.holidays.emptyText')}
      />
    )
  }

  return (
    <>
      <ul className="divide-y divide-[var(--border)]">
        {sortHolidays(holidays.data).map((holiday) => (
          <li key={holiday.date} className="flex items-center justify-between gap-3 py-2.5">
            <div className="min-w-0">
              <p className="truncate text-subhead">{holiday.title}</p>
              <p className="text-footnote text-[var(--fg-secondary)]">{date(holiday.date)}</p>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <Badge tone={holiday.kind === 'Holiday' ? 'info' : 'neutral'}>
                {t(`processes.settings.holidays.kinds.${holiday.kind}`)}
              </Badge>
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label={t('common.delete')}
                onClick={() => setDeleting(holiday)}
              >
                <Trash2 />
              </Button>
            </div>
          </li>
        ))}
      </ul>
      {deleting !== null && (
        <DeleteHolidayDialog holiday={deleting} onClose={() => setDeleting(null)} />
      )}
    </>
  )
}

/** The working-day calendar's exceptions (SPEC §6.10, §10 #17). Hayit days and government transfers are entered here. */
export function ProcessHolidaysCard() {
  const { t } = useLocale()
  const [adding, setAdding] = useState(false)

  return (
    <Card>
      <CardHeader
        title={t('processes.settings.holidays.title')}
        subtitle={t('processes.settings.holidays.hint')}
        action={
          <Button size="sm" disabled={!PROCESS_SETTINGS_API_READY} onClick={() => setAdding(true)}>
            <Plus />
            {t('common.add')}
          </Button>
        }
      />
      <CardContent>
        {PROCESS_SETTINGS_API_READY ? (
          <HolidayRows />
        ) : (
          <Alert tone="info">{t('processes.settings.notYet')}</Alert>
        )}
      </CardContent>
      {adding && <HolidayDialog onClose={() => setAdding(false)} />}
    </Card>
  )
}
