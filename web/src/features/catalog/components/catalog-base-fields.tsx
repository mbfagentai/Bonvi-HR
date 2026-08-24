import type { UseFormRegisterReturn } from 'react-hook-form'

import { useLocale } from '@/shared/i18n/use-locale'
import { TextArea, TextField } from '@/shared/ui'

export interface CatalogFieldErrors {
  name?: string | undefined
  code?: string | undefined
  sortOrder?: string | undefined
  note?: string | undefined
}

/**
 * Barcha ma'lumotnomalarga umumiy maydonlar: nom, kod, tartib, izoh.
 *
 * NEGA react-hook-form generiklari yo'q: komponentga tayyor
 * `register(...)` natijasi uzatiladi. Shu sababli u qaysi sxema bilan
 * ishlayotganini bilishi shart emas va uchala formada (oddiy ma'lumotnoma,
 * bo'lim, lavozim) o'zgarishsiz ishlatiladi.
 */
export function CatalogBaseFields({
  name,
  code,
  sortOrder,
  note,
  errors,
  disabled,
}: {
  name: UseFormRegisterReturn
  code: UseFormRegisterReturn
  sortOrder: UseFormRegisterReturn
  note: UseFormRegisterReturn
  errors: CatalogFieldErrors
  disabled: boolean
}) {
  const { t } = useLocale()

  return (
    <>
      <TextField
        label={t('catalog.fieldName')}
        placeholder={t('catalog.fieldNamePlaceholder')}
        autoComplete="off"
        disabled={disabled}
        error={errors.name}
        {...name}
      />

      <div className="grid gap-4 sm:grid-cols-2">
        <TextField
          label={t('catalog.fieldCode')}
          placeholder={t('catalog.fieldCodePlaceholder')}
          autoComplete="off"
          disabled={disabled}
          hint={t('catalog.fieldCodeHint')}
          error={errors.code}
          {...code}
        />

        <TextField
          label={t('catalog.fieldOrder')}
          type="number"
          inputMode="numeric"
          min={0}
          placeholder="0"
          disabled={disabled}
          hint={t('catalog.fieldOrderHint')}
          error={errors.sortOrder}
          {...sortOrder}
        />
      </div>

      <TextArea
        label={t('catalog.fieldNote')}
        placeholder={t('catalog.fieldNotePlaceholder')}
        disabled={disabled}
        error={errors.note}
        {...note}
      />
    </>
  )
}
