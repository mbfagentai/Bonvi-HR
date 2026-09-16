import { Building2, CalendarDays, Network, type LucideIcon } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Card, Skeleton } from '@/shared/ui'
import type { EmployeeListItem } from '../api/employees-api'
import { DirectionBadge, FlagBadge, NeedsReviewMark, StatusBadge } from './employee-badges'
import { EmployeePhoto } from './employee-photo'

/**
 * Kartochka ko'rinishi.
 *
 * HR aynan shuni ko'rsatgan edi: «расмли қилиб қилинса, битта босганда
 * ичига кириб кетиб…». Shu sababli rasm eng katta element va butun karta
 * bitta tugma — chetiga aniq tegish shart emas.
 *
 * Jadval ko'rinishidan farqi maqsadida: jadval — solishtirish va saralash
 * uchun, kartochka — odamni YUZIDAN tanish uchun. Shu sababli bu yerda
 * ustunlar emas, kam sonli eng muhim ma'lumot qoladi.
 */
export function EmployeesGrid({
  employees,
  loading,
  skeletonCount = 12,
  onSelect,
}: {
  employees: EmployeeListItem[]
  loading: boolean
  skeletonCount?: number
  onSelect: (employee: EmployeeListItem) => void
}) {
  if (loading) {
    return (
      <div className={GRID}>
        {Array.from({ length: skeletonCount }, (_, index) => (
          <Card key={`skeleton-${index}`} className="flex flex-col items-center p-5">
            <Skeleton className="size-20 rounded-full" />
            <Skeleton className="mt-4 h-4 w-32" />
            <Skeleton className="mt-2 h-3 w-24" />
            <Skeleton className="mt-4 h-5 w-20 rounded-full" />
          </Card>
        ))}
      </div>
    )
  }

  return (
    <div className={GRID}>
      {employees.map((employee) => (
        <EmployeeTile key={employee.id} employee={employee} onSelect={onSelect} />
      ))}
    </div>
  )
}

const GRID =
  'grid gap-4 px-4 py-4 sm:grid-cols-2 sm:px-5 lg:grid-cols-3 2xl:grid-cols-4'

function EmployeeTile({
  employee,
  onSelect,
}: {
  employee: EmployeeListItem
  onSelect: (employee: EmployeeListItem) => void
}) {
  const { t, date } = useLocale()

  return (
    <Card
      // `Card` — `div`, shu sababli tugma vazifasi qo'lda beriladi:
      // ichida yorliqlar bor va ularni `<button>` ichiga solish
      // HTML qoidasini buzmaydi, lekin ekran o'quvchisi uchun chalkash
      role="button"
      tabIndex={0}
      aria-label={employee.fullName}
      onClick={() => onSelect(employee)}
      onKeyDown={(event) => {
        if (event.key !== 'Enter' && event.key !== ' ') return
        event.preventDefault()
        onSelect(employee)
      }}
      className={cn(
        'press relative flex cursor-pointer flex-col items-center p-5 text-center',
        'transition-shadow hover:shadow-raised focus-visible:shadow-raised',
      )}
    >
      {employee.needsReview && (
        <NeedsReviewMark compact className="absolute top-3 right-3" />
      )}

      <EmployeePhoto
        employeeId={employee.id}
        photoKey={employee.photoKey}
        name={employee.fullName}
        size="xl"
      />

      <p className="truncate-2 mt-3.5 text-callout leading-snug font-semibold">
        {employee.fullName}
      </p>

      <p className="truncate-1 mt-1 text-footnote text-[var(--fg-secondary)]">
        {employee.positionName ?? t('employees.noPosition')}
      </p>

      {/*
        ⭐ Holat yorlig'i — FAQAT doimiy bo'lmaganda.

        Ro'yxatda endi sinovdagilar ham turadi (standart filtr
        `employed`, 2026-09-16): sariq yorliq HR ga «bu odam hali
        sinovda» deb aytadi. «Ishlayapti» esa CHIZILMAYDI — u
        kartochkalarning ko'pchiligida takrorlanib, hech nimani
        ajratmasdi va sarig'ini ko'mib yuborardi.
      */}
      {employee.status !== 'active' && (
        <div className="mt-3 flex justify-center">
          <StatusBadge status={employee.status} size="sm" />
        </div>
      )}

      {/*
        Belgilar — «Qora ro'yxat» yoki «Rezerv» aynan shu yerdan
        ko'rinishi kerak. Belgi yo'q bo'lsa qator ham yo'q: kartochka
        balandligi ortiqcha o'smaydi.
      */}
      {employee.activeFlags.length > 0 && (
        <div className="mt-3 flex flex-wrap items-center justify-center gap-1.5">
          {employee.activeFlags.map((flag) => (
            <FlagBadge key={flag} type={flag} size="sm" />
          ))}
        </div>
      )}

      {employee.directions.length > 0 && (
        <div className="mt-2 flex flex-wrap items-center justify-center gap-1">
          {employee.directions.slice(0, 3).map((direction) => (
            <DirectionBadge key={direction.id} name={direction.name} />
          ))}
          {employee.directions.length > 3 && (
            <span className="text-caption2 text-[var(--fg-tertiary)]">
              +{employee.directions.length - 3}
            </span>
          )}
        </div>
      )}

      <div className="mt-4 w-full space-y-1.5 border-t border-[var(--border)] pt-3.5 text-left">
        <TileRow icon={Network} value={employee.departmentName} />
        <TileRow icon={Building2} value={employee.legalEntityName} />
        <TileRow
          icon={CalendarDays}
          value={employee.hireDate === null ? null : date(employee.hireDate)}
        />
      </div>
    </Card>
  )
}

/** Bo'sh qiymat qatorni butunlay olib tashlaydi — kartochka baravar qoladi. */
function TileRow({
  icon: Icon,
  value,
}: {
  icon: LucideIcon
  value: string | null
}) {
  return (
    <div className="flex items-center gap-2">
      <Icon className="size-3.5 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
      <span
        className={cn(
          'truncate-1 text-caption1',
          value === null ? 'text-[var(--fg-tertiary)]' : 'text-[var(--fg-secondary)]',
        )}
      >
        {value ?? '—'}
      </span>
    </div>
  )
}
