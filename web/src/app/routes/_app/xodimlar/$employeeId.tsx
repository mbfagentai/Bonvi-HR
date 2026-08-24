import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { CloseFlagDialog } from '@/features/flags/components/close-flag-dialog'

import {
  FlagBadge,
  NeedsReviewMark,
  StatusBadge,
} from '@/features/employees/components/employee-badges'
import {
  EmployeeOverviewTab,
  type EmploymentAction,
} from '@/features/employees/components/employee-overview-tab'
import { EmployeeEditDialog } from '@/features/employees/components/employee-edit-dialog'
import { EmployeePhotoEditor } from '@/features/employees/components/employee-photo-editor'
import { EmployeeTimelineTab } from '@/features/employees/components/employee-timeline-tab'
import {
  AdditionalDutyDialog,
  SalaryDialog,
  TerminateDialog,
  TransferDialog,
} from '@/features/employees/components/employment-dialogs'
import { RaiseFlagDialog } from '@/features/employees/components/raise-flag-dialog'
import {
  EMPLOYEES_EDIT,
  EMPLOYMENT_MANAGE,
  LISTS_MANAGE,
} from '@/features/employees/lib/employee-presentation'
import {
  useEmployee,
  useEmployeeFlags,
  useEmployeeTimeline,
} from '@/features/employees/api/employees-queries'
import type { EmploymentSummary } from '@/features/employees/api/employees-api'
import { useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, CardContent, SegmentedControl, Skeleton } from '@/shared/ui'

export const Route = createFileRoute('/_app/xodimlar/$employeeId')({
  component: EmployeeCardPage,
})

type Tab = 'overview' | 'timeline' | 'flags'

/**
 * Xodim kartasi.
 *
 * Uch bo'lim: umumiy ma'lumot, mehnat tarixi va belgilar. Ular tab bilan
 * almashadi — bir vaqtda hammasini ko'rsatish sahifani cho'zib yuborardi,
 * HR esa odatda bittasiga qaraydi.
 *
 * Ma'lumot uchta alohida so'rov bilan olinadi: karta ochilishi bilan
 * asosiy qism ko'rinadi, tarix va belgilar esa keyin yuklanadi.
 */
function EmployeeCardPage() {
  const { employeeId } = Route.useParams()
  const { t } = useLocale()
  const navigate = useNavigate()

  const [tab, setTab] = useState<Tab>('overview')

  /*
   * Ochiq oyna bitta holatda saqlanadi: to'rt xil `useState<... | null>`
   * bo'lsa, ikkitasi bir vaqtda ochilib qolishi mumkin edi.
   */
  const [action, setAction] = useState<{
    kind: EmploymentAction
    employment: EmploymentSummary
  } | null>(null)

  const [editing, setEditing] = useState(false)
  const [raisingFlag, setRaisingFlag] = useState(false)
  const [closingFlag, setClosingFlag] = useState<{ id: string; employeeId: string; employeeName: string } | null>(null)

  const { hasPermission } = useAuth()
  const canEdit = hasPermission(EMPLOYEES_EDIT)
  const canManageEmployment = hasPermission(EMPLOYMENT_MANAGE)
  const canManageFlags = hasPermission(LISTS_MANAGE)

  const employee = useEmployee(employeeId)
  const timeline = useEmployeeTimeline(employeeId)
  const flags = useEmployeeFlags(employeeId)

  if (employee.isPending) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-72 w-full rounded-lg" />
      </div>
    )
  }

  if (employee.isError || !employee.data) {
    return (
      <div className="space-y-4">
        <BackButton onClick={() => void navigate({ to: '/xodimlar' })} label={t('common.back')} />
        <Alert tone="danger">{t('errors.xodim.topilmadi')}</Alert>
      </div>
    )
  }

  const person = employee.data

  // Joriy lavozim va bo'lim alohida maydon emas — ular ASOSIY mehnat
  // munosabatidan olinadi. Asosiysi yo'q bo'lsa (masalan xodim ishdan
  // ketgan), birinchisi ko'rsatiladi.
  const primary =
    person.employments.find((e) => e.isPrimary) ?? person.employments[0] ?? null

  return (
    <div className="space-y-5">
      <BackButton onClick={() => void navigate({ to: '/xodimlar' })} label={t('nav.employees')} />

      {/* ───────── Sarlavha kartasi ───────── */}
      <Card className="p-5">
        <div className="flex flex-wrap items-start gap-4">
          <EmployeePhotoEditor
            employeeId={person.id}
            photoKey={person.photoKey}
            name={person.fullName}
          />

          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-title2 leading-tight font-semibold tracking-tight">
                {person.fullName}
              </h1>
              {person.needsReview && <NeedsReviewMark note={person.reviewNote} />}
            </div>

            {primary?.position != null && (
              <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">
                {primary.position.name}
                {primary.department != null && ` · ${primary.department.name}`}
              </p>
            )}

            <div className="mt-3 flex flex-wrap items-center gap-2">
              {person.employments.map((employment) => (
                <StatusBadge key={employment.id} status={employment.status} />
              ))}
              {flags.data?.map((flag) => (
                <FlagBadge key={flag.id} type={flag.type} />
              ))}
            </div>
          </div>

          {/*
            Tahrirlash tugmasi aynan SARLAVHA kartasida turadi: xodim
            belgilangan bo'lsa, HR ogohlantirishni shu yerda ko'radi va
            tuzatishni ham shu yerdan boshlaydi — pastdagi tablarni
            aylanib chiqishi shart emas.
          */}
          {canEdit && (
            <Button
              type="button"
              variant="secondary"
              onClick={() => setEditing(true)}
              className="shrink-0"
            >
              <Pencil />
              {t('common.edit')}
            </Button>
          )}
        </div>
      </Card>

      {/*
        Ko'chirilgan yozuv ogohlantirishi — kartaning eng ko'rinadigan joyida.
        Bu Excel'dan kelgan va tekshirilmagan ma'lumot: HR unga tizimda
        kiritilgan yozuvdek ishonmasligi kerak.
      */}
      {person.needsReview && person.reviewNote !== null && (
        <Alert tone="warning">{person.reviewNote}</Alert>
      )}

      <SegmentedControl
        value={tab}
        onChange={setTab}
        aria-label={t('employees.tabs')}
        options={[
          { value: 'overview', label: t('employees.tabOverview') },
          { value: 'timeline', label: t('employees.tabTimeline') },
          { value: 'flags', label: t('employees.tabFlags') },
        ]}
      />

      {tab === 'overview' && (
        <EmployeeOverviewTab
          employee={person}
          onAction={
            canManageEmployment
              ? (kind, employment) => setAction({ kind, employment })
              : null
          }
        />
      )}

      {tab === 'timeline' && (
        <EmployeeTimelineTab events={timeline.data ?? []} loading={timeline.isPending} />
      )}

      {tab === 'flags' && (
        <Card>
          <CardContent className="pt-5">
            {canManageFlags && (
              <div className="mb-4 flex justify-end">
                <Button size="sm" variant="create" onClick={() => setRaisingFlag(true)}>
                  <Plus />
                  {t('employees.raiseFlag')}
                </Button>
              </div>
            )}

            {flags.isPending ? (
              <Skeleton className="h-24 w-full" />
            ) : flags.data && flags.data.length > 0 ? (
              <ul className="space-y-3">
                {flags.data.map((flag) => (
                  <li key={flag.id} className="flex items-start gap-3">
                    <FlagBadge type={flag.type} />
                    <span className="min-w-0 flex-1">
                      <span className="block text-footnote">{flag.reason ?? '—'}</span>
                      <span className="tabular block text-caption2 text-[var(--fg-tertiary)]">
                        {flag.startDate}
                        {flag.endDate !== null && ` — ${flag.endDate}`}
                      </span>
                    </span>

                    {/* Yopilgan belgini qayta yopib bo'lmaydi — domen rad etadi. */}
                    {canManageFlags && flag.endDate === null && (
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() =>
                          setClosingFlag({
                            id: flag.id,
                            employeeId: person.id,
                            employeeName: person.fullName,
                          })
                        }
                      >
                        {t('flags.close')}
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-subhead text-[var(--fg-secondary)]">{t('common.empty')}</p>
            )}
          </CardContent>
        </Card>
      )}

      {/* ───────── Oynalar ───────── */}

      {canEdit && (
        <EmployeeEditDialog
          open={editing}
          employee={person}
          onClose={() => setEditing(false)}
        />
      )}

      <TransferDialog
        employeeId={person.id}
        employment={action?.kind === 'transfer' ? action.employment : null}
        onClose={() => setAction(null)}
      />

      <AdditionalDutyDialog
        employeeId={person.id}
        employment={action?.kind === 'duty' ? action.employment : null}
        onClose={() => setAction(null)}
      />

      <SalaryDialog
        employeeId={person.id}
        employment={action?.kind === 'salary' ? action.employment : null}
        onClose={() => setAction(null)}
      />

      <TerminateDialog
        employeeId={person.id}
        employment={action?.kind === 'terminate' ? action.employment : null}
        onClose={() => setAction(null)}
      />

      <RaiseFlagDialog
        employeeId={person.id}
        employeeName={person.fullName}
        open={raisingFlag}
        onClose={() => setRaisingFlag(false)}
      />

      <CloseFlagDialog item={closingFlag} onClose={() => setClosingFlag(null)} />
    </div>
  )
}

function BackButton({ onClick, label }: { onClick: () => void; label: string }) {
  return (
    <Button variant="ghost" size="sm" onClick={onClick} className="-ml-2">
      <ArrowLeft />
      {label}
    </Button>
  )
}
