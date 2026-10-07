import { getRouteApi, useNavigate } from '@tanstack/react-router'
import {
  ArrowLeft,
  ArrowRight,
  ArrowRightLeft,
  Bookmark,
  DoorOpen,
  Pencil,
  ShieldAlert,
  Undo2,
  UserCheck,
  UserRoundX,
} from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { EMPLOYEES_CREATE, EMPLOYMENT_HIRE } from '@/features/employees/lib/employee-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { formatPhone } from '@/shared/lib/phone'
import {
  Alert,
  Badge,
  Button,
  Card,
  CardContent,
  SegmentedControl,
  Skeleton,
} from '@/shared/ui'
import type { Candidate, CandidateStatus } from '../api/recruiting-api'
import { useCandidate, useStages, useVacancyOptions } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import {
  CANDIDATE_LOOK,
  RECRUITING_MANAGE,
  SOURCE_LOOK,
} from '../lib/recruiting-presentation'
import { CandidateActionDialog, type CandidateAction } from './candidate-action-dialog'
import { CandidateFilesCard } from './candidate-files-card'
import { CandidateFormDialog } from './candidate-form-dialog'
import { CandidateHireDialog } from './candidate-hire-dialog'
import { CandidatePhotoEditor } from './candidate-photo-editor'
import { CandidateQuestionnaireCard } from './candidate-questionnaire-card'
import { CandidateStagePicker } from './candidate-stage-picker'
import { CandidateStagesTab } from './candidate-stages-tab'

const route = getRouteApi('/_app/nomzodlar/$candidateId')

type Tab = 'overview' | 'stages'

/**
 * ⭐ Nomzod kartasi — 2-bosqichning markaziy ekrani.
 *
 * Ikkita tab (`employee-card` dagi bilan bir xil naqsh: bir vaqtda
 * hammasini ko'rsatish sahifani cho'zib yuborardi, HR esa odatda
 * bittasiga qaraydi):
 *   · <b>Umumiy</b> — shaxsiy ma'lumot, botda to'ldirilgan anketa,
 *     biriktirilgan fayllar va voronkadan chiqarish amallari;
 *   · <b>Bosqichlar</b> — vertikal lenta va natija yozish oynasi.
 *
 * Uchinchi tab — «Takliflar» — ish taklifi funksiyasi bilan birga
 * o'chirildi (`docs/SPEC-nomzod-yakuni.md`, 3-bo'lim).
 *
 * <b>Nega tab ro'yxati DINAMIK emas.</b> Xodim kartasidan farqli
 * (u yerda tab'lar alohida ruxsatlarga bog'liq), butun rekruting
 * IKKITA ruxsat bilan boshqariladi: `recruiting.view` — hammasini
 * ko'radi, `recruiting.manage` — o'zgartiradi. Ya'ni sahifaga kira
 * olgan odam ikkala tabni ham ko'ra oladi.
 */
export function CandidateCardPage() {
  const { candidateId } = route.useParams()
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const { hasPermission } = useAuth()
  const canManage = hasPermission(RECRUITING_MANAGE)

  const [tab, setTab] = useState<Tab>('overview')
  const [editing, setEditing] = useState(false)
  const [hiring, setHiring] = useState(false)
  const [action, setAction] = useState<CandidateAction | null>(null)

  const candidate = useCandidate(candidateId)

  /*
   * Tahrirlash oynasi vakansiya va bosqich ro'yxatlarini so'raydi,
   * lekin ular MAVJUD nomzodda ko'rsatilmaydi (forma ularni faqat
   * yaratishda chizadi). Bo'sh massiv berish mumkin edi; bir xil
   * hook'ni chaqirish esa keshdan keladi va nomzod boshqa vakansiyaga
   * o'tkazilganda ro'yxat allaqachon tayyor turadi.
   */
  const vacancies = useVacancyOptions('Open')
  const stages = useStages(true)

  if (candidate.isPending) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-24 w-full rounded-lg" />
        <Skeleton className="h-72 w-full rounded-lg" />
      </div>
    )
  }

  if (candidate.isError || !candidate.data) {
    return (
      <div className="space-y-4">
        <BackButton onClick={() => void navigate({ to: '/nomzodlar' })} label={t('common.back')} />
        <Alert tone="danger">{errorMessage(recruitingErrorCode(candidate.error))}</Alert>
      </div>
    )
  }

  const person = candidate.data
  const look = CANDIDATE_LOOK[person.status]
  const source = SOURCE_LOOK[person.source]
  const SourceIcon = source.icon
  const inProgress = person.status === 'InProgress'
  const inReserve = person.status === 'Reserve'
  /*
    ⭐ Voronkaga qaytarish UCH holatdan mumkin (foydalanuvchi talabi,
    2026-09-16): rezervdan, RAD ETILGANDAN va o'zi VOZ KECHGANDAN.
    Amalda rad etilgan odam ko'pincha o'zi qayta aloqaga chiqadi yoki
    sharoit o'zgaradi — uni noldan kiritish esa butun tarixni, anketani
    va fayllarni yo'qotardi.

    «Ishga olingan» bu ro'yxatda YO'Q va server ham uni rad etadi: u
    bir vaqtda ham xodim, ham faol nomzod bo'lib qolardi.
  */
  const inBlacklist = person.status === 'Blacklisted'
  const canReturn =
    inReserve || person.status === 'Rejected' || person.status === 'Withdrawn' || inBlacklist
  /*
    ⭐ Qora ro'yxatga YOPILGAN nomzod ham olinadi (2026-10-07): amalda HR
    odamni avval rad etadi, keyin (masalan, stajirovkadagi janjaldan so'ng)
    qora ro'yxatga oladi. Faqat ishga olingan va allaqachon qora
    ro'yxatdagi nomzodda tugma yo'q — server ham ularni rad etadi.
  */
  const canBlacklist = person.status !== 'Hired' && !inBlacklist
  /*
    ⭐ Ishga olish IKKI holatdan mumkin: voronkadagi nomzod va
    REZERVDAGI. Rezervning butun ma'nosi shu — o'rin bo'shaganda HR
    izlanishni noldan boshlamaydi. Serverga ketadigan ikkinchi so'rov
    (avval voronkaga qaytarish) oynaning o'zida, HR uchun bu bitta
    qaror bo'lib qoladi (`candidate-hire-dialog.tsx`).
  */
  /*
    «Ishga qabul qilish» va «Xodim qo'shish» ruxsatlari ham SHART: ishga
    olish xodim kartasi, mehnat munosabati va buyruq yaratadi, server ham
    shu ruxsatlarni talab qiladi (2026-09-26). Tugma ruxsatsiz odamga
    umuman chizilmaydi.
  */
  const canHire =
    (inProgress || inReserve) && hasPermission(EMPLOYMENT_HIRE) && hasPermission(EMPLOYEES_CREATE)

  return (
    <div className="space-y-5">
      {/*
        «Orqaga» nomzod HAQIQATAN turgan ro'yxatga olib boradi. Rezervdagi
        nomzod «Nomzodlar» ro'yxatida KO'RINMAYDI (2026-09-26 dan uning o'rni
        «Rezerv» bo'limida), rad etilgan va o'zi voz kechgan esa «Rad
        etilganlar» bo'limida — sukut ro'yxatga qaytarish HR ni bu odam
        umuman yo'q ro'yxatga olib borardi. Rezerv sahifasining sukut
        bo'limi — aynan nomzodlar.
      */}
      {inReserve ? (
        <BackButton onClick={() => void navigate({ to: '/rezerv' })} label={t('nav.reserve')} />
      ) : inBlacklist ? (
        <BackButton
          onClick={() => void navigate({ to: '/qora-royxat' })}
          label={t('nav.blacklist')}
        />
      ) : person.status === 'Rejected' || person.status === 'Withdrawn' ? (
        <BackButton
          onClick={() => void navigate({ to: '/nomzodlar', search: { rejected: true } })}
          label={t('nav.rejectedCandidates')}
        />
      ) : (
        <BackButton
          onClick={() => void navigate({ to: '/nomzodlar' })}
          label={t('nav.candidates')}
        />
      )}

      {/* ───────── Sarlavha kartasi ───────── */}
      <Card className="p-5">
        <div className="flex flex-wrap items-start gap-4">
          {/*
            Surat — xodim kartasidagi bilan AYNAN bir joyda va aynan bir
            xil ishlaydi. U ANKETADAN mustaqil: anketasi yo'q nomzodda
            ham (HR qo'lda kiritgan) surat yuklash mumkin, chunki bu
            nomzodning O'ZI haqidagi ma'lumot, anketa javobi emas.
          */}
          <CandidatePhotoEditor
            candidateId={person.id}
            hasPhoto={person.hasPhoto}
            name={person.fullName}
          />

          <div className="min-w-0 flex-1">
            <h1 className="text-title2 leading-tight font-semibold tracking-tight">
              {person.fullName}
            </h1>

            <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">
              {person.vacancy ?? '—'}
              {person.currentStage !== null && ` · ${person.currentStage}`}
            </p>

            <div className="mt-3 flex flex-wrap items-center gap-2">
              <Badge tone={look.tone} size="sm">
                {t(`recruiting.candidateStatus.${look.key}`)}
              </Badge>

              <Badge tone={source.tone} size="sm">
                <SourceIcon className="size-3" strokeWidth={2.5} />
                {t(`recruiting.source.${source.key}`)}
              </Badge>
            </div>
          </div>

          {canManage && (
            <Button
              type="button"
              variant="secondary"
              onClick={() => setEditing(true)}
              className="shrink-0"
            >
              <Pencil />
              {t('common.edit')}
            </Button>
          )}
        </div>
      </Card>

      {/*
        Nomzod voronkadan chiqqan bo'lsa — sabab kartaning eng
        ko'rinadigan joyida. Ishga olingan bo'lsa esa yaratilgan XODIM
        kartasiga havola: HR ko'pincha aynan shuni qidiradi («o'sha
        odamning kartasi qayerda?»).
      */}
      {!inProgress && (
        <Alert tone={closedTone(person.status)}>
          <span className="flex flex-wrap items-center gap-2">
            <span>
              {t(`recruiting.candidateClosed.${look.key}`, {
                date: person.closedAt ?? '—',
              })}
              {person.closingNote !== null && ` · ${person.closingNote}`}
            </span>

            {person.hiredEmployeeId !== null && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => {
                  const employeeId = person.hiredEmployeeId
                  if (employeeId === null) return
                  void navigate({ to: '/xodimlar/$employeeId', params: { employeeId } })
                }}
              >
                {t('recruiting.openEmployee')}
                <ArrowRight />
              </Button>
            )}
          </span>
        </Alert>
      )}

      <SegmentedControl
        value={tab}
        onChange={setTab}
        aria-label={t('employees.tabs')}
        options={[
          { value: 'overview', label: t('recruiting.tabOverview') },
          { value: 'stages', label: t('recruiting.tabStages') },
        ]}
      />

      {tab === 'overview' && (
        <div className="space-y-4">
          <PersonalCard candidate={person} />

          {/*
            Anketa — shaxsiy ma'lumotdan KEYIN, FAYLLARDAN oldin.
            Tartib HR ning o'qish yo'lidan: avval «kim», keyin «nima
            deb javob berdi», oxirida biriktirilgan fayllar (endi
            bittasi emas, o'ntagacha). Nomzodda anketa bo'lmasa karta
            umuman chizilmaydi.
          */}
          <CandidateQuestionnaireCard questionnaire={person.questionnaire} />

          <CandidateFilesCard candidate={person} canManage={canManage} />

          {/*
            ⭐ Bosqich qatori — voronkadan CHIQARISH amallaridan darrov
            YUQORIDA va ataylab alohida kartada. Ikkalasi bitta qatorga
            qo'shilsa, «Гаплашилди» bilan «Рад этиш» yonma-yon turardi:
            bittasi kuniga o'nlab marta bosiladigan odatiy qadam,
            ikkinchisi esa qaytarib bo'lmaydigan yakun.

            Faqat JARAYONDAGI nomzodda: yopilgan nomzodni bosqichga
            o'tkazishni server baribir rad etadi («nomzod.jarayonda_emas»)
            va tugmani ko'rsatib, keyin xato berish HR ni bekorga sarson
            qilardi.
          */}
          {canManage && inProgress && <CandidateStagePicker candidate={person} />}

          {/*
            Voronkadan chiqarish amallari — «Umumiy» tabining ENG
            OXIRIDA. Ular kamdan-kam bosiladi va bosqich lentasining
            yonida turishi kerak emas: HR bosqich yozayotganda «rad
            etish» tugmasi qo'l ostida bo'lsa, tasodifiy bosish
            ehtimoli ortadi.
          */}
          {canManage && (
            <Card>
              <CardContent className="flex flex-wrap gap-2 py-4">
                {/*
                  ⭐ «Ishga olish» — qatordagi YAGONA `primary` tugma va
                  u BIRINCHI turadi. Voronkaning maqsadi shu: qolgan
                  amallar odam OLINMAGANDA bosiladi. Rezervdagi nomzodda
                  ham ko'rinadi — o'rin bo'shaganda uni chaqirish uchun
                  ikkita ekran kezishning ma'nosi yo'q.
                */}
                {canHire && (
                  <Button type="button" size="sm" variant="primary" onClick={() => setHiring(true)}>
                    <UserCheck />
                    {t('recruiting.hireAction')}
                  </Button>
                )}

                {/*
                  Voronkaga qaytarish — rezervdan, rad etilgandan va voz
                  kechgandan. Nomzod qaysi bosqichda to'xtagan bo'lsa,
                  o'sha yerdan davom etadi; rad etish va qaytarish
                  sabablari esa bosqichlar lentasida qoladi.
                */}
                {canReturn && (
                  <Button
                    type="button"
                    size="sm"
                    variant="secondary"
                    onClick={() => setAction('returnToFunnel')}
                  >
                    <Undo2 />
                    {t('recruiting.candidateAction.returnToFunnelTitle')}
                  </Button>
                )}

                {/*
                  Tartib — ENG YAXSHI natijadan eng yomoniga:
                  «olindi» → «boshqa o'ringa» → «rezerv» → «rad etdik» →
                  «o'zi ketdi». HR qatorni chapdan o'qiydi va ro'yxatning
                  o'zi qarorlarni og'irligi bo'yicha saralab beradi.
                */}
                {inProgress && (
                  <>
                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      onClick={() => setAction('reassign')}
                    >
                      <ArrowRightLeft />
                      {t('recruiting.candidateAction.reassignTitle')}
                    </Button>

                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      onClick={() => setAction('reserve')}
                    >
                      <Bookmark />
                      {t('recruiting.candidateAction.reserveTitle')}
                    </Button>

                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      onClick={() => setAction('reject')}
                    >
                      <UserRoundX />
                      {t('recruiting.candidateAction.rejectTitle')}
                    </Button>

                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      onClick={() => setAction('withdraw')}
                    >
                      <DoorOpen />
                      {t('recruiting.candidateAction.withdrawTitle')}
                    </Button>
                  </>
                )}

                {/*
                  Qora ro'yxat — qatorning ENG OXIRIDA: eng og'ir qaror
                  (odam haqida, ariza haqida emas) va kamdan-kam bosiladi.
                */}
                {canBlacklist && (
                  <Button
                    type="button"
                    size="sm"
                    variant="secondary"
                    onClick={() => setAction('blacklist')}
                  >
                    <ShieldAlert />
                    {t('recruiting.candidateAction.blacklistTitle')}
                  </Button>
                )}

                {/*
                  ⚠️ «O'chirish» tugmasi YO'Q va u ataylab olib
                  tashlangan (foydalanuvchi qarori, 2026-09-07): har bir
                  anketa — nomzodning o'zi to'ldirgan ma'lumot va u
                  kelasi izlanishning bazasi. Nomzod YAKUNLANADI
                  (rezerv, rad etish, ishga olish), lekin yo'q
                  qilinmaydi. Server tomonda ham endpoint qolmagan.
                */}
              </CardContent>
            </Card>
          )}
        </div>
      )}

      {tab === 'stages' && <CandidateStagesTab candidate={person} canManage={canManage} />}

      {/* ───────── Oynalar ───────── */}

      <CandidateFormDialog
        open={editing}
        item={person}
        vacancyOptions={vacancies.options}
        stageOptions={(stages.data ?? []).map((stage) => ({
          value: stage.id,
          label: stage.name,
        }))}
        onClose={() => setEditing(false)}
      />

      <CandidateActionDialog
        candidate={person}
        action={action}
        onClose={() => setAction(null)}
      />

      <CandidateHireDialog
        candidate={person}
        open={hiring}
        onClose={() => setHiring(false)}
      />
    </div>
  )
}

/**
 * Shaxsiy ma'lumot.
 *
 * Bo'sh maydon ham KO'RSATILADI (chiziqcha bilan): nomzod anketasida
 * ko'p maydon bo'sh qoladi va «yo'q» ekanini ko'rish ham ma'lumot —
 * HR qaysi ma'lumotni so'rash kerakligini shu yerdan biladi. Xodim
 * kartasida esa teskarisi bo'lardi: u yerda ma'lumot to'liq bo'lishi
 * kutiladi.
 */
function PersonalCard({ candidate }: { candidate: Candidate }) {
  const { t, date } = useLocale()

  return (
    <Card>
      <CardContent className="py-5">
        <dl className="grid gap-x-8 gap-y-4 sm:grid-cols-2">
          <Field label={t('employee.birthDate')} value={candidate.birthDate === null ? null : date(candidate.birthDate)} />
          <Field label={t('employee.phone')} value={formatPhone(candidate.phone)} tabular />
          <Field label={t('employee.speciality')} value={candidate.speciality} />
          <Field label={t('employee.address')} value={candidate.address} />
          <Field label={t('recruiting.vacancy')} value={candidate.vacancy} />
          <Field label={t('recruiting.stage')} value={candidate.currentStage} />
          {/*
            ⭐ Keyingi aloqa sanasi SHU KARTADA, bosqich qatorida emas.
            Sabab ruxsatda: bosqich kartasi faqat `recruiting.manage`
            bo'lganda chiziladi (`canManage && inProgress`), ya'ni
            faqat KO'RISH ruxsati bo'lgan odam rejani boshqa hech
            qaysi ekranda ko'rmasdi (SPEC §5.3 va §6 dagi jadval).

            Ohang ATAYLAB neytral: muddati kelgan sanani bosqich
            kartasidagi ogohlantirish yorlig'i va ro'yxatdagi to'q
            sariq ustun allaqachon belgilaydi — bir ekranda uchinchi
            marta qizartirish shovqin bo'lardi (SPEC §5.2).

            Bo'sh bo'lsa chiziqcha chiqadi: «reja qo'yilmagan» ekanini
            ko'rish ham ma'lumot (kartaning yuqoridagi qoidasi).
          */}
          <Field
            label={t('recruiting.nextContact')}
            value={candidate.nextContactDate === null ? null : date(candidate.nextContactDate)}
          />
        </dl>

        {candidate.note !== null && (
          <p className="mt-5 rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote text-[var(--fg-secondary)]">
            {candidate.note}
          </p>
        )}

        {candidate.closedAt !== null && (
          <p className="tabular mt-4 text-caption2 text-[var(--fg-tertiary)]">
            {t('recruiting.closedAtLabel', { date: date(candidate.closedAt) })}
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function Field({
  label,
  value,
  tabular = false,
}: {
  label: string
  value: string | null
  tabular?: boolean
}) {
  return (
    <div className="min-w-0">
      <dt className="text-caption1 text-[var(--fg-tertiary)]">{label}</dt>
      <dd
        className={
          value === null
            ? 'mt-0.5 text-subhead text-[var(--fg-tertiary)]'
            : `mt-0.5 text-subhead${tabular ? ' tabular' : ''}`
        }
      >
        {value ?? '—'}
      </dd>
    </div>
  )
}

/**
 * Voronkadan chiqqan nomzod yozuvining ohangi.
 *
 * ⚠️ Rezerv — SARIQ EMAS, ko'k (`info`). Sariq «e'tibor bering, nimadir
 * noto'g'ri» degani va o'zi voz kechgan nomzod uchun to'g'ri; rezerv esa
 * HR ning O'Z rejasi — «keyin qaytamiz». Uni ogohlantirish rangida
 * chizish yorliqning kulrang («neytral») ma'nosi bilan ham urishib
 * qolardi (`CANDIDATE_LOOK` izohi).
 */
function closedTone(status: CandidateStatus): 'success' | 'info' | 'warning' | 'danger' {
  if (status === 'Hired') return 'success'
  if (status === 'Reserve') return 'info'
  if (status === 'Blacklisted') return 'danger'
  return 'warning'
}

function BackButton({ onClick, label }: { onClick: () => void; label: string }) {
  return (
    <Button variant="ghost" size="sm" onClick={onClick} className="-ml-2">
      <ArrowLeft />
      {label}
    </Button>
  )
}
