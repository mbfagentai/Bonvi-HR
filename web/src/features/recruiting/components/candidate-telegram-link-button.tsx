import { Link2 } from 'lucide-react'
import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Button } from '@/shared/ui'
import { issueCandidateTelegramLink } from '../api/recruiting-api'

/**
 * A bot can't write to someone who never opened it, so HR passes this link on
 * (by phone message, or to the username from the form). Once it's opened the
 * pending invitation goes out by itself.
 */
export function CandidateTelegramLinkButton({ candidateId }: { candidateId: string }) {
  const { t } = useLocale()
  const [state, setState] = useState<'idle' | 'copied' | 'failed'>('idle')
  const [link, setLink] = useState<string | null>(null)

  async function copy() {
    let url = link
    try {
      url ??= await issueCandidateTelegramLink(candidateId)
      setLink(url)
      // The clipboard API works only in a secure context; the link is then shown to copy by hand.
      await navigator.clipboard.writeText(url)
      setState('copied')
    } catch {
      setState('failed')
    }
  }

  return (
    <span className="flex flex-wrap items-center gap-2">
      <Button type="button" size="sm" variant="secondary" onClick={() => void copy()}>
        <Link2 />
        {state === 'copied' ? t('recruiting.telegramLinkCopied') : t('recruiting.telegramLinkCopy')}
      </Button>
      {state === 'failed' &&
        (link === null ? (
          <span className="text-caption2 text-[var(--danger-text)]">{t('recruiting.telegramLinkFailed')}</span>
        ) : (
          <span className="select-all break-all text-caption2">{link}</span>
        ))}
    </span>
  )
}
