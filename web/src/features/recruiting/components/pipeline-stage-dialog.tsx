import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Switch, TextArea, TextField } from '@/shared/ui'
import type { PipelineStage } from '../api/recruiting-api'
import { useSaveStage, useToggleStage } from '../api/recruiting-queries'
import { RECRUITING_ERROR_FIELD, recruitingErrorCode } from '../lib/recruiting-error'
import { orNull } from '../lib/recruiting-presentation'

const FORM_ID = 'pipeline-stage-form'

/**
 * Voronka bosqichini yaratish va tahrirlash.
 *
 * `CatalogFormDialog` ning naqshi: bitta oyna ikkala holat uchun,
 * nom + izoh, faollik esa alohida maydonda tasdiqlash bilan.
 *
 * <b>Nega umumiy `CatalogFormDialog` QAYTA ISHLATILMADI.</b> U
 * `/api/catalog/{kind}` yo'liga qattiq bog'langan (`CatalogKind`
 * sanog'i, `useSaveCatalogItem`, `catalogKeys`), bosqichlar esa
 * `/api/recruiting/stages` da yashaydi va boshqa xato kodlarini
 * qaytaradi (`bosqich.*`, `malumotnoma.*` emas). Uni umumiy qilish
 * uchun `CatalogKind` ga rekruting yo'lini qo'shish kerak bo'lardi —
 * ya'ni ma'lumotnomalar moduliga rekruting haqidagi bilim kirib
 * kelardi. Bu yerdagi ellik qator takror shundan arzonroq.
 *
 * <b>Tartib bu yerda O'ZGARMAYDI.</b> Yangi bosqich doim OXIRIGA
 * qo'shiladi (server qoidasi), tartib esa ro'yxatda strelkalar bilan
 * o'zgartiriladi — `DutiesEditor` dagi bilan bir xil qaror.
 */
export function PipelineStageDialog({
  open,
  onClose,
  item,
}: {
  open: boolean
  onClose: () => void
  /** `null` — yangi bosqich. */
  item: PipelineStage | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveStage()

  const [name, setName] = useState('')
  const [note, setNote] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<{ field: string; code: string } | null>(null)

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setFieldError(null)
    save.reset()
    setName(item?.name ?? '')
    setNote(item?.note ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item])

  if (!open) return null

  const incomplete = name.trim() === ''

  const submit = () => {
    if (incomplete) return
    setFailure(null)
    setFieldError(null)

    save.mutate(
      { id: item?.id ?? null, body: { name: name.trim(), note: orNull(note) } },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = recruitingErrorCode(error)
          const field = RECRUITING_ERROR_FIELD[code]
          if (field === undefined) setFailure(code)
          else setFieldError({ field, code })
        },
      },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!save.isPending}
      title={item === null ? t('recruiting.stageCreate') : t('recruiting.stageEdit')}
      description={t('recruiting.stagesTitle')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={save.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="submit"
            form={FORM_ID}
            variant="primary"
            disabled={save.isPending || incomplete}
          >
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        <TextField
          label={t('catalog.fieldName')}
          placeholder={t('recruiting.stageNamePlaceholder')}
          autoComplete="off"
          value={name}
          error={fieldError?.field === 'name' ? errorMessage(fieldError.code) : undefined}
          disabled={save.isPending}
          onChange={(event) => setName(event.target.value)}
        />

        <TextArea
          label={t('catalog.fieldNote')}
          placeholder={t('catalog.fieldNotePlaceholder')}
          value={note}
          disabled={save.isPending}
          onChange={(event) => setNote(event.target.value)}
        />

        {/* Yangi yozuv doim faol yaratiladi — holat faqat mavjud yozuvda */}
        {item !== null && <StageActiveField item={item} disabled={save.isPending} />}
      </form>
    </Dialog>
  )
}

/**
 * Bosqichni faollashtirish/faolsizlantirish — FAQAT tahrirlash
 * oynasida va TASDIQLASH bilan.
 *
 * `CatalogActiveField` ning aynan naqshi va sababi ham bir xil:
 * ro'yxatdagi almashtirgich butun tizimga ta'sir qiladigan amalni bir
 * tegishda bajarardi. Bosqich esa o'nlab nomzodga bog'langan bo'ladi.
 *
 * ⚠️ Bosqichga xos qo'shimcha ma'no: faolsizlantirilgan bosqichda
 * TURGAN nomzodlar joyida qoladi va bosqich nomi ularning kartasida
 * ko'rinib turadi (server shunday qurilgan — aks holda tarix
 * «noma'lum bosqich» bo'lib qolardi). Faqat YANGI nomzodni u yerga
 * o'tkazib bo'lmaydi. Tasdiqlash matni ham aynan shuni aytadi.
 */
function StageActiveField({ item, disabled }: { item: PipelineStage; disabled: boolean }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const toggle = useToggleStage()

  /*
   * Holat shu yerda ham saqlanadi: oynaga berilgan `item` ro'yxat
   * bosilgan ondagi nusxa — server javobidan keyin u yangilanmaydi.
   * Mahalliy qiymat bo'lmasa, almashtirgich muvaffaqiyatli
   * o'zgarishdan keyin ortga qaytib ketardi.
   */
  const [current, setCurrent] = useState(item.isActive)
  const [asking, setAsking] = useState(false)

  useEffect(() => {
    setCurrent(item.isActive)
    setAsking(false)
    toggle.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item.id, item.isActive])

  const next = !current

  return (
    <>
      <div className="rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
        <Switch
          checked={current}
          disabled={disabled || toggle.isPending}
          onChange={() => setAsking(true)}
          label={t('catalog.statusTitle')}
          hint={t(current ? 'recruiting.stageActiveHint' : 'recruiting.stageInactiveHint')}
        />
      </div>

      <Dialog
        open={asking}
        onClose={() => setAsking(false)}
        size="sm"
        dismissible={!toggle.isPending}
        title={t(next ? 'catalog.confirmActivateTitle' : 'catalog.confirmDeactivateTitle')}
        footer={
          <>
            <Button type="button" onClick={() => setAsking(false)} disabled={toggle.isPending}>
              {t('common.cancel')}
            </Button>
            <Button
              type="button"
              variant={next ? 'primary' : 'danger'}
              disabled={toggle.isPending}
              onClick={() =>
                toggle.mutate(
                  { id: item.id, active: next },
                  {
                    onSuccess: () => {
                      setCurrent(next)
                      setAsking(false)
                    },
                  },
                )
              }
            >
              {toggle.isPending && <LoaderCircle className="animate-spin" />}
              {t(next ? 'catalog.activate' : 'catalog.deactivate')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-subhead text-[var(--fg-secondary)]">
            {t(
              next ? 'recruiting.stageActivateText' : 'recruiting.stageDeactivateText',
              { name: item.name },
            )}
          </p>

          {toggle.isError && (
            <Alert tone="danger" title={t('catalog.statusChangeFailed')}>
              {errorMessage(recruitingErrorCode(toggle.error))}
            </Alert>
          )}
        </div>
      </Dialog>
    </>
  )
}
