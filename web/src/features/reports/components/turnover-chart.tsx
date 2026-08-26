import { useMemo } from 'react'
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

import { useLocale } from '@/shared/i18n/use-locale'
import type { TurnoverMonth } from '../api/turnover-api'
import { monthTick, percentText } from '../lib/reports-presentation'
import { monthRate } from '../lib/turnover-math'

/**
 * ⭐ RANGLAR VA MAVZU — QANDAY HAL QILINGAN
 *
 * Barcha ranglar recharts'ga TO'G'RIDAN-TO'G'RI `var(--…)` ko'rinishida
 * beriladi (`fill="var(--color-ios-green)"`), hech qanday JavaScript
 * o'qish qatlamisiz.
 *
 * BRAUZERDA TEKSHIRILGAN (headless Chrome, ikkala rejimda):
 *   • SVG'ning `fill`/`stroke` kabi TAQDIMOT ATRIBUTLARI CSS qiymati
 *     sifatida o'qiladi, ya'ni `var()` ular ichida ham uslubdagidek
 *     yoyiladi: `getComputedStyle(rect).fill` → «rgb(52, 199, 89)»;
 *   • `<html>` ga `.dark` sinfi qo'yilgan zahoti o'sha element yangi
 *     qiymatni beradi (to'r chizig'i #E3E8EF → #2E343C) — hech qanday
 *     qayta render'siz;
 *   • ustunlar, chiziq, to'r va o'q yorliqlari yorug' va tungi rejimda
 *     to'g'ri chiziladi.
 *
 * NEGA `getComputedStyle` BILAN O'QISH EMAS (birinchi ko'rinishda
 * aynan shu «xavfsiz» yo'l tuyuladi): u bu loyihada JIMGINA BUZILGAN
 * bo'lardi. Tungi rejim `.dark` sinfini `<html>` ga `ThemeProvider`
 * ning `useEffect`i qo'yadi, React esa effektlarni PASTDAN YUQORIGA
 * — avval bolalarniki, keyin ota-onasiniki — ishga tushiradi. Ya'ni
 * grafikning effekti sinf ALMASHISHIDAN OLDIN o'qirdi va eski rangni
 * olardi; keyin esa hech narsa qayta render bo'lmagani uchun grafik
 * o'sha eski rangda qotib qolardi. Buni tuzatish uchun `MutationObserver`
 * yoki `requestAnimationFrame` kerak bo'lardi — CSS o'zi bepul
 * bajaradigan ish uchun uchta qo'shimcha qatlam.
 *
 * `var()` bilan esa mavzu almashganda brauzer rangni O'ZI qayta
 * hisoblaydi: React umuman aralashmaydi, qayta render ham bo'lmaydi.
 */

/** Bitta ustun guruhi — grafik o'qiydigan ko'rinish. */
interface TurnoverPoint {
  /** Gorizontal o'qdagi qisqa yorliq: «08.26». */
  tick: string
  /** Izoh oynasining sarlavhasi: «01.08.2026 – 26.08.2026». */
  range: string
  hired: number
  left: number
  /** Oy OXIRIDAGI shtat — pastdagi izohga qarang. */
  headcount: number
  /** Shu oyning oqimi, foizda. `undefined` — hisoblab bo'lmaydi. */
  rate: number | undefined
}

/**
 * O'q yorliqlarining uslubi.
 *
 * Modul darajasida: bu obyekt har render'da yangidan yaratilsa, recharts
 * uni yangi prop deb bilib butun o'qni qayta chizardi.
 */
const AXIS_TICK = { fill: 'var(--fg-tertiary)', fontSize: 11 } as const

/**
 * O'ng o'q yorliqlari tint rangida — chiziq bilan bir xil.
 *
 * Chap o'qdan farq qilishi SHART: ikkala o'q ham bir xil kulrangda bo'lsa,
 * qaysi biri qaysi ma'lumotga tegishli ekani yo'qolardi va HR 40 ni ham,
 * 4 ni ham bir xil narsaning soni deb o'qishi mumkin edi.
 */
const HEADCOUNT_TICK = { fill: 'var(--accent-text)', fontSize: 11 } as const

/**
 * Bitta oyga ajratiladigan eng kichik kenglik, piksel.
 *
 * Har oyda IKKITA ustun bor. Bundan tor bo'lsa ular chiziqqa aylanadi va
 * «qabul ko'pmi, ketish ko'pmi» degan asosiy savol ko'z bilan o'qilmay
 * qoladi. Shu sababli grafik siqilmaydi — kerak bo'lsa gorizontal
 * siljiydi (pastdagi `overflow-x-auto`).
 */
const MONTH_WIDTH = 40

/** Grafikning eng kichik kengligi — olti oylik ko'rinish ham cho'zilib ketmasin. */
const MIN_WIDTH = 420

/**
 * Kadrlar oqimi grafigi: oylik qabul/ketish ustunlari va shtat chizig'i.
 *
 * NEGA IKKITA VERTIKAL O'Q: ustunlar bir xonali sonlar (oyiga 0–15 kishi),
 * shtat esa yuzlarga chiqishi mumkin. Bitta o'qda ular yonma-yon
 * chizilganda ustunlar butunlay yassilanib, pastda ko'rinmas chiziqqa
 * aylanardi. Chap o'q — hodisalar soni, o'ng o'q — shtat; chiziq rangi
 * o'ng o'qning yorlig'i bilan bir xil (tint), ya'ni qaysi son qaysi o'qqa
 * tegishli ekani izohsiz ko'rinadi.
 *
 * NEGA chiziq `headcountEnd` bo'yicha, `headcountStart` emas: ustunlar
 * shu OY ICHIDA nima bo'lganini ko'rsatadi, chiziq esa oy TUGAGANDA
 * qanday natija chiqqanini. Boshlang'ich songa qo'yilsa, chiziq
 * ustunlardan bir oy oldinda yurib, sabab va oqibat joylari almashib
 * qolardi.
 */
export function TurnoverChart({ months }: { months: readonly TurnoverMonth[] }) {
  const { t, locale, date } = useLocale()

  const points = useMemo<TurnoverPoint[]>(
    () =>
      months.map((month) => ({
        tick: monthTick(month.monthStart, locale),
        range: `${date(month.monthStart)} – ${date(month.monthEnd)}`,
        hired: month.hired,
        left: month.left,
        headcount: month.headcountEnd,
        rate: monthRate(month),
      })),
    [months, locale, date],
  )

  return (
    <div>
      {/*
        Izoh (legenda) recharts'ning `<Legend>` i EMAS, oddiy HTML.

        Sabab: `<Legend>` yorliqlarni `dataKey` dan oladi va ularni tarjima
        qilish uchun baribir `formatter` yozish kerak bo'lardi — ya'ni
        foyda yo'q. HTML'da esa u grafikdan TASHQARIDA turadi: gorizontal
        siljiydigan maydonga tushmaydi va tor ekranda ham doim ko'rinadi.
      */}
      <ul className="mb-3 flex flex-wrap items-center gap-x-5 gap-y-1.5">
        <LegendItem color="var(--color-ios-green)" label={t('reports.hiredShort')} />
        <LegendItem color="var(--color-ios-red)" label={t('reports.leftShort')} />
        <LegendItem color="var(--accent)" label={t('reports.headcount')} line />
      </ul>

      {/*
        Gorizontal siljish relsi — jadvallardagi bilan bir xil (`scroll-rail`
        izohi `globals.css` da: macOS yashiringan scrollbar'ni ko'rsatib
        turish uchun).
      */}
      <div className="scroll-rail -mx-1 overflow-x-auto px-1 pb-1">
        <div
          className="h-[300px]"
          style={{ minWidth: Math.max(MIN_WIDTH, points.length * MONTH_WIDTH) }}
        >
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart
              data={points}
              /*
                Chap chekka manfiy: `YAxis` o'zining 34px kengligini
                allaqachon ajratib beradi, ustiga standart chekka
                qo'shilsa grafik o'ngga surilib, o'ng o'q bilan orasi
                assimetrik bo'lib qolardi.
              */
              margin={{ top: 4, right: 10, bottom: 0, left: -12 }}
              barCategoryGap="24%"
              barGap={2}
            >
              {/*
                Faqat GORIZONTAL to'r. Vertikal chiziqlar 24 oylik
                ko'rinishda ustunlar orasini panjaraga aylantirardi, hech
                qanday o'qishga esa yordam bermaydi: qiymat balandlik
                bilan o'qiladi, ya'ni ko'zga faqat gorizontal daraja kerak.
              */}
              <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />

              <XAxis
                dataKey="tick"
                tickLine={false}
                axisLine={false}
                tick={AXIS_TICK}
                /*
                  Yorliqlar SIYRAKLASHADI: recharts bir-biriga 18px dan
                  yaqin kelgan yorliqlarni o'zi tashlab yuboradi, birinchi
                  va oxirgisi esa doim qoladi. Ya'ni 24 oylik grafik
                  torayganda o'q o'qilmas holga kelmaydi — shunchaki
                  siyraklashadi. Qat'iy `interval={1}` bunday qila
                  olmasdi: u keng ekranda ham har ikkinchi oyni yashirardi.

                  18 — tekshirib tanlangan: «26-08» yorlig'i 11px shriftda
                  ≈32px, oy kengligi esa `MONTH_WIDTH` = 40px. Kichikroq
                  qiymatda (12px) chegara aynan shu 40px ga tushib qolardi
                  va yorliqlar bir joyda saqlanib, bir joyda tashlanib,
                  notekis oraliqlar hosil qilardi.
                */
                interval="preserveStartEnd"
                minTickGap={18}
              />

              <YAxis
                yAxisId="count"
                tickLine={false}
                axisLine={false}
                tick={AXIS_TICK}
                width={34}
                // Odam soni — butun son. Aks holda «2,5 kishi» chiqardi.
                allowDecimals={false}
              />

              <YAxis
                yAxisId="headcount"
                orientation="right"
                tickLine={false}
                axisLine={false}
                tick={HEADCOUNT_TICK}
                width={38}
                allowDecimals={false}
              />

              <Tooltip
                // Kursor — ustun ortidagi yumshoq to'rtburchak (iOS'dagi
                // «tanlangan qator» yuzasi). Qora chiziq emas: u ustunni
                // kesib o'tib, balandlikni o'qishga xalaqit berardi.
                cursor={{ fill: 'var(--bg-sunken)' }}
                content={(props) => {
                  const point = props.active
                    ? (props.payload?.[0]?.payload as TurnoverPoint | undefined)
                    : undefined

                  return point ? <TurnoverTooltip point={point} /> : null
                }}
              />

              <Bar
                yAxisId="count"
                dataKey="hired"
                fill="var(--color-ios-green)"
                radius={[3, 3, 0, 0]}
                maxBarSize={14}
              />
              <Bar
                yAxisId="count"
                dataKey="left"
                fill="var(--color-ios-red)"
                radius={[3, 3, 0, 0]}
                maxBarSize={14}
              />

              <Line
                yAxisId="headcount"
                /*
                  `monotone` — «linear» emas: shtat soni sakrab emas,
                  bosqichma-bosqich o'zgaradi va yumshoq egri chiziq
                  aynan shu tabiatni ko'rsatadi. Muhimi, `monotone`
                  ma'lumot nuqtalaridan CHIQIB ketmaydi (`basis` dan
                  farqli) — ya'ni chiziq hech qachon bo'lmagan sonni
                  ko'rsatmaydi.
                */
                type="monotone"
                dataKey="headcount"
                stroke="var(--accent)"
                strokeWidth={2}
                /*
                  Doimiy nuqtalar YO'Q: 24 oylik grafikda ular chiziqni
                  marjonga aylantirardi. Faqat kursor ostidagi oy
                  belgilanadi — karta foni rangidagi halqa uni ustunlardan
                  ajratib turadi.
                */
                dot={false}
                activeDot={{
                  r: 4,
                  fill: 'var(--accent)',
                  stroke: 'var(--bg-elevated)',
                  strokeWidth: 2,
                }}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  )
}

/**
 * Izoh oynasi.
 *
 * ALOHIDA komponent, `content` ichidagi oddiy JSX emas: shunda uning
 * ichida `useLocale()` chaqirish mumkin bo'ladi va tarjimalar recharts'ga
 * prop bo'lib uzatilmaydi.
 */
function TurnoverTooltip({ point }: { point: TurnoverPoint }) {
  const { t, num } = useLocale()

  return (
    <div className="rounded-md border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2.5 shadow-floating">
      <p className="tabular text-caption1 text-[var(--fg-secondary)]">{point.range}</p>

      <dl className="mt-2 space-y-1">
        <TooltipRow
          color="var(--color-ios-green)"
          label={t('reports.hiredShort')}
          value={num(point.hired)}
        />
        <TooltipRow
          color="var(--color-ios-red)"
          label={t('reports.leftShort')}
          value={num(point.left)}
        />
        <TooltipRow
          color="var(--accent)"
          label={t('reports.headcount')}
          value={num(point.headcount)}
        />

        {/*
          Oylik foiz FAQAT hisoblanganda chiziladi. Shtat nol bo'lgan oyda
          (tizim ishga tushishidan oldingi oylar bunday) qator butunlay
          yo'qoladi — «0%» yozish «oqim yo'q» degan yolg'on ma'no berardi
          (`turnover-math.ts` dagi izoh).
        */}
        {point.rate !== undefined && (
          <TooltipRow
            label={t('reports.monthRate')}
            value={percentText(point.rate, num)}
          />
        )}
      </dl>
    </div>
  )
}

function TooltipRow({
  color,
  label,
  value,
}: {
  color?: string
  label: string
  value: string
}) {
  return (
    <div className="flex items-center gap-2">
      {/*
        Rangsiz qator (foiz) ham bir xil kenglikdan boshlanadi — nuqta
        o'rni bo'sh qoldiriladi, aks holda oxirgi qator chapga sirg'anib,
        ro'yxat qiyshayib ko'rinardi.
      */}
      <span
        aria-hidden
        className="size-2 shrink-0 rounded-full"
        style={color === undefined ? undefined : { backgroundColor: color }}
      />
      <dt className="flex-1 text-caption1 text-[var(--fg-secondary)]">{label}</dt>
      <dd className="tabular text-caption1 font-semibold">{value}</dd>
    </div>
  )
}

function LegendItem({
  color,
  label,
  line = false,
}: {
  color: string
  label: string
  /** Chiziqli ma'lumot — belgisi ham chiziq bo'ladi, ustun emas. */
  line?: boolean
}) {
  return (
    <li className="flex items-center gap-2 text-caption1 text-[var(--fg-secondary)]">
      {/*
        Belgi shakli ma'lumot turini takrorlaydi: ustun uchun to'rtburchak,
        chiziq uchun chiziq. Rang ko'rmaydigan foydalanuvchi ham qaysi
        yorliq qaysi elementga tegishli ekanini shakldan ajratadi.
      */}
      <span
        aria-hidden
        className={line ? 'h-0.5 w-4 rounded-full' : 'size-2.5 rounded-xs'}
        style={{ backgroundColor: color }}
      />
      {label}
    </li>
  )
}
