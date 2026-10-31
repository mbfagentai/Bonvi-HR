import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import {
  Alert,
  Button,
  DateField,
  Dialog,
  Select,
  Skeleton,
  Switch,
  TextField,
  type SelectOption,
} from '@/shared/ui'
import {
  useCatalogList,
  useCatalogUsage,
  useDepartmentTree,
  useReassignCatalogItem,
  useToggleCatalogItem,
} from '../api/catalog-queries'
import {
  REASSIGN_CATALOG_KINDS,
  USAGE_CATALOG_KINDS,
  type CatalogKind,
  type DepartmentNode,
  type ReassignCatalogKind,
  type ReferenceUsage,
} from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'
import { collectSubtreeIds, flattenTree } from '../lib/tree'

const EMPTY_TREE: DepartmentNode[] = []

const isReassignKind = (kind: CatalogKind): kind is ReassignCatalogKind =>
  (REASSIGN_CATALOG_KINDS as readonly string[]).includes(kind)

/**
 * Yozuvni faollashtirish/faolsizlantirish — FAQAT tahrirlash oynasida.
 *
 * NEGA ro'yxatda emas: ro'yxatdagi almashtirgich butun tizim bo'ylab
 * ta'sir qiladigan amalni bir tegishda bajarardi — barmoq tekkanini ham
 * sezmay qolish mumkin edi. Ma'lumotnoma yozuvi esa o'nlab xodim
 * kartasiga bog'langan bo'ladi: uni tasodifan o'chirib qo'yish qimmat.
 * Endi yo'l uzunroq: yozuvni ochish → almashtirgich → tasdiqlash oynasi.
 *
 * Faol emas qilishda oyna avval yozuvda kim qolganini so'raydi (egasi,
 * 2026-10-31; SPEC-malumotnoma-faolsizlantirish): ishlayotgan xodim bo'lsa,
 * bo'lim, lavozim va yo'nalishda ular shu yerning o'zida boshqasiga
 * o'tkaziladi; yuridik shaxsda esa faqat ro'yxat ko'rsatiladi — uni
 * almashtirish yangi shartnoma.
 *
 * O'zgarish DARHOL saqlanadi («Saqlash» tugmasini kutmaydi): tasdiqlash
 * oynasi allaqachon aniq savol berdi, uni ikkinchi marta so'rash ortiqcha.
 */
export function CatalogActiveField({
  kind,
  id,
  name,
  active,
  disabled = false,
}: {
  kind: CatalogKind
  id: string
  /** Tasdiqlash matnida ko'rsatiladi — qaysi yozuv haqida ekani aniq bo'lsin. */
  name: string
  active: boolean
  disabled?: boolean
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const toggle = useToggleCatalogItem(kind)
  const reassign = useReassignCatalogItem(isReassignKind(kind) ? kind : 'departments')

  /*
   * Holat shu yerda ham saqlanadi: oynaga berilgan `item` ro'yxat bosilgan
   * ondagi nusxa — server javobidan keyin u yangilanmaydi. Mahalliy qiymat
   * bo'lmasa, almashtirgich muvaffaqiyatli o'zgarishdan keyin ortga qaytib
   * ketardi.
   */
  const [current, setCurrent] = useState(active)
  const [asking, setAsking] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const [targetId, setTargetId] = useState('')
  const [effectiveDate, setEffectiveDate] = useState(todayIso)
  const [orderNo, setOrderNo] = useState('')
  const [orderDate, setOrderDate] = useState('')

  useEffect(() => {
    setCurrent(active)
    setAsking(false)
    setFailure(null)
  }, [id, active])

  const next = !current
  const tracksUsage = (USAGE_CATALOG_KINDS as readonly string[]).includes(kind)
  const usage = useCatalogUsage(kind, id, asking && !next && tracksUsage)
  const usageData: ReferenceUsage | undefined = tracksUsage ? usage.data : undefined
  const occupied = (usageData?.activeEmployees ?? 0) > 0
  const moving = occupied && usageData?.canReassign === true && isReassignKind(kind)
  const blocked = occupied && !moving

  const pending = toggle.isPending || reassign.isPending
  const loadingUsage = asking && !next && tracksUsage && usage.isPending

  const open = () => {
    setFailure(null)
    setTargetId('')
    setEffectiveDate(todayIso())
    setOrderNo('')
    setOrderDate('')
    setAsking(true)
  }

  const apply = async () => {
    setFailure(null)
    try {
      if (moving && isReassignKind(kind)) {
        await reassign.mutateAsync({
          id,
          input: {
            targetId,
            effectiveDate: kind === 'directions' || effectiveDate === '' ? null : effectiveDate,
            orderNo: kind === 'directions' || orderNo.trim() === '' ? null : orderNo.trim(),
            orderDate: kind === 'directions' || orderDate === '' ? null : orderDate,
          },
        })
      } else {
        await toggle.mutateAsync({ id, active: next })
      }
      setCurrent(next)
      setAsking(false)
    } catch (error) {
      setFailure(catalogErrorCode(error))
    }
  }

  return (
    <>
      <div className="rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
        <Switch
          checked={current}
          disabled={disabled || pending}
          onChange={open}
          label={t('catalog.statusTitle')}
          hint={
            current ? t('catalog.statusHintActive') : t('catalog.statusHintInactive')
          }
        />
      </div>

      {/* ───────── Tasdiqlash ───────── */}
      <Dialog
        open={asking}
        onClose={() => setAsking(false)}
        size={occupied ? 'md' : 'sm'}
        dismissible={!pending}
        title={
          next
            ? t('catalog.confirmActivateTitle')
            : t('catalog.confirmDeactivateTitle')
        }
        footer={
          <>
            <Button
              type="button"
              onClick={() => setAsking(false)}
              disabled={pending}
            >
              {t('common.cancel')}
            </Button>

            {/* Faolsizlantirish — qaytariladigan, lekin sezilarli amal:
                qizil emas, lekin asosiy tugma o'rnida turadi */}
            <Button
              type="button"
              variant={next ? 'primary' : 'danger'}
              onClick={() => void apply()}
              disabled={
                pending || loadingUsage || usage.isError || blocked || (moving && targetId === '')
              }
            >
              {pending && <LoaderCircle className="animate-spin" />}
              {pending
                ? t('catalog.statusChanging')
                : next
                  ? t('catalog.activate')
                  : moving
                    ? t('catalog.retire.moveAndDeactivate')
                    : t('catalog.deactivate')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {next && (
            <p className="text-subhead text-[var(--fg-secondary)]">
              {t('catalog.confirmActivateText', { name })}
            </p>
          )}

          {!next && loadingUsage && <Skeleton className="h-20 w-full" />}

          {!next && usage.isError && (
            <Alert tone="danger">{errorMessage(catalogErrorCode(usage.error))}</Alert>
          )}

          {!next && !loadingUsage && !usage.isError && (
            <>
              {!occupied && (
                <p className="text-subhead text-[var(--fg-secondary)]">
                  {t('catalog.confirmDeactivateText', { name })}
                </p>
              )}

              {kind === 'departments' && (usageData?.activeSubdepartments ?? 0) > 0 && (
                <Alert tone="info">
                  {t('catalog.retire.subdepartments', { n: num(usageData?.activeSubdepartments ?? 0) })}
                </Alert>
              )}

              {occupied && usageData !== undefined && (
                <OccupiedSection
                  kind={kind}
                  id={id}
                  name={name}
                  usage={usageData}
                  moving={moving}
                  targetId={targetId}
                  onTarget={setTargetId}
                  effectiveDate={effectiveDate}
                  onEffectiveDate={setEffectiveDate}
                  orderNo={orderNo}
                  onOrderNo={setOrderNo}
                  orderDate={orderDate}
                  onOrderDate={setOrderDate}
                  disabled={pending}
                />
              )}
            </>
          )}

          {failure !== null && (
            <Alert tone="danger" title={t('catalog.statusChangeFailed')}>
              {errorMessage(failure)}
            </Alert>
          )}
        </div>
      </Dialog>
    </>
  )
}

/** Yozuvda ishlayotganlar ro'yxati va — o'tkazib bo'lsa — qayerga, qachon. */
function OccupiedSection({
  kind,
  id,
  name,
  usage,
  moving,
  targetId,
  onTarget,
  effectiveDate,
  onEffectiveDate,
  orderNo,
  onOrderNo,
  orderDate,
  onOrderDate,
  disabled,
}: {
  kind: CatalogKind
  id: string
  name: string
  usage: ReferenceUsage
  moving: boolean
  targetId: string
  onTarget: (value: string) => void
  effectiveDate: string
  onEffectiveDate: (value: string) => void
  orderNo: string
  onOrderNo: (value: string) => void
  orderDate: string
  onOrderDate: (value: string) => void
  disabled: boolean
}) {
  const { t, num } = useLocale()
  const count = num(usage.activeEmployees)
  const hiddenRest = usage.activeEmployees - usage.employees.length

  return (
    <div className="space-y-4">
      <Alert tone={moving ? 'warning' : 'danger'}>
        {moving
          ? t('catalog.retire.mustMove', { name, n: count })
          : t('catalog.retire.blocked', { name, n: count })}
      </Alert>

      {usage.employeesHidden ? (
        <p className="text-footnote text-[var(--fg-tertiary)]">{t('catalog.retire.namesHidden')}</p>
      ) : (
        <ul className="max-h-56 divide-y divide-[var(--border)] overflow-y-auto rounded-lg border border-[var(--border)]">
          {usage.employees.map((person) => (
            <li key={person.employmentId ?? person.employeeId} className="px-3 py-2">
              <p className="truncate text-subhead font-medium">{person.fullName}</p>
              <p className="truncate text-footnote text-[var(--fg-tertiary)]">
                {[person.departmentName, person.positionName].filter(Boolean).join(' · ')}
              </p>
            </li>
          ))}
          {hiddenRest > 0 && (
            <li className="px-3 py-2 text-footnote text-[var(--fg-tertiary)]">
              {t('catalog.retire.more', { n: num(hiddenRest) })}
            </li>
          )}
        </ul>
      )}

      {moving && (
        <>
          <TargetSelect kind={kind} id={id} value={targetId} onChange={onTarget} disabled={disabled} />

          {kind !== 'directions' && (
            <>
              <DateField
                label={t('catalog.retire.effectiveDate')}
                value={effectiveDate}
                onChange={onEffectiveDate}
                disabled={disabled}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <TextField
                  label={t('employee.orderNo')}
                  value={orderNo}
                  onChange={(event) => onOrderNo(event.target.value)}
                  disabled={disabled}
                />
                <DateField
                  label={t('employee.orderDate')}
                  value={orderDate}
                  onChange={onOrderDate}
                  disabled={disabled}
                />
              </div>
              <p className="text-footnote text-[var(--fg-tertiary)]">{t('catalog.retire.historyHint')}</p>
            </>
          )}
        </>
      )}
    </div>
  )
}

interface TargetSelectProps {
  kind: CatalogKind
  id: string
  value: string
  onChange: (value: string) => void
  disabled: boolean
}

/** Faqat faol yozuvlar, o'zidan tashqari; bo'limda — uning ichidagilar ham emas. */
function TargetSelect(props: TargetSelectProps) {
  return props.kind === 'departments' ? <DepartmentTarget {...props} /> : <ListTarget {...props} />
}

function DepartmentTarget({ id, value, onChange, disabled }: TargetSelectProps) {
  const { t } = useLocale()
  const tree = useDepartmentTree(false)

  const options = useMemo<SelectOption[]>(() => {
    const flat = flattenTree(tree.data ?? EMPTY_TREE)
    const own = flat.find((row) => row.node.id === id)
    const excluded = new Set(own !== undefined ? collectSubtreeIds(own.node) : [id])
    return flat
      .filter((row) => row.node.isActive && !excluded.has(row.node.id))
      .map((row) => ({ value: row.node.id, label: row.node.name, depth: row.depth }))
  }, [tree.data, id])

  return (
    <Select
      label={t('catalog.retire.target.departments')}
      placeholder={t('catalog.retire.targetPlaceholder')}
      options={options}
      value={value}
      disabled={disabled || tree.isPending}
      onChange={(event) => onChange(event.target.value)}
    />
  )
}

function ListTarget({ kind, id, value, onChange, disabled }: TargetSelectProps) {
  const { t } = useLocale()
  const list = useCatalogList(kind, true)

  const options = useMemo<SelectOption[]>(
    () => (list.data ?? []).filter((item) => item.id !== id).map((item) => ({ value: item.id, label: item.name })),
    [list.data, id],
  )

  return (
    <Select
      label={t(`catalog.retire.target.${kind}`)}
      placeholder={t('catalog.retire.targetPlaceholder')}
      options={options}
      value={value}
      disabled={disabled || list.isPending}
      onChange={(event) => onChange(event.target.value)}
    />
  )
}
