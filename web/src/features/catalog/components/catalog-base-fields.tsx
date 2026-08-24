import type { UseFormRegisterReturn } from 'react-hook-form'

import { useLocale } from '@/shared/i18n/use-locale'
import { TextArea, TextField } from '@/shared/ui'

export interface CatalogFieldErrors {
  name?: string | undefined
  note?: string | undefined
}

/**
 * Barcha ma'lumotnomalarga umumiy maydonlar: nom va izoh.
 *
 * NEGA react-hook-form generiklari yo'q: komponentga tayyor
 * `register(...)` natijasi uzatiladi. Shu sababli u qaysi sxema bilan
 * ishlayotganini bilishi shart emas va uchala formada (oddiy ma'lumotnoma,
 * bo'lim, lavozim) o'zgarishsiz ishlatiladi.
 */
export function CatalogBaseFields({
  name,
  note,
  errors,
  disabled,
}: {
  name: UseFormRegisterReturn
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
