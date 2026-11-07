import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Expand } from 'lucide-react'
import { useState } from 'react'

import { useEmployeePhoto } from '@/features/employees/api/employees-queries'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useAttendanceEmployeeHeader } from '@/features/verification/api/attendance-queries'
import { EmployeeAttendanceTab } from '@/features/verification/components/employee-attendance-tab'
import { currentMonth, type MonthValue } from '@/features/verification/lib/attendance-search'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useReturnTo } from '@/shared/lib/return-to'
import { Alert, Button, Card, FileViewer, Skeleton } from '@/shared/ui'

interface EmployeeAttendanceSearch {
  /** Calendar month, `year` and `month` always together (same rule as the employee card). */
  year?: number
  month?: number
}

function readNumber(value: unknown): number | undefined {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined
}

/**
 * One employee's attendance for a «Davomat mas'uli», who has no access to the employee card.
 *
 * The body is the card's own «Davomat» tab, so the two cannot drift apart. The header comes from
 * `/api/attendance/employees/{id}/header` and holds a name, department, position and photo: no
 * call on this page goes to `/api/employees`, which the server refuses this user anyway.
 */
export const Route = createFileRoute('/_app/verifikatsiya/xodim/$employeeId')({
  validateSearch: (search: Record<string, unknown>): EmployeeAttendanceSearch => ({
    year: readNumber(search['year']),
    month: readNumber(search['month']),
  }),
  component: EmployeeAttendancePage,
})

function EmployeeAttendancePage() {
  const errorMessage = useErrorMessage()
  const { employeeId } = Route.useParams()
  const search = Route.useSearch()
  const navigate = useNavigate({ from: Route.fullPath })
  const header = useAttendanceEmployeeHeader(employeeId)
  const back = useReturnTo(() => void navigate({ to: '/verifikatsiya/jonli' }))

  const month: MonthValue =
    search.year !== undefined && search.month !== undefined
      ? { year: search.year, month: search.month }
      : currentMonth()

  const setMonth = (value: MonthValue) => {
    void navigate({ search: { year: value.year, month: value.month }, replace: true })
  }

  const backButton = (
    <Button variant="ghost" size="sm" onClick={back.go} className="-ml-2">
      <ArrowLeft />
      {back.label}
    </Button>
  )

  if (header.isPending) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-72 w-full rounded-lg" />
      </div>
    )
  }

  if (header.isError) {
    return (
      <div className="space-y-4">
        {backButton}
        <Alert tone="danger">{errorMessage(problemCode(header.error))}</Alert>
      </div>
    )
  }

  const person = header.data
  const job = [person.positionName, person.departmentName].filter(Boolean).join(' · ')

  return (
    <div className="space-y-5">
      {backButton}

      <Card className="flex items-center gap-4 p-5">
        <HeaderPhoto
          employeeId={person.id}
          photoKey={person.hasPhoto ? 'attendance-header' : null}
          name={person.fullName}
        />
        <div className="min-w-0">
          <h1 className="text-title2 leading-tight font-semibold tracking-tight">{person.fullName}</h1>
          {job !== '' && <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">{job}</p>}
        </div>
      </Card>

      <EmployeeAttendanceTab employeeId={person.id} month={month} onMonth={setMonth} />
    </div>
  )
}

/**
 * The header photo, opened full screen on a click — the employee card's viewer without its upload
 * and delete: this user only checks that the face matches. `useEmployeePhoto` already reads the
 * attendance photo route for a scoped user, and the viewer shares the avatar's cache entry.
 */
function HeaderPhoto({
  employeeId,
  photoKey,
  name,
}: {
  employeeId: string
  photoKey: string | null
  name: string
}) {
  const { t } = useLocale()
  const [viewing, setViewing] = useState(false)
  const viewingPhoto = useEmployeePhoto(employeeId, photoKey, viewing)

  const avatar = <EmployeePhoto employeeId={employeeId} photoKey={photoKey} name={name} size="lg" />

  // No photo, nothing to enlarge: a plain avatar, no button and no hover hint
  if (photoKey === null) return avatar

  return (
    <>
      <button
        type="button"
        onClick={() => setViewing(true)}
        aria-label={t('photo.view')}
        title={t('photo.view')}
        className="press group relative block shrink-0 cursor-pointer rounded-full"
      >
        {avatar}
        <span
          aria-hidden
          className={cn(
            'absolute inset-0 grid place-items-center rounded-full bg-black/45',
            'opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)]',
            'group-hover:opacity-100 group-focus-visible:opacity-100',
          )}
        >
          <Expand className="size-5 text-white" strokeWidth={2} />
        </span>
      </button>

      <FileViewer
        open={viewing}
        onClose={() => setViewing(false)}
        title={name}
        subtitle={t('employees.photoTitle')}
        kind="image"
        blob={viewingPhoto.data ?? null}
        error={viewingPhoto.isError}
      />
    </>
  )
}
