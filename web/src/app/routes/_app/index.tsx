import { createFileRoute, Link } from '@tanstack/react-router'
import {
  BriefcaseBusiness,
  Building2,
  Cake,
  ChevronRight,
  IdCard,
  Landmark,
  Timer,
  TrendingDown,
  TrendingUp,
  UserPlus,
  Users,
} from 'lucide-react'
import { useState } from 'react'

import { useDashboardStats } from '@/features/dashboard/api/dashboard-queries'
import type { DashboardPeriod } from '@/features/dashboard/api/dashboard-api'
import { EmployeeBreakdownCard } from '@/features/dashboard/components/employee-breakdown-card'
import { useLocale } from '@/shared/i18n/use-locale'
import { useGetSystemInfo } from '@/shared/api/generated/system/system'
import {
  Alert,
  Badge,
  Card,
  CardContent,
  CardHeader,
  IconTile,
  PageHeader,
  SegmentedControl,
  Skeleton,
  StatCard,
} from '@/shared/ui'

export const Route = createFileRoute('/_app/')({
  component: Dashboard,
})

function Dashboard() {
  const { t, num, date } = useLocale()
  const [period, setPeriod] = useState<DashboardPeriod>('month')

  // Davr almashtirgich kesh kalitining ichida — bosilishi bilan
  // yangi so'rov ketadi (`dashboard-queries.ts`).
  const stats = useDashboardStats(period)

  // C# endpoint'idan generatsiya qilingan hook (qo'lda yozilmagan)
  const system = useGetSystemInfo()

  const data = stats.data

  /**
   * `isPending` — hech qachon ma'lumot bo'lmagan, `isFetching` —
   * yangilanmoqda. Skelet FAQAT birinchi yuklashda: davr almashganda
   * eski raqamlar joyida turadi va ekran sakramaydi.
   */
  const firstLoad = stats.isPending

  // Davr oralig'i — «25.07.2026 – 24.08.2026». O'sish ko'rsatkichi
  // bo'lmaganda kartaning izohi bo'lib turadi: raqam qaysi kunlarga
  // tegishli ekani ekrandan o'qilishi kerak.
  const range = data ? `${date(data.from)} – ${date(data.to)}` : undefined

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('dashboard.title')}
        subtitle={t('dashboard.subtitle', { company: t('app.company') })}
        actions={
          <SegmentedControl
            value={period}
            onChange={setPeriod}
            aria-label={t('dashboard.periodLabel')}
            options={[
              { value: 'day', label: t('period.day') },
              { value: 'week', label: t('period.week') },
              { value: 'days15', label: t('period.days15') },
              { value: 'month', label: t('period.month') },
            ]}
          />
        }
      />

      {stats.isError && (
        <Alert tone="danger" title={t('dashboard.loadFailed')}>
          {t('dashboard.loadFailedHint')}
        </Alert>
      )}

      {/* ---------- Asosiy ko'rsatkichlar ---------- */}
      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label={t('dashboard.totalEmployees')}
          value={data ? num(data.activeEmployees) : '—'}
          icon={Users}
          tone="brand"
          loading={firstLoad}
          hint={
            data
              ? `${t('dashboard.formerEmployees')}: ${num(data.formerEmployees)}`
              : undefined
          }
        />
        <StatCard
          label={t('dashboard.onProbation')}
          value={data ? num(data.onProbation) : '—'}
          icon={Timer}
          tone="orange"
          loading={firstLoad}
        />
        <StatCard
          label={t('dashboard.hired')}
          value={data ? num(data.hired) : '—'}
          icon={TrendingUp}
          tone="green"
          loading={firstLoad}
          delta={growth(data?.hired, data?.hiredPrevious, t('dashboard.vsPrevious'))}
          hint={range}
        />
        <StatCard
          label={t('dashboard.left')}
          value={data ? num(data.left) : '—'}
          icon={TrendingDown}
          tone="red"
          loading={firstLoad}
          delta={growth(data?.left, data?.leftPrevious, t('dashboard.vsPrevious'))}
          hint={range}
        />

        {/*
          ---------- Rekruting: ochiq vakansiyalar va arizalar ----------

          ⚠️ Bu ikkita karta RUXSATGA bog'liq va shart `data.recruiting`
          ning o'zi: serverda `recruiting.view` bo'lmasa butun blok `null`
          bo'lib keladi va bu yerda hech narsa chizilmaydi. Nol
          ko'rsatilmaydi ham, bo'sh karta ham qolmaydi — «0 ta vakansiya»
          yolg'on bo'lardi: ruxsati yo'q odam vakansiya YO'Q deb emas,
          ko'ra OLMAYDI. Bu oklad maskalanishi bilan bir xil qoida
          (`salary.view` yo'q — maydon umuman ko'rsatilmaydi).

          NEGA ruxsat frontendda («foydalanuvchining ruxsatlari» ro'yxati
          bo'yicha) tekshirilmaydi: u holda ikki joyda ikki xil qoida
          bo'lardi va biri o'zgarganda ikkinchisi jimgina eskirardi.
          Ma'lumot kelgan bo'lsa — ko'rsatiladi; kelmagan bo'lsa —
          ko'rsatilmaydi. Bitta manba, bitta qoida.

          NEGA alohida qatorda emas, xuddi shu to'rda: bular ham «nechta»
          degan bir xil savolga javob beradi va StatCard ko'rinishi bir
          xil. Alohida bo'lim ochish ekranga sarlavha qo'shishni talab
          qilardi, sarlavha esa kartalar chizilmaganda osilib qolardi.
        */}
        {data?.recruiting && (
          <>
            <StatCard
              label={t('dashboard.openVacancies')}
              value={num(data.recruiting.openVacancies)}
              icon={BriefcaseBusiness}
              tone="blue"
              /*
                `loading` ATAYLAB berilmagan (qo'shnilaridan farqi):
                birinchi yuklashda `data` hali yo'q, ya'ni bu kartalar
                umuman chizilmaydi. Ularga skelet qo'yish «hozir shu yerda
                karta paydo bo'ladi» degan va'da bo'lardi — ruxsati yo'q
                odamda esa u va'da bajarilmasdi va skelet o'rnida bo'shliq
                qolardi.

                Izohda O'RINLAR soni — vakansiya sonidan BOSHQA raqam.
                Bitta e'londa bir nechta o'rin bo'lishi mumkin (zavod
                sexiga 20 kishi bitta e'lon bilan qidiriladi). Asosiy
                raqam — rekruter nechta e'lon olib boryapti; izoh —
                nechta odam yetishmayapti. Faqat bittasi ko'rsatilsa,
                «5» ni ko'rgan rahbar besh kishi kerak deb o'ylardi.
              */
              hint={t('dashboard.openingsNeeded', {
                n: num(data.recruiting.openings),
              })}
            />
            <StatCard
              label={t('dashboard.applications')}
              value={num(data.recruiting.activeCandidates)}
              icon={UserPlus}
              tone="teal"
              /*
                Asosiy raqam — voronkada HOZIR turgan arizalar (joriy
                holat), izoh esa TANLANGAN DAVRDA kelgan yangilari.
                Ya'ni bu kartada davr almashtirgichi faqat izohga ta'sir
                qiladi — xuddi «Xodimlar» kartasidagi «Arxivda: 14» kabi
                naqsh: yuqorida holat, pastda qo'shimcha kesim.
              */
              hint={t('dashboard.newApplications', {
                n: num(data.recruiting.newCandidates),
              })}
            />
          </>
        )}
      </section>

      {/*
        Tug'ilgan kunlar qatori FAQAT kimningdir bayrami bo'lganda
        ko'rinadi. «Bugun 0 ta tug'ilgan kun» degan doimiy qator hech
        qanday ish bermaydi — u shunchaki ekranni to'ldirardi.
      */}
      {data && data.birthdaysToday > 0 && (
        <Card className="flex items-center gap-3 p-4">
          <IconTile icon={Cake} tone="purple" />
          <p className="text-subhead">
            {t('dashboard.birthdaysToday', { n: num(data.birthdaysToday) })}
          </p>
        </Card>
      )}

      {/*
        ---------- Xodimlar taqsimoti ----------

        O'rni ataylab shu yerda: yuqoridagi kartalar «nechta» degan
        savolga javob beradi, bu esa keyingi savolga — «qayerda». Tizim
        holati va ma'lumotnomalar qatori esa xodimlar haqida emas,
        tizimning o'zi haqida, ya'ni u pastroqda turishi to'g'ri.

        Karta o'z ma'lumotini O'ZI so'raydi va davr almashtirgichiga
        umuman qaramaydi — taqsimot oraliqqa bog'liq emas (izohi
        `dashboard-api.ts` da). Shu sababli unga bu yerdan hech qanday
        parametr uzatilmaydi.
      */}
      <EmployeeBreakdownCard />

      {/* ---------- Tizim holati va ma'lumotnomalar ---------- */}
      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader
            title={t('dashboard.backendStatus')}
            subtitle={t('dashboard.stageNote')}
          />
          <CardContent>
            {system.isPending && (
              <p className="text-subhead text-[var(--fg-secondary)]">
                {t('common.loading')}
              </p>
            )}

            {system.isError && (
              <div className="flex items-center gap-3">
                <Badge tone="danger" dot>
                  {t('common.error')}
                </Badge>
                <p className="text-footnote text-[var(--fg-secondary)]">
                  {t('dashboard.apiUnreachable')}
                </p>
              </div>
            )}

            {system.data && (
              <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
                <Row label={t('dashboard.service')} value={system.data.service} />
                <Row label={t('dashboard.version')} value={system.data.version} />
                <Row
                  label={t('dashboard.environment')}
                  value={system.data.environment}
                />
                <Row
                  label={t('dashboard.database')}
                  value={
                    <Badge tone={system.data.databaseConnected ? 'success' : 'danger'} dot>
                      {system.data.databaseConnected
                        ? t('dashboard.connected')
                        : t('dashboard.disconnected')}
                    </Badge>
                  }
                />
              </dl>
            )}
          </CardContent>
        </Card>

        {/*
          Ilgari bu kartaning boshida bitta «Tahrirlash» tugmasi turardi va
          u hech qayerga olib bormasdi — bora ham olmasdi: ma'lumotnoma
          to'rtta, tugma esa qaysi birini ochishni bila olmaydi. Shu sababli
          tugma olib tashlandi va HAR BIR QATORNING o'zi o'z sahifasiga
          havola bo'ldi. iOS ro'yxatlaridagi odatiy naqsh: qator bosiladi,
          o'ngdagi belgi buni aytib turadi.
        */}
        <Card>
          <CardHeader title={t('nav.sectionCatalog')} />
          <CardContent className="space-y-1">
            {firstLoad ? (
              <div className="space-y-3 py-1">
                <Skeleton className="h-9" />
                <Skeleton className="h-9" />
                <Skeleton className="h-9" />
                <Skeleton className="h-9" />
              </div>
            ) : (
              <>
                <CatalogRow
                  to="/malumotnomalar/bolimlar"
                  icon={Building2}
                  label={t('nav.departments')}
                  value={data ? num(data.catalog.departments) : '—'}
                />
                <CatalogRow
                  to="/malumotnomalar/lavozimlar"
                  icon={IdCard}
                  label={t('nav.positions')}
                  value={data ? num(data.catalog.positions) : '—'}
                />
                <CatalogRow
                  to="/malumotnomalar/yonalishlar"
                  icon={Users}
                  label={t('nav.directions')}
                  value={data ? num(data.catalog.directions) : '—'}
                />
                <CatalogRow
                  to="/malumotnomalar/yuridik-shaxslar"
                  icon={Landmark}
                  label={t('nav.legalEntities')}
                  value={data ? num(data.catalog.legalEntities) : '—'}
                />
              </>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

/**
 * Oldingi davrga nisbatan o'sish, foizda.
 *
 * Ilgari bu yerda qo'lda yozilgan «+12%» turardi. Endi u ikkita HAQIQIY
 * sondan hisoblanadi — joriy davr va unga tegib turgan, xuddi shu
 * uzunlikdagi oldingi davr.
 *
 * Oldingi davrda hech kim bo'lmasa, ko'rsatkich UMUMAN chizilmaydi:
 * nolga bo'lish «+∞%» beradi, «+100%» esa yolg'on bo'lardi. Bunday
 * holatda kartada davr oralig'i qoladi — u ham foydali ma'lumot.
 */
function growth(
  current: number | undefined,
  previous: number | undefined,
  label: string,
): { value: number; label: string } | undefined {
  if (current === undefined || previous === undefined || previous <= 0) {
    return undefined
  }

  return { value: Math.round(((current - previous) / previous) * 100), label }
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-[var(--border)] pb-2.5 last:border-0">
      <dt className="text-footnote text-[var(--fg-secondary)]">{label}</dt>
      <dd className="text-footnote font-medium">{value}</dd>
    </div>
  )
}

/**
 * Ma'lumotnoma qatori — o'z sahifasiga havola.
 *
 * `to` ATAYLAB to'rtta aniq manzildan iborat birlashma, oddiy `string` emas:
 * marshrut nomi o'zgarsa, xato bosilganda emas, KOMPILYATSIYADA chiqadi.
 */
type CatalogHref =
  | '/malumotnomalar/bolimlar'
  | '/malumotnomalar/lavozimlar'
  | '/malumotnomalar/yonalishlar'
  | '/malumotnomalar/yuridik-shaxslar'

function CatalogRow({
  to,
  icon: Icon,
  label,
  value,
}: {
  to: CatalogHref
  icon: React.ComponentType<{ className?: string }>
  label: string
  value: string
}) {
  return (
    <Link
      to={to}
      className="-mx-2 flex items-center gap-3 rounded-sm px-2 py-1.5 transition-colors hover:bg-[var(--bg-sunken)]"
    >
      <span className="grid size-9 shrink-0 place-items-center rounded-sm bg-[var(--bg-sunken)] text-[var(--fg-secondary)]">
        <Icon className="size-4" />
      </span>
      <span className="min-w-0 flex-1 truncate text-footnote">{label}</span>
      <span className="tabular text-footnote font-semibold">{value}</span>
      <ChevronRight className="size-4 text-[var(--fg-tertiary)]" strokeWidth={2.5} />
    </Link>
  )
}
