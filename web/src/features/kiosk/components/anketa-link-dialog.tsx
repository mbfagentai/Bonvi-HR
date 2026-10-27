import { Check, Copy, ExternalLink } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, Dialog } from '@/shared/ui'
import { hasFormOnlySite, publicFormUrl, tabletFormUrl } from '../lib/kiosk-site'

/** How long «Nusxa olindi» stays on the button. */
const COPIED_MS = 2000

/**
 * «Anketa havolasi» on the Nomzodlar page (SPEC §8): the link HR sends to
 * candidates, and the one-time link that turns the office tablet into tablet
 * mode. Both are on the form-only site when `VITE_ANKETA_URL` is set (SPEC
 * §2.3 item 1) — that site serves nothing but the form, so a tablet set up on
 * it cannot open HR pages at all; otherwise both are this site's `/anketa`.
 */
export function AnketaLinkDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useLocale()

  const link = publicFormUrl()
  const tabletLink = tabletFormUrl()

  return (
    <Dialog open={open} onClose={onClose} size="md" title={t('kiosk.link.title')} description={t('kiosk.link.subtitle')}>
      <div className="space-y-6">
        <LinkField label={t('kiosk.link.public')} url={link} openable />

        <div className="space-y-2">
          {/* No «open» here: opening it on HR's own computer would switch that browser to tablet mode. */}
          <LinkField label={t('kiosk.link.tablet')} url={tabletLink} />
          <p className="text-footnote text-[var(--fg-secondary)]">
            {hasFormOnlySite() ? t('kiosk.link.tabletHintFormSite') : t('kiosk.link.tabletHint')}
          </p>
        </div>
      </div>
    </Dialog>
  )
}

/**
 * Read-only address with copy and open buttons. `navigator.clipboard` works
 * only in a secure context — over plain HTTP the text is selected instead and
 * HR copies it with Ctrl+C (same as `mcp-section.tsx`).
 */
function LinkField({ label, url, openable = false }: { label: string; url: string; openable?: boolean }) {
  const { t } = useLocale()
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [copy, setCopy] = useState<'idle' | 'done' | 'failed'>('idle')

  useEffect(() => {
    if (copy !== 'done') return
    const timer = window.setTimeout(() => setCopy('idle'), COPIED_MS)
    return () => window.clearTimeout(timer)
  }, [copy])

  const onCopy = async () => {
    try {
      if (navigator.clipboard === undefined) throw new Error('clipboard')
      await navigator.clipboard.writeText(url)
      setCopy('done')
    } catch {
      inputRef.current?.focus()
      inputRef.current?.select()
      setCopy('failed')
    }
  }

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-footnote font-medium text-[var(--fg-secondary)]">
        {label}
      </label>

      <div className="flex items-center gap-2">
        <input
          id={id}
          ref={inputRef}
          readOnly
          value={url}
          spellCheck={false}
          className={cn(
            'tabular h-10 w-full min-w-0 rounded-md border px-3 text-subhead',
            'border-[var(--border)] bg-[var(--bg-sunken)] text-[var(--fg)]',
            'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10 focus:outline-none',
          )}
        />

        <Button type="button" variant="secondary" onClick={() => void onCopy()} className="shrink-0">
          {copy === 'done' ? <Check className="text-ios-green" /> : <Copy />}
          <span className="hidden sm:inline">{copy === 'done' ? t('kiosk.link.copied') : t('kiosk.link.copy')}</span>
          <span className="sr-only sm:hidden">{t('kiosk.link.copy')}</span>
        </Button>

        {openable && (
        <a
          href={url}
          target="_blank"
          rel="noopener noreferrer"
          aria-label={t('kiosk.link.open')}
          title={t('kiosk.link.open')}
          className={cn(
            'grid size-10 shrink-0 place-items-center rounded-md border border-[var(--border-strong)]',
            'bg-[var(--bg-elevated)] text-[var(--fg-secondary)] transition-colors hover:bg-[var(--bg-hover)]',
          )}
        >
          <ExternalLink className="size-[18px]" />
        </a>
        )}
      </div>

      {copy === 'failed' && (
        <p role="alert" className="text-footnote text-ios-orange">
          {t('kiosk.link.copyFailed')}
        </p>
      )}
    </div>
  )
}
