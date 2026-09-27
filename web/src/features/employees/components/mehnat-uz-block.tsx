import { ExternalLink } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'

/**
 * mehnat.uz — profil havolasi.
 *
 * <b>Skrinshot endi bu yerda emas (2026-09-27).</b> Ilgari blokda bitta
 * skrinshot ham turardi va yangisi eskisini almashtirardi. HR uchun bitta
 * yuklama yetmay qoldi — dalillar endi kartaning «Materiallar» ro'yxatida
 * (`EmployeeCheckFiles`) va eski skrinshot o'sha ro'yxatning birinchi
 * qatoriga ko'chirildi. Ro'yxat mehnat.uz ga bog'lanmagan: qarzdorlik yoki
 * sudlanganlik ma'lumotnomasi ham o'sha yerda.
 */

export function MehnatUzBlock({
  url,
}: {
  /** `null` — havola kiritilmagan. Tahrirlash kartaning formasida. */
  url: string | null
}) {
  const { t } = useLocale()

  return (
    <div className="flex items-start gap-2.5 rounded-md bg-[var(--bg-sunken)] px-3 py-2.5">
      <ExternalLink className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />

      <div className="min-w-0 flex-1">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">
          {t('employees.mehnatUzLink')}
        </p>

        {url === null ? (
          <p className="mt-0.5 text-subhead text-[var(--fg-tertiary)]">
            {t('employees.mehnatUzNoLink')}
          </p>
        ) : (
          /*
            `noopener noreferrer` — yangi tabda ochilgan sahifa bizning
            oynamizga (`window.opener`) tegmasligi uchun. Havola matni
            ATAYLAB to'liq manzil emas: u ekranni yorib yuborardi.
          */
          <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            title={url}
            className="mt-0.5 inline-flex items-center gap-1.5 text-subhead text-[var(--accent-text)] hover:underline"
          >
            {t('employees.mehnatUzOpenProfile')}
            <ExternalLink className="size-3.5" strokeWidth={2.2} />
          </a>
        )}
      </div>
    </div>
  )
}
