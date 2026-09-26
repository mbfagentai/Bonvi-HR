import { CheckCheck, LoaderCircle, TriangleAlert } from 'lucide-react'
import { useState } from 'react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { employeeErrorCode } from '@/features/employees/lib/employee-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, Checkbox } from '@/shared/ui'

import type { MissingProbationItem } from '../api/probations-api'
import { useConfirmMissingProbations, useMissingProbations } from '../api/probations-queries'

/**
 * «Sinovda» bo'lib qolgan, lekin sinov yozuvi yo'q xodimlar.
 *
 * <b>Nima bo'lgan.</b> Mehnat munosabati doim «sinovda» holatida ochiladi va
 * uni «doimiy» ga sinov muddatining yopilishi o'tkazadi. Tizim ichida bu
 * ikkalasi birga tug'iladi. Excel'dan ko'chirishda esa sinov ma'lumoti faqat
 * «91.5» varag'ida bor — u yerda bo'lmagan odam holatsiz qolib ketadi.
 *
 * <b>Nega bu eng qimmat yo'qotish.</b> Bunday xodim xodimlar ro'yxatida ham
 * ko'rinmaydi (u yerda doimiylar), quyidagi sinov jadvalida ham (u yerda
 * sinov YOZUVLARI). Ya'ni tizimda bor, ekranda yo'q va hech qanday xato
 * chiqmaydi. Tekshirilgan bazada 59 xodimdan 18 tasi shu holatda edi —
 * ular orasida 2010-yildan beri «sinovda» turgan ijrochi direktor ham.
 *
 * <b>Nega tizim o'zi yopmaydi.</b> «Muddati o'tgan — demak doimiy» degan
 * qoida bilan haqiqatan sinovda turgan bir necha odam ham qo'shilib
 * ketardi va keyin ularni ajratib bo'lmasdi. Qaror HR da qoladi.
 *
 * <b>Blok butunlay yo'qoladi</b> ro'yxat bo'shagach: bu bir martalik
 * tozalash ishi, doimiy ekran elementi emas.
 */
export function MissingProbations({
  canConfirm,
}: {
  /**
   * Ommaviy tasdiqlash — sinovni yakunlashning o'zi (server ham
   * `employment.probation.complete` ni talab qiladi). Ruxsatsiz odam
   * ro'yxatni KO'RADI, lekin belgilay olmaydi.
   */
  canConfirm: boolean
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  const list = useMissingProbations()
  const confirm = useConfirmMissingProbations()

  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set())

  const rows = list.data ?? []

  // Yuklanayotganda ham, ro'yxat bo'shaganda ham blok umuman chizilmaydi.
  // Skelet ko'rsatish noto'g'ri bo'lardi: eng ko'p uchraydigan holat —
  // ro'yxat BO'SH, va HR har ochganda bir zumga qizil blok chaqnab
  // o'tishi kerak emas.
  if (rows.length === 0) return null

  const toggle = (employmentId: string) => {
    setSelected((previous) => {
      const next = new Set(previous)
      if (!next.delete(employmentId)) next.add(employmentId)
      return next
    })
  }

  const allSelected = selected.size === rows.length

  const toggleAll = () => {
    setSelected(allSelected ? new Set() : new Set(rows.map((row) => row.employmentId)))
  }

  const submit = () => {
    confirm.mutate([...selected], { onSuccess: () => setSelected(new Set()) })
  }

  return (
    <Card className="overflow-hidden border-ios-orange/40">
      <div className="flex flex-wrap items-start gap-3 border-b border-[var(--border)] bg-[var(--bg-sunken)] p-4">
        <TriangleAlert className="mt-0.5 size-5 shrink-0 text-ios-orange" aria-hidden />

        <div className="min-w-0 flex-1">
          <p className="text-subhead font-semibold">
            {t('probation.missingTitle', { n: num(rows.length) })}
          </p>
          <p className="mt-1 text-footnote text-[var(--fg-secondary)]">
            {t('probation.missingText')}
          </p>
        </div>
      </div>

      {confirm.isError && (
        <div className="p-4 pb-0">
          <Alert tone="danger">{errorMessage(employeeErrorCode(confirm.error))}</Alert>
        </div>
      )}

      {/*
        Muvaffaqiyat xabari ATAYLAB qoladi: ro'yxat qisqarib ketgani
        o'zgarish sodir bo'lganini aytmaydi — HR nechtasi o'tganini
        raqam bilan ko'rishi kerak.
      */}
      {confirm.isSuccess && (
        <div className="p-4 pb-0">
          <Alert tone="success">
            {t('probation.missingDone', { n: num(confirm.data.confirmed) })}
          </Alert>
        </div>
      )}

      <ul className="divide-y divide-[var(--border)]">
        {rows.map((row) => (
          <MissingRow
            key={row.employmentId}
            row={row}
            checked={selected.has(row.employmentId)}
            disabled={!canConfirm || confirm.isPending}
            onToggle={() => toggle(row.employmentId)}
          />
        ))}
      </ul>

      {canConfirm && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border)] p-4">
          <Checkbox
            label={t('probation.missingSelectAll')}
            checked={allSelected}
            onChange={toggleAll}
            disabled={confirm.isPending}
          />

          <Button
            variant="create"
            onClick={submit}
            disabled={selected.size === 0 || confirm.isPending}
          >
            {confirm.isPending ? (
              <LoaderCircle className="animate-spin" />
            ) : (
              <CheckCheck />
            )}
            {t('probation.missingConfirm', { n: num(selected.size) })}
          </Button>
        </div>
      )}
    </Card>
  )
}

function MissingRow({
  row,
  checked,
  disabled,
  onToggle,
}: {
  row: MissingProbationItem
  checked: boolean
  disabled: boolean
  onToggle: () => void
}) {
  const { t, num, date } = useLocale()

  return (
    <li className="flex items-center gap-3 px-4 py-3">
      <Checkbox checked={checked} disabled={disabled} onChange={onToggle} />

      <EmployeePhoto
        employeeId={row.employeeId}
        photoKey={row.photoKey}
        name={row.employeeName}
        size="md"
      />

      <div className="min-w-0 flex-1">
        <p className="truncate-1 text-subhead font-medium">{row.employeeName}</p>
        <p className="truncate-1 text-caption1 text-[var(--fg-secondary)]">
          {[row.position, row.department, row.legalEntity].filter(Boolean).join(' · ') || '—'}
        </p>
      </div>

      <div className="shrink-0 text-right">
        <p className="tabular text-footnote">
          {row.hireDate === null ? '—' : date(row.hireDate)}
        </p>
        {/*
          Ishlagan kun — HR qarorining asosi. «5826 kun sinovda» degan
          raqam savolni butunlay yopadi.
        */}
        <p className="tabular text-caption2 text-[var(--fg-tertiary)]">
          {t('probation.workedDays', { n: num(row.workedDays) })}
        </p>
      </div>
    </li>
  )
}
