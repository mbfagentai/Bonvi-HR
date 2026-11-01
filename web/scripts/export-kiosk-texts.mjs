/**
 * Kiosk anketasining matnlarini HRMentor katalogi uchun backendga eksport qiladi
 * (SPEC-blankalar-hrmentor §4).
 *
 * <b>Nima uchun.</b> HR anketa matnlarini HRMentor da tahrirlaydi. Katalogni (har bir
 * matnning kaliti, nomi va asl matni) BonviHR backendi yuboradi — u esa `web/` dagi
 * tarjimalarni ko'rmaydi. Shu sababli kiosk kalitlari bitta faylga yig'iladi va backend
 * uni o'ziga qo'shib qo'yadi (`EmbeddedResource`):
 *
 *     api/src/Bonvi.Application/Recruiting/Forms/Texts/kiosk-texts.json
 *
 * Fayl QO'LDA tahrirlanmaydi. Tarjima o'zgarsa — shu skript qayta ishga tushiriladi:
 *
 *     node web/scripts/export-kiosk-texts.mjs
 *
 * `check:locales` (`./bonvi check`) fayl eskirganini sezadi va yiqitadi.
 *
 * <b>Guruhlar.</b> Har bir matn anketaning qaysi ekranida turgani bo'yicha guruhlanadi —
 * HR uni o'sha ekran nomi ostida qidiradi. Ekran maydonlari `kiosk-options.ts` dagi
 * `SCREEN_FIELDS` dan O'QILADI (qo'lda takrorlanmaydi); qator ichidagi maydonlar
 * (o'quv yurti, ish joyi, …) pastdagi `ROW_FIELDS` da. Hech qaysi guruhga tushmagan
 * yangi kalit skriptni YIQITADI — u katalogdan jim tushib qolmasin.
 *
 * <b>Katalogga kirmaydi:</b> variant nomlari (`kiosk.housing.*`, `kiosk.marital.*`, …) —
 * ular HRMentor da RO'YXAT bo'lib tahrirlanadi va faylning `texts` qismida turadi,
 * guruhlarda emas; HR ning o'z ekranlari (`kiosk.link.*`, `kiosk.hrSession.*`) — nomzod
 * ularni ko'rmaydi.
 */
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const WEB_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..')
const LOCALES = join(WEB_ROOT, 'src', 'shared', 'i18n', 'locales')
const OPTIONS_SOURCE = join(WEB_ROOT, 'src', 'features', 'kiosk', 'lib', 'kiosk-options.ts')

/** Backenddagi fayl — `BONVI_API_DIR` (Docker) yoki qo'shni `api/` jildi. */
export function kioskTextsTarget(apiDir = process.env.BONVI_API_DIR ?? join(WEB_ROOT, '..', 'api')) {
  return join(apiDir, 'src', 'Bonvi.Application', 'Recruiting', 'Forms', 'Texts', 'kiosk-texts.json')
}

/** Kioskning tillari — `KIOSK_LANGUAGES` (`kiosk-api.ts`). */
export const KIOSK_TEXT_LANGUAGES = ['uz-Latn', 'uz-Cyrl', 'ru']

/** Variant nomlari — HRMentor da ro'yxat bo'lib tahrirlanadi. */
const OPTION_PREFIXES = [
  'kiosk.housing.',
  'kiosk.marital.',
  'kiosk.military.',
  'kiosk.startAfter.',
  'kiosk.education.',
  'kiosk.language.',
  'kiosk.relation.',
  'kiosk.computer.group.',
  'kiosk.computer.skill.',
]

/**
 * Faqat konstruktor anketasining so'zlari (SPEC-anketa-konstruktori §5.3, §7). Konstruktor
 * anketasi amalda bo'lganda HR matnlari qo'llanmaydi — so'zlar ta'rifning o'zida, ustaning
 * o'z so'zlari (tugmalar, xatolar) esa tarjima fayllarida. Shu sababli o'rnatilgan anketa
 * katalogiga kirmaydi: HRMentor da tahrirlansa ham hech qayerda ko'rinmas edi.
 */
const CONSTRUCTOR_ONLY_PREFIXES = [
  'kiosk.dyn.',
  'kiosk.error.ageRange',
  'kiosk.error.notBeforeToday',
  'kiosk.error.phoneAny',
  'kiosk.error.monthFuture',
  'kiosk.error.selectedMin',
  'kiosk.error.selectedMax',
  'kiosk.error.levelsAll',
  'kiosk.error.otherName',
  'kiosk.error.rowsMin',
  'kiosk.error.rowsMax',
  'kiosk.error.characters',
  'kiosk.error.invalid',
]

/** HR ning o'z ekranlari — nomzod ko'rmaydi (`kiosk.preview.*` — HR ning konstruktor ko'rinishi). */
const HR_ONLY_PREFIXES = ['kiosk.link.', 'kiosk.hrSession.', 'kiosk.preview.']

/** Qator ichidagi va ekranga yordamchi maydonlar → ekran. */
const ROW_FIELDS = {
  institution: 'education',
  faculty: 'education',
  startYear: 'education',
  endYear: 'education',
  diplomaNumber: 'education',
  organization: 'work',
  position: 'work',
  jobFrom: 'work',
  jobTo: 'work',
  jobCurrent: 'work',
  leaveReason: 'work',
  fullName: 'references',
  organizationPosition: 'references',
  contactPhone: 'references',
  relation: 'family',
  birthYear: 'family',
  workplace: 'family',
  address: 'family',
  sameAddress: 'address',
  referralSource: 'referral',
  referralPlaceholder: 'referral',
  referralDetailPlaceholder: 'referral',
  healthHint: 'health',
  signatureNameHint: 'signature',
}

/** Kalit (yoki uning boshi) → ekran: ro'yxat tugmalari, oynalar, izohlar. */
const KEY_SCREENS = [
  ['kiosk.addEducation', 'education'],
  ['kiosk.sheet.education', 'education'],
  ['kiosk.addJob', 'work'],
  ['kiosk.sheet.job', 'work'],
  ['kiosk.work.', 'work'],
  ['kiosk.addReference', 'references'],
  ['kiosk.sheet.reference', 'references'],
  ['kiosk.references.', 'references'],
  ['kiosk.addFamilyMember', 'family'],
  ['kiosk.sheet.family', 'family'],
  ['kiosk.family.', 'family'],
  ['kiosk.vacancy.', 'vacancy'],
  ['kiosk.photo.', 'vacancy'],
  ['kiosk.signature.', 'signature'],
  ['kiosk.languages.', 'languages'],
  ['kiosk.computer.', 'computer'],
  ['kiosk.about.hint', 'about'],
  ['kiosk.consent.', 'consent'],
]

/** Ekranga bog'lanmagan umumiy matnlar — o'z guruhlari bilan (tartib — katalogdagi). */
const GENERAL_GROUPS = [
  { id: 'start', title: 'Boshlash sahifasi', prefixes: ['kiosk.start.', 'kiosk.unavailable.'] },
  {
    id: 'wizard',
    title: 'Anketa tugmalari va xabarlari',
    prefixes: ['kiosk.wizard.', 'kiosk.idle.', 'kiosk.submit.', 'kiosk.offline.'],
  },
  { id: 'common', title: 'Umumiy so‘zlar', prefixes: ['kiosk.yes', 'kiosk.no', 'kiosk.choose', 'kiosk.row.'] },
  { id: 'errors', title: 'Xato xabarlari', prefixes: ['kiosk.error.'] },
  { id: 'done', title: 'Yakunlash sahifasi', prefixes: ['kiosk.done.'] },
]

function flatten(object, prefix = '') {
  const out = []
  for (const [key, value] of Object.entries(object)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) out.push(...flatten(value, path))
    else out.push([path, value])
  }
  return out
}

/** `prefix` — aniq kalit yoki nuqta bilan tugagan bosh qism. */
function matches(key, prefix) {
  return prefix.endsWith('.') ? key.startsWith(prefix) : key === prefix
}

/** `export const NAME = [ 'a', ... ]` va `SCREEN_FIELDS` — TS faylidan, ishga tushirmasdan. */
function readScreens(source) {
  const list = /export const WIZARD_SCREENS\s*=\s*\[([^\]]*)\]/s.exec(source)
  const fields = /export const SCREEN_FIELDS[^=]*=\s*\{(.*?)\n\}/s.exec(source)
  if (!list || !fields) throw new Error(`${OPTIONS_SOURCE}: WIZARD_SCREENS yoki SCREEN_FIELDS topilmadi`)

  const screens = [...list[1].matchAll(/'([^']+)'/g)].map((m) => m[1])
  const owner = new Map()
  for (const entry of fields[1].matchAll(/(\w+)\s*:\s*\[([^\]]*)\]/gs)) {
    for (const field of entry[2].matchAll(/'([^']+)'/g)) owner.set(field[1], entry[1])
  }
  return { screens, owner }
}

/**
 * Fayl mazmuni (satr, oxirida yangi qator). Xato bo'lsa — `Error` (qaysi kalit, nima uchun).
 * `check-locales.mjs` ham shu funksiyani chaqiradi.
 */
export function buildKioskTexts() {
  const bundles = Object.fromEntries(
    KIOSK_TEXT_LANGUAGES.map((lang) => [
      lang,
      new Map(flatten(JSON.parse(readFileSync(join(LOCALES, `${lang}.json`), 'utf8')))),
    ]),
  )
  const { screens, owner } = readScreens(readFileSync(OPTIONS_SOURCE, 'utf8'))
  const latin = bundles['uz-Latn']
  const keys = [...latin.keys()].filter(
    (key) =>
      key.startsWith('kiosk.') &&
      !HR_ONLY_PREFIXES.some((p) => matches(key, p)) &&
      !CONSTRUCTOR_ONLY_PREFIXES.some((p) => matches(key, p)),
  )

  const problems = []
  const texts = Object.fromEntries(KIOSK_TEXT_LANGUAGES.map((lang) => [lang, {}]))
  for (const key of keys) {
    for (const lang of KIOSK_TEXT_LANGUAGES) {
      const value = bundles[lang].get(key)
      if (typeof value !== 'string' || value.trim() === '') problems.push(`${lang}: ${key} — matn yo'q`)
      else texts[lang][key] = value
    }
  }

  const screenOf = (key) => {
    const screen = /^kiosk\.screen\.(\w+)$/.exec(key)
    if (screen) return screen[1]
    const field = /^kiosk\.field\.(\w+)$/.exec(key)
    if (field) return owner.get(field[1]) ?? ROW_FIELDS[field[1]]
    const about = /^kiosk\.about\.q\.(\w+)$/.exec(key)
    if (about) return owner.get(about[1])
    return KEY_SCREENS.find(([prefix]) => matches(key, prefix))?.[1]
  }

  const byGroup = new Map([...screens, ...GENERAL_GROUPS.map((g) => g.id)].map((id) => [id, []]))
  for (const key of keys) {
    if (OPTION_PREFIXES.some((p) => matches(key, p))) continue
    const screen = screenOf(key)
    const general = GENERAL_GROUPS.find((g) => g.prefixes.some((p) => matches(key, p)))?.id
    const group = screen ?? general
    if (!group || !byGroup.has(group)) {
      problems.push(`${key} — qaysi ekranga tegishli ekani noma'lum (export-kiosk-texts.mjs)`)
      continue
    }
    byGroup.get(group).push(key)
  }

  if (problems.length > 0) throw new Error(`Kiosk matnlari eksport qilinmadi:\n  ${problems.join('\n  ')}`)

  // HRMentor dagi guruh: `screen.<ekran>` yoki `general.<nom>`; sarlavha — HR ko'radigan nom
  const groups = [
    ...screens.map((id) => ({
      id: `screen.${id}`,
      title: `Anketa — ${latin.get(`kiosk.screen.${id}`) ?? id}`,
      keys: byGroup.get(id),
    })),
    ...GENERAL_GROUPS.map(({ id, title }) => ({ id: `general.${id}`, title: `Anketa — ${title}`, keys: byGroup.get(id) })),
  ].filter((group) => group.keys.length > 0)

  return `${JSON.stringify({ groups, texts }, null, 2)}\n`
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const target = kioskTextsTarget()
  writeFileSync(target, buildKioskTexts(), 'utf8')
  console.log(`✓ ${target}`)
}
