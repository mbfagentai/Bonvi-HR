import { Award, LoaderCircle, Pencil, Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  Dialog,
  EmptyState,
  IconTile,
  SegmentedControl,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { Incentive, IncentiveKindValue } from '../api/incentives-api'
import { useDeleteIncentive, useIncentives } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'
import { INCENTIVES_MANAGE, INCENTIVE_KIND_LOOK } from '../lib/employee-presentation'
import { IncentiveDialog } from './incentive-dialog'

const helper = dataColumnHelper<Incentive>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: Incentive[] = []

type KindFilter = 'all' | IncentiveKindValue

/**
 * «Rag'bat va jarima» tabi (91.14 + M8).
 *
 * Xodim tarixidagi HAMMA rag'bat va jarima yozuvi bitta jadvalda —
 * `EmployeeTimelineTab` bilan bir xil mantiq: bitta yozuv, bitta joyda.
 * Filtr — segmentli boshqaruv (`Barchasi/Rag'bat/Jarima`), `kind` server
 * so'roviga to'g'ridan-to'g'ri o'tadi.
 */
export function EmployeeIncentivesTab({ employeeId }: { employeeId: string }) {
  const { t, money, percent } = useLocale()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(INCENTIVES_MANAGE)

  const [filter, setFilter] = useState<KindFilter>('all')
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<Incentive | null>(null)
  const [deleting, setDeleting] = useState<Incentive | null>(null)

  const list = useIncentives(employeeId, filter === 'all' ? undefined : filter)
  const rows = list.data ?? EMPTY

  const columns = useMemo<Array<DataColumn<Incentive>>>(() => {
    const definitions: Array<DataColumn<Incentive>> = [
      helper.accessor('date', {
        // 130px — boshqa sana ustunlari bilan bir xil (`flag-list-page.tsx`,
        // `employments-page.tsx`): «25.08.2026» (10 belgi, tabular) sig'adi.
        header: t('incentives.date'),
        meta: { width: '130px' },
        cell: ({ getValue }) => <DateText value={getValue()} />,
      }),

      /*
        YAGONA `minWidth`li ustun — qoidaga ko'ra jadvalda bittadan ortiq
        bo'lmasligi kerak. Tur nomi ma'lumotnomadan (admin kiritadi,
        uzunligi taxminan ma'lum), lekin baribir eng uzun kutilgan qiymat
        («Қўшимча ҳақ тўланадиган таътил», ~31 belgi) qat'iy kenglikka
        sig'maydi — ustun nomgacha kengayadi. Nom va nishon (rag'bat /
        jarima) BIR qatorda (egasi 2026-10-22: jadval qatori bir qator).
      */
      helper.accessor('typeName', {
        header: t('incentives.type'),
        enableSorting: false,
        meta: { minWidth: '200px' },
        cell: ({ row }) => {
          const look = INCENTIVE_KIND_LOOK[row.original.kind]
          const name = row.original.typeName ?? '—'

          return (
            <div className="flex items-center gap-2.5">
              <IconTile icon={look.icon} tone={look.iconTone} size="sm" />
              <span className="text-subhead font-medium">{name}</span>
              <Badge tone={look.tone} size="sm">
                {t(look.labelKey)}
              </Badge>
            </div>
          )
        },
      }),

      helper.accessor('amount', {
        /*
          150px, o'ngga tekislangan. Hisob: eng uzun realistik summa
          «12 000 000 so'm» — raqamlar `tabular` (~9px/belgi 15px shriftda):
          8 raqam+bo'shliq (2) = 8×9+2×4 = 80px; + « so'm» (5 belgi,
          proporsional ~7px/belgi) = 35px. Jami ~115px + katak to'ldirishi
          (32px) = 147px, kichik zaxira bilan 150px.
        */
        header: t('incentives.columnAmount'),
        meta: { align: 'end', width: '150px' },
        cell: ({ getValue, row }) => {
          // Jarima summa o'rniga foiz bilan berilgan bo'lishi mumkin («4 %»).
          if (row.original.percent !== null) {
            return <span className="tabular">{percent(row.original.percent)}</span>
          }

          const value = getValue()
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          return <span className="tabular">{money(value.amount, value.currency)}</span>
        },
      }),

      helper.accessor((row) => row.reason ?? row.giftDescription, {
        // Erkin matn: bir qatorda, 320px da kesiladi, to'lig'i `title` da
        // (flags jadvalidagi «sabab» ustuni bilan bir xil qoida).
        id: 'note',
        header: t('incentives.columnNote'),
        enableSorting: false,
        cell: ({ row }) => {
          const value = row.original.reason ?? row.original.giftDescription
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          return (
            <span className="block max-w-[320px] truncate text-[var(--fg-secondary)]" title={value}>
              {value}
            </span>
          )
        },
      }),
    ]

    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          header: '',
          /*
            Ikkita ikonkali tugma (`icon-sm` = 32px har biri) + oraliq
            (gap-1.5 = 6px) = 70px + katak to'ldirishi (32px) = 102px,
            kichik zaxira bilan 110px (`users-page.tsx`dagi bilan bir xil).
          */
          meta: { align: 'end', width: '110px' },
          cell: ({ row }) => (
            <span className="flex justify-end gap-1.5">
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('common.edit')}
                aria-label={t('common.edit')}
                onClick={(event) => {
                  event.stopPropagation()
                  setEditing(row.original)
                }}
              >
                <Pencil />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('common.delete')}
                aria-label={t('common.delete')}
                onClick={(event) => {
                  event.stopPropagation()
                  setDeleting(row.original)
                }}
              >
                <Trash2 className="text-[var(--danger-text)]" />
              </Button>
            </span>
          ),
        }),
      )
    }

    return definitions
  }, [canManage, money, percent, t])

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <SegmentedControl
          size="sm"
          value={filter}
          onChange={setFilter}
          aria-label={t('incentives.filterLabel')}
          options={[
            { value: 'all', label: t('common.all') },
            { value: 'award', label: t(INCENTIVE_KIND_LOOK.award.labelKey) },
            { value: 'penalty', label: t(INCENTIVE_KIND_LOOK.penalty.labelKey) },
          ]}
        />

        {canManage && (
          <Button type="button" size="sm" variant="create" onClick={() => setCreating(true)}>
            <Plus />
            {t('incentives.add')}
          </Button>
        )}
      </div>

      <Card>
        <DataTable
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          loading={list.isPending}
          rowLabel={(row) => row.typeName ?? ''}
          empty={
            <EmptyState
              icon={Award}
              title={t('incentives.emptyTitle')}
              description={t('incentives.emptyText')}
            />
          }
        />
      </Card>

      <IncentiveDialog
        employeeId={employeeId}
        mode={creating ? 'create' : editing !== null ? 'edit' : null}
        item={editing}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
      />

      <DeleteIncentiveDialog employeeId={employeeId} item={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}

function DateText({ value }: { value: string }) {
  const { date } = useLocale()
  return <span className="tabular">{date(value)}</span>
}

/**
 * O'chirishni tasdiqlash.
 *
 * Yumshoq o'chirish (server tomonda yozuv bazada qoladi, faqat
 * ro'yxatlarda ko'rinmaydi), lekin foydalanuvchi buni oddiy amaldek
 * ko'radi — shu sababli tasdiqlash so'raladi (topshiriqning talabi).
 */
function DeleteIncentiveDialog({
  employeeId,
  item,
  onClose,
}: {
  employeeId: string
  item: Incentive | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const del = useDeleteIncentive(employeeId)

  // Oldingi yozuvning xato holati keyingisiga o'tib qolmasin.
  useEffect(() => {
    if (item !== null) del.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!del.isPending}
      title={t('incentives.deleteTitle')}
      description={item.typeName ?? undefined}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={del.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={() => del.mutate(item.id, { onSuccess: onClose })}
            disabled={del.isPending}
          >
            {del.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {del.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(del.error))}</Alert>}
        <p className="text-subhead text-[var(--fg-secondary)]">{t('incentives.deleteConfirm')}</p>
      </div>
    </Dialog>
  )
}
