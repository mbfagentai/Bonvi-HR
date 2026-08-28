import { Phone, UserPlus } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Badge, EmptyState, Skeleton } from '@/shared/ui'
import type { CandidateFilters, CandidateListItem, PipelineStage } from '../api/recruiting-api'
import { useCandidates } from '../api/recruiting-queries'
import { CANDIDATE_LOOK, SOURCE_LOOK } from '../lib/recruiting-presentation'

/**
 * Bir ustunda ko'rsatiladigan eng ko'p nomzod.
 *
 * NEGA cheklov bor: ustun sahifalanmaydi (voronkada sahifalash
 * ma'nosiz — ko'z bilan qarash uchun taxta) va cheklovsiz bitta ustun
 * yuzta kartani tortib, brauzerni ham, ko'zni ham toldirardi. Ortiqchasi
 * «yana N ta» qatoriga aylanadi va u JADVAL ko'rinishiga o'tkazadi —
 * u yerda sahifalash bor.
 */
const COLUMN_LIMIT = 20

/**
 * ⭐ Voronka taxtasi — bosqichlar bo'yicha ustunlar.
 *
 * <b>Bu ALOHIDA endpoint EMAS.</b> Har bir ustun — nomzodlar
 * ro'yxatining bir filtri (`?stageId=…&status=InProgress`). Backendda
 * «voronka» degan so'rov yo'q va bo'lishi ham shart emas: taxta
 * ro'yxatning boshqa CHIZILISHI, boshqa ma'lumot emas. Shu sababli
 * filtr, qidiruv va manzil holati ham ikkala ko'rinish uchun BITTA
 * (`candidates-search.ts`).
 *
 * <b>Nega har bir ustun O'Z so'rovini yuboradi.</b> Muqobil — hamma
 * nomzodni bir so'rovda olib, klientda bosqichlarga bo'lish. U rad
 * etildi: (1) klientda `filter()` qilish bu loyihada qat'iy taqiqlangan
 * konvensiya, (2) «yuzta nomzodni bir so'rovda ol» sahifalashni buzadi
 * va ustundagi HAQIQIY sanoq («jami nechta») yo'qoladi — server esa
 * uni har bir filtr uchun `total` da tekinga qaytaradi.
 * Bosqich soni oltita atrofida, ya'ni olti engil so'rov — bu narx
 * ma'lumotning to'g'riligiga arziydi.
 *
 * <b>Nega faqat FAOL bosqichlar.</b> Faolsizlantirilgan bosqichda
 * qolgan nomzodlar bo'lishi mumkin, lekin u endi voronkaning bir
 * qismi emas — yangi nomzod u yerga o'tkazilmaydi (server rad etadi).
 * Ular jadval ko'rinishida, bosqich filtri orqali topiladi.
 */
export function CandidateFunnel({
  stages,
  stagesLoading,
  filters,
  onOpen,
  onShowAll,
}: {
  stages: PipelineStage[]
  stagesLoading: boolean
  /** Bosqichdan tashqari BARCHA filtrlar — qidiruv, vakansiya, manba, holat. */
  filters: Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>
  onOpen: (candidate: CandidateListItem) => void
  /** «Yana N ta» — jadval ko'rinishiga o'sha bosqich filtri bilan o'tadi. */
  onShowAll: (stage: PipelineStage) => void
}) {
  const { t } = useLocale()

  if (stagesLoading) {
    return (
      <div className="flex gap-4 overflow-x-auto p-4 sm:p-5">
        {[0, 1, 2, 3].map((index) => (
          <Skeleton key={index} className="h-64 w-[260px] shrink-0" />
        ))}
      </div>
    )
  }

  if (stages.length === 0) {
    return (
      <EmptyState
        icon={UserPlus}
        title={t('recruiting.funnelNoStagesTitle')}
        description={t('recruiting.funnelNoStagesText')}
      />
    )
  }

  return (
    /*
      Gorizontal aylantirish — ustunlar SIQILMAYDI (`shrink-0`).
      Oltita ustunni ekranga tiqishtirish har birini 150px ga
      qisqartirardi va F.I.Sh. o'qib bo'lmasdi. Kanban taxtalarida
      gorizontal aylantirish tanish naqsh.
    */
    <div className="flex gap-4 overflow-x-auto p-4 sm:p-5">
      {stages.map((stage) => (
        <FunnelColumn
          key={stage.id}
          stage={stage}
          filters={filters}
          onOpen={onOpen}
          onShowAll={onShowAll}
        />
      ))}
    </div>
  )
}

/**
 * Bitta bosqich ustuni.
 *
 * Hook (`useCandidates`) aynan shu yerda chaqiriladi va shuning uchun
 * ustun alohida komponent: React hook'ni sikl ichida chaqirib
 * bo'lmaydi, ya'ni «har bosqichga bitta so'rov» qoidasi boshqacha
 * yozilishi mumkin emas.
 */
function FunnelColumn({
  stage,
  filters,
  onOpen,
  onShowAll,
}: {
  stage: PipelineStage
  filters: Omit<CandidateFilters, 'stageId' | 'page' | 'pageSize'>
  onOpen: (candidate: CandidateListItem) => void
  onShowAll: (stage: PipelineStage) => void
}) {
  const { t, num } = useLocale()

  const query = useCandidates({
    ...filters,
    stageId: stage.id,
    page: 1,
    pageSize: COLUMN_LIMIT,
    /*
      Voronkada tartib DOIM «yangidan eskiga» — bosqichdagi eng yangi
      nomzod tepada. Ro'yxatning umumiy saralashi bu yerga
      ko'chirilmaydi: «bosqich bo'yicha saralash» ustun ichida ma'nosiz
      (hammasi bitta bosqichda), «ism bo'yicha» esa taxtaning
      maqsadiga («kim eng uzoq turibdi?») javob bermaydi.
    */
    sort: '-createdAt',
  })

  const items = query.data?.items ?? []
  const total = query.data?.total ?? 0
  const hidden = total - items.length

  return (
    <section className="flex w-[280px] shrink-0 flex-col rounded-lg bg-[var(--bg-sunken)]">
      <header className="flex items-center gap-2 px-3.5 py-3">
        <h3 className="truncate-1 text-subhead font-semibold" title={stage.name}>
          {stage.name}
        </h3>
        {!query.isPending && (
          <Badge size="sm" tone={total === 0 ? 'neutral' : 'brand'} className="ml-auto">
            {num(total)}
          </Badge>
        )}
      </header>

      <div className="flex-1 space-y-2 px-2.5 pb-2.5">
        {query.isPending &&
          [0, 1, 2].map((index) => <Skeleton key={index} className="h-[68px] w-full" />)}

        {!query.isPending && items.length === 0 && (
          <p className="py-6 text-center text-caption1 text-[var(--fg-tertiary)]">
            {t('recruiting.funnelColumnEmpty')}
          </p>
        )}

        {items.map((item) => (
          <FunnelCard key={item.id} item={item} onOpen={() => onOpen(item)} />
        ))}

        {hidden > 0 && (
          <button
            type="button"
            onClick={() => onShowAll(stage)}
            className={cn(
              'w-full rounded-md px-3 py-2 text-caption1 font-medium',
              'text-[var(--accent-on-soft)] transition-colors hover:bg-[var(--bg-hover)]',
            )}
          >
            {t('recruiting.funnelMore', { n: num(hidden) })}
          </button>
        )}
      </div>
    </section>
  )
}

/**
 * Voronkadagi nomzod kartochkasi.
 *
 * Kartada ATAYLAB kam ma'lumot: F.I.Sh., telefon va manba. Vakansiya
 * ko'rsatilmaydi — voronka odatda BITTA vakansiya filtri bilan
 * o'qiladi va u holda har bir kartada bir xil matn takrorlanardi.
 * Qolgan hamma narsa nomzod kartasida.
 */
function FunnelCard({ item, onOpen }: { item: CandidateListItem; onOpen: () => void }) {
  const { t } = useLocale()

  const look = CANDIDATE_LOOK[item.status]
  const source = SOURCE_LOOK[item.source]
  const SourceIcon = source.icon

  return (
    <button
      type="button"
      onClick={onOpen}
      className={cn(
        'press block w-full rounded-md border border-[var(--border)] bg-[var(--bg-elevated)]',
        'px-3 py-2.5 text-left transition-colors hover:bg-[var(--bg-hover)]',
      )}
    >
      <span className="flex items-center gap-2">
        <span className="truncate-1 flex-1 text-subhead font-medium">{item.fullName}</span>

        {/*
          Manba — kichik ikona, yorliq emas: u kartochkaning yarmini
          egallamasligi kerak, lekin bot ulangan kuni farq DARHOL
          ko'rinsin (`SOURCE_LOOK` izohi).
        */}
        <SourceIcon
          className="size-3.5 shrink-0 text-[var(--fg-tertiary)]"
          strokeWidth={2}
          aria-label={t(`recruiting.source.${source.key}`)}
        />
      </span>

      {item.phone !== null && (
        <span className="tabular mt-1 flex items-center gap-1.5 text-caption2 text-[var(--fg-tertiary)]">
          <Phone className="size-3" strokeWidth={2} />
          {item.phone}
        </span>
      )}

      {/*
        Holat yorlig'i FAQAT «jarayonda» bo'lmaganda: voronkada
        deyarli hamma jarayonda turadi va har kartada bir xil ko'k
        yorliq shovqindan boshqa narsa emas. Rad etilgan yoki ishga
        olingan nomzod esa bosqichda qolib ketishi mumkin — u ajralib
        turishi kerak.
      */}
      {item.status !== 'InProgress' && (
        <Badge tone={look.tone} size="sm" className="mt-1.5">
          {t(`recruiting.candidateStatus.${look.key}`)}
        </Badge>
      )}
    </button>
  )
}
