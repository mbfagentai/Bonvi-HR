import { Building2, ChevronDown, ChevronUp, Users } from 'lucide-react'
import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Button,
  Card,
  CardContent,
  CardHeader,
  EmptyState,
  SegmentedControl,
  Skeleton,
} from '@/shared/ui'
import type { DashboardBreakdownItem } from '../api/dashboard-api'
import { useDashboardBreakdown } from '../api/dashboard-queries'

/** Kesim: xodimlar bo'lim bo'yicha yoki yo'nalish bo'yicha. */
type BreakdownView = 'departments' | 'directions'

/**
 * Boshida ko'rinadigan qatorlar soni.
 *
 * 8 — ikki ustunda to'liq to'rt qator. Undan ko'pi kartani bosh
 * sahifadagi qolgan hamma narsadan balandroq qilib yuborardi: bu karta
 * «eng kattalari kim» degan savolga javob beradi, to'liq ma'lumotnoma
 * emas — to'liq ro'yxat uchun ma'lumotnoma sahifalari bor.
 */
const PREVIEW_COUNT = 8

/**
 * Xodimlar taqsimoti — bo'lim va yo'nalish kesimi.
 *
 * NEGA DIAGRAMMA EMAS, RO'YXAT. Bo'lim o'nlab bo'lishi mumkin. Pirogda
 * o'nta bo'lakning yarmi nomi sig'maydigan yupqa tilimga aylanadi,
 * vertikal ustunlarda esa nomlar yonboshlab yoziladi va o'qib bo'lmaydi.
 * Gorizontal «bar list» da esa nom oddiy matn qatori bo'lib qoladi,
 * uzunligi ixtiyoriy — va bu iOS'ning ro'yxat naqshiga ham mos tushadi.
 *
 * NEGA `recharts` EMAS: bu chizma uchun kutubxona ortiqcha — ikkita
 * `div` va bitta foiz yetadi. Kutubxona bosh sahifada boshqa joyda,
 * vaqt o'qi bo'ylab o'zgarishni ko'rsatadigan grafikda ishlatiladi;
 * u yerda hisob-kitob haqiqatan ham murakkab.
 */
export function EmployeeBreakdownCard() {
  const { t, num } = useLocale()
  const query = useDashboardBreakdown()

  const [view, setView] = useState<BreakdownView>('departments')
  const [expanded, setExpanded] = useState(false)

  const items = query.data?.[view] ?? []

  /**
   * Umumiy xodimlar soni — BO'LIMLAR ustunlarining yig'indisi.
   *
   * NEGA `DashboardStats.activeEmployees` emas: shartnoma bo'yicha bu
   * ikki son teng, lekin kartani `/stats` ga bog'lash uni davrga ham
   * bog'lab qo'yardi — davr almashtirgichi bosilganda foizlar hech
   * qanday sababsiz qayta hisoblanardi. Yig'indi shu javobning o'z
   * ichida, ya'ni karta o'zini o'zi ta'minlaydi.
   */
  const total = (query.data?.departments ?? []).reduce((sum, item) => sum + item.count, 0)

  /**
   * Ustun uzunligining shkalasi — ro'yxatdagi ENG KATTA qiymat.
   *
   * NEGA umumiy sondan emas: 540 xodim 30 ta bo'limga tarqalganda eng
   * kattasi ham 12% chiqadi va hamma ustunlar bir xil qisqa chiziqchaga
   * aylanadi — ya'ni chizma hech narsa ko'rsatmaydi. Eng kattasiga
   * nisbatan olinganda esa u butun kenglikni egallaydi va qolganlari
   * unga nisbatan o'qiladi. Bu qoida IKKALA kesim uchun ham bir xil:
   * almashtirgich bosilganda ustunning MA'NOSI o'zgarib ketmasligi kerak.
   *
   * Ro'yxat serverda ko'pdan kamga tartiblangan — eng kattasi birinchi.
   */
  const max = items[0]?.count ?? 0

  const visible = expanded ? items : items.slice(0, PREVIEW_COUNT)
  const hidden = items.length - visible.length

  /**
   * Kesim almashganda ro'yxat yana yig'iladi.
   *
   * Aks holda «Yo'nalishlar» da 30 tasini ochib, «Bo'limlar» ga
   * qaytgan odam u yerda ham ochilgan uzun ro'yxatni ko'rardi —
   * holat bir ro'yxatdan boshqasiga sizib o'tardi.
   */
  function changeView(next: BreakdownView) {
    setView(next)
    setExpanded(false)
  }

  return (
    <Card>
      <CardHeader
        title={t('dashboard.breakdownTitle')}
        subtitle={
          view === 'departments'
            ? t('dashboard.breakdownDepartmentHint')
            : /*
                Yo'nalish kesimining izohi ATAYLAB boshqacha: bu yerda
                yig'indi xodimlar sonidan oshib ketishi mumkin va buni
                ekrandan tushuntirmasa, HR raqamlarni qo'shib ko'rib
                «xato bor» deb o'ylaydi.
              */
              t('dashboard.breakdownDirectionHint')
        }
        action={
          <SegmentedControl
            size="sm"
            value={view}
            onChange={changeView}
            aria-label={t('dashboard.breakdownViewLabel')}
            options={[
              { value: 'departments', label: t('nav.departments') },
              { value: 'directions', label: t('nav.directions') },
            ]}
          />
        }
      />

      <CardContent>
        {query.isPending && <BreakdownSkeleton />}

        {/*
          Xato holatida `Alert` EMAS, oddiy qator. Sahifaning yuqorisida
          `/stats` uchun allaqachon qizil banner turibdi va ikkinchi
          banner bir xil nosozlikni ikki marta e'lon qilardi. Taqsimot —
          yordamchi kesim: u yuklanmasa ish to'xtamaydi.
        */}
        {query.isError && (
          <p className="text-footnote text-[var(--fg-secondary)]">
            {t('dashboard.loadFailed')}
          </p>
        )}

        {query.data && items.length === 0 && (
          <EmptyState
            icon={view === 'departments' ? Building2 : Users}
            title={t('dashboard.breakdownEmpty')}
            description={t('dashboard.breakdownEmptyText')}
            className="py-10"
          />
        )}

        {items.length > 0 && (
          <>
            {/*
              `key={view}` — kesim almashganda ro'yxat butunlay qayta
              quriladi. Ikki ro'yxatning kalitlari (`id`) mutlaqo boshqa
              to'plam, React esa ularni juftlashtirmoqchi bo'lib
              qatorlarni aralashtirib yuborardi.

              Ikki ustun `md` dan boshlab: bitta ustunda ustunlar ekran
              kengligicha cho'zilib, 13px'lik nom chap chekkada yolg'iz
              qolardi. Ikkala ustun BIR XIL shkalada (`max`) — ya'ni
              ustunlar orasidagi taqqoslash ham to'g'ri qoladi.
            */}
            <ul key={view} className="grid gap-x-10 gap-y-3.5 md:grid-cols-2">
              {visible.map((item) => (
                <BreakdownRow
                  key={item.id ?? 'unassigned'}
                  item={item}
                  max={max}
                  /*
                    Foiz FAQAT bo'lim kesimida. Yo'nalishda bitta xodim
                    bir nechta yo'nalishga tegishli bo'lishi mumkin, ya'ni
                    ustunlar yig'indisi xodimlar sonidan oshadi va
                    «umumiy xodimning 18%i» degan yozuv oddiygina YOLG'ON
                    bo'lardi. Boshqa maxraj tanlash (yig'indining o'zi)
                    ham yordam bermaydi: u «yo'nalish biriktirmalarining
                    18%i» degani bo'lardi va buni ekranda tushuntirib
                    bo'lmaydi. Shu sababli u yerda faqat SON qoladi —
                    taqqoslashni ustunning o'zi bajaradi.
                  */
                  percent={view === 'departments' && total > 0 ? item.count / total : undefined}
                  format={num}
                />
              ))}
            </ul>

            {(hidden > 0 || expanded) && (
              <Button
                variant="ghost"
                size="sm"
                className="mt-4 w-full"
                onClick={() => setExpanded((previous) => !previous)}
              >
                {expanded ? (
                  <>
                    <ChevronUp />
                    {t('dashboard.breakdownLess')}
                  </>
                ) : (
                  <>
                    <ChevronDown />
                    {t('dashboard.breakdownMore', { n: num(hidden) })}
                  </>
                )}
              </Button>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * Bitta qator: nom · son · proporsional chiziq.
 *
 * Son va nom BIR qatorda, ustun esa ularning ostida — ya'ni ko'z avval
 * matnni o'qiydi, keyin ustunlarni chapdan pastga qarab yuguritib
 * chiqadi. Ustunni matn bilan bir qatorga qo'yish (nom | ustun | son)
 * sinab ko'rildi va tashlandi: uzun bo'lim nomlari ustunni siqib
 * qo'yadi, natijada qatorlarning shkalasi bir xil bo'lmaydi.
 */
function BreakdownRow({
  item,
  max,
  percent,
  format,
}: {
  item: DashboardBreakdownItem
  max: number
  /** 0…1 oralig'ida. `undefined` — foiz ko'rsatilmaydi (izohi chaqiruv joyida). */
  percent: number | undefined
  format: (value: number) => string
}) {
  /*
    «Belgilanmagan» — bo'lim emas, ma'lumotdagi bo'shliq. Uni tizim tinti
    bilan bo'yash uni haqiqiy bo'limlar bilan tenglashtirardi, holbuki bu
    qator aslida «bularni kimdir to'ldirishi kerak» degani.
  */
  const unassigned = item.id === null

  /*
    Eng kichik qiymat ham KO'RINADI: 540 dan 1 xodim 0,2% beradi va
    ustun umuman chizilmasdi — qator «nol» ga o'xshab qolardi.
  */
  const width = max > 0 ? Math.max(2, Math.round((item.count / max) * 100)) : 0

  return (
    <li>
      <div className="flex items-baseline justify-between gap-3">
        <span
          className={cn(
            'min-w-0 truncate text-footnote',
            unassigned && 'text-[var(--fg-secondary)] italic',
          )}
          title={item.name}
        >
          {item.name}
        </span>

        <span className="flex shrink-0 items-baseline gap-1.5">
          <span className="tabular text-footnote font-semibold">{format(item.count)}</span>
          {percent !== undefined && (
            <span className="tabular text-caption2 text-[var(--fg-tertiary)]">
              {Math.round(percent * 100)}%
            </span>
          )}
        </span>
      </div>

      {/*
        Chiziqning o'zi `aria-hidden`: uning butun mazmuni yuqoridagi
        matnda allaqachon bor. Ekran o'qigichga ikkinchi marta aytish
        faqat shovqin bo'lardi.
      */}
      <div
        aria-hidden
        className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[var(--bg-sunken)]"
      >
        <div
          className={cn(
            'h-full rounded-full',
            'transition-[width] duration-300 ease-[var(--ease-ios-out)]',
            unassigned ? 'bg-[var(--fg-tertiary)]' : 'bg-[var(--accent)]',
          )}
          style={{ width: `${width}%` }}
        />
      </div>
    </li>
  )
}

/**
 * Birinchi yuklash.
 *
 * Skelet qatorlari haqiqiy qatorning shakliga takrorlanadi (matn +
 * ustun), shu sababli ma'lumot kelganda karta balandligi sakramaydi.
 * Nom kengliklari ataylab har xil — bir xil bo'lsa jadvalga o'xshab
 * qoladi va «yuklanmoqda» degan taassurot yo'qoladi.
 */
function BreakdownSkeleton() {
  const widths = ['w-28', 'w-36', 'w-24', 'w-32', 'w-40', 'w-24', 'w-32', 'w-28']

  return (
    <div className="grid gap-x-10 gap-y-3.5 md:grid-cols-2">
      {widths.map((width, index) => (
        <div key={index}>
          <Skeleton className={cn('h-3.5', width)} />
          <Skeleton className="mt-1.5 h-1.5 rounded-full" />
        </div>
      ))}
    </div>
  )
}
