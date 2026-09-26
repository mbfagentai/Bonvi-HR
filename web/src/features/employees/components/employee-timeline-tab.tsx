import { ArrowRight, FileClock, History, Import, Pencil } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Badge, Button, Card, CardContent, EmptyState, IconTile, Skeleton } from '@/shared/ui'
import type { MoneyValue, TimelineEvent } from '../api/employees-api'
import { EMPLOYMENT_SALARY, EVENT_LOOK, SALARY_VIEW } from '../lib/employee-presentation'
import { EmploymentEventEditDialog } from './employment-dialogs'

/**
 * ⭐ Mehnat tarixi — butun mehnat yo'li bitta vertikal lentada.
 *
 * Excel'da bitta o'zgarish sakkizta varaqqa qo'lda ko'chirilardi: umumiy
 * ro'yxat, yangi kelganlar, lavozimi o'zgarganlar, arxiv va boshqalar.
 * Bu yerda esa teskarisi: barcha hodisalar bitta oqimda va HR bir
 * qarashda «bu odam qachon keldi, nima bo'ldi, qachon ketdi» degan
 * savolga javob oladi.
 *
 * Tartib — YANGISIDAN ESKISIGA. HR ko'pincha «oxirgi nima bo'ldi?» deb
 * qaraydi, shu sababli oxirgi hodisa tepada. Yil ajratgichlari uzun
 * tarixda mo'ljal bo'lib xizmat qiladi.
 *
 * <b>⭐ Har bir yozuvni TUZATISH mumkin</b> (foydalanuvchi talabi,
 * 2026-09-16) — lekin faqat TERISH XATOSI ma'nosida: sana, buyruq
 * raqami, izoh va oklad summasi. Hodisaning o'zi bekor qilinmaydi va
 * turi o'zgarmaydi; haqiqiy o'zgarish yangi buyruq bilan yoziladi
 * (sababi `EmploymentEventEditDialog` va domen izohida).
 */
export function EmployeeTimelineTab({
  employeeId,
  events,
  loading,
  canCorrect,
}: {
  employeeId: string
  events: TimelineEvent[]
  loading: boolean
  /** «Yozuvni tuzatish» — `employment.correct`. */
  canCorrect: boolean
}) {
  const { t } = useLocale()

  /** Tuzatilayotgan yozuv — `null` bo'lsa oyna yopiq. */
  const [editing, setEditing] = useState<TimelineEvent | null>(null)
  const { hasPermission } = useAuth()

  /** Yil bo'yicha guruhlab, har birining ichida sana bo'yicha kamayish tartibida. */
  const groups = useMemo(() => {
    const sorted = [...events].sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate))

    const byYear = new Map<string, TimelineEvent[]>()
    for (const event of sorted) {
      // Sana «2024-01-15» ko'rinishida — birinchi to'rt belgi yil
      const year = event.effectiveDate.slice(0, 4) || '—'
      const bucket = byYear.get(year)
      if (bucket === undefined) {
        byYear.set(year, [event])
      } else {
        bucket.push(event)
      }
    }

    return [...byYear.entries()]
  }, [events])

  if (loading) {
    return (
      <Card>
        <CardContent className="space-y-5 pt-5">
          {[0, 1, 2, 3].map((index) => (
            <div key={index} className="flex gap-4">
              <Skeleton className="size-10 shrink-0 rounded-md" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-40" />
                <Skeleton className="h-3 w-64" />
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    )
  }

  if (events.length === 0) {
    return (
      <Card>
        <EmptyState
          icon={History}
          title={t('employees.timelineEmpty')}
          description={t('employees.timelineEmptyText')}
        />
      </Card>
    )
  }

  return (
    <Card>
      <CardContent className="pt-5">
        {groups.map(([year, yearEvents]) => (
          <section key={year}>
            {/* Yil — lentaning mo'ljali. Yopishib turadi, shu sababli uzun
                tarixda ham qaysi yilda ekaningiz doim ko'rinib turadi */}
            <h3 className="sticky top-0 z-10 -mx-1 mb-3 bg-[var(--bg-elevated)] px-1 py-1.5">
              <span className="tabular text-caption1 font-semibold tracking-wider text-[var(--fg-tertiary)]">
                {year}
              </span>
            </h3>

            <ol className="relative">
              {/* Vertikal chiziq — ikonalar markazidan o'tadi */}
              <span
                aria-hidden
                className="absolute top-2 bottom-2 left-5 w-px bg-[var(--border)]"
              />

              {yearEvents.map((event) => (
                <TimelineRow
                  key={event.id}
                  event={event}
                  canCorrect={canCorrect}
                  onEdit={() => setEditing(event)}
                />
              ))}
            </ol>
          </section>
        ))}
      </CardContent>

      <EmploymentEventEditDialog
        employeeId={employeeId}
        event={editing}
        canEditSalary={hasPermission(SALARY_VIEW) && hasPermission(EMPLOYMENT_SALARY)}
        onClose={() => setEditing(null)}
      />
    </Card>
  )
}

function TimelineRow({
  event,
  canCorrect,
  onEdit,
}: {
  event: TimelineEvent
  canCorrect: boolean
  onEdit: () => void
}) {
  const { t, date, dateTime } = useLocale()
  const { hasPermission } = useAuth()
  const canSeeSalary = hasPermission(SALARY_VIEW)

  const look = EVENT_LOOK[event.type]

  return (
    <li className="relative flex gap-4 pb-6 last:pb-1">
      {/* Ikona chiziq ustida turadi — atrofidagi halqa fon rangida */}
      <span className="relative z-10 rounded-md ring-4 ring-[var(--bg-elevated)]">
        <IconTile icon={look.icon} tone={look.tone} />
      </span>

      <div className="min-w-0 flex-1 pt-0.5">
        <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
          <span className="text-subhead font-semibold">
            {t(`employees.events.${look.key}`)}
          </span>

          {/*
            Ko'chirilgan hodisa ALOHIDA belgilanadi.
            Excel'dan kelgan yozuvda buyruq raqami yo'q yoki sana taxminiy
            bo'lishi mumkin — HR unga tizimda kiritilgan aniq yozuvdek
            ishonmasligi kerak.
          */}
          {event.isImported && (
            <Badge tone="neutral" size="sm" title={t('employees.importedHint')}>
              <Import className="size-3" strokeWidth={2.5} />
              {t('employees.imported')}
            </Badge>
          )}

          {event.legalEntityName !== null && (
            <span className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">
              {event.legalEntityName}
            </span>
          )}

          {/*
            ⚠️ Tugma «tahrirlash» deb emas, «tuzatish» deb o'qilishi
            kerak: u qarorni o'zgartirmaydi, noto'g'ri yozib olingan
            hujjatni to'g'rilaydi (oynaning o'zida ham shu yozilgan).
          */}
          {canCorrect && (
            <Button type="button" size="sm" variant="ghost" onClick={onEdit} className="-my-1">
              <Pencil />
              {t('employees.eventEditTitle')}
            </Button>
          )}
        </div>

        <p className="tabular mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-caption1 text-[var(--fg-secondary)]">
          <span>{date(event.effectiveDate)}</span>

          {event.orderNo !== null && (
            <span className="inline-flex items-center gap-1.5">
              <FileClock className="size-3.5" strokeWidth={2} />
              {t('employees.orderShort', { no: event.orderNo })}
              {event.orderDate !== null && ` · ${date(event.orderDate)}`}
            </span>
          )}
        </p>

        <div className="mt-2 space-y-1.5">
          <Change
            label={t('employee.position')}
            from={event.fromPositionName}
            to={event.toPositionName}
          />
          <Change
            label={t('employee.department')}
            from={event.fromDepartmentName}
            to={event.toDepartmentName}
          />

          {/* ⚠️ Oklad o'zgarishi ruxsatsiz ko'rinmaydi. Hodisaning O'ZI
              ko'rinadi (u mehnat yo'lining bir qismi), faqat summalar yashiriladi */}
          {canSeeSalary ? (
            <MoneyChange
              label={t('employee.salary')}
              from={event.fromSalary}
              to={event.toSalary}
            />
          ) : (
            (event.fromSalary !== null || event.toSalary !== null) && (
              <p className="text-caption1 text-[var(--fg-tertiary)]">
                {t('employees.salaryHiddenShort')}
              </p>
            )
          )}
        </div>

        {event.note !== null && (
          <p className="mt-2 rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote text-[var(--fg-secondary)]">
            {event.note}
          </p>
        )}

        {(event.recordedByName !== null || event.updatedAt !== null) && (
          <p className="mt-1.5 text-caption2 text-[var(--fg-tertiary)]">
            {event.recordedByName !== null &&
              t('employees.recordedBy', { name: event.recordedByName })}

            {/*
              Tuzatilgan yozuv ajralib turadi: HR ko'rgan qiymat
              birinchi kiritilganidan farq qilishi mumkinligini bilishi
              kerak (nomzod bosqichlaridagi bilan bir xil qoida).
            */}
            {event.updatedAt !== null && (
              <span className={cn(event.recordedByName !== null && 'ml-1')}>
                {event.recordedByName !== null && '· '}
                {t('employees.eventEdited')} · {dateTime(event.updatedAt)}
              </span>
            )}
          </p>
        )}
      </div>
    </li>
  )
}

/**
 * «Eski qiymat → yangi qiymat».
 *
 * Ikkalasi ham bo'lmasa yoki o'zgarmagan bo'lsa — qator umuman chizilmaydi:
 * lentada faqat HAQIQATAN o'zgargan narsa ko'rinishi kerak, aks holda
 * har bir hodisa bir xil ko'rinadi va farqni topib bo'lmaydi.
 */
function Change({
  label,
  from,
  to,
}: {
  label: string
  from: string | null
  to: string | null
}) {
  if (to === null || from === to) return null

  return (
    <p className="flex flex-wrap items-center gap-1.5 text-footnote">
      <span className="text-[var(--fg-tertiary)]">{label}:</span>
      {from !== null && (
        <>
          <span className="text-[var(--fg-secondary)] line-through decoration-[var(--fg-tertiary)]">
            {from}
          </span>
          <ArrowRight className="size-3.5 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2.5} />
        </>
      )}
      <span className="font-medium">{to}</span>
    </p>
  )
}

function MoneyChange({
  label,
  from,
  to,
}: {
  label: string
  from: MoneyValue | null
  to: MoneyValue | null
}) {
  const { money } = useLocale()

  if (to === null) return null

  const fromText = from === null ? null : money(from.amount, from.currency)
  const toText = money(to.amount, to.currency)

  return (
    <p
      className={cn(
        'flex flex-wrap items-center gap-1.5 text-footnote',
        // Oklad — raqam, shu sababli ustma-ust tushadigan shriftda
        'tabular',
      )}
    >
      <span className="text-[var(--fg-tertiary)]">{label}:</span>
      {fromText !== null && fromText !== toText && (
        <>
          <span className="text-[var(--fg-secondary)] line-through decoration-[var(--fg-tertiary)]">
            {fromText}
          </span>
          <ArrowRight className="size-3.5 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2.5} />
        </>
      )}
      <span className="font-medium">{toText}</span>
    </p>
  )
}
