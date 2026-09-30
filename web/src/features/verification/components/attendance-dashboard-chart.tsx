import {
  CartesianGrid,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  type PieLabelRenderProps,
} from 'recharts'

import type { LocaleCode } from '@/shared/i18n/locales'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Skeleton } from '@/shared/ui'

import type { DashboardStatus, TrendDay } from '../api/attendance-api'
import {
  DASHBOARD_STATUSES,
  DASHBOARD_STATUS_LOOK,
  formatPercent,
} from '../lib/attendance-presentation'

/**
 * Davomat statistikasining diagrammasi va legendasi.
 *
 * ⭐ RANGLAR `var(--color-ios-…)` ko'rinishida to'g'ridan-to'g'ri SVG ga
 * beriladi — JS da o'qilmaydi. Sabab va brauzerdagi tekshiruv
 * `reports/components/turnover-chart.tsx` boshida: tungi rejim almashganda
 * SVG o'zi qayta bo'yaladi, qayta render kerak emas.
 */

/**
 * Bu komponent chiza oladigan turlar.
 *
 * ⚠️ `DashboardView` dagi uchinchi qiymat — `list` — bu yerda ATAYLAB
 * yo'q: unda diagramma umuman chizilmaydi va sahifa komponentni
 * chaqirmaydi ham (`attendance-presentation.ts` dagi izoh). Tip shuni
 * kompilyatsiyada ushlab turadi.
 */
export type DashboardChartType = 'donut' | 'bars'

/** Bitta holat: sanoq va ulush (`null` — xodim yo'q, «—»). */
export interface DashboardSlice {
  status: DashboardStatus
  count: number
  percent: number | null
}

/** recharts o'qiydigan ko'rinish — `fill` va `fillOpacity` bo'lakka o'zi tushadi. */
interface ChartDatum {
  status: DashboardStatus
  name: string
  value: number
  fill: string
  fillOpacity: number
  percentText: string
  /** Ustun oxiridagi yozuv: «75 · 47,8%». */
  summary: string
  /** Jadval aynan shu holat bo'yicha filtrlangan. */
  selected: boolean
  /** Boshqa holat tanlangan — bu band so'nadi. */
  dimmed: boolean
}

/**
 * Diagramma balandligi — ikkala turda BIR XIL: tur almashganda legenda va
 * jadval sakramasin.
 */
const CHART_HEIGHT = 'h-[260px]'

/** Modul darajasida — har renderda yangi obyekt recharts o'qini qayta chizdirardi. */
const AXIS_TICK = { fill: 'var(--fg-secondary)', fontSize: 12 } as const

/** Tanlanmagan bo'lak shu shaffoflikda — rangi saqlanadi, e'tibor tanlanganga o'tadi. */
const DIMMED_OPACITY = 0.3

/**
 * Izoh oynasining qatlami.
 *
 * ⚠️ recharts oynani `position: absolute` bilan chizadi, lekin `z-index`
 * BERMAYDI. Doiraviy diagrammaning markazidagi jami («966 xodim») esa
 * HTML qatlam va DOM da oynadan KEYIN keladi — ya'ni sichqoncha doira
 * ustiga borganda izoh o'sha yozuvning TAGIGA tushib qolardi (egasi
 * 2026-09-18 da ushlagan). Qiymat markaz qatlamidan yuqori bo'lsa yetadi;
 * uchala diagrammada bir xil — keyin ustma-ust qatlam qo'shilsa ham
 * izoh doim tepada qolsin.
 */
const TOOLTIP_WRAPPER = { zIndex: 20 } as const

/*
 * ⚠️ ANIMATSIYA ATAYLAB O'CHIQ (bo'lak ham, ustun ham).
 *
 * Sahifa bugungi kunda 30 soniyada yangilanadi va har yangi javob — yangi
 * massiv. recharts har safar animatsiyani qaytadan boshlaydi va uning
 * davomida son yorliqlarini YASHIRADI (~2 soniya). Ya'ni har yarim daqiqada
 * raqamlar miltillab yo'qolib turardi — aynan SPEC §8.5 dagi «no spinner
 * churn» taqiqlagan narsa. Tinch, darhol chizilgan diagramma bu ekranda
 * bezakli aylanishdan qimmatroq.
 */

/** Bo'laklar orasidagi tirqish, gradusda. */
const PADDING_ANGLE = 1.5

/** Bir tomondagi ikki son yorlig'i orasidagi eng kichik vertikal masofa, px. */
const LABEL_GAP = 13

export function DashboardChart({
  type,
  slices,
  total,
  active,
  onToggle,
}: {
  type: DashboardChartType
  slices: readonly DashboardSlice[]
  total: number
  /** Jadval shu holat bo'yicha filtrlangan (`null` — filtr yo'q). */
  active: DashboardStatus | null
  onToggle: (status: DashboardStatus) => void
}) {
  const { t, num, locale } = useLocale()

  const data: ChartDatum[] = slices.map((slice) => {
    const look = DASHBOARD_STATUS_LOOK[slice.status]
    const percentText = formatPercent(slice.percent, locale)

    return {
      status: slice.status,
      name: t(look.legendKey),
      value: slice.count,
      fill: look.fill,
      fillOpacity: active === null || active === slice.status ? 1 : DIMMED_OPACITY,
      percentText,
      summary: `${num(slice.count)} · ${percentText}`,
      selected: active === slice.status,
      dimmed: active !== null && active !== slice.status,
    }
  })

  return type === 'donut' ? (
    <StatusDonut data={data} total={total} onToggle={onToggle} />
  ) : (
    <StatusBars data={data} onToggle={onToggle} />
  )
}

/**
 * Doiraviy diagramma: markazda jami, tashqarida har bo'lakning soni
 * yetaklovchi chiziq bilan (Verifix'dagi rasm).
 */
function StatusDonut({
  data,
  total,
  onToggle,
}: {
  data: readonly ChartDatum[]
  total: number
  onToggle: (status: DashboardStatus) => void
}) {
  const { t, num } = useLocale()

  /*
    Nol sanoqli holat diagrammaga TUSHMAYDI (legendada esa qoladi): nol
    burchakli bo'lak ham kontur va yorliq chizib, doira ustida ortiqcha
    chiziqcha qoldirardi. Bosilgan bo'lakning indeksi shu qisqa ro'yxatga
    tegishli — shu sababli holat ham undan olinadi.
  */
  const visible = data.filter((datum) => datum.value > 0)

  /*
    Bitta holat butun doirani egallasa (masalan jadval hali biriktirilmagan
    kunda — hamma «Jadval yo'q»), tirqish, burchak yumaloqligi va kontur
    O'CHIRILADI: 360° li bo'lakda ular boshlanish nuqtasida ko'ndalang
    «yoriq» chizib qo'yardi.
  */
  const whole = visible.length === 1

  const paddingAngle = whole ? 0 : PADDING_ANGLE
  const midAngles = sliceMidAngles(
    visible.map((datum) => datum.value),
    paddingAngle,
  )

  /*
    Yorliqlar joylashuvi shu render uchun BIR MARTA hisoblanadi (birinchi
    yorliq chizilganda — radius va markaz faqat recharts dan keladi) va
    qolgan yorliqlar shu natijadan o'qiydi. Kichik qo'shni bo'laklar
    («2», «1», «2») aks holda bir-birining ustiga yozilib qolardi.
  */
  const layout: { key: string; points: readonly LabelPoint[] } = { key: '', points: [] }

  const renderLabel = (props: PieLabelRenderProps) => {
    const value = Number(props.value)
    const midAngle = props.midAngle
    if (!(value > 0) || midAngle === undefined) return null

    const cx = Number(props.cx)
    const cy = Number(props.cy)
    const radius = Number(props.outerRadius)

    const key = `${cx}:${cy}:${radius}`
    if (layout.key !== key) {
      layout.points = layoutLabels(midAngles, cx, cy, radius)
      layout.key = key
    }

    const point = layout.points[props.index]
    if (point === undefined) return null

    const cos = Math.cos((-midAngle * Math.PI) / 180)
    const sin = Math.sin((-midAngle * Math.PI) / 180)
    const right = point.side === 1

    // Chiziq bo'lak chetidan tashqariga, keyin gorizontal «tirsak» —
    // yorliq doiraga yopishmaydi va matn doim to'g'ri o'qiladi
    const startX = cx + (radius + 2) * cos
    const startY = cy + (radius + 2) * sin
    const endX = point.elbowX + (right ? 8 : -8)

    return (
      <g>
        <path
          d={`M${startX},${startY}L${point.elbowX},${point.elbowY}L${endX},${point.elbowY}`}
          fill="none"
          stroke={typeof props.fill === 'string' ? props.fill : 'var(--border-strong)'}
          strokeWidth={1}
        />
        <text
          x={endX + (right ? 3 : -3)}
          y={point.elbowY}
          dy={4}
          textAnchor={right ? 'start' : 'end'}
          fill="var(--fg-secondary)"
          fontSize={11}
          fontWeight={600}
          className="tabular"
        >
          {num(value)}
        </text>
      </g>
    )
  }

  return (
    <div
      className={cn(
        'relative',
        CHART_HEIGHT,
        '[&_.recharts-sector]:cursor-pointer [&_.recharts-sector]:outline-none',
      )}
    >
      <ResponsiveContainer width="100%" height="100%">
        {/*
          Yon chekkalar keng: bo'lak sonlari doiradan TASHQARIDA turadi va tor
          ekranda (telefon) ular karta chetidan kesilib qolmasligi kerak.
        */}
        <PieChart margin={{ top: 20, right: 36, bottom: 20, left: 36 }}>
          {total > 0 ? (
            <Pie
              data={visible}
              dataKey="value"
              nameKey="name"
              innerRadius="60%"
              outerRadius="82%"
              paddingAngle={paddingAngle}
              cornerRadius={whole ? 0 : 3}
              // Bo'laklar orasidagi ingichka tirqish — karta rangida, ya'ni
              // tungi rejimda ham fon bilan birga almashadi
              stroke={whole ? 'none' : 'var(--bg-elevated)'}
              strokeWidth={whole ? 0 : 2}
              label={renderLabel}
              labelLine={false}
              isAnimationActive={false}
              onClick={(_, index) => {
                const datum = visible[index]
                if (datum !== undefined) onToggle(datum.status)
              }}
            />
          ) : (
            /*
              Xodim yo'q — kulrang BO'SH halqa: diagrammaning joyi va shakli
              saqlanadi, markazda «0». Butunlay bo'sh maydon esa «yuklanmay
              qoldi» deb o'qilardi.
            */
            <Pie
              data={EMPTY_RING}
              dataKey="value"
              innerRadius="60%"
              outerRadius="82%"
              fill="var(--bg-sunken)"
              stroke="none"
              isAnimationActive={false}
            />
          )}

          {total > 0 && (
            <Tooltip
              wrapperStyle={TOOLTIP_WRAPPER}
              content={(props) => {
                const entry = props.active ? props.payload?.[0]?.payload : undefined
                return isChartDatum(entry) ? <ChartTooltip datum={entry} /> : null
              }}
            />
          )}
        </PieChart>
      </ResponsiveContainer>

      {/* Markazdagi jami — SVG emas, HTML: shrift va tungi rejim tokenlari o'zi ishlaydi */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 z-0 grid place-items-center text-center"
      >
        <div>
          <p className="tabular text-title1 leading-none font-semibold tracking-tight">
            {num(total)}
          </p>
          <p className="mt-1 text-caption1 text-[var(--fg-secondary)]">
            {t('attendance.statsEmployees')}
          </p>
        </div>
      </div>
    </div>
  )
}

const EMPTY_RING = [{ value: 1 }]

interface LabelPoint {
  /** `1` — o'ng yarim, `-1` — chap yarim (yorliq qaysi tomonga yoziladi). */
  side: 1 | -1
  elbowX: number
  elbowY: number
}

/**
 * Har bir bo'lakning o'rta burchagi — recharts dagi AYNAN o'sha formula
 * (0° dan 360° gacha, nol bo'lmagan har bo'lakdan keyin tirqish). Yorliqlar
 * joylashuvini oldindan hisoblash uchun kerak: recharts ularni birma-bir,
 * bir-biridan bexabar chizadi.
 */
function sliceMidAngles(values: readonly number[], paddingAngle: number): number[] {
  const sum = values.reduce((total, value) => total + value, 0)
  if (sum <= 0) return values.map(() => 0)

  const available = 360 - values.length * paddingAngle
  let previousEnd = 0

  return values.map((value, index) => {
    const start = index === 0 ? 0 : previousEnd + paddingAngle
    const end = start + (value / sum) * available
    previousEnd = end
    return (start + end) / 2
  })
}

/**
 * Yorliq nuqtalari: har tomonda yuqoridan pastga saralanadi va bir-biriga
 * `LABEL_GAP` dan yaqin kelganlari pastga suriladi. Chiziq bo'lakdan
 * chiqadigan joy o'zgarmaydi — faqat «tirsak» balandligi, ya'ni har bir
 * son baribir o'z bo'lagiga ulangan holda qoladi.
 */
function layoutLabels(
  midAngles: readonly number[],
  cx: number,
  cy: number,
  radius: number,
): LabelPoint[] {
  const points: LabelPoint[] = midAngles.map((angle) => {
    const cos = Math.cos((-angle * Math.PI) / 180)
    const sin = Math.sin((-angle * Math.PI) / 180)
    return {
      side: cos >= 0 ? 1 : -1,
      elbowX: cx + (radius + 12) * cos,
      elbowY: cy + (radius + 12) * sin,
    }
  })

  for (const side of [1, -1] as const) {
    const column = points
      .filter((point) => point.side === side)
      .sort((upper, lower) => upper.elbowY - lower.elbowY)

    for (let index = 1; index < column.length; index += 1) {
      const above = column[index - 1]
      const current = column[index]
      if (above === undefined || current === undefined) continue
      if (current.elbowY - above.elbowY < LABEL_GAP) current.elbowY = above.elbowY + LABEL_GAP
    }
  }

  return points
}

/**
 * Ustunli diagramma — ikki shaklda, joyga qarab (egasi 2026-09-18).
 *
 *   · TOR joyda (diagramma jadvalning chapidagi 300 px ustunda) —
 *     GORIZONTAL qatorlar: nom chapda, ustun, oxirida «353 · 36,5%».
 *     Oltita vertikal ustun 300 px ga sig'maydi: har biriga 50 px, «Ish
 *     kuni boshlanmagan» esa uch qatorga bo'linardi.
 *   · KENG joyda (filtr ustuni bor, diagramma jadval tepasida tasma —
 *     `attendance-dashboard-page.tsx` boshidagi izoh) — PASTDAN o'sadigan
 *     VERTIKAL ustunlar tasmaning butun kengligida. Birinchi versiyada bu
 *     yerda ham gorizontal ustunlar 300 px ga siqilgan, yonida esa xuddi
 *     o'sha ma'lumotni takrorlaydigan legenda 700 px egallab turardi.
 *
 * Ikkala shakl ham o'z-o'zini YORLIQLAYDI (nom, son, foiz) — shu sababli
 * bu turda legenda chizilmaydi (sahifa uni faqat doiraviyda ko'rsatadi).
 *
 * ⭐ HTML, SVG emas. Sabablari: (1) har band haqiqiy TUGMA — klaviatura
 * va ekran o'quvchi bilan ishlaydi, recharts ustuni esa sichqoncha bilan
 * bosilardi xolos; (2) shakl CSS container query bilan almashadi —
 * yashirilgan recharts konteyneri nol o'lcham bilan konsolga ogohlantirish
 * yozardi; (3) oltita to'rtburchak uchun o'qlar, masshtab va animatsiya
 * dvigateli ortiqcha. Ranglar baribir `var(--color-ios-…)` — tungi
 * rejimda o'zi almashadi.
 *
 * Uzunlik ENG KATTA sanoqqa nisbatan (jamiga emas): bu tur holatlarni
 * bir-biri bilan SOLISHTIRADI; ulushni doiraviy ko'rsatadi.
 */
function StatusBars({
  data,
  onToggle,
}: {
  data: readonly ChartDatum[]
  onToggle: (status: DashboardStatus) => void
}) {
  const { num } = useLocale()

  // Hamma sanoq nol bo'lsa — nolga bo'lish yo'q, ustunlar ingichka chiziq
  const max = Math.max(1, ...data.map((datum) => datum.value))
  const share = (value: number) => `${(value / max) * 100}%`

  return (
    <>
      {/* ---------- tor joy: gorizontal qatorlar ---------- */}
      <ul className={cn('flex flex-col justify-center gap-0.5 @min-[1100px]/page:hidden', CHART_HEIGHT)}>
        {data.map((datum) => (
          <li key={datum.status}>
            <button
              type="button"
              aria-pressed={datum.selected}
              title={`${datum.name}: ${datum.summary}`}
              onClick={() => onToggle(datum.status)}
              className={cn(
                'press grid w-full grid-cols-[104px_minmax(0,1fr)_auto] items-center gap-2.5 rounded-md px-1.5 py-1.5 text-left',
                'transition-[background-color,opacity] duration-150 ease-[var(--ease-ios)]',
                'hover:bg-[var(--bg-hover)]',
                datum.selected && 'bg-[var(--bg-active)] hover:bg-[var(--bg-active)]',
                datum.dimmed && 'opacity-40',
              )}
            >
              <span className="text-right text-caption1 leading-tight text-[var(--fg-secondary)]">
                {datum.name}
              </span>
              <span className="h-[18px] min-w-0">
                <span
                  className="block h-full min-w-[3px] rounded-r-md"
                  style={{ width: share(datum.value), background: datum.fill }}
                />
              </span>
              <span className="tabular text-caption1 text-[var(--fg-secondary)]">{datum.summary}</span>
            </button>
          </li>
        ))}
      </ul>

      {/* ---------- keng joy: pastdan o'sadigan ustunlar ---------- */}
      <ul className="hidden h-[280px] gap-2 @min-[1100px]/page:flex">
        {data.map((datum) => (
          <li key={datum.status} className="flex min-w-0 flex-1">
            <button
              type="button"
              aria-pressed={datum.selected}
              title={`${datum.name}: ${datum.summary}`}
              onClick={() => onToggle(datum.status)}
              className={cn(
                'press flex w-full flex-col items-center rounded-lg px-1.5 pt-2 pb-2',
                'transition-[background-color,opacity] duration-150 ease-[var(--ease-ios)]',
                'hover:bg-[var(--bg-hover)]',
                datum.selected && 'bg-[var(--bg-active)] hover:bg-[var(--bg-active)]',
                datum.dimmed && 'opacity-40',
              )}
            >
              <span className="tabular text-title3 leading-none font-semibold">{num(datum.value)}</span>
              <span className="tabular mt-1 text-caption1 text-[var(--fg-secondary)]">
                {datum.percentText}
              </span>

              {/*
                Ustun — `absolute` va foizli balandlik: `flex-1` bolasining
                foizi brauzerlarda turlicha hisoblanadi, joylashtirilgan
                blokniki esa doim konteynerning balandligidan.
              */}
              <span className="relative mt-2.5 w-full flex-1 border-b border-[var(--border)]">
                <span
                  className="absolute bottom-0 left-1/2 w-full max-w-[64px] min-h-[3px] -translate-x-1/2 rounded-t-md"
                  style={{ height: share(datum.value), background: datum.fill }}
                />
              </span>

              <span className="mt-2 line-clamp-2 min-h-[2.6em] text-center text-caption1 leading-snug text-[var(--fg-secondary)]">
                {datum.name}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

/** Izoh oynasi: «Vaqtida: 47,8%» va soni. */
function ChartTooltip({ datum }: { datum: ChartDatum }) {
  const { num } = useLocale()

  return (
    <div className="rounded-md border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2 shadow-floating">
      <p className="flex items-center gap-2 text-caption1 font-semibold">
        <span
          aria-hidden
          className={cn('size-2 shrink-0 rounded-full', DASHBOARD_STATUS_LOOK[datum.status].dotClass)}
        />
        {datum.name}: {datum.percentText}
      </p>
      <p className="tabular mt-0.5 pl-4 text-caption1 text-[var(--fg-secondary)]">
        {num(datum.value)}
      </p>
    </div>
  )
}

/**
 * recharts izoh oynasiga `payload` ni tipsiz beradi — shakl shu yerda
 * tekshiriladi, tasdiq (`as`) bilan emas.
 */
function isChartDatum(value: unknown): value is ChartDatum {
  return (
    typeof value === 'object' &&
    value !== null &&
    'status' in value &&
    typeof value.status === 'string' &&
    value.status in DASHBOARD_STATUS_LOOK &&
    'percentText' in value
  )
}

/**
 * Legenda — rangli nuqta, qalin foiz va ostida nomi; to'rttadan qatorga.
 *
 * Har bir band TUGMA: bosilsa jadval shu holatga filtrlanadi, qayta
 * bosilsa filtr olinadi — diagramma bo'lagi bilan aynan bir xil. Legenda
 * klaviatura bilan ham ishlaydi, SVG bo'laklari esa yo'q; ya'ni filtrning
 * to'liq yo'li aynan shu yerda.
 *
 * Nol sanoqli holat ham ko'rinadi (shartnoma): «Kechikkanlar 0,0%» — bu
 * ham javob, legenda esa kundan kunga shaklini o'zgartirmasligi kerak.
 */
export function DashboardLegend({
  slices,
  active,
  onToggle,
}: {
  slices: readonly DashboardSlice[]
  active: DashboardStatus | null
  onToggle: (status: DashboardStatus) => void
}) {
  const { t, num, locale } = useLocale()

  return (
    /*
      `@min-[1100px]/page` — sahifada filtr ustuni bor va diagramma jadval
      TEPASIDA tasma bo'lib turgan holat (`attendance-dashboard-page.tsx`
      boshidagi izoh): legenda doiraning o'ng tomonida ~650 px oladi, ya'ni
      oltita band ikki qator × uch ustun bo'lib tekis turadi.
    */
    <ul className="grid grid-cols-2 gap-1 sm:grid-cols-4 lg:grid-cols-2 2xl:grid-cols-4 @min-[1100px]/page:grid-cols-3">
      {slices.map((slice) => {
        const look = DASHBOARD_STATUS_LOOK[slice.status]
        const label = t(look.legendKey)
        const selected = active === slice.status

        return (
          <li key={slice.status} className="min-w-0">
            <button
              type="button"
              aria-pressed={selected}
              title={`${label}: ${num(slice.count)}`}
              onClick={() => onToggle(slice.status)}
              className={cn(
                'press flex w-full min-w-0 items-start gap-2 rounded-md p-2 text-left',
                'transition-[background-color,opacity] duration-150 ease-[var(--ease-ios)]',
                'hover:bg-[var(--bg-hover)]',
                selected && 'bg-[var(--bg-active)] hover:bg-[var(--bg-active)]',
                active !== null && !selected && 'opacity-50',
              )}
            >
              <span aria-hidden className={cn('mt-1.5 size-2.5 shrink-0 rounded-full', look.dotClass)} />
              <span className="min-w-0">
                <span className="tabular block text-subhead leading-tight font-semibold">
                  {formatPercent(slice.percent, locale)}
                </span>
                <span className="mt-0.5 block text-caption1 leading-snug text-[var(--fg-secondary)]">
                  {label}
                </span>
              </span>
            </button>
          </li>
        )
      })}
    </ul>
  )
}

/** Birinchi yuklash — diagramma va legenda shaklida, sakrashsiz. */
export function DashboardChartSkeleton() {
  return (
    <div>
      <div className={cn('grid place-items-center', CHART_HEIGHT)}>
        <Skeleton className="size-[188px] rounded-full" />
      </div>
      <div className="grid grid-cols-2 gap-1 sm:grid-cols-4 lg:grid-cols-2 2xl:grid-cols-4">
        {Array.from({ length: 6 }, (_, index) => (
          <div key={index} className="flex items-start gap-2 p-2">
            <Skeleton className="mt-1 size-2.5 rounded-full" />
            <span className="flex-1 space-y-1.5">
              <Skeleton className="h-4 w-12" />
              <Skeleton className="h-3 w-16" />
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

/* ═══════════════════════════════════════════════════ DAVR GRAFIGI */

/**
 * Kunlik chiziq balandligi — doiraviy/ustunlidan balandroq.
 *
 * Bu ko'rinishda jadval YO'Q (SPEC C5: «faqat kunlik grafik»), ya'ni
 * grafik kartaning butun bo'yini egallaydi va past chiziq 92 ta nuqtani
 * bir-birining ustiga yopishtirib qo'yardi.
 */
const TREND_HEIGHT = 'h-[340px]'

/** x o'qining eng kichik tirqishi, px — 92 kunda sanalar bir-birining ustiga yozilmasin. */
const TREND_TICK_GAP = 28

/** recharts o'qiydigan bitta kun: sana + har bir holat uchun ustun. */
type TrendDatum = Record<string, number | string>

/**
 * Davr grafigi: kunma-kun, har holat — bitta chiziq.
 *
 * ⭐ Chiziqlar SERVER bergan `days` tartibida chiziladi va x o'qi shu
 * massivdan olinadi — klient sana qatorini O'ZI to'qimaydi. Server
 * oraliqning har bir kunini tirqishsiz beradi (shartnoma), ya'ni hech
 * kim ishlamagan hafta grafikdan yo'qolib, ikki uzoq sana to'g'ri chiziq
 * bilan bog'lanib qolmaydi.
 *
 * Ranglar doiraviy diagrammadagi AYNAN o'sha oltita (`DASHBOARD_STATUS_LOOK`):
 * bitta holat ikki ekranda ikki xil rangda bo'lsa, HR ularni solishtira
 * olmasdi.
 */
export function DashboardTrendChart({
  days,
  series,
}: {
  days: readonly TrendDay[]
  /** Yoqilgan qatorlar. Bo'sh bo'lsa ham grafik o'z o'qini saqlaydi. */
  series: ReadonlySet<DashboardStatus>
}) {
  const { t, locale } = useLocale()

  const data: TrendDatum[] = days.map((day) => {
    const datum: TrendDatum = { date: day.date, label: axisLabel(day.date, locale) }

    for (const status of DASHBOARD_STATUSES) {
      datum[status] = day.counts[DASHBOARD_STATUS_LOOK[status].count]
    }

    return datum
  })

  const enabled = DASHBOARD_STATUSES.filter((status) => series.has(status))

  return (
    <div className={TREND_HEIGHT}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 8, right: 16, bottom: 4, left: -16 }}>
          {/*
            Faqat gorizontal to'r: «shu kuni nechta edi» degan savolga ko'z
            o'q bo'ylab emas, balandlik bo'yicha javob beradi. Vertikal
            chiziqlar 92 kunda maydonni to'rga aylantirardi.
          */}
          <CartesianGrid horizontal vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
          <XAxis
            dataKey="label"
            tickLine={false}
            axisLine={false}
            tick={AXIS_TICK}
            minTickGap={TREND_TICK_GAP}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            tick={AXIS_TICK}
            allowDecimals={false}
            width={44}
          />
          <Tooltip
            wrapperStyle={TOOLTIP_WRAPPER}
            cursor={{ stroke: 'var(--border-strong)', strokeWidth: 1 }}
            content={(props) => {
              const datum = props.active === true ? props.payload?.[0]?.payload : undefined
              return isTrendDatum(datum) ? (
                <TrendTooltip datum={datum} series={enabled} locale={locale} />
              ) : null
            }}
          />

          {enabled.map((status) => (
            <Line
              key={status}
              type="monotone"
              dataKey={status}
              name={t(DASHBOARD_STATUS_LOOK[status].legendKey)}
              stroke={DASHBOARD_STATUS_LOOK[status].fill}
              strokeWidth={2}
              /*
                Nuqtalar faqat sichqoncha ostida: 92 kunlik davrda doimiy
                nuqtalar chiziqni tasbehga aylantirardi. Qisqa davrda ham
                bir xil — ko'rinish davr uzunligiga qarab o'zgarmasin.
              */
              dot={false}
              activeDot={{ r: 4, strokeWidth: 0 }}
              isAnimationActive={false}
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

/** Izoh oynasi: sana va o'sha kundagi YOQILGAN qatorlarning hammasi. */
function TrendTooltip({
  datum,
  series,
  locale,
}: {
  datum: TrendDatum
  series: readonly DashboardStatus[]
  locale: LocaleCode
}) {
  const { t, num } = useLocale()

  return (
    <div className="rounded-md border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2 shadow-floating">
      <p className="tabular text-caption1 font-semibold">{fullDate(String(datum['date']), locale)}</p>
      <ul className="mt-1.5 space-y-0.5">
        {series.map((status) => (
          <li key={status} className="flex items-center gap-2 text-caption1">
            <span
              aria-hidden
              className={cn('size-2 shrink-0 rounded-full', DASHBOARD_STATUS_LOOK[status].dotClass)}
            />
            <span className="flex-1 text-[var(--fg-secondary)]">
              {t(DASHBOARD_STATUS_LOOK[status].legendKey)}
            </span>
            <span className="tabular font-semibold">{num(Number(datum[status] ?? 0))}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

/**
 * Qatorlar tugmalari — grafik ostida, har holat uchun bittadan.
 *
 * Legendadan farqi: u FILTR (jadvalni toraytiradi), bu esa KO'RINISH
 * (chiziqni yoqadi yoki o'chiradi). Shu sababli o'chirilgan band
 * so'nadi va nuqtasi bo'shab qoladi — «bosilmagan tugma» emas,
 * «chizilmagan qator» degani.
 */
export function DashboardTrendToggles({
  series,
  onToggle,
}: {
  series: ReadonlySet<DashboardStatus>
  onToggle: (status: DashboardStatus) => void
}) {
  const { t } = useLocale()

  return (
    <ul className="flex flex-wrap gap-1">
      {DASHBOARD_STATUSES.map((status) => {
        const look = DASHBOARD_STATUS_LOOK[status]
        const on = series.has(status)

        return (
          <li key={status}>
            <button
              type="button"
              aria-pressed={on}
              onClick={() => onToggle(status)}
              className={cn(
                'press flex items-center gap-2 rounded-full px-3 py-1.5 text-footnote font-medium',
                'transition-[background-color,opacity,color] duration-150 ease-[var(--ease-ios)]',
                'hover:bg-[var(--bg-hover)]',
                on ? 'bg-[var(--bg-sunken)]' : 'text-[var(--fg-tertiary)]',
              )}
            >
              <span
                aria-hidden
                className={cn(
                  'size-2.5 shrink-0 rounded-full',
                  on ? look.dotClass : 'border border-[var(--border-strong)]',
                )}
              />
              {t(look.legendKey)}
            </button>
          </li>
        )
      })}
    </ul>
  )
}

/** Birinchi yuklash — kunlik chiziq shaklida, sakrashsiz. */
export function DashboardTrendSkeleton() {
  return (
    <div>
      {/*
        Balandliklar Tailwind sinflari bilan, `style` bilan emas:
        `Skeleton` atayin faqat `className` ni qabul qiladi — ichki
        o'lchamlar sinf tizimidan tashqariga chiqib ketmasin.
      */}
      <div className={cn('flex items-end gap-2 px-2', TREND_HEIGHT)}>
        {['h-[40%]', 'h-[68%]', 'h-[52%]', 'h-[84%]', 'h-[60%]', 'h-[92%]', 'h-[48%]'].map(
          (height, index) => (
            <Skeleton key={index} className={cn('flex-1 rounded-md', height)} />
          ),
        )}
      </div>
      <div className="mt-3 flex flex-wrap gap-1">
        {Array.from({ length: 6 }, (_, index) => (
          <Skeleton key={index} className="h-8 w-28 rounded-full" />
        ))}
      </div>
    </div>
  )
}

/** x o'qi yorlig'i: «18.09» (inglizchada «09-18») — yil o'qda ortiqcha. */
function axisLabel(iso: string, locale: LocaleCode): string {
  const month = iso.slice(5, 7)
  const day = iso.slice(8, 10)

  return locale === 'en' ? `${month}-${day}` : `${day}.${month}`
}

/** Izoh oynasidagi to'liq sana — `formatDate` bilan bir xil shaklda. */
function fullDate(iso: string, locale: LocaleCode): string {
  const [year, month, day] = [iso.slice(0, 4), iso.slice(5, 7), iso.slice(8, 10)]

  return locale === 'en' ? `${year}-${month}-${day}` : `${day}.${month}.${year}`
}

/** recharts `payload` ni tipsiz beradi — shakl shu yerda tekshiriladi. */
function isTrendDatum(value: unknown): value is TrendDatum {
  return typeof value === 'object' && value !== null && 'date' in value && 'label' in value
}
