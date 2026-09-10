import { ListFilter, Search, X } from 'lucide-react'
import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { useSearchDraft } from '@/shared/lib/list-search'
import { Button, Checkbox, Input, Select, type SelectOption } from '@/shared/ui'
import {
  CANDIDATE_SOURCES,
  CANDIDATE_STATUSES,
  type CandidateSource,
  type CandidateStatus,
} from '../api/recruiting-api'
import { DEFAULT_FILTERS, type CandidatesFilterState } from '../lib/candidates-search'

/**
 * Qidiruvdan tashqari nechta filtr yoqilgan — tugmadagi sanoq uchun.
 *
 * «Bugun aloqa kerak» ham SANALADI, garchi u satr emas, katakcha bo'lsa
 * ham: u ro'yxatni eng qattiq qisqartiradigan filtr (odatda bir necha
 * qator qoladi). Sanalmasa, yig'ilgan panel ortida ro'yxat sababsiz
 * qisqargandek ko'rinardi va HR nima uchun ekanini topa olmasdi.
 */
function activeFilterCount(filters: CandidatesFilterState): number {
  const chosen = [filters.vacancyId, filters.stageId, filters.status, filters.source].filter(
    (value) => value !== '',
  ).length

  return filters.contactDue ? chosen + 1 : chosen
}

/**
 * Nomzodlar ro'yxati ustidagi boshqaruv.
 *
 * `EmployeesToolbar`/`EmploymentsToolbar` bilan bir xil naqsh: kundalik
 * ishlatiladigan qidiruv doim ko'rinadi, qolgan to'rtta filtr esa
 * yig'ilgan panelda.
 *
 * ⭐ «BOSQICH» filtri bu yerda alohida ahamiyatga ega: u rejadagi
 * «Suhbatlar» bo'limining o'rnini bosadi. Sabab `candidates-search.ts`
 * dagi `stage` maydonining izohida — backendda alohida `Interview`
 * agregati yo'q va qaysi bosqich «suhbat» ekanini kod bila olmaydi,
 * chunki bosqichlar SOZLANADIGAN ma'lumotnoma.
 */
export function CandidatesToolbar({
  filters,
  onChange,
  vacancies,
  stages,
}: {
  filters: CandidatesFilterState
  onChange: (next: CandidatesFilterState) => void
  vacancies: SelectOption[]
  stages: SelectOption[]
}) {
  const { t } = useLocale()

  const count = activeFilterCount(filters)
  const [open, setOpen] = useState(count > 0)

  /*
   * Qidiruv har harfda so'rov yubormaydi — yozish to'xtagach 350 ms
   * kutiladi. Mantiq `useSearchDraft` da: u endi to'qqizta ro'yxatda
   * ishlatiladi va nusxalar bir-biridan ajralib ketmasligi kerak.
   */
  const [draft, setDraft] = useSearchDraft(filters.search, (value) => {
    onChange({ ...filters, search: value })
  })

  const set = <K extends keyof CandidatesFilterState>(
    key: K,
    value: CandidatesFilterState[K],
  ) => {
    onChange({ ...filters, [key]: value })
  }

  const clearAll = () => {
    onChange(DEFAULT_FILTERS)
    setDraft('')
  }

  const statusOptions = [
    { value: '', label: t('recruiting.anyStatus') },
    ...CANDIDATE_STATUSES.map((status) => ({
      value: status,
      label: t(`recruiting.candidateStatus.${status.charAt(0).toLowerCase()}${status.slice(1)}`),
    })),
  ]

  const sourceOptions = [
    { value: '', label: t('recruiting.anySource') },
    ...CANDIDATE_SOURCES.map((source) => ({
      value: source,
      label: t(`recruiting.source.${source.charAt(0).toLowerCase()}${source.slice(1)}`),
    })),
  ]

  return (
    <div className="border-b border-[var(--border)]">
      <div className="flex flex-wrap items-center gap-2.5 px-4 py-3.5 sm:gap-3 sm:px-5">
        {/*
          Placeholder ATAYLAB nomzodga xos: nomzodda JSHSHIR ham,
          pasport ham bo'lmaydi (u hujjat topshirmagan) — server faqat
          F.I.Sh. va TELEFON bo'yicha qidiradi. Xodimlardagi umumiy
          matnni ko'chirish yolg'on va'da bo'lardi.
        */}
        <div className="min-w-[200px] flex-1">
          <Input
            icon={Search}
            shape="pill"
            type="search"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={t('recruiting.candidateSearchPlaceholder')}
            aria-label={t('common.search')}
          />
        </div>

        <Button
          type="button"
          pill
          variant={open ? 'soft' : 'secondary'}
          aria-expanded={open}
          onClick={() => setOpen((previous) => !previous)}
        >
          <ListFilter />
          <span className="hidden sm:inline">{t('common.filters')}</span>
          {count > 0 && (
            <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
              {count}
            </span>
          )}
        </Button>
      </div>

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Select
              label={t('recruiting.vacancy')}
              placeholder={t('recruiting.anyVacancy')}
              options={vacancies}
              value={filters.vacancyId}
              onChange={(event) => set('vacancyId', event.target.value)}
            />
            <Select
              label={t('recruiting.stage')}
              placeholder={t('recruiting.anyStage')}
              options={stages}
              value={filters.stageId}
              onChange={(event) => set('stageId', event.target.value)}
            />
            {/*
              ⚠️ «Barcha holatlar» — TO'LIQ ro'yxat EMAS: server sukut
              bo'yicha UCHTA holatni chiqarib tashlaydi — ishga
              olinganlar (ular endi xodim va bu ro'yxatda shovqin), rad
              etilganlar va o'zi voz kechganlar (`HideHired`,
              `HideRejected`, `HideWithdrawn`). Izoh shu sababli bor —
              usiz HR ulardan birini qidirib topolmay, «yozuv yo'qolibdi»
              degan xulosaga kelardi.

              Ro'yxatning O'ZI esa har uchtasini ham tanlashga ruxsat
              beradi va bu talabning ikkinchi yarmi: «Rad etilganlar»
              bo'limi ikkita holatni birga ko'rsatadi, `<select>` esa
              ularni AYRIM-AYRIM ajratib bera olishi kerak
              (`rejected=true&status=Withdrawn` — faqat o'zi voz
              kechganlar, SPEC §4.7).

              Rezerv esa KO'RINADI: rezervning butun ma'nosi «kerak
              bo'lganda shu yerdan chaqiramiz» degani.
            */}
            <Select
              label={t('recruiting.status')}
              hint={t('recruiting.statusFilterHint')}
              options={statusOptions}
              value={filters.status}
              onChange={(event) => set('status', event.target.value as CandidateStatus | '')}
            />
            <Select
              label={t('recruiting.sourceLabel')}
              options={sourceOptions}
              value={filters.source}
              onChange={(event) => set('source', event.target.value as CandidateSource | '')}
            />
          </div>

          {/*
            ⭐ «Bugun aloqa kerak» — mijozning aynan shu so'zlari bilan
            aytilgan talab: HR qo'ng'iroq qiladi, nomzod «5-6 kundan keyin
            kelaman» deydi, va ertalab ro'yxatni ochgan HR bugun kim bilan
            gaplashishi kerakligini KO'RISHI kerak.

            Katakcha, `Switch` EMAS: o'tkirgich sozlama («shu ko'rinish
            yoqilgan») ma'nosini beradi, bu esa filtr — bir marta belgilanib,
            ish tugagach bo'shatiladi.

            Panelning ICHIDA, `Select`lar qatoridan keyin: u ro'yxatni eng
            qattiq qisqartiradigan filtr, lekin kundalik qidiruv emas —
            birinchi qatorga chiqarilsa (xodimlardagi «tekshirish kerak»
            kabi) ekranning eng qimmat joyini har kuni bir marta bosiladigan
            tugmaga berardi.
          */}
          <div className="mt-3.5 space-y-1.5">
            <Checkbox
              checked={filters.contactDue}
              onChange={(checked) => set('contactDue', checked)}
              label={t('recruiting.contactDue')}
            />
            {/* Chapdan 30px — katakcha (20px) va oraliq (10px) kengligi:
                izoh yorliq matni bilan bir tekislikda turadi. */}
            <p className="pl-[30px] text-footnote text-[var(--fg-tertiary)]">
              {t('recruiting.contactDueHint')}
            </p>
          </div>

          {(count > 0 || filters.search !== '') && (
            <div className="mt-3.5 flex justify-end">
              <Button type="button" size="sm" variant="ghost" onClick={clearAll}>
                <X />
                {t('employees.clearFilters')}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
