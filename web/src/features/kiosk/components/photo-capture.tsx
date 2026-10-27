import { Camera, LoaderCircle, RotateCcw, UserRound } from 'lucide-react'
import { useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Button } from '@/shared/ui'
import { downscalePhoto } from '../lib/kiosk-images'
import { FieldMessage, Label } from './kiosk-fields'

/**
 * Selfie for the «Rasm 3 × 4» box of the blank. `capture="user"` opens the
 * front camera on a phone or tablet; on a computer without a camera the same
 * input picks a file. The picture is downscaled before it is kept.
 * The blob lives in memory only — it is never written to session storage.
 */
export function PhotoCapture({
  photo,
  onChange,
  error,
}: {
  photo: Blob | null
  onChange: (photo: Blob | null) => void
  error?: string | undefined
}) {
  const { t } = useLocale()
  const input = useRef<HTMLInputElement>(null)
  const preview = useObjectUrl(photo)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  const accept = async (file: File | undefined) => {
    if (file === undefined) return

    setBusy(true)
    setFailed(false)

    try {
      onChange(await downscalePhoto(file))
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
      // The same file chosen again must fire `change` again.
      if (input.current) input.current.value = ''
    }
  }

  return (
    <div data-field="photo" className="space-y-2.5">
      <p className="text-body font-medium">
        <Label required>{t('kiosk.photo.label')}</Label>
      </p>

      <div className="flex items-center gap-4">
        <button
          type="button"
          disabled={busy}
          onClick={() => input.current?.click()}
          aria-label={photo === null ? t('kiosk.photo.take') : t('kiosk.photo.retake')}
          className={cn(
            'relative grid aspect-[3/4] w-28 shrink-0 place-items-center overflow-hidden rounded-xl border-2 border-dashed sm:w-32',
            error ? 'border-ios-red/60' : 'border-[var(--border-strong)]',
            preview !== null && 'border-solid border-[var(--border)]',
            'bg-[var(--bg-elevated)]',
          )}
        >
          {preview !== null ? (
            <img src={preview} alt={t('kiosk.photo.alt')} className="size-full object-cover" />
          ) : (
            <UserRound className="size-12 text-[var(--fg-tertiary)]" strokeWidth={1.5} />
          )}

          {busy && (
            <span className="absolute inset-0 grid place-items-center bg-black/30">
              <LoaderCircle className="size-8 animate-spin text-white" />
            </span>
          )}
        </button>

        <div className="flex min-w-0 flex-1 flex-col items-start gap-3">
          <p className="text-subhead text-[var(--fg-secondary)]">{t('kiosk.photo.hint')}</p>

          <Button
            type="button"
            variant={photo === null ? 'primary' : 'secondary'}
            size="lg"
            pill
            disabled={busy}
            onClick={() => input.current?.click()}
            className="h-[52px]"
          >
            {photo === null ? <Camera /> : <RotateCcw />}
            {photo === null ? t('kiosk.photo.take') : t('kiosk.photo.retake')}
          </Button>

          <input
            ref={input}
            type="file"
            accept="image/*"
            capture="user"
            className="sr-only"
            tabIndex={-1}
            onChange={(event) => void accept(event.target.files?.[0])}
          />
        </div>
      </div>

      <FieldMessage error={failed ? t('kiosk.photo.failed') : error} />
    </div>
  )
}
