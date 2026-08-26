import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, type SelectOption } from '@/shared/ui'
import {
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

  useEffect(() => {
    if (!open) return
    setFailure(null)
    reset(toValues(item, defaultParentId))
  }, [open, item, defaultParentId, reset])

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

    const parentId = values.parentId === '' ? null : values.parentId
    const body: DepartmentInput = {
      name: values.name.trim(),
      note: toTextOrNull(values.note),
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
          <Button type="submit" form={FORM_ID} variant="primary" disabled={isSubmitting}>
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
