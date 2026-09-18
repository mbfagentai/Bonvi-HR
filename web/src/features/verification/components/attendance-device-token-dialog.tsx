import { Check, Copy } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Button, Dialog } from '@/shared/ui'

import { buildIngestUrl, type AttendanceDeviceCreated } from '../api/attendance-api'

/** «Nusxalandi» belgisi qancha turadi — ko'z ilg'ashi uchun yetarli, xalaqit bermaydi. */
const COPIED_MS = 2000

/**
 * Bir martalik sir — qaysi amal natijasida paydo bo'lgan.
 *
 * `rotated` da qo'shimcha ogohlantirish bor: eski token SHU ZAHOTI o'lgan
 * va terminal yangi manzil kiritilguncha o'tishlarni yubora olmaydi.
 */
export interface DeviceSecret extends AttendanceDeviceCreated {
  reason: 'created' | 'rotated'
}

/**
 * Qabul manzili va token — HAYOTDA BIR MARTA (SPEC §3.2).
 *
 * ⚠️ Token faqat shu oynaning xotirasida yashaydi: keshga yozilmaydi,
 * manzilga tushmaydi, localStorage'ga saqlanmaydi. Sahifa oynani yopishi
 * bilan `secret` ni `null` qiladi va oyna DOM'dan butunlay chiqadi —
 * yopilish animatsiyasi ham kutilmaydi, token ekranda bir kadr ham
 * ortiqcha turmasin.
 *
 * Fon bosilganda va Escape bilan YOPILMAYDI: tasodifiy bosish tokenni
 * qaytarib bo'lmas qilib yo'qotadi va terminalni sozlash uchun tokenni
 * yana almashtirishga (ya'ni ishlab turgan boshqa sozlamani buzishga)
 * majbur qiladi. Yopish — faqat ongli tugma.
 *
 * Manzil tayyor holda beriladi (`buildIngestUrl`): Hikvision'ning «HTTP
 * Listening» formasi to'liq URL kutadi va tokenni qo'lda yo'lga yopishtirish
 * — xato qilishning eng ishonchli usuli.
 */
export function AttendanceDeviceTokenDialog({
  secret,
  onClose,
}: {
  secret: DeviceSecret | null
  onClose: () => void
}) {
  const { t } = useLocale()

  if (secret === null) return null

  const url = buildIngestUrl(secret.token)

  return (
    <Dialog
      open
      onClose={onClose}
      size="md"
      dismissible={false}
      title={t('attendance.tokenTitle')}
      description={secret.device.name}
      footer={
        <Button type="button" variant="primary" onClick={onClose}>
          {t('attendance.tokenSavedClose')}
        </Button>
      }
    >
      <div className="space-y-5">
        <Alert tone="warning" title={t('attendance.tokenOnceWarning')}>
          {t('attendance.tokenOnceWarningText')}
        </Alert>

        {secret.reason === 'rotated' && (
          <Alert tone="danger">{t('attendance.tokenRotatedHint')}</Alert>
        )}

        <SecretField
          label={t('attendance.ingestUrl')}
          hint={t('attendance.ingestUrlHint')}
          value={url}
        />

        <SecretField
          label={t('attendance.tokenLabel')}
          hint={t('attendance.tokenHeaderHint')}
          value={secret.token}
        />
      </div>
    </Dialog>
  )
}

/**
 * Faqat o'qiladigan maydon + nusxalash tugmasi.
 *
 * NEGA `<input readOnly>`, oddiy matn emas: `navigator.clipboard` faqat
 * xavfsiz kontekstda (HTTPS yoki localhost) ishlaydi. Tizim IP manzil
 * orqali oddiy HTTP da ochilsa, tugma ishlamaydi — u holda matn o'zi
 * BELGILANADI va operator Ctrl+C bilan oladi. Belgilanadigan maydon
 * bo'lmasa, token ko'rinib turardi-yu, uni olishning yo'li qolmasdi.
 */
function SecretField({ label, hint, value }: { label: string; hint: string; value: string }) {
  const { t } = useLocale()
  const inputRef = useRef<HTMLInputElement>(null)
  const [state, setState] = useState<'idle' | 'copied' | 'failed'>('idle')

  useEffect(() => {
    if (state !== 'copied') return
    const timer = window.setTimeout(() => setState('idle'), COPIED_MS)
    return () => window.clearTimeout(timer)
  }, [state])

  const copy = async () => {
    try {
      // Xavfsiz bo'lmagan kontekstda `clipboard` umuman `undefined`
      if (navigator.clipboard === undefined) throw new Error('clipboard')
      await navigator.clipboard.writeText(value)
      setState('copied')
    } catch {
      inputRef.current?.focus()
      inputRef.current?.select()
      setState('failed')
    }
  }

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>

      <div className="flex items-center gap-2">
        <input
          ref={inputRef}
          readOnly
          value={value}
          aria-label={label}
          onFocus={(event) => event.currentTarget.select()}
          className={cn(
            'h-12 min-w-0 flex-1 rounded-md border border-[var(--border-strong)] px-3.5',
            'bg-[var(--bg-sunken)] font-mono text-footnote text-[var(--fg)] outline-none',
            'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10',
          )}
        />
        <Button
          type="button"
          variant={state === 'copied' ? 'soft' : 'secondary'}
          className="h-12 shrink-0"
          onClick={() => void copy()}
        >
          {state === 'copied' ? <Check /> : <Copy />}
          {state === 'copied' ? t('attendance.copied') : t('attendance.tokenCopy')}
        </Button>
      </div>

      <p
        role={state === 'failed' ? 'alert' : undefined}
        className={cn(
          'text-footnote',
          state === 'failed' ? 'text-ios-orange' : 'text-[var(--fg-tertiary)]',
        )}
      >
        {state === 'failed' ? t('attendance.copyFailed') : hint}
      </p>
    </div>
  )
}
