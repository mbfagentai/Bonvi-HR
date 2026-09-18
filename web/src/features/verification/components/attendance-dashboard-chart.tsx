import {
  Bar,
  BarChart,
  LabelList,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  type PieLabelRenderProps,
} from 'recharts'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Skeleton } from '@/shared/ui'

import type { DashboardStatus } from '../api/attendance-api'
import { DASHBOARD_STATUS_LOOK, formatPercent } from '../lib/attendance-presentation'

/**
 * Davomat statistikasining diagrammasi va legendasi.
 *
 * ⭐ RANGLAR `var(--color-ios-…)` ko'rinishida to'g'ridan-to'g'ri SVG ga
 * beriladi — JS da o'qilmaydi. Sabab va brauzerdagi tekshiruv
 * `reports/components/turnover-chart.tsx` boshida: tungi rejim almashganda
 * SVG o'zi qayta bo'yaladi, qayta render kerak emas.
 */

/** Diagramma turi. `donut` — sukut (Verifix'dagi «Круговая диаграмма»). */
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
}

/**
 * Diagramma balandligi — ikkala turda BIR XIL: tur almashganda legenda va
 * jadval sakramasin.
 */
const CHART_HEIGHT = 'h-[260px]'

/** Modul darajasida — har renderda yangi obyekt recharts o'qini qayta chizdirardi. */
const AXIS_TICK = { fill: 'var(--fg-secondary)', fontSize: 12 } as const

/**
 * Ustunlar o'qi: eng kami 1. Hamma sanoq nol bo'lsa `[0, 0]` domen ustunlarni
 * NaN kenglik bilan chizardi.
 */
const BAR_DOMAIN = [0, (dataMax: number) => Math.max(1, dataMax)] as const

/** Tanlanmagan bo'lak shu shaffoflikda — rangi saqlanadi, e'tibor tanlanganga o'tadi. */
const DIMMED_OPACITY = 0.3

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
        className="pointer-events-none absolute inset-0 grid place-items-center text-center"
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
 * Ustunli diagramma: har holat — bitta gorizontal ustun, oxirida «soni ·
 * ulushi». Gorizontal, chunki yorliqlar uzun («Ish kuni boshlanmagan») va
 * vertikal ustun ostida yonboshlab o'qilmas edi (`employee-breakdown-card`
 * dagi mulohaza).
 */
function StatusBars({
  data,
  onToggle,
}: {
  data: ChartDatum[]
  onToggle: (status: DashboardStatus) => void
}) {
  return (
    <div className={cn(CHART_HEIGHT, '[&_.recharts-rectangle]:cursor-pointer')}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart
          data={data}
          layout="vertical"
          // O'ng chekka — ustun oxiridagi «75 · 47,8%» yozuvi uchun joy
          margin={{ top: 4, right: 88, bottom: 4, left: 0 }}
          barCategoryGap="26%"
        >
          <XAxis type="number" hide domain={BAR_DOMAIN} allowDecimals={false} />
          <YAxis
            type="category"
            dataKey="name"
            width={136}
            tickLine={false}
            axisLine={false}
            tick={AXIS_TICK}
            // Oltita yorliqning HAMMASI ko'rinsin — recharts sukut bo'yicha
            // tor joyda ba'zilarini tashlab yuboradi
            interval={0}
          />
          <Tooltip
            cursor={{ fill: 'var(--bg-sunken)' }}
            content={(props) => {
              const entry = props.active ? props.payload?.[0]?.payload : undefined
              return isChartDatum(entry) ? <ChartTooltip datum={entry} /> : null
            }}
          />
          <Bar
            dataKey="value"
            radius={[0, 6, 6, 0]}
            maxBarSize={22}
            /*
              Nol ham ko'rinadigan ingichka chiziq bo'lib qoladi: busiz nol
              ustun chizilmaydi, uning yozuvi ham yo'qoladi va qator «yuklanmay
              qolgan»ga o'xshardi.
            */
            minPointSize={3}
            isAnimationActive={false}
            onClick={(_, index) => {
              const datum = data[index]
              if (datum !== undefined) onToggle(datum.status)
            }}
          >
            <LabelList
              dataKey="summary"
              position="right"
              fill="var(--fg-secondary)"
              fontSize={12}
              className="tabular"
            />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
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
    <ul className="grid grid-cols-2 gap-1 sm:grid-cols-4 lg:grid-cols-2 2xl:grid-cols-4">
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
