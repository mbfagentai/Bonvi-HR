import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Pencil } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'

import {
  FlagBadge,
  NeedsReviewMark,
  StatusBadge,
} from '@/features/employees/components/employee-badges'
import { EmployeeCompetencyTab } from '@/features/employees/components/employee-competency-tab'
import { EmployeeDocumentsTab } from '@/features/employees/components/employee-documents-tab'
import { EmployeeEditDialog } from '@/features/employees/components/employee-edit-dialog'
import { EmployeeIncentivesTab } from '@/features/employees/components/employee-incentives-tab'
import { EmployeeMarksTab } from '@/features/employees/components/employee-marks-tab'
import {
  EmployeeOverviewTab,
  type EmploymentAction,
} from '@/features/employees/components/employee-overview-tab'
import { EmployeePhotoEditor } from '@/features/employees/components/employee-photo-editor'
import { EmployeeTimelineTab } from '@/features/employees/components/employee-timeline-tab'
import {
  AdditionalDutyDialog,
  SalaryDialog,
  TerminateDialog,
  TransferDialog,
} from '@/features/employees/components/employment-dialogs'
import type { ProbationTarget } from '@/features/probations/api/probations-api'
import {
  ExtendDialog,
  ResolveDialog,
} from '@/features/probations/components/probation-dialogs'
import {
  EMPLOYEES_EDIT,
  EMPLOYEES_VIEW,
  EMPLOYMENT_MANAGE,
  INCENTIVES_VIEW,
  employeeStage,
  flagInForce,
} from '@/features/employees/lib/employee-presentation'
import {
  useEmployee,
  useEmployeeFlags,
  useEmployeeTimeline,
} from '@/features/employees/api/employees-queries'
import type { EmploymentSummary } from '@/features/employees/api/employees-api'
import { useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, SegmentedControl, Skeleton } from '@/shared/ui'
import { todayIso } from '@/shared/lib/today'

export const Route = createFileRoute('/_app/xodimlar/$employeeId')({
  component: EmployeeCardPage,
})

type Tab = 'overview' | 'timeline' | 'documents' | 'competency' | 'incentives' | 'flags'

/**
 * Xodim kartasi.
 *
 * Reja (PLAN.md) dagi oltita tab endi TO'LIQ: Umumiy · Mehnat tarixi ·
 * Hujjatlar · Qobiliyatlar · Rag'bat/Jarima · Belgilar. Ular tab bilan
 * almashadi — bir vaqtda hammasini ko'rsatish sahifani cho'zib yuborardi,
 * HR esa odatda bittasiga qaraydi.
 *
 * Ma'lumot alohida so'rovlar bilan olinadi: karta ochilishi bilan asosiy
 * qism, tarix va belgilar bir vaqtda yuklanadi (uchtasi ham engil).
 * «Hujjatlar», «Qobiliyatlar» va «Rag'bat/Jarima» esa TANLANGANDAGINA
 * so'raladi — ular `EmployeeDocumentsTab`/`EmployeeCompetencyTab`/
 * `EmployeeIncentivesTab` ichida, faqat shu komponent chizilganda ishga
 * tushadigan `useQuery` bilan.
 *
 * Uchtasi ham ALOHIDA ruxsat bilan ko'rinadi (`employees.view` va
 * `incentives.view`): boshqa ikkitasidan farqli, ular sahifaning o'ziga
 * kirish huquqidan mustaqil — shu sababli tab ro'yxati ham dinamik
 * yig'iladi (`tabOptions`). Hujjatlar `employees.view` bilan chegaralanadi
 * — «Qobiliyatlar» bilan bir xil, chunki ikkalasi ham xodim kartasining
 * bir qismi, alohida modul emas.
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

  const { hasPermission } = useAuth()
  const canEdit = hasPermission(EMPLOYEES_EDIT)
  const canManageEmployment = hasPermission(EMPLOYMENT_MANAGE)
  const canViewCompetency = hasPermission(EMPLOYEES_VIEW)
  const canViewDocuments = hasPermission(EMPLOYEES_VIEW)
  const canViewIncentives = hasPermission(INCENTIVES_VIEW)

  const employee = useEmployee(employeeId)
  const timeline = useEmployeeTimeline(employeeId)
  const flags = useEmployeeFlags(employeeId)

  const fullName = employee.data?.fullName ?? ''

  /**
   * Sinov oynalari kutadigan qiymat — mehnat munosabatidan yig'iladi.
   *
   * <b>NEGA `useMemo`.</b> Oynalar formani `item` O'ZGARGANDA tiklaydi
   * (obyekt tengligi bo'yicha). Bu yerda har render'da yangi obyekt
   * yasalsa, HR yozayotgan xulosa har bosishda o'chib ketardi.
   *
   * <b>NEGA erta qaytishlardan OLDIN.</b> Hook shart ostida chaqirilmaydi:
   * pastda «yuklanmoqda» va «xato» tarmoqlari bor va ular komponentdan
   * chiqib ketadi.
   *
   * `probationId` faqat OCHIQ sinovda to'ladi (`employees-api.ts`), ya'ni
   * holat bu yerda qaytadan tekshirilmaydi — kartadagi tugmalar ham xuddi
   * shu shart bilan chiziladi, ikkalasi bitta manbadan.
   */
  const probationTarget = useMemo<ProbationTarget | null>(() => {
    const employment = action?.employment
    if (employment === undefined || employment.probationId === null) return null

    return {
      id: employment.probationId,
      // Oyna sarlavhasi ostida kimning sinovi yopilayotgani turadi —
      // sinov ro'yxatidagi bilan bir xil ko'rinish.
      employeeName: fullName,
      departmentId: employment.department?.id ?? null,
      positionId: employment.position?.id ?? null,
      endDate: employment.probationEndDate,
    }
    /*
      Bog'liqlikda `employee.data` EMAS, undan olingan SATR. Sabab: karta
      so'rovi fon rejimida qayta yuklanganda (oyna fokusga qaytgan payt)
      obyekt yangisiga almashadi — matn esa o'sha-o'sha qoladi. Obyektga
      bog'lansak, HR yozayotgan xulosa alt-tab bosilishi bilan o'chib
      ketardi.
    */
  }, [action, fullName])

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

  /*
   * Xodim hozir ishlayaptimi — belgi tanlovi shunga qarab tuziladi
   * (`FLAG_STAGE` izohi). Bitta ochiq munosabat yetarli: o'rindosh xodim
   * bitta firmadan ketsa ham bizda ishlashda davom etadi.
   */
  const stage = employeeStage(person.employments)

  // Bugundan KEYIN ham kuchda qoladigan belgilar — serverdagi
  // `IsInForceAfter` bilan bir xil shart (`flagInForce` izohi).
  const bugun = todayIso()
  const inForceFlags = (flags.data ?? []).filter((flag) => flagInForce(flag, bugun))

  /*
   * Tab ro'yxati DINAMIK: «Hujjatlar», «Qobiliyatlar» va «Rag'bat/Jarima»
   * faqat shu ruxsat bor foydalanuvchiga ko'rinadi (topshiriq qoidasi —
   * ruxsat yo'q bo'lsa tab umuman chizilmaydi, «bo'sh/xatoli» holat
   * ko'rsatilmaydi). Qolgan uchtasi (umumiy, tarix, belgilar) sahifaning
   * o'ziga kirish huquqidan kelib chiqadi, shu sababli shartsiz.
   */
  const tabOptions: Array<{ value: Tab; label: string }> = [
    { value: 'overview', label: t('employees.tabOverview') },
    { value: 'timeline', label: t('employees.tabTimeline') },
  ]
  if (canViewDocuments) {
    tabOptions.push({ value: 'documents', label: t('employees.tabDocuments') })
  }
  if (canViewCompetency) {
    tabOptions.push({ value: 'competency', label: t('employees.tabCompetency') })
  }
  if (canViewIncentives) {
    tabOptions.push({ value: 'incentives', label: t('employees.tabIncentives') })
  }
  tabOptions.push({ value: 'flags', label: t('employees.tabFlags') })

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
              {/*
                FAQAT kuchdagi belgilar. Ilgari bu yerda butun tarix
                chizilardi va almashtirilgan belgi o'sha KUNI hali ham
                nishon bo'lib turardi — natijada kartada bir vaqtda ikkita
                belgi ko'rinib, «bir vaqtda atigi bitta belgi» qoidasi
                buzilgandek taassurot qolardi. Tarix pastdagi «Belgilar»
                tabida, u yerda sanalari bilan o'qiladi.
              */}
              {inForceFlags.map((flag) => (
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
        options={tabOptions}
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

      {tab === 'documents' && canViewDocuments && (
        <EmployeeDocumentsTab employeeId={person.id} />
      )}

      {tab === 'competency' && canViewCompetency && (
        <EmployeeCompetencyTab employeeId={person.id} />
      )}

      {tab === 'incentives' && canViewIncentives && (
        <EmployeeIncentivesTab employeeId={person.id} />
      )}

      {tab === 'flags' && (
        <EmployeeMarksTab
          employeeId={person.id}
          employeeName={person.fullName}
          employeePhotoKey={person.photoKey}
          employeePinfl={person.pinfl}
          stage={stage}
        />
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

      {/*
        Sinov muddati oynalari — SINOV MUDDATLARI modulidan, ikkinchi nusxa
        emas. «Doimiy xodim · o'tolmadi · o'zi ketdi · bekor qilindi» va
        doimiyga o'tkazishdagi lavozim/bo'lim/oklad/stavka/buyruq —
        hammasi `ResolveDialog` ichida allaqachon bor va u serverga BITTA
        so'rov yuboradi (`/api/probations/{id}/resolve`).

        Oynalar to'liq ro'yxat qatorini emas, tor `ProbationTarget` ni
        so'raydi — kartada o'sha qator yo'q. Bo'lim va lavozim
        identifikatorlari mehnat munosabatidan olinadi: doimiyga
        o'tkazish formasi tanlovlarni aynan shular bilan to'ldiradi.
      */}
      <ResolveDialog
        item={action?.kind === 'probationResolve' ? probationTarget : null}
        onClose={() => setAction(null)}
      />

      <ExtendDialog
        item={action?.kind === 'probationExtend' ? probationTarget : null}
        onClose={() => setAction(null)}
      />
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
