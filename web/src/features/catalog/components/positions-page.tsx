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
import { useCatalogList, useDepartmentTree } from '../api/catalog-queries'
import type { DepartmentNode, PositionItem } from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'
import { CATALOG_MANAGE_PERMISSION, POSITIONS_META } from '../lib/catalog-kinds'
import { flattenTree } from '../lib/tree'
import { CatalogToolbar } from './catalog-toolbar'
import { PositionFormDialog } from './position-form-dialog'

const helper = dataColumnHelper<PositionItem>()

const EMPTY: PositionItem[] = []
const EMPTY_TREE: DepartmentNode[] = []

/**
 * Lavozimlar ro'yxati.
 *
 * Oddiy ma'lumotnomadan farqi: lavozimda bo'lim va vazifalar bor.
 * Vazifalar soni ustuni ataylab ro'yxatda — HR uchun «vazifasi yozilmagan
 * lavozim» ishga qabulda muammo bo'ladi, uni bir qarashda ko'rish kerak.
 *
 * «Daraja» ustuni yo'q: u ma'lumotlar bazasining ichki maydoni (hisob-kitob
 * va tartiblash uchun), HR esa lavozimni nomi bilan biladi. Ekranda turgan
 * har bir raqam «buni to'ldirish kerakmi?» degan savol tug'diradi.
 */
export function PositionsPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(CATALOG_MANAGE_PERMISSION)

  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState<PositionItem | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  /** Faol emas lavozimlar ham ko'rinadi — nomi yonidagi yorliq bilan. */
  const list = useCatalogList<PositionItem>('positions', false)

  /**
   * Bo'lim nomi lavozim yozuvida yo'q — faqat `departmentId` keladi.
   * Daraxt baribir tahrirlash oynasi uchun yuklanadi, shu sababli nom
   * shu yerda yechiladi: har bir qator uchun alohida so'rov (N+1) bo'lmaydi.
   */
  const departments = useDepartmentTree(false)
  const departmentNames = useMemo(() => {
    const map = new Map<string, string>()
    for (const row of flattenTree(departments.data ?? EMPTY_TREE)) {
      map.set(row.node.id, row.node.path ?? row.node.name)
    }
    return map
  }, [departments.data])

  const openCreate = useCallback(() => {
    setEditing(null)
    setDialogOpen(true)
  }, [])

  const openEdit = useCallback((item: PositionItem) => {
    setEditing(item)
    setDialogOpen(true)
  }, [])

  const columns = useMemo<Array<DataColumn<PositionItem>>>(() => {
    const definitions: Array<DataColumn<PositionItem>> = [
      // Ortgan joyni shu ustun oladi. Lavozim nomlari uzun bo'ladi
      // («Katta smena ustasi», ba'zilari 60+ belgi), shu sababli eng keng
      // joy aynan shu ustunga tegishi kerak.
      //
      // `width` EMAS, `minWidth`: ikkalasi ham `department`/`dutyCount`/
      // `actions`ni «belgilangan» (piksellik) qoldiradi, lekin farqi bor.
      // `width` bergan bo'lsak, `name` ham «belgilangan» ustunga aylanardi
      // va brauzer (`table-layout: auto`) keng ekranda ortgan joyni
      // TO'RTALASI o'rtasida proporsional taqsimlab, `dutyCount`/`actions`
      // ni ham shishirib yuborardi (tekshirilgan: 140px→241px, 56px→96px).
      // `minWidth` esa `name`ni moslashuvchan qoldiradi: uchala qo'shni
      // ustun har doim aynan deklaratsiyasidagidek (260/140/56px), ortgan
      // joyning HAMMASI faqat shu yerga tushadi (tekshirilgan: 700px'da
      // 300px, 1100px'da 640px, 1600px'da 1140px — qo'shnilar o'zgarmadi).
      //
      // 300px — pastki chegara (fixed ustunlar 456px + 300 = 756px jami):
      // real bazadagi 481 ta lavozim nomi mediana 18, 90-foizi ≤30 belgi —
      // 300px'da ~30 belgi bir qatorga sig'adi (o'lchangan: 30 belgili
      // haqiqiy nom «Ички бухгалтерия бўлим бошлиғи» 256px matn talab
      // qiladi, +32px karra to'ldirish = 288px, 300px kichik zaxira bilan).
      // Undan uzunroqlari (10-foiz, 68 belgigacha) kesiladi — bu qabul
      // qilingan: to'liq nom tahrirlash oynasida ko'rinadi.
      helper.accessor('name', {
        header: t('catalog.columnName'),
        meta: { minWidth: '300px' },
        cell: ({ row }) => (
          <div className="flex min-w-0 items-center gap-2">
            <p
              className={cn(
                'truncate-1 font-medium',
                !row.original.isActive && 'text-[var(--fg-secondary)]',
              )}
            >
              {row.original.name}
            </p>

            {!row.original.isActive && (
              <Badge tone="neutral" size="sm" className="shrink-0">
                {t('catalog.statusInactive')}
              </Badge>
            )}
          </div>
        ),
      }),

      helper.accessor((row) => departmentNames.get(row.departmentId ?? '') ?? '', {
        id: 'department',
        header: t('catalog.positions.department'),
        meta: { width: '260px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          return value === '' ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span className="truncate-1 text-[var(--fg-secondary)]">{value}</span>
          )
        },
      }),

      helper.accessor('dutyCount', {
        header: t('catalog.positions.dutiesCount'),
        meta: { align: 'end', width: '140px' },
        cell: ({ getValue }) => {
          const count = getValue()
          // Server bu maydonni hali yubormaydi — yo'qligini «0» deb
          // ko'rsatish yolg'on bo'lardi, shu sababli chiziqcha
          if (count === null || count === undefined) {
            return <span className="text-[var(--fg-tertiary)]">—</span>
          }
          // Vazifasiz lavozim — HR uchun ogohlantirish, oddiy nol emas
          return (
            <Badge tone={count > 0 ? 'info' : 'warning'} size="sm">
              {num(count)}
            </Badge>
          )
        },
      }),
    ]

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
            <ChevronRight
              className="ml-auto size-4 text-[var(--fg-tertiary)]"
              strokeWidth={2.5}
            />
          ),
        }),
      )
    }

    return definitions
  }, [t, num, canManage, departmentNames])

  const rows = useMemo(() => {
    const source = list.data ?? EMPTY
    const needle = query.trim()
    if (needle === '') return source

    // Nom va bo'lim nomi — ekranda ko'rinib turgan ikkala matn. Bir xil
    // nomli lavozim turli bo'limlarda uchraydi, shu sababli bo'lim bo'yicha
    // qidirish HR uchun kerakli qisqartma («ta'mirlash» → o'sha bo'lim ichi).
    return source.filter(
      (item) =>
        matchesSearch(item.name, needle) ||
        matchesSearch(departmentNames.get(item.departmentId ?? '') ?? '', needle),
    )
  }, [list.data, query, departmentNames])

  const searching = query.trim() !== ''

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={POSITIONS_META.icon} tone={POSITIONS_META.tone} size="lg" />
            {t('catalog.kinds.positions.title')}
          </span>
        }
        subtitle={t('catalog.kinds.positions.subtitle')}
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
                icon={POSITIONS_META.icon}
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

      <PositionFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        item={editing}
        canManage={canManage}
      />
    </div>
  )
}
