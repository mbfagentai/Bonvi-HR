import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select, TextField, type SelectOption } from '@/shared/ui'
import { useDepartmentTree, useSaveCatalogItem } from '../api/catalog-queries'
import type { DepartmentNode, PositionInput, PositionItem } from '../api/catalog-api'
import { CATALOG_ERROR_FIELD, catalogErrorCode } from '../lib/catalog-error'
import {
  positionSchema,
  toNumberOrNull,
  toTextOrNull,
  type PositionValues,
} from '../lib/catalog-schemas'
import { flattenTree } from '../lib/tree'
import { CatalogActiveField } from './catalog-active-field'
import { CatalogBaseFields } from './catalog-base-fields'
import { DutiesEditor } from './duties-editor'

const FORM_ID = 'position-form'

const EMPTY_TREE: DepartmentNode[] = []

function toValues(item: PositionItem | null): PositionValues {
  return {
    name: item?.name ?? '',
    note: item?.note ?? '',
    departmentId: item?.departmentId ?? '',
    // `null` — kiritilmagan: maydon bo'sh qoladi, «0» emas
    probationDays:
      item === null || item.probationDays === null ? '' : String(item.probationDays),
  }
}

/**
 * Lavozim oynasi: umumiy maydonlar + bo'lim, sinov muddati va
 * vazifalar ro'yxati.
 *
 * «Daraja» maydoni ataylab YO'Q: u bazada qoladi (yangi lavozimda 1,
 * mavjudlarida o'zgarmaydi), lekin HR uni to'ldirmaydi — 1 dan 5 gacha
 * bo'lgan raqamning ma'nosi tizim ichida, foydalanuvchi ishida emas.
 *
 * Vazifalarning o'z endpoint'i bor (`/positions/{id}/duties`), ya'ni ular
 * lavozim `id` siz saqlanmaydi. Shu sababli yangi lavozim saqlangach oyna
 * YOPILMAYDI — tahrirlash rejimiga o'tadi va foydalanuvchi vazifalarni
 * darhol qo'sha oladi. Aks holda u ro'yxatga qaytib, endigina yaratgan
 * yozuvni qidirib topishi kerak bo'lardi.
 */
export function PositionFormDialog({
  open,
  onClose,
  item,
  canManage,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi lavozim. */
  item: PositionItem | null
  canManage: boolean
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const save = useSaveCatalogItem<PositionItem, PositionInput>('positions')
  const departments = useDepartmentTree(false)

  const [failure, setFailure] = useState<string | null>(null)
  /** Shu oynada yaratilgan lavozim — vazifalar shu `id` ga biriktiriladi. */
  const [createdId, setCreatedId] = useState<string | null>(null)

  const positionId = item?.id ?? createdId

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PositionValues>({
    resolver: zodResolver(positionSchema),
    defaultValues: toValues(item),
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setCreatedId(null)
    reset(toValues(item))
  }, [open, item, reset])

  const departmentOptions = useMemo<SelectOption[]>(
    () =>
      flattenTree(departments.data ?? EMPTY_TREE).map((row) => ({
        value: row.node.id,
        label: row.node.name,
        depth: row.depth,
      })),
    [departments.data],
  )

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    const body: PositionInput = {
      name: values.name.trim(),
      note: toTextOrNull(values.note),
      departmentId: values.departmentId === '' ? null : values.departmentId,
      // `grade` yuborilmaydi — server uni o'z holicha qoldiradi
      probationDays: toNumberOrNull(values.probationDays),
    }

    try {
      const saved = await save.mutateAsync({ id: positionId, body })

      if (positionId === null) {
        // Yangi lavozim saqlandi — oyna ochiq qoladi, endi vazifalar mumkin
        setCreatedId(saved.id)
        reset(toValues(saved))
      } else {
        onClose()
      }
    } catch (error) {
      const code = catalogErrorCode(error)
      const field = CATALOG_ERROR_FIELD[code]

      if (field === 'name' || field === 'probationDays') {
        setError(field, { message: code })
      } else {
        setFailure(code)
      }
    }
  })

  const justCreated = item === null && createdId !== null

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={!isSubmitting}
      title={item === null ? t('catalog.positions.create') : t('catalog.positions.edit')}
      description={t('catalog.kinds.positions.title')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={isSubmitting}>
            {justCreated ? t('common.close') : t('common.cancel')}
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

        {justCreated && (
          <Alert tone="success" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {t('catalog.positions.createdHint')}
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
          label={t('catalog.positions.department')}
          placeholder={t('catalog.positions.noDepartment')}
          options={departmentOptions}
          disabled={isSubmitting}
          error={errors.departmentId ? errorMessage(errors.departmentId.message) : undefined}
          {...register('departmentId')}
        />

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label={t('catalog.positions.probationDays')}
            type="number"
            inputMode="numeric"
            min={0}
            max={180}
            placeholder="90"
            disabled={isSubmitting}
            hint={t('catalog.positions.probationHint')}
            error={
              errors.probationDays
                ? errorMessage(errors.probationDays.message)
                : undefined
            }
            {...register('probationDays')}
          />
        </div>

        <DutiesEditor positionId={positionId} canManage={canManage} />

        {item !== null && (
          <CatalogActiveField
            kind="positions"
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
