import { useTranslation } from 'react-i18next'

import type { AttendanceMarkDto } from '@/api/types'
import { MARK_SOURCE_KEY } from '@/data/status-look'
import { useFormat } from '@/lib/format'
import { Icon } from './icon'
import { Skeleton } from './feedback'

/**
 * Bitta o'tish qatori — Verifix'ning «Qaydnoma» qatori, lekin ikki
 * sirli nishoncha o'rniga SO'Z bilan (UX §E.4).
 *
 * Yo'nalish: kirish yashil, chiqish qizil; yonida so'zning o'zi.
 * Manba: `Turniket` yoki `Mobil ilova` — HR jurnalidagi so'z bilan
 * bir xil, chunki ikkalasi bitta yozuv haqida gapiradi.
 */
export function MarkRow({ mark }: { mark: AttendanceMarkDto }) {
  const { t } = useTranslation()
  const format = useFormat()

  const isIn = mark.direction === 'In'

  return (
    <div className="flex min-h-[52px] items-center border-t border-border py-2 first:border-t-0">
      <Icon
        name={isIn ? 'arrow-right-on-rectangle' : 'arrow-left-on-rectangle'}
        className={isIn ? 'mr-3 text-hue-success' : 'mr-3 text-hue-absent'}
      />

      <div className="min-w-0 flex-1">
        <div className="text-body text-fg">{isIn ? t('mark.in') : t('mark.out')}</div>
        <div className="mt-0.5 flex min-w-0 items-center text-footnote text-fg-secondary">
          <span className="truncate">{t(MARK_SOURCE_KEY[mark.source])}</span>
          {mark.siteName ? (
            <span className="ml-1 truncate server-text">· {mark.siteName}</span>
          ) : null}
        </div>
      </div>

      <div className="nums ml-3 shrink-0 text-body text-fg">{format.time(mark.occurredAt)}</div>
    </div>
  )
}

export function MarkRowSkeleton() {
  return (
    <div className="flex min-h-[52px] items-center border-t border-border py-2 first:border-t-0">
      <Skeleton className="mr-3 h-6 w-6 rounded-pill" />
      <div className="flex-1 space-y-2">
        <Skeleton className="h-4 w-20" />
        <Skeleton className="h-3 w-28" />
      </div>
      <Skeleton className="h-4 w-12" />
    </div>
  )
}
