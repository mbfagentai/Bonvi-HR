import { getRouteApi } from '@tanstack/react-router'
import { ChartColumn, LogOut, Percent, TrendingDown, TrendingUp } from 'lucide-react'
import { useCallback, useMemo } from 'react'

import { useAuth } from '@/features/auth'
import { RECRUITING_VIEW } from '@/features/recruiting/lib/recruiting-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  EmptyState,
  IconTile,
  PageHeader,
  SegmentedControl,
  Skeleton,
  StatCard,
} from '@/shared/ui'

import type { TurnoverMonth } from '../api/turnover-api'
import { useTurnover } from '../api/reports-queries'
import { REPORTS_VIEW, percentText } from '../lib/reports-presentation'
import {
  TURNOVER_PERIODS,
  periodFromSearch,
  periodToSearch,
  referralFiltersFromSearch,
  referralFiltersToSearch,
  toPeriod,
  type ReferralFilters,
  type TurnoverPeriod,
} from '../lib/reports-search'
import { periodTotals } from '../lib/turnover-math'
import { CandidateReferrals } from './candidate-referrals'
import { TurnoverChart } from './turnover-chart'
import { TurnoverReasons } from './turnover-reasons'

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employments-page.tsx` dagi izohga qarang: marshrut fayli komponentni
 * import qiladi, komponent esa marshrutni import qila olmaydi).
 */
const route = getRouteApi('/_app/hisobotlar')

/** Modul darajasida — har render'da yangi massiv `useMemo` ni bekor qilardi. */
const EMPTY_MONTHS: TurnoverMonth[] = []

/**
 * Hisobotlar — kadrlar oqimi.
 *
 * NEGA BOSH SAHIFAGA QO'SHILMAGAN, ALOHIDA EKRAN:
 *
 *   1. Ruxsat. Bosh sahifa `employees.view` bilan ochiladi, bu hisobot esa
 *      `reports.view` talab qiladi — chegarani backend ataylab chizgan
 *      (`ReportsEndpoints.cs` boshidagi izoh). Grafikni bosh sahifaga
 *      qo'yish uni hisobot huquqi yo'q foydalanuvchiga ham ko'rsatib
 *      qo'yardi.
 *   2. Savolning tabiati. Bosh sahifa — «hozir nima bo'lyapti» ekrani, HR
 *      unga kuniga o'n marta kiradi. 12–24 oylik tahlil esa boshqa ish:
 *      u kamdan-kam, lekin uzoq o'tirib bajariladi va butun ekranni
 *      talab qiladi.
 */
export function ReportsPage() {
  const { t, num, date } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const search = route.useSearch()
  const navigate = route.useNavigate()
  const period = periodFromSearch(search)

  const canView = hasPermission(REPORTS_VIEW)
  /*
    «Nomzodlar qayerdan bilgan» counts candidates — shown only to someone who
    may see recruiting data at all (SPEC-qayerdan-bilgan §7: the report must
    not widen access to candidate data).
  */
  const canSeeReferrals = canView && hasPermission(RECRUITING_VIEW)
  const referralFilters = useMemo(() => referralFiltersFromSearch(search), [search])

  /*
   * `canView` yo'q bo'lsa ham so'rov chaqiriladi — `incidents-page.tsx`
   * dagi `useIncidents()` bilan bir xil naqsh: server baribir 403
   * qaytaradi, bu yerda faqat natija ko'rsatilmaydi. Hook'ni shart ostida
   * chaqirib bo'lmaydi (React qoidasi), `enabled` esa bitta ekran uchun
   * ikkinchi «ruxsat bormi» manbasini yaratardi.
   */
  const query = useTurnover(period)
  const data = canView ? query.data : undefined

  /**
   * `isPending` — hech qachon ma'lumot bo'lmagan, `isFetching` —
   * yangilanmoqda. Skelet FAQAT birinchi yuklashda: davr almashganda eski
   * grafik joyida turadi va kartaning balandligi sakramaydi
   * (`placeholderData` — `reports-queries.ts`).
   */
  const firstLoad = query.isPending

  const months = data?.months ?? EMPTY_MONTHS
  const totals = useMemo(() => periodTotals(months), [months])

  /**
   * Davr almashtirilganda tarixga YANGI yozuv qo'shilmaydi (`replace`).
   *
   * NEGA: bu ekran yo'lning oxiri — HR unga menyudan kiradi va «orqaga»
   * bosganda o'zi kelgan joyga qaytishni kutadi. Har bosish tarixga
   * tushsa, uchta davrni ko'rib chiqqan odam sahifadan chiqish uchun
   * «orqaga» ni to'rt marta bosishi kerak bo'lardi. Manzilning O'ZI esa
   * baribir davrni saqlaydi, ya'ni havola bilan bo'lishish imkoni
   * yo'qolmaydi (`reports-search.ts`).
   */
  const changePeriod = useCallback(
    (next: TurnoverPeriod) => {
      // The referral section's own filters stay as they are
      void navigate({
        to: '/hisobotlar',
        search: (previous) => ({ ...previous, ...periodToSearch(next) }),
        replace: true,
      })
    },
    [navigate],
  )

  /** The referral section's period and vacancy — in the address too, same `replace` rule. */
  const changeReferralFilters = useCallback(
    (next: ReferralFilters) => {
      void navigate({
        to: '/hisobotlar',
        search: (previous) => ({ ...previous, ...referralFiltersToSearch(next) }),
        replace: true,
      })
    },
    [navigate],
  )

  const header = (
    <PageHeader
      title={
        <span className="flex items-center gap-3">
          <IconTile icon={ChartColumn} tone="brand" size="lg" />
          {t('nav.reports')}
        </span>
      }
      description={t('reports.subtitle')}
      // Davr — sahifada qoladi (egasi 2026-10-22), panelga faqat tugmalar chiqadi
      toolbar={
        canView ? (
          <SegmentedControl
            /*
              Boshqaruv SATR bilan ishlaydi, davr esa SON — o'girish
              `toPeriod` da (izohi `reports-search.ts` da).
            */
            value={String(period)}
            onChange={(value) => changePeriod(toPeriod(value))}
            aria-label={t('reports.periodLabel')}
            options={TURNOVER_PERIODS.map((option) => ({
              value: String(option),
              label: t('reports.periodMonths', { n: option }),
            }))}
          />
        ) : undefined
      }
    />
  )

  if (!canView) {
    return (
      <div className="space-y-6">
        {header}
        <Card>
          <EmptyState
            icon={ChartColumn}
            title={t('reports.noAccessTitle')}
            description={t('reports.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  /*
    Davr oralig'i — «01.09.2025 – 26.08.2026». Kartalarning ostida turadi:
    foiz yillikka keltirilmagani uchun (`turnover-math.ts`) raqam AYNAN
    shu kunlarga tegishli va bu ekrandan o'qilishi shart.
  */
  const range = data ? `${date(data.from)} – ${date(data.to)}` : undefined

  return (
    <div className="space-y-6">
      {header}

      {query.isError && (
        <Alert tone="danger" title={t('reports.loadFailed')}>
          {errorMessage(problemCode(query.error))}
        </Alert>
      )}

      {/*
        ---------- Qisqa ko'rsatkichlar ----------

        Tartib ataylab shunday: nima keldi → nima ketdi → bu nimani
        anglatadi. Foiz uchinchi bo'lib turadi, chunki u birinchi
        ikkitasidan hosil bo'ladi — oldiga qo'yilsa, o'quvchi uni qaysi
        sonlardan chiqqanini bilmasdan o'qirdi.
      */}
      <section className="grid gap-4 sm:grid-cols-3">
        <StatCard
          label={t('reports.hired')}
          value={data ? num(totals.hired) : '—'}
          icon={TrendingUp}
          tone="green"
          loading={firstLoad}
          hint={range}
        />
        <StatCard
          label={t('reports.left')}
          value={data ? num(totals.left) : '—'}
          icon={TrendingDown}
          tone="red"
          loading={firstLoad}
          hint={range}
        />
        <StatCard
          label={t('reports.rate')}
          /*
            Maxraj nol bo'lganda foiz UMUMAN chizilmaydi — na «0%», na
            «∞». Sabab `turnover-math.ts` da: bo'sh bazada o'lchanadigan
            narsaning o'zi yo'q, «0%» esa «oqim yo'q, hammasi joyida»
            degan yolg'on ma'no berardi. Kartaning izohi ham shu holatda
            almashadi: HR raqam nega yo'qligini ekrandan o'qiydi.
          */
          value={totals.rate === undefined ? '—' : percentText(totals.rate, num)}
          icon={Percent}
          tone="brand"
          loading={firstLoad}
          hint={
            totals.rate === undefined
              ? t('reports.rateUnavailable')
              : t('reports.rateHint', { n: num(Math.round(totals.averageHeadcount)) })
          }
        />
      </section>

      {/* ---------- Kadrlar oqimi grafigi ---------- */}
      <Card>
        <CardHeader title={t('reports.chartTitle')} subtitle={t('reports.chartHint')} />
        <CardContent>
          {firstLoad ? (
            // Balandligi grafikning o'zi bilan bir xil (legenda + 300px):
            // ma'lumot kelganda karta sakrab kattalashmaydi.
            <Skeleton className="h-[326px]" />
          ) : months.length === 0 ? (
            <EmptyState
              icon={ChartColumn}
              title={t('reports.emptyTitle')}
              description={t('reports.emptyText')}
            />
          ) : (
            <TurnoverChart months={months} />
          )}
        </CardContent>
      </Card>

      {/* ---------- Ketish sabablari ---------- */}
      <Card>
        <CardHeader
          title={t('reports.reasonsTitle')}
          subtitle={
            data && data.reasons.length > 0
              ? t('reports.reasonsHint', { n: num(totals.left) })
              : undefined
          }
        />
        <CardContent>
          {firstLoad ? (
            <div className="space-y-4">
              <Skeleton className="h-9" />
              <Skeleton className="h-9" />
              <Skeleton className="h-9" />
            </div>
          ) : !data || data.reasons.length === 0 ? (
            /*
              Bo'sh holat bu yerda XATO EMAS, balki yaxshi xabar: davrda
              hech kim ketmagan. Shu sababli matn ham «hech narsa
              topilmadi» emas.
            */
            <EmptyState
              icon={LogOut}
              title={t('reports.reasonsEmptyTitle')}
              description={t('reports.reasonsEmptyText')}
            />
          ) : (
            <TurnoverReasons reasons={data.reasons} />
          )}
        </CardContent>
      </Card>

      {/* ---------- Nomzodlar qayerdan bilgan (SPEC-qayerdan-bilgan §7) ---------- */}
      {canSeeReferrals && <CandidateReferrals filters={referralFilters} onChange={changeReferralFilters} />}
    </div>
  )
}
