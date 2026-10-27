import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle, UserRound, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'

import { useAuth } from '@/features/auth'
import type { EmployeeDetails } from '@/features/employees/api/employees-api'
import { useEmployee } from '@/features/employees/api/employees-queries'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import {
  AttendanceEmployeePicker,
  type PickedEmployee,
} from '@/features/verification/components/attendance-employee-picker'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, Skeleton, type SelectOption } from '@/shared/ui'
import {
  useDepartmentList,
  useDepartmentTree,
  useMoveDepartment,
  useSaveCatalogItem,
} from '../api/catalog-queries'
import type { DepartmentInput, DepartmentNode } from '../api/catalog-api'
import { CATALOG_ERROR_FIELD, catalogErrorCode } from '../lib/catalog-error'
import {
  departmentSchema,
  toTextOrNull,
  type DepartmentValues,
} from '../lib/catalog-schemas'
import { collectSubtreeIds, flattenTree } from '../lib/tree'
import { CatalogActiveField } from './catalog-active-field'
import { CatalogBaseFields } from './catalog-base-fields'

const FORM_ID = 'department-form'

const EMPTY_TREE: DepartmentNode[] = []

/**
 * Boshliq maydonining holati. `touched: false` — HR boshliqqa tegmagan:
 * saqlashda serverdagi JORIY qiymat qaytariladi (u forma ochilganda hali
 * yuklanmagan bo'lishi ham mumkin). `touched: true` — HR tanladi yoki
 * olib tashladi, `picked: null` — boshliqsiz.
 */
interface HeadState {
  touched: boolean
  picked: PickedEmployee | null
}

const UNTOUCHED: HeadState = { touched: false, picked: null }

/** Xodim kartasidan tanlagich qatori — asosiy, ishlayotgan munosabatning lavozimi va bo'limi. */
function pickedFromDetails(details: EmployeeDetails): PickedEmployee {
  const working = details.employments.filter((row) => row.status !== 'terminated')
  const primary = working.find((row) => row.isPrimary) ?? working[0] ?? details.employments[0]

  return {
    id: details.id,
    fullName: details.fullName,
    photoKey: details.photoKey,
    position: primary?.position?.name ?? null,
    department: primary?.department?.name ?? null,
  }
}

function toValues(item: DepartmentNode | null, parentId: string | null): DepartmentValues {
  return {
    name: item?.name ?? '',
    note: item?.note ?? '',
    parentId: (item === null ? parentId : item.parentId) ?? '',
  }
}

/**
 * Bo'lim yaratish va tahrirlash oynasi.
 *
 * Ota bo'lim oddiy `<select>` bilan almashtiriladi — sudrab tashlash (DnD)
 * bu yerda ortiqcha: 73 ta bo'limli daraxtda sichqonchani ushlab yurishdan
 * ko'ra ro'yxatdan tanlash tezroq va xatosizroq.
 */
export function DepartmentFormDialog({
  open,
  onClose,
  item,
  defaultParentId,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi bo'lim. */
  item: DepartmentNode | null
  /** «Ichiga qo'shish» bosilganda oldindan tanlanadigan ota bo'lim. */
  defaultParentId: string | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const save = useSaveCatalogItem<DepartmentNode, DepartmentInput>('departments')
  const move = useMoveDepartment()

  // Ota tanlash uchun BUTUN daraxt kerak: faolsizlantirilgan bo'lim ham
  // ierarxiyada qoladi va uning ichidagilarni ko'rsatish shart
  const tree = useDepartmentTree(false)

  /*
    Boshliq va yuridik shaxs daraxt tugunida YO'Q — tekis ro'yxatdan
    olinadi. Tahrirda ular ma'lum bo'lmaguncha saqlash YOPIQ: server bu
    ikkisini har saqlashda buyruqdan yozadi, ya'ni «bilmaymiz» deb `null`
    yuborish boshliqni jimgina o'chirib yuborardi (SPEC-jamoam §E).
  */
  const departments = useDepartmentList()
  const current =
    item === null ? null : ((departments.data ?? []).find((row) => row.id === item.id) ?? null)
  const currentKnown = item === null || current !== null
  // Ro'yxat yiqildi yoki bo'lim undan topilmadi (shu orada o'chirilgan) — kutish befoyda
  const currentFailed = !currentKnown && (departments.isError || departments.isSuccess)
  const originalHeadId = current?.headEmployeeId ?? null
  // Ochilishdagi qayta o'qish tugaguncha saqlash kutadi: keshdagi eski boshliq qaytarib yozilmasin
  const currentFresh = item === null || !departments.isFetching

  const { hasPermission } = useAuth()
  // Tanlagich xodimlar ro'yxatidan qidiradi — `employees.view` siz u ishlamaydi
  const canPickEmployee = hasPermission(EMPLOYEES_VIEW)

  const [head, setHead] = useState<HeadState>(UNTOUCHED)
  const [headError, setHeadError] = useState<string | null>(null)

  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<DepartmentValues>({
    resolver: zodResolver(departmentSchema),
    defaultValues: toValues(item, defaultParentId),
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  const { refetch: refetchDepartments } = departments

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setHead(UNTOUCHED)
    setHeadError(null)
    reset(toValues(item, defaultParentId))
    // Boshliqni boshqa HR shu orada almashtirgan bo'lishi mumkin — eski keshdagi
    // qiymatni qaytarib yozmaslik uchun ochilishda yangisi o'qiladi
    if (item !== null) void refetchDepartments()
  }, [open, item, defaultParentId, reset, refetchDepartments])

  /**
   * Ota bo'lim variantlari.
   * Bo'limning o'zi va uning butun avlodi ro'yxatdan chiqariladi — aks holda
   * foydalanuvchi halqa yasab qo'yadi va serverdan xato oladi.
   */
  const parentOptions = useMemo<SelectOption[]>(() => {
    const flat = flattenTree(tree.data ?? EMPTY_TREE)
    const forbidden = new Set<string>(
      item === null
        ? []
        : collectSubtreeIds(flat.find((row) => row.node.id === item.id)?.node ?? item),
    )

    return flat
      .filter((row) => !forbidden.has(row.node.id))
      .map((row) => ({
        value: row.node.id,
        label: row.node.name,
        depth: row.depth,
      }))
  }, [tree.data, item])

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    setHeadError(null)

    // Joriy qiymat noma'lum yoki eskirgan — yubormaymiz (tugma ham yopiq, bu ikkinchi qavat)
    if (!currentKnown || !currentFresh) return

    const parentId = values.parentId === '' ? null : values.parentId
    const body: DepartmentInput = {
      name: values.name.trim(),
      note: toTextOrNull(values.note),
      // DOIM yuboriladi — tegilmagan bo'lsa serverdagi joriy qiymat
      headEmployeeId: head.touched ? (head.picked?.id ?? null) : originalHeadId,
      legalEntityId: current?.legalEntityId ?? null,
    }

    try {
      if (item === null) {
        await save.mutateAsync({ id: null, body: { ...body, parentId } })
      } else {
        // Ko'chirish BIRINCHI: aynan shu qadam halqa xatosi bilan yiqilishi
        // mumkin, va yiqilsa hech narsa o'zgarmagan bo'lib qoladi
        if (parentId !== (item.parentId ?? null)) {
          await move.mutateAsync({ id: item.id, parentId })
        }
        await save.mutateAsync({ id: item.id, body })
      }
      onClose()
    } catch (error) {
      const code = catalogErrorCode(error)

      if (code === 'bolim.halqa') {
        // Xabar aynan ota maydoni ostida chiqadi — muammo o'sha yerda
        setError('parentId', { message: code })
      } else if (code === 'bolim.boshliq_topilmadi') {
        setHeadError(code)
      } else if (CATALOG_ERROR_FIELD[code] === 'name') {
        setError('name', { message: code })
      } else {
        setFailure(code)
      }
    }
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!isSubmitting}
      title={item === null ? t('catalog.departments.create') : t('catalog.departments.edit')}
      description={t('catalog.kinds.departments.title')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={isSubmitting || !currentKnown || !currentFresh}
          >
            {isSubmitting && <LoaderCircle className="animate-spin" />}
            {isSubmitting ? t('catalog.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id={FORM_ID} onSubmit={onSubmit} noValidate className="space-y-4">
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        {currentFailed && (
          <Alert tone="danger" title={t('catalog.loadFailed')}>
            {t('catalog.departments.headUnknown')}
          </Alert>
        )}

        <CatalogBaseFields
          disabled={isSubmitting}
          name={register('name')}
          note={register('note')}
          errors={{
            name: errors.name ? errorMessage(errors.name.message) : undefined,
            note: errors.note ? errorMessage(errors.note.message) : undefined,
          }}
        />

        <Select
          label={t('catalog.departments.parent')}
          placeholder={t('catalog.departments.noParent')}
          options={parentOptions}
          disabled={isSubmitting}
          hint={t('catalog.departments.parentHint')}
          error={errors.parentId ? errorMessage(errors.parentId.message) : undefined}
          {...register('parentId')}
        />

        {!currentFailed && (
          <HeadField
            loading={!currentKnown}
            originalHeadId={originalHeadId}
            value={head}
            canPick={canPickEmployee}
            disabled={isSubmitting}
            error={headError !== null ? errorMessage(headError) : undefined}
            onChange={(picked) => {
              setHeadError(null)
              setHead({ touched: true, picked })
            }}
          />
        )}

        {item !== null && (
          <CatalogActiveField
            kind="departments"
            id={item.id}
            name={item.name}
            active={item.isActive}
            disabled={isSubmitting}
          />
        )}
      </form>
    </Dialog>
  )
}

/**
 * «Bo'lim boshlig'i» — xodim tanlagichi (davomatdagining o'zi: rasm, lavozim,
 * bo'lim — bir xil ismli ikki odamni yuzidan ajratish uchun) va «olib tashlash».
 *
 * Boshliq ilovada shu bo'lim va ICHIDAGI bo'limlar xodimlarining davomati va
 * telefonini ko'radi — izoh buni HR ga oldindan aytadi: bu oddiy yorliq emas,
 * ruxsat.
 */
function HeadField({
  loading,
  originalHeadId,
  value,
  canPick,
  disabled,
  error,
  onChange,
}: {
  /** Joriy boshliq hali ma'lum emas (tekis ro'yxat yuklanmoqda). */
  loading: boolean
  originalHeadId: string | null
  value: HeadState
  canPick: boolean
  disabled: boolean
  error?: string
  onChange: (picked: PickedEmployee | null) => void
}) {
  const { t } = useLocale()

  // Tegilmagan joriy boshliqning kartasi — tanlagichdagi rasm va ism uchun
  const original = useEmployee(!value.touched && canPick && originalHeadId ? originalHeadId : '')

  const label = t('catalog.departments.head')
  const hint = <p className="text-footnote text-[var(--fg-tertiary)]">{t('catalog.departments.headHint')}</p>

  const effectiveId = value.touched ? (value.picked?.id ?? null) : originalHeadId
  const picked = value.touched
    ? value.picked
    : original.data && original.data.id === originalHeadId
      ? pickedFromDetails(original.data)
      : null

  const clear =
    effectiveId !== null && !disabled ? (
      <Button type="button" variant="ghost" size="sm" onClick={() => onChange(null)}>
        <X />
        {t('catalog.departments.headClear')}
      </Button>
    ) : null

  if (loading || (!value.touched && originalHeadId !== null && canPick && original.isPending)) {
    return (
      <div className="space-y-2">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>
        <Skeleton className="h-16 rounded-xl" />
      </div>
    )
  }

  /*
    Kartasi ochilmaydigan joriy boshliq: ruxsat yo'q (`employees.view`) yoki
    xodim o'chirilgan. Qiymat o'zgarmasdan qaytariladi; HR uni olib tashlashi
    mumkin — keyin (ruxsat bo'lsa) qidiruvdan boshqasini tanlaydi.
  */
  if (!value.touched && originalHeadId !== null && picked === null) {
    return (
      <div className="space-y-2">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>
        <div className="flex items-center gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
          <UserRound className="size-5 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={1.8} />
          <p className="min-w-0 flex-1 text-subhead text-[var(--fg-secondary)]">
            {t('catalog.departments.headHidden')}
          </p>
        </div>
        {clear}
        {error !== undefined && (
          <p role="alert" className="text-footnote text-ios-red">
            {error}
          </p>
        )}
        {hint}
      </div>
    )
  }

  if (!canPick) {
    return (
      <div className="space-y-2">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>
        <p className="text-subhead text-[var(--fg-secondary)]">{t('catalog.departments.headNoAccess')}</p>
      </div>
    )
  }

  return (
    <div className="space-y-2">
      <AttendanceEmployeePicker
        label={label}
        value={picked}
        onChange={onChange}
        error={error}
        disabled={disabled}
        employedOnly
      />
      {clear}
      {hint}
    </div>
  )
}
