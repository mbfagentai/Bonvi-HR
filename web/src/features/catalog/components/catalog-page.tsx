import { ChevronRight, Plus, SearchX } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { matchesSearch } from '@/shared/lib/search-fold'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'
import { useCatalogList } from '../api/catalog-queries'
import type { CatalogItem } from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'
import { CATALOG_MANAGE_PERMISSION, type CatalogKindMeta } from '../lib/catalog-kinds'
import { CatalogFormDialog } from './catalog-form-dialog'
import { CatalogToolbar } from './catalog-toolbar'

const helper = dataColumnHelper<CatalogItem>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: CatalogItem[] = []

/**
 * Oddiy ma'lumotnoma sahifasi.
 *
 * Oltita ma'lumotnoma (yuridik shaxslar, yo'nalishlar, ishdan ketish
 * sabablari, rag'bat/jarima va hujjat turlari) tuzilishi bo'yicha bir xil.
 * Har biriga alohida sahifa yozish — bir xil kodni oltita joyda saqlash
 * degani; farq faqat sarlavha, ikona va API yo'lida, ular esa `meta` da.
 */
export function CatalogPage({ meta }: { meta: CatalogKindMeta }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(CATALOG_MANAGE_PERMISSION)

  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState<CatalogItem | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  /*
   * Ro'yxatda HAMMA yozuv: faol emaslari ham. Ilgari bu «Faqat faollar»
   * filtriga bog'liq edi, endi filtr yo'q — yozuv qayerga ketganini
   * qidirishga hojat qolmasin.
   */
  const list = useCatalogList(meta.kind, false)

  const openCreate = useCallback(() => {
    setEditing(null)
    setDialogOpen(true)
  }, [])

  const openEdit = useCallback((item: CatalogItem) => {
    setEditing(item)
    setDialogOpen(true)
  }, [])

  const columns = useMemo<Array<DataColumn<CatalogItem>>>(() => {
    const definitions: Array<DataColumn<CatalogItem>> = [
      // Ortgan joyni shu ustun oladi — nom yozuvni tanishning birdan-bir
      // yo'li, shu sababli u qanchalik keng bo'lsa, shunchalik kam
      // qisqartiriladi.
      //
      // `width` EMAS, `minWidth`: `width` ustunni QAT'IY o'lchamga qo'yib,
      // ortgan-joy taqsimotidan chiqarib yuborardi (tekshirilgan: shunday
      // qilinsa, yonidagi `actions` 56px'dan 243px'gacha shishadi — brauzer
      // keng ekranda ortgan joyni ikkala «belgilangan» ustun o'rtasida
      // proporsional taqsimlaydi). `minWidth` esa ustunni moslashuvchan
      // holda qoldiradi: `actions` har doim aynan 56px, ortgan joyning
      // HAMMASI shu yerga tushadi (tekshirilgan: 700/1100/1600px kenglikda
      // — 700px'da 300px, 1100px'da 640px, 1600px'da 1140px).
      //
      // 300px — pastki chegara: real ma'lumotnomalardagi nomlarning aksari
      // (oltala ma'lumotnoma, eng uzuni ~35 belgi) shu kenglikda bir
      // qatorga sig'adi, faqat eng uzun uchtasi biroz kesiladi.
      helper.accessor('name', {
        header: t('catalog.columnName'),
        meta: { minWidth: '300px' },
        cell: ({ row }) => (
          <div className="min-w-0">
            <div className="flex min-w-0 items-center gap-2">
              <p
                className={cn(
                  'truncate-1 font-medium',
                  // Faol emas yozuv o'chirilmagan — shunchaki so'ndirilgan
                  !row.original.isActive && 'text-[var(--fg-secondary)]',
                )}
              >
                {row.original.name}
              </p>

              {row.original.countsAsWorkTime === true && (
                <Badge tone="info" size="sm" className="shrink-0">
                  {t('catalog.workTimeBadge')}
                </Badge>
              )}

              {!row.original.isActive && (
                <Badge tone="neutral" size="sm" className="shrink-0">
                  {t('catalog.statusInactive')}
                </Badge>
              )}
            </div>
            {row.original.note !== null && row.original.note !== '' && (
              <p className="truncate-1 mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
                {row.original.note}
              </p>
            )}
          </div>
        ),
      }),
    ]

    // Ruxsat yo'q bo'lsa ustunning o'zi ham chiqmaydi — bo'sh ustun
    // «bu yerda nimadir bo'lishi kerak edi» degan taassurot qoldiradi
    if (canManage) {
      definitions.push(
        helper.display({
          id: 'actions',
          // Sarlavha ATAYLAB bo'sh: katakda hech qanday amal yo'q, faqat
          // strelka — u qatorning bosiladiganini ko'rsatadi, xolos.
          // «Amallar»/«Действия» yolg'on nom bo'lardi va ustunni 56px
          // o'rniga 108px ga cho'zib turardi (employees-page.tsx dagi
          // xuddi shu strelka ustuni bilan bir xil: `header: ''`, 56px).
          header: '',
          meta: { align: 'end', width: '56px' },
          cell: () => (
            // iOS ro'yxatidagi «ochiladi» belgisi: qator bosiladigan
            // ekanini alohida tugmasiz aytadi
            <ChevronRight
              className="ml-auto size-4 text-[var(--fg-tertiary)]"
              strokeWidth={2.5}
            />
          ),
        }),
      )
    }

    return definitions
  }, [t, canManage])

  const rows = useMemo(() => {
    const source = list.data ?? EMPTY
    const needle = query.trim()
    if (needle === '') return source

    // Qidiruv faqat nom bo'yicha: nom butun ma'lumotnoma bo'yicha unikal va
    // foydalanuvchi yozuvni aynan shu nom bilan biladi. Izoh ichki eslatma —
    // uni qidiruvga qo'shish tasodifiy mos kelishlar keltirib chiqaradi.
    //
    // `matchesSearch` — server qidiruvidagi qoidaning O'ZI: HR rus
    // klaviaturada «Кукон» yozsa ham «Қўқон» topiladi. Usiz ma'lumotnoma
    // tizimdagi yagona «boshqacha qidiradigan» ekran bo'lib qolardi.
    return source.filter((item) => matchesSearch(item.name, needle))
  }, [list.data, query])

  const searching = query.trim() !== ''

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={meta.icon} tone={meta.tone} size="lg" />
            {t(`catalog.kinds.${meta.i18nKey}.title`)}
          </span>
        }
        subtitle={t(`catalog.kinds.${meta.i18nKey}.subtitle`)}
        actions={
          canManage ? (
            <Button variant="create" onClick={openCreate}>
              <Plus />
              {t('common.add')}
            </Button>
          ) : undefined
        }
      />

      {list.isError && (
        <Alert tone="danger" title={t('catalog.loadFailed')}>
          {errorMessage(catalogErrorCode(list.error))}
        </Alert>
      )}

      <Card className="overflow-hidden">
        <CatalogToolbar query={query} onQueryChange={setQuery} />

        <DataTable
          columns={columns}
          data={rows}
          getRowId={(item) => item.id}
          loading={list.isPending}
          onRowClick={canManage ? openEdit : undefined}
          rowLabel={(item) => item.name}
          empty={
            searching ? (
              <EmptyState
                icon={SearchX}
                title={t('catalog.notFoundTitle')}
                description={t('catalog.notFoundText', { query: query.trim() })}
              />
            ) : (
              <EmptyState
                icon={meta.icon}
                title={t('catalog.emptyTitle')}
                description={t('catalog.emptyText')}
                action={
                  canManage ? (
                    <Button variant="create" onClick={openCreate}>
                      <Plus />
                      {t('common.add')}
                    </Button>
                  ) : undefined
                }
              />
            )
          }
        />

        {!list.isPending && rows.length > 0 && (
          <p className="border-t border-[var(--border)] px-5 py-3 text-caption1 text-[var(--fg-tertiary)]">
            {t('catalog.total', { n: num(rows.length) })}
          </p>
        )}
      </Card>

      <CatalogFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        kind={meta.kind}
        item={editing}
      />
    </div>
  )
}
