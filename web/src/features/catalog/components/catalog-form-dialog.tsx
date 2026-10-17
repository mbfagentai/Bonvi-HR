import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Switch } from '@/shared/ui'
import { useSaveCatalogItem } from '../api/catalog-queries'
import type { CatalogItem, CatalogKind } from '../api/catalog-api'
import { CATALOG_ERROR_FIELD, catalogErrorCode } from '../lib/catalog-error'
import {
  catalogItemSchema,
  toTextOrNull,
  type CatalogItemValues,
} from '../lib/catalog-schemas'
import { CatalogActiveField } from './catalog-active-field'
import { CatalogBaseFields } from './catalog-base-fields'

function toValues(item: CatalogItem | null): CatalogItemValues {
  return {
    name: item?.name ?? '',
    note: item?.note ?? '',
  }
}

/**
 * Oddiy ma'lumotnoma yozuvini yaratish va tahrirlash oynasi.
 *
 * Bitta oyna ikkala holat uchun: farq faqat sarlavhada va `item` borligida.
 * Ikkiga bo'linsa, maydonlar ta'rifi ikki joyda takrorlanardi.
 */
export function CatalogFormDialog({
  open,
  onClose,
  kind,
  item,
}: {
  open: boolean
  onClose: () => void
  kind: CatalogKind
  /** `null` — yangi yozuv. */
  item: CatalogItem | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveCatalogItem(kind)

  /** Maydonga bog'lanmagan server xatosi (masalan tarmoq uzilishi). */
  const [failure, setFailure] = useState<string | null>(null)

  /*
    Yo'qlik turining «ish vaqti hisoblanadi» bayrog'i (SPEC-so'rovlar §B3
    AMENDED). Faol/faolsiz almashtirgichidan farqli o'laroq «Saqlash» bilan
    birga yuboriladi: bu yozuvning xossasi, alohida amal emas.
  */
  const isAbsenceType = kind === 'absence-types'
  const [countsAsWorkTime, setCountsAsWorkTime] = useState(false)

  /*
    «Xodim o'zi so'ray oladi» (SPEC-jadval-sorovi §A) — xuddi shu qoida bilan.
    Yangi tur uchun YOQIQ: Verifix'dagi turlarning hammasida shunday va
    o'chirish — istisno («bu turni faqat HR kiritadi»).
  */
  const [employeeCanRequest, setEmployeeCanRequest] = useState(true)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CatalogItemValues>({
    resolver: zodResolver(catalogItemSchema),
    defaultValues: toValues(item),
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  // Oyna har ochilganda forma qayta to'ldiriladi: oldingi yozuvning
  // qiymatlari yangisiga «yopishib» qolmasligi kerak
  useEffect(() => {
    if (!open) return
    setFailure(null)
    reset(toValues(item))
    setCountsAsWorkTime(item?.countsAsWorkTime ?? false)
    setEmployeeCanRequest(item?.employeeCanRequest ?? true)
  }, [open, item, reset])

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)

    try {
      await save.mutateAsync({
        id: item?.id ?? null,
        body: {
          name: values.name.trim(),
          note: toTextOrNull(values.note),
          ...(isAbsenceType ? { countsAsWorkTime, employeeCanRequest } : {}),
        },
      })
      onClose()
    } catch (error) {
      const code = catalogErrorCode(error)
      if (CATALOG_ERROR_FIELD[code] === 'name') {
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
      title={item === null ? t('catalog.createTitle') : t('catalog.editTitle')}
      description={t(`catalog.kinds.${kindKey(kind)}.title`)}
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

        {isAbsenceType && (
          // Ikki almashtirgich bitta guruhda — iOS sozlamalar ro'yxati kabi
          <div className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)]">
            <Switch
              className="px-4 py-3"
              checked={countsAsWorkTime}
              disabled={isSubmitting}
              onChange={setCountsAsWorkTime}
              label={t('catalog.workTimeTitle')}
              hint={countsAsWorkTime ? t('catalog.workTimeHintOn') : t('catalog.workTimeHintOff')}
            />
            <Switch
              className="px-4 py-3"
              checked={employeeCanRequest}
              disabled={isSubmitting}
              onChange={setEmployeeCanRequest}
              label={t('catalog.employeeRequestTitle')}
              hint={
                employeeCanRequest
                  ? t('catalog.employeeRequestHintOn')
                  : t('catalog.employeeRequestHintOff')
              }
            />
          </div>
        )}

        {/* Yangi yozuv doim faol yaratiladi — holat faqat mavjud yozuvda */}
        {item !== null && (
          <CatalogActiveField
            kind={kind}
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
 * Forma oyna tanasida, «Saqlash» tugmasi esa pastki panelda turadi.
 * `form` atributi ularni bog'laydi — tugmani forma ichiga kiritish uchun
 * tartibni buzish shart emas.
 */
const FORM_ID = 'catalog-form'

/** `legal-entities` → `legalEntities`: tarjima kalitlari camelCase'da. */
function kindKey(kind: CatalogKind): string {
  return kind.replaceAll(/-(\w)/gu, (_, letter: string) => letter.toUpperCase())
}
