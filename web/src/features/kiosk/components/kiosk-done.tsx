import { CircleCheckBig, RotateCcw, Send } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { buttonVariants, Button } from '@/shared/ui'
import { QrCode } from './qr-code'

const TELEGRAM_RETURN_SECONDS = 90

/**
 * «Rahmat!» — the number lets HR match the form to the person.
 *
 * On a personal device the screen stays (`returnAfter = null`) and says HR
 * will get in touch; a quiet link starts a new form. On the office tablet it
 * asks to hand the tablet back and returns to the start on its own.
 */
export function KioskDone({
  formNumber,
  telegramLink,
  returnAfter,
  onFinish,
}: {
  formNumber: number
  /** The link that lets the bot reach this person; `null` — nothing to ask. */
  telegramLink: string | null
  /** Seconds before returning to the start screen; `null` — stay. */
  returnAfter: number | null
  onFinish: () => void
}) {
  const { t } = useLocale()
  // Scanning takes longer than reading «thank you»; the tablet waits for it.
  const [left, setLeft] = useState(() =>
    returnAfter === null ? 0 : telegramLink === null ? returnAfter : Math.max(returnAfter, TELEGRAM_RETURN_SECONDS),
  )

  useEffect(() => {
    if (returnAfter === null) return
    const timer = window.setInterval(() => setLeft((value) => Math.max(0, value - 1)), 1000)
    return () => window.clearInterval(timer)
  }, [returnAfter])

  useEffect(() => {
    if (returnAfter !== null && left === 0) onFinish()
  }, [returnAfter, left, onFinish])

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center bg-[var(--bg)] px-6 pt-[max(2.5rem,env(safe-area-inset-top))] pb-[max(2.5rem,env(safe-area-inset-bottom))] text-center">
      <span className="grid size-24 place-items-center rounded-full bg-ios-green/12">
        <CircleCheckBig className="size-14 text-ios-green" strokeWidth={1.8} />
      </span>

      <h1 className="mt-7 text-largetitle font-semibold tracking-tight">{t('kiosk.done.title')}</h1>
      <p className="mt-3 text-title3 text-[var(--fg-secondary)]">{t('kiosk.done.text')}</p>

      <p className="mt-7 rounded-2xl bg-[var(--bg-elevated)] px-8 py-4 text-title2 font-semibold tabular shadow-card">
        {t('kiosk.done.number', { n: formNumber })}
      </p>

      {telegramLink !== null && (
        <section className="mt-7 flex w-full max-w-sm flex-col items-center rounded-2xl bg-[var(--bg-elevated)] p-5 shadow-card">
          <h2 className="text-headline font-semibold">{t('kiosk.done.telegramTitle')}</h2>
          <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">{t('kiosk.done.telegramText')}</p>

          {/*
            The office tablet shows only the code: opening the link there would
            bind the tablet's own Telegram account to this person.
          */}
          {returnAfter === null ? (
            <>
              <a
                href={telegramLink}
                target="_blank"
                rel="noreferrer"
                className={buttonVariants({ variant: 'primary', size: 'lg', pill: true, className: 'mt-4 w-full' })}
              >
                <Send />
                {t('kiosk.done.telegramOpen')}
              </a>
              <QrCode value={telegramLink} className="mt-4 hidden size-40 sm:block" />
            </>
          ) : (
            <>
              <QrCode value={telegramLink} className="mt-4 size-52" />
              <p className="mt-3 text-footnote text-[var(--fg-secondary)]">{t('kiosk.done.telegramScan')}</p>
            </>
          )}
        </section>
      )}

      {returnAfter === null ? (
        <>
          <p className="mt-7 max-w-sm text-body text-[var(--fg-secondary)]">{t('kiosk.done.contact')}</p>
          <Button type="button" variant="ghost" size="lg" pill onClick={onFinish} className="mt-6">
            <RotateCcw />
            {t('kiosk.done.again')}
          </Button>
        </>
      ) : (
        <>
          <p className="mt-7 max-w-sm text-body text-[var(--fg-secondary)]">{t('kiosk.done.returnTablet')}</p>
          <Button type="button" variant="secondary" size="lg" pill onClick={onFinish} className="mt-7 h-14 min-w-56">
            {t('kiosk.done.finish', { n: left })}
          </Button>
        </>
      )}
    </div>
  )
}
