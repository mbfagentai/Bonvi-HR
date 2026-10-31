import { Crown, X } from 'lucide-react'
import { useMemo, useState } from 'react'

import type { DepartmentNode } from '@/features/catalog/api/catalog-api'
import {
  useDepartmentHeadNames,
  useDepartmentList,
  useDepartmentTree,
  useSetDepartmentHead,
} from '@/features/catalog/api/catalog-queries'
import { flattenTree } from '@/features/catalog/lib/tree'
import { useEmployee } from '@/features/employees/api/employees-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Select, Skeleton, type SelectOption } from '@/shared/ui'

const EMPTY_TREE: DepartmentNode[] = []

/**
 * Xodim kartasi «Davomat» → «Ilova»: bo'lim boshlig'i (egasi, 2026-10-31).
 *
 * Boshliqlik bo'limning xususiyati (`Department.HeadEmployeeId`, bo'limda bitta
 * boshliq), lekin HR uni odamdan boshlab o'ylaydi: «Aliyev — Metan zavod
 * boshlig'i». Shu sababli u «Bo'limlar» menyusidan TASHQARI xodim kartasida
 * ham bor — ilova hisobi yonida, chunki boshliqlik aynan ilovadagi «Jamoa»
 * tabini (davomat va vakansiya so'rovi) ochadi.
 *
 * ⚠️ Faqat `catalog.manage` + `attendance.view` egasiga chiziladi — server
 * ham shu ikkalasini so'raydi (`PUT /api/catalog/departments/{id}/head`).
 */
export function EmployeeTeamLeadSection({ employeeId }: { employeeId: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const list = useDepartmentList()
  const tree = useDepartmentTree(false)
  const employee = useEmployee(employeeId)
  const headNames = useDepartmentHeadNames(true)
  const setHead = useSetDepartmentHead()

  const headed = useMemo(
    () => (list.data ?? []).filter((item) => item.headEmployeeId === employeeId),
    [list.data, employeeId],
  )

  const options = useMemo<SelectOption[]>(
    () =>
      flattenTree(tree.data ?? EMPTY_TREE)
        .filter((row) => row.node.isActive)
        .map((row) => ({ value: row.node.id, label: row.node.name, depth: row.depth })),
    [tree.data],
  )

  // Sukut — xodimning asosiy ish joyidagi bo'lim (odatda aynan o'shaning boshlig'i bo'ladi)
  const ownDepartmentId = useMemo(() => {
    const employments = employee.data?.employments ?? []
    const primary =
      employments.find((item) => item.isPrimary && item.status === 'active') ??
      employments.find((item) => item.status === 'active')
    return primary?.department?.id ?? ''
  }, [employee.data])

  const [picked, setPicked] = useState<string | null>(null)
  const departmentId = picked ?? ownDepartmentId

  const target = (list.data ?? []).find((item) => item.id === departmentId) ?? null
  const alreadyHead = target?.headEmployeeId === employeeId
  const otherHead =
    target?.headEmployeeId && !alreadyHead
      ? (headNames.get(target.id) ?? t('attendance.teamLeadAnother'))
      : null

  if (list.isPending || tree.isPending) {
    return <Skeleton className="h-24 w-full" />
  }

  return (
    <section className="space-y-3 border-t border-[var(--border)] pt-4">
      <div>
        <h3 className="text-callout font-semibold">{t('attendance.teamLeadTitle')}</h3>
        <p className="mt-0.5 text-footnote text-[var(--fg-tertiary)]">{t('attendance.teamLeadHint')}</p>
      </div>

      {headed.length === 0 ? (
        <p className="text-subhead text-[var(--fg-secondary)]">{t('attendance.teamLeadNone')}</p>
      ) : (
        <ul className="divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
          {headed.map((item) => (
            <li key={item.id} className="flex items-center gap-3 px-3 py-2">
              <Crown className="size-4 shrink-0 text-[var(--accent)]" aria-hidden />
              <span className="min-w-0 flex-1 truncate text-subhead" title={item.path ?? item.name}>
                {item.path ?? item.name}
              </span>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                pill
                disabled={setHead.isPending}
                onClick={() => setHead.mutate({ departmentId: item.id, employeeId: null })}
              >
                <X />
                {t('attendance.teamLeadRemove')}
              </Button>
            </li>
          ))}
        </ul>
      )}

      <div className="flex flex-wrap items-end gap-2">
        <Select
          className="min-w-[240px] flex-1"
          size="sm"
          label={t('attendance.teamLeadDepartment')}
          placeholder={t('attendance.teamLeadDepartmentPlaceholder')}
          options={options}
          value={departmentId}
          disabled={setHead.isPending}
          onChange={(event) => {
            setPicked(event.target.value)
            setHead.reset()
          }}
        />
        <Button
          type="button"
          size="sm"
          variant="soft"
          pill
          disabled={departmentId === '' || alreadyHead || setHead.isPending}
          onClick={() => setHead.mutate({ departmentId, employeeId })}
        >
          <Crown />
          {t('attendance.teamLeadAssign')}
        </Button>
      </div>

      {otherHead !== null && (
        <Alert tone="warning">{t('attendance.teamLeadReplaces', { name: otherHead })}</Alert>
      )}

      {setHead.isError && <Alert tone="danger">{errorMessage(problemCode(setHead.error))}</Alert>}
    </section>
  )
}
