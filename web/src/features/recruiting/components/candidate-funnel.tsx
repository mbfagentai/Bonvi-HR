import {
  DndContext,
  DragOverlay,
  KeyboardCode,
  KeyboardSensor,
  MouseSensor,
  TouchSensor,
  closestCenter,
  pointerWithin,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type ClientRect,
  type CollisionDetection,
  type DragEndEvent,
  type DragStartEvent,
  type KeyboardCodes,
  type KeyboardCoordinateGetter,
  type ScreenReaderInstructions,
  type UniqueIdentifier,
} from '@dnd-kit/core'
import { BriefcaseBusiness, CalendarClock, Paperclip, Phone, UserPlus } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { formatPhone } from '@/shared/lib/phone'
import { Alert, Badge, Dialog, EmptyState, Skeleton } from '@/shared/ui'
import type { CandidateListItem, PipelineStage } from '../api/recruiting-api'
import {
  useCandidate,
  useFunnelCardFinder,
  useFunnelColumn,
  type FunnelCard,
  type FunnelFilters,
} from '../api/recruiting-queries'
import {
  CANDIDATE_LOOK,
  RECRUITING_MANAGE,
  SOURCE_LOOK,
  today,
} from '../lib/recruiting-presentation'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { CandidateInterviewDialog } from './candidate-interview-dialog'
import { CandidateStageDialog } from './candidate-stage-dialog'

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
 *
 * ⭐ <b>Kartani sudrab boshqa bosqichga o'tkazish OYNA OCHADI</b>
 * (foydalanuvchi talabi, 2026-09-26). Ilgari tashlash bosqichni darhol
 * bo'sh xulosa bilan yozardi va xulosani «keyin yozasiz» deb qoldirardi —
 * nomzod kartasidagi tezkor qator bilan AYNAN bir xil muammo, u yerda
 * ham amalda keyin yozilmagan edi (`CandidateStageDialog` izohi). Endi
 * tashlash nomzod kartasidagi bosqich tanlagichi bilan BIR XIL oynani
 * ochadi (`DroppedCardDialog`). Karta «Saqlash» dan KEYIN ko'chadi
 * (`providers.tsx` keshni bekor qiladi), «Bekor qilish» esa uni joyida
 * qoldiradi.
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
  filters: FunnelFilters
  onOpen: (candidate: CandidateListItem) => void
  /** «Yana N ta» — jadval ko'rinishiga o'sha bosqich filtri bilan o'tadi. */
  onShowAll: (stage: PipelineStage) => void
}) {
  const { t } = useLocale()
  const { hasPermission } = useAuth()

  /**
   * Sudrash — YOZUV amali, ya'ni `recruiting.manage`.
   *
   * Ruxsati yo'q foydalanuvchi uchun taxta oddiy ro'yxat bo'lib qoladi:
   * `DndContext` umuman o'rnatilmaydi, kartada sudrash tinglovchilari
   * ham, `cursor-grab` ham yo'q. «Sudra, keyin 403 ol» eng yomon
   * variant bo'lardi (`candidate-photo-editor.tsx` dagi bilan bir xil
   * qoida).
   */
  const canMove = hasPermission(RECRUITING_MANAGE)

  const findCard = useFunnelCardFinder(filters, stages)

  /** Sudralayotgan karta — `DragOverlay` da ko'tarilgan nusxa chiziladi. */
  const [lifted, setLifted] = useState<FunnelCard | null>(null)

  /** Tashlangan karta va nishon bosqich — oyna shular bilan ochiladi. */
  const [dropped, setDropped] = useState<{ item: CandidateListItem; stage: PipelineStage } | null>(
    null,
  )

  const sensors = useSensors(
    /*
      Sichqoncha: 6 piksel yurmaguncha sudrash BOSHLANMAYDI. Usiz
      kartaning ustiga bosishning o'zi sudrashga aylanardi va nomzod
      kartasini ochib bo'lmay qolardi.
    */
    useSensor(MouseSensor, { activationConstraint: { distance: 6 } }),
    /*
      Sensorli ekran: 220 ms ushlab turish. Darhol boshlansa, taxtani
      barmoq bilan aylantirish mumkin bo'lmasdi — har surish kartani
      sudrardi. `tolerance` esa teskarisini ta'minlaydi: kutish
      paytida barmoq 8 pikseldan ko'p yursa, bu aylantirish deb
      hisoblanadi va sudrash umuman boshlanmaydi.
    */
    useSensor(TouchSensor, { activationConstraint: { delay: 220, tolerance: 8 } }),
    useSensor(KeyboardSensor, {
      keyboardCodes: KEYBOARD_CODES,
      coordinateGetter: columnCoordinates,
    }),
  )

  /*
   * Ekran o'quvchisi uchun xabarlar — TARJIMA QILINGAN.
   *
   * dnd-kit ning o'z xabarlari ingliz tilida va ular boshqa hech qayerda
   * ko'rinmaydigan yagona ingliz matni bo'lib qolardi. Nomzodning ismi
   * xabarga ATAYLAB qo'shilmagan: kim sudralayotganini foydalanuvchi
   * o'zi tanladi, unga kerakli yagona yangilik — qaysi BOSQICH ustida
   * turgani.
   */
  const announcements = useMemo<Announcements>(() => {
    const stageName = (id: UniqueIdentifier): string =>
      stages.find((stage) => stage.id === id)?.name ?? ''

    return {
      onDragStart: () => t('recruiting.funnelDragStart'),
      onDragOver: ({ over }) =>
        over === null ? undefined : t('recruiting.funnelDragOver', { stage: stageName(over.id) }),
      onDragEnd: ({ over }) =>
        over === null
          ? t('recruiting.funnelDragCancel')
          : t('recruiting.funnelDragEnd', { stage: stageName(over.id) }),
      onDragCancel: () => t('recruiting.funnelDragCancel'),
    }
  }, [stages, t])

  const instructions = useMemo<ScreenReaderInstructions>(
    () => ({ draggable: t('recruiting.funnelDragHint') }),
    [t],
  )

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

  const board = (
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
          draggable={canMove}
          showVacancy={filters.vacancyId === undefined}
          onOpen={onOpen}
          onShowAll={onShowAll}
        />
      ))}
    </div>
  )

  if (!canMove) return board

  const startDrag = ({ active }: DragStartEvent) => {
    setLifted(findCard(String(active.id)))
  }

  const endDrag = ({ active, over }: DragEndEvent) => {
    const card = lifted ?? findCard(String(active.id))
    setLifted(null)

    // Taxtadan tashqariga tashlandi yoki karta topilmadi — jim qolamiz
    if (over === null || card === null) return

    const target = stages.find((stage) => stage.id === over.id)

    /*
      O'Z ustuniga tashlash — hech narsa qilinmaydi va so'rov ham
      ketmaydi. Server buni qabul qilardi (oxirgi «kutilmoqda» yozuvi
      shunchaki qayta to'ldirilardi), lekin foydalanuvchi hech narsani
      o'zgartirmadi — bekorga oyna ochishning ma'nosi yo'q.
    */
    if (target === undefined || target.id === card.fromStageId) return

    setDropped({ item: card.item, stage: target })
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={funnelCollision}
      accessibility={{ announcements, screenReaderInstructions: instructions }}
      onDragStart={startDrag}
      onDragEnd={endDrag}
      onDragCancel={() => setLifted(null)}
    >
      {board}

      {/*
        Ko'tarilgan nusxa. `dropAnimation` ATAYLAB o'chirilgan: karta
        allaqachon yangi ustuniga optimistik ko'chib bo'lgan, ya'ni
        qaytish animatsiyasi uni eski o'rniga uchirib, keyin sakratardi.
        Tashlash — bir zumda va shu sababli tinch.
      */}
      <DragOverlay dropAnimation={null}>
        {lifted !== null && <LiftedCard item={lifted.item} showVacancy={false} />}
      </DragOverlay>

      {/*
        Xato (masalan bosqich faolsizlantirilgan) oynaning ICHIDA
        ko'rsatiladi va karta joyida qoladi — oyna o'zi shunday ishlaydi.
      */}
      {dropped !== null && (
        <DroppedCardDialog
          candidateId={dropped.item.id}
          stage={dropped.stage}
          onClose={() => setDropped(null)}
        />
      )}
    </DndContext>
  )
}

/**
 * Tashlangandan keyingi oyna — `CandidateStagePicker` dagi tanlov AYNAN.
 *
 * «Suhbat taklifi» bayrog'i qo'yilgan bosqich suhbat oynasini ochadi
 * (sana va vaqt so'raladi, nomzodga Telegram xabari ketadi), qolganlari —
 * bosqich oynasini. Ikkala oyna ham TO'LIQ nomzodni oladi (suhbat vaqti
 * ro'yxat qatorida yo'q), shu sababli karta avval yuklanadi. U odatda
 * keshda bo'lmaydi, lekin so'rov bir zumlik va oyna shundan keyin ochiladi.
 */
function DroppedCardDialog({
  candidateId,
  stage,
  onClose,
}: {
  candidateId: string
  stage: PipelineStage
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const candidate = useCandidate(candidateId)

  /*
    Nomzodni yuklab bo'lmadi — jim qolinmaydi: aks holda HR kartani
    tashlagani «hech narsa bo'lmadi» bo'lib qolardi. Xato oynada, karta
    esa joyida (hech narsa yozilmagan).
  */
  if (candidate.isError) {
    return (
      <Dialog open onClose={onClose} title={t('recruiting.funnelMoveFailed')}>
        <Alert tone="danger">{errorMessage(recruitingErrorCode(candidate.error))}</Alert>
      </Dialog>
    )
  }

  if (candidate.data === undefined) return null

  return stage.invitesToInterview ? (
    <CandidateInterviewDialog candidate={candidate.data} stage={stage} open onClose={onClose} />
  ) : (
    <CandidateStageDialog candidate={candidate.data} lockedStage={stage} open onClose={onClose} />
  )
}

/* ═══════════════════════════════════════════════════════════════ ustun */

/**
 * Bitta bosqich ustuni.
 *
 * Hook (`useFunnelColumn`) aynan shu yerda chaqiriladi va shuning uchun
 * ustun alohida komponent: React hook'ni sikl ichida chaqirib
 * bo'lmaydi, ya'ni «har bosqichga bitta so'rov» qoidasi boshqacha
 * yozilishi mumkin emas.
 */
function FunnelColumn({
  stage,
  filters,
  draggable,
  showVacancy,
  onOpen,
  onShowAll,
}: {
  stage: PipelineStage
  filters: FunnelFilters
  draggable: boolean
  /** Vakansiya filtri qo'yilmagan — kartada vakansiya nomi kerak. */
  showVacancy: boolean
  onOpen: (candidate: CandidateListItem) => void
  onShowAll: (stage: PipelineStage) => void
}) {
  const { t, num } = useLocale()

  const query = useFunnelColumn(filters, stage.id)

  const items = query.data?.items ?? []
  const total = query.data?.total ?? 0
  const hidden = Math.max(0, total - items.length)

  const body = (
    <>
      {query.isPending &&
        [0, 1, 2].map((index) => <Skeleton key={index} className="h-[104px] w-full" />)}

      {!query.isPending && items.length === 0 && (
        <p className="py-6 text-center text-caption1 text-[var(--fg-tertiary)]">
          {t('recruiting.funnelColumnEmpty')}
        </p>
      )}

      {items.map((item) =>
        draggable ? (
          <DraggableCard
            key={item.id}
            item={item}
            showVacancy={showVacancy}
            onOpen={() => onOpen(item)}
          />
        ) : (
          <FunnelCardButton
            key={item.id}
            item={item}
            showVacancy={showVacancy}
            onOpen={() => onOpen(item)}
          />
        ),
      )}

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
    </>
  )

  const shell = { name: stage.name, total: query.isPending ? null : total }

  return draggable ? (
    <DroppableColumn stageId={stage.id} {...shell}>
      {body}
    </DroppableColumn>
  ) : (
    <ColumnShell {...shell}>{body}</ColumnShell>
  )
}

/**
 * Ustunning tashlash maydoni.
 *
 * NEGA alohida komponent: `useDroppable` FAQAT `DndContext` ichida
 * ma'noga ega, sudrash huquqi bo'lmaganda esa kontekst umuman
 * o'rnatilmaydi. Hook'ni shartli chaqirib bo'lmaydi, shu sababli
 * shartlilik komponent darajasiga chiqarilgan — ustunning ko'rinishi
 * esa ikkala holatda ham bitta joydan (`ColumnShell`) keladi.
 */
function DroppableColumn({
  stageId,
  name,
  total,
  children,
}: {
  stageId: string
  name: string
  total: number | null
  children: ReactNode
}) {
  const drop = useDroppable({ id: stageId })

  return (
    <ColumnShell name={name} total={total} dropRef={drop.setNodeRef} active={drop.isOver}>
      {children}
    </ColumnShell>
  )
}

/**
 * Ustun yuzasi.
 *
 * Maqsad ustun YUMSHOQ belgilanadi: fon tint rangining eng suyultirilgan
 * variantiga o'tadi, xolos. Chegara chizish, sakrash yoki rang portlashi
 * yo'q — iOS'da nishon «yonmaydi», u shunchaki ajralib turadi.
 */
function ColumnShell({
  name,
  total,
  dropRef,
  active = false,
  children,
}: {
  name: string
  /** `null` — sanoq hali ma'lum emas (birinchi yuklash). */
  total: number | null
  dropRef?: (element: HTMLElement | null) => void
  active?: boolean
  children: ReactNode
}) {
  const { num } = useLocale()

  return (
    <section
      ref={dropRef}
      className={cn(
        'flex w-[280px] shrink-0 flex-col rounded-lg',
        'transition-colors duration-200 ease-[var(--ease-ios)]',
        active ? 'bg-[var(--accent-soft)]' : 'bg-[var(--bg-sunken)]',
      )}
    >
      <header className="flex items-center gap-2 px-3.5 py-3">
        <h3 className="truncate-1 text-subhead font-semibold" title={name}>
          {name}
        </h3>
        {total !== null && (
          <Badge size="sm" tone={total === 0 ? 'neutral' : 'brand'} className="ml-auto">
            {num(total)}
          </Badge>
        )}
      </header>

      <div className="flex-1 space-y-2 px-2.5 pb-2.5">{children}</div>
    </section>
  )
}

/* ═════════════════════════════════════════════════════════════ kartochka */

/**
 * Kartochka yuzasi — sudraladigani ham, oddiysi ham AYNAN shu.
 *
 * `press` qoldirilgan: sichqoncha 6 pikselgacha yurmaguncha bu hali
 * bosish, ya'ni kichrayish javobi to'g'ri. Sudrash boshlangach karta
 * o'zi so'nadi va ko'tarilgan nusxaga (`LiftedCard`) o'tadi.
 */
const CARD_SURFACE = cn(
  'press block w-full rounded-md border border-[var(--border)] bg-[var(--bg-elevated)]',
  'px-3 py-2.5 text-left transition-colors hover:bg-[var(--bg-hover)]',
)

/** Ruxsatsiz foydalanuvchi ko'radigan karta — hech qanday sudrash izi yo'q. */
function FunnelCardButton({ item, showVacancy, onOpen }: CardProps & { onOpen: () => void }) {
  return (
    <button type="button" onClick={onOpen} className={CARD_SURFACE}>
      <CardFace item={item} showVacancy={showVacancy} />
    </button>
  )
}

/**
 * Sudraladigan karta.
 *
 * <b>Enter — kartani OCHADI, bo'sh joy — ko'taradi.</b> dnd-kit sukut
 * bo'yicha ikkalasini ham sudrashga oladi va u holda klaviatura bilan
 * nomzod kartasiga umuman kirib bo'lmasdi. Tugmalar `KEYBOARD_CODES`
 * da ajratilgan.
 *
 * `touch-manipulation` — barmoq bilan taxtani aylantirish uchun SHART:
 * `touch-none` bo'lsa brauzer surishni butunlay to'xtatardi va
 * ushlab turish sensori (220 ms) o'z ishini qilolmasdi.
 */
function DraggableCard({ item, showVacancy, onOpen }: CardProps & { onOpen: () => void }) {
  const { t } = useLocale()

  const drag = useDraggable({
    id: item.id,
    attributes: { roleDescription: t('recruiting.funnelCardRole') },
  })

  return (
    <button
      type="button"
      ref={drag.setNodeRef}
      onClick={onOpen}
      {...drag.attributes}
      {...drag.listeners}
      className={cn(
        CARD_SURFACE,
        'cursor-grab touch-manipulation active:cursor-grabbing',
        // Asl karta joyida qoladi, lekin so'nadi: ustun «yiqilib»
        // ketmaydi va nomzod qayerdan chiqqani ko'rinib turadi
        drag.isDragging && 'opacity-40',
      )}
    >
      <CardFace item={item} showVacancy={showVacancy} />
    </button>
  )
}

/**
 * Ko'tarilgan nusxa — sudrash paytida barmoq/kursor ostida turadigan karta.
 *
 * Ko'tarilish uchtadan iborat: suzuvchi soya, 1.5° burilish va 2%
 * kattalashish. Ko'proq narsa (katta burchak, sakrash, rang o'zgarishi)
 * iOS ruhiga zid — u yerda ko'tarilgan element o'zgarmaydi, faqat
 * yuzadan ajraladi.
 */
function LiftedCard({ item, showVacancy }: CardProps) {
  return (
    <div
      className={cn(
        'w-full rounded-md border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2.5',
        'rotate-[1.5deg] scale-[1.02] cursor-grabbing shadow-[var(--shadow-floating)]',
      )}
    >
      <CardFace item={item} showVacancy={showVacancy} />
    </div>
  )
}

interface CardProps {
  item: CandidateListItem
  /** Vakansiya filtri qo'yilmagan — kartada vakansiya nomi kerak. */
  showVacancy: boolean
}

/**
 * Voronkadagi nomzod kartochkasining ichi.
 *
 * ⭐ Ma'lumot KO'PAYTIRILDI (foydalanuvchi talabi, 2026-09-26): ilgari
 * faqat bir qatorga kesilgan F.I.Sh., telefon va manba ikonasi bor edi
 * va HR kimni ko'rayotganini kartani ochmasdan bilolmasdi. Endi:
 *
 * - F.I.Sh. ikki qatorgacha — o'zbekcha to'liq ism odatda sig'adi;
 * - vakansiya — faqat vakansiya filtri YO'Q bo'lganda (filtr bilan har
 *   kartada bir xil matn takrorlanardi);
 * - telefon;
 * - pastki qator: manba (ikona + nom) va sana — keyingi aloqa rejasi
 *   bo'lsa o'sha (muddati kelgan bo'lsa ogohlantirish rangida), bo'lmasa
 *   kiritilgan sana.
 */
function CardFace({ item, showVacancy }: CardProps) {
  const { t, date } = useLocale()

  const look = CANDIDATE_LOOK[item.status]
  const source = SOURCE_LOOK[item.source]
  const SourceIcon = source.icon

  // ISO sanalar (`YYYY-MM-DD`) satr sifatida to'g'ri solishtiriladi
  const contactDue = item.nextContactDate !== null && item.nextContactDate <= today()

  return (
    <>
      <span className="flex items-start gap-2">
        <span className="truncate-2 flex-1 text-subhead font-medium" title={item.fullName}>
          {item.fullName}
        </span>

        {item.hasResume && (
          <Paperclip
            className="mt-1 size-3.5 shrink-0 text-[var(--fg-tertiary)]"
            strokeWidth={2}
            aria-label={t('recruiting.hasResume')}
          />
        )}
      </span>

      {showVacancy && item.vacancy !== null && (
        <span className="mt-1 flex items-center gap-1.5 text-caption1 text-[var(--fg-secondary)]">
          <BriefcaseBusiness className="size-3 shrink-0" strokeWidth={2} />
          <span className="truncate-1" title={item.vacancy}>
            {item.vacancy}
          </span>
        </span>
      )}

      {item.phone !== null && (
        <span className="tabular mt-1 flex items-center gap-1.5 text-caption1 text-[var(--fg-secondary)]">
          <Phone className="size-3 shrink-0" strokeWidth={2} />
          {formatPhone(item.phone)}
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

      <span className="mt-2 flex items-center gap-2 border-t border-[var(--border)] pt-2 text-caption2 text-[var(--fg-tertiary)]">
        <span className="flex min-w-0 items-center gap-1">
          <SourceIcon className="size-3 shrink-0" strokeWidth={2} />
          <span className="truncate-1">{t(`recruiting.source.${source.key}`)}</span>
        </span>

        {item.nextContactDate !== null ? (
          <span
            className={cn(
              'tabular ml-auto flex shrink-0 items-center gap-1',
              contactDue && 'font-medium text-ios-orange',
            )}
            title={t('recruiting.nextContact')}
          >
            <CalendarClock className="size-3" strokeWidth={2} />
            {date(item.nextContactDate)}
          </span>
        ) : (
          <span className="tabular ml-auto shrink-0" title={t('recruiting.createdAt')}>
            {date(item.createdAt)}
          </span>
        )}
      </span>
    </>
  )
}

/* ══════════════════════════════════════════════════ sudrash mexanikasi */

/**
 * Klaviatura: BO'SH JOY ko'taradi va qo'yadi, Enter esa kartani OCHADI.
 *
 * dnd-kit ning sukuti — ikkalasi ham sudrashni boshlaydi. Karta esa
 * `<button>`: Enter uni ochishi kerak, aks holda klaviatura bilan
 * ishlaydigan foydalanuvchi nomzod kartasiga umuman kira olmasdi.
 * Sudrash BOSHLANGANDAN keyin Enter «qo'y» degani bo'ladi — o'sha
 * paytda tugmaning oddiy bosilishi baribir to'sib qo'yiladi.
 */
const KEYBOARD_CODES: KeyboardCodes = {
  start: [KeyboardCode.Space],
  cancel: [KeyboardCode.Esc],
  end: [KeyboardCode.Space, KeyboardCode.Enter],
}

/**
 * Klaviatura bilan sudrash: chap/o'ng — QO'SHNI USTUN.
 *
 * dnd-kit ning sukut bo'yicha hisoblagichi kartani har bosishda 25
 * pikselga suradi — 280 pikselli ustunga o'tish uchun o'n ikki marta
 * bosish kerak bo'lardi. Bu yerda esa taxtaning haqiqiy tuzilishi
 * ishlatiladi: nishonlar faqat USTUNLAR, ya'ni bosish «keyingi
 * bosqich» degani. Yuqori/quyi o'qlar ataylab hech narsa qilmaydi —
 * ustun ichida kartaning o'rni saqlanmaydi, demak ularning ma'nosi
 * ham yo'q.
 */
const columnCoordinates: KeyboardCoordinateGetter = (event, { currentCoordinates, context }) => {
  const step = event.code === KeyboardCode.Left ? -1 : event.code === KeyboardCode.Right ? 1 : 0
  if (step === 0) return

  event.preventDefault()

  const { collisionRect, droppableContainers, droppableRects } = context
  if (collisionRect === null) return

  // Ustunlar — ekrandagi tartibda, chapdan o'ngga
  const columns = droppableContainers
    .getEnabled()
    .map((container) => droppableRects.get(container.id))
    .filter((rect): rect is ClientRect => rect !== undefined)
    .sort((left, right) => left.left - right.left)

  // Karta hozir qaysi ustun ustida turibdi (markazlar orasidagi masofa)
  const center = collisionRect.left + collisionRect.width / 2
  let index = -1
  let nearest = Number.POSITIVE_INFINITY

  columns.forEach((rect, at) => {
    const distance = Math.abs(rect.left + rect.width / 2 - center)
    if (distance < nearest) {
      nearest = distance
      index = at
    }
  })

  const from = columns[index]
  const to = columns[index + step]

  // Chekkadagi ustun — bundan narisi yo'q, karta joyida qoladi
  if (from === undefined || to === undefined) return

  return { x: currentCoordinates.x + (to.left - from.left), y: currentCoordinates.y }
}

/**
 * Qayerga tashlanadi.
 *
 * Sichqoncha va barmoq uchun — KURSOR ostidagi ustun: foydalanuvchi
 * kartaning geometrik markazini emas, o'zi ko'rsatayotgan joyni
 * kutadi. Ustunlar orasidagi bo'shliqda va klaviatura bilan
 * sudralganda kursor umuman yo'q — o'shanda eng yaqin markaz olinadi
 * (ustunlar bir xil balandlikda, ya'ni bu sof gorizontal taqqoslash).
 */
const funnelCollision: CollisionDetection = (args) => {
  const underPointer = pointerWithin(args)
  return underPointer.length > 0 ? underPointer : closestCenter(args)
}
