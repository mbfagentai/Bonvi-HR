import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Switch } from '@/shared/ui'
import { useToggleCatalogItem } from '../api/catalog-queries'
import type { CatalogKind } from '../api/catalog-api'
import { catalogErrorCode } from '../lib/catalog-error'

/**
 * Yozuvni faollashtirish/faolsizlantirish — FAQAT tahrirlash oynasida.
 *
 * NEGA ro'yxatda emas: ro'yxatdagi almashtirgich butun tizim bo'ylab
 * ta'sir qiladigan amalni bir tegishda bajarardi — barmoq tekkanini ham
 * sezmay qolish mumkin edi. Ma'lumotnoma yozuvi esa o'nlab xodim
 * kartasiga bog'langan bo'ladi: uni tasodifan o'chirib qo'yish qimmat.
 * Endi yo'l uzunroq: yozuvni ochish → almashtirgich → tasdiqlash oynasi.
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
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const toggle = useToggleCatalogItem(kind)

  /*
   * Holat shu yerda ham saqlanadi: oynaga berilgan `item` ro'yxat bosilgan
   * ondagi nusxa — server javobidan keyin u yangilanmaydi. Mahalliy qiymat
   * bo'lmasa, almashtirgich muvaffaqiyatli o'zgarishdan keyin ortga qaytib
   * ketardi.
   */
  const [current, setCurrent] = useState(active)
  const [asking, setAsking] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  useEffect(() => {
    setCurrent(active)
    setAsking(false)
    setFailure(null)
  }, [id, active])

  const next = !current

  const apply = async () => {
    setFailure(null)
    try {
      await toggle.mutateAsync({ id, active: next })
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
          disabled={disabled || toggle.isPending}
          onChange={() => setAsking(true)}
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
        size="sm"
        dismissible={!toggle.isPending}
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
              disabled={toggle.isPending}
            >
              {t('common.cancel')}
            </Button>

            {/* Faolsizlantirish — qaytariladigan, lekin sezilarli amal:
                qizil emas, lekin asosiy tugma o'rnida turadi */}
            <Button
              type="button"
              variant={next ? 'primary' : 'danger'}
              onClick={() => void apply()}
              disabled={toggle.isPending}
            >
              {toggle.isPending && <LoaderCircle className="animate-spin" />}
              {toggle.isPending
                ? t('catalog.statusChanging')
                : next
                  ? t('catalog.activate')
                  : t('catalog.deactivate')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-subhead text-[var(--fg-secondary)]">
            {next
              ? t('catalog.confirmActivateText', { name })
              : t('catalog.confirmDeactivateText', { name })}
          </p>

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
