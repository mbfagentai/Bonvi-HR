/**
 * Tarjimalar to'liqligini UCH tomondan tekshiradi.
 *
 * <b>1. To'rt til bir-biriga mos.</b> Yangi kalit qo'shilganda bitta tilda
 * unutilishi oson. U holda foydalanuvchi kalit NOMINI ko'radi
 * («nav.employees»), matnni emas.
 *
 * <b>2. Frontend kodi ishlatgan har bir kalit etalonda bor.</b> (1)-tekshiruv
 * TO'RT TIL bir-biriga mosligini ko'radi, lekin kodning o'zi umuman
 * yo'q kalitni chaqirsa — masalan `t('nav.users')`, u hech qaysi tilda
 * yo'q bo'lsa — (1) buni SEZMAYDI: yo'qlik hammada bab-baravar, demak
 * «mos». Xuddi shu sabab bilan `nav.users` xatosi ekranga ikkinchi marta
 * chiqdi (birinchisi — `employees.events.*`). Bu tekshiruv kodni o'qiydi
 * va har bir `t(...)` chaqiruvini pastdagi izohda tushuntirilgan usulda
 * etalon bilan solishtiradi.
 *
 * <b>3. Backend'ning har bir xato kodi tarjima qilingan.</b> Bu tekshiruv
 * sababi aniq: `useErrorMessage` tarjima topmasa XOM KODNI ekranga
 * chiqaradi. Bir vaqtlar 39 ta kod shu holatda edi va HR «mehnat.tugagan»
 * degan matnni o'qirdi — hech qanday xato chiqmagani uchun buni hech kim
 * sezmasdi. Endi yangi `Error.Validation(...)` yozgan odam tarjimasini
 * ham qo'shmaguncha tekshiruv o'tmaydi.
 *
 * Ishga tushirish:  ./bonvi pnpm check:locales
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { buildKioskTexts, kioskTextsTarget } from './export-kiosk-texts.mjs'

const DIR = new URL('../src/shared/i18n/locales/', import.meta.url).pathname
const REFERENCE = 'uz-Latn.json'

/**
 * Backend manbasi. Skript `web/` ichida, kodlar esa `api/` da —
 * shartnomaning ikki tomoni ikki papkada yotadi va tekshiruv aynan
 * ular orasidagi bo'shliqni ushlaydi.
 *
 * ⚠️ `web` konteyneriga faqat `web/` jildi ulanadi, ya'ni u yerdan
 * `api/` KO'RINMAYDI. To'liq tekshiruv uchun `./bonvi check` ishlatiladi:
 * u loyihaning ildizini ulaydi va yo'lni shu o'zgaruvchi orqali beradi.
 * `pnpm check:locales` esa konteyner ichida ham ishlaydi — u holda
 * ikkinchi tekshiruv o'tkazib yuborilgani ochiq aytiladi.
 */
const API_DIR = process.env.BONVI_API_DIR ?? new URL('../../api/', import.meta.url).pathname

function flatten(object, prefix = '') {
  const keys = []
  for (const [key, value] of Object.entries(object)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      keys.push(...flatten(value, path))
    } else {
      keys.push(path)
    }
  }
  return keys
}

const files = readdirSync(DIR).filter((f) => f.endsWith('.json'))
const reference = new Set(flatten(JSON.parse(readFileSync(join(DIR, REFERENCE), 'utf8'))))

let failed = false

for (const file of files) {
  if (file === REFERENCE) continue

  const keys = new Set(flatten(JSON.parse(readFileSync(join(DIR, file), 'utf8'))))
  const missing = [...reference].filter((k) => !keys.has(k))
  const extra = [...keys].filter((k) => !reference.has(k))

  if (missing.length === 0 && extra.length === 0) {
    console.log(`✓ ${file.padEnd(14)} ${keys.size} kalit`)
    continue
  }

  failed = true
  console.error(`✗ ${file}`)
  if (missing.length > 0) console.error(`    yetishmayapti: ${missing.join(', ')}`)
  if (extra.length > 0) console.error(`    ortiqcha:      ${extra.join(', ')}`)
}

console.log(`\n${REFERENCE} (etalon): ${reference.size} kalit`)

// ────────────────────────────────────────────────────────────
//  Bitta qavsli o'rin belgisi — `{n}`
// ────────────────────────────────────────────────────────────

/**
 * i18next o'rin belgisini IKKI qavs bilan kutadi (`{{n}}`). Bitta qavsli
 * `{n}` xato bermaydi — u ekranga XOM holida chiqadi: «{n} ta qator qayta
 * yozishga qo'yildi». 2026-09-27 da ikkita kalit to'rt tilda shunday edi va
 * hech bir tekshiruv buni sezmagan. Bu yerda har bir qiymat qaraladi;
 * `{{n}}` ning ichidagi qavslar hisobga olinmaydi.
 */
const SINGLE_BRACE = /(?<!\{)\{\s*[A-Za-z_][A-Za-z0-9_]*\s*\}(?!\})/

function flattenValues(object, prefix = '') {
  const entries = []
  for (const [key, value] of Object.entries(object)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      entries.push(...flattenValues(value, path))
    } else if (typeof value === 'string') {
      entries.push([path, value])
    }
  }
  return entries
}

const singleBrace = []
for (const file of files) {
  for (const [key, value] of flattenValues(JSON.parse(readFileSync(join(DIR, file), 'utf8')))) {
    if (SINGLE_BRACE.test(value)) singleBrace.push(`${file}: ${key} → ${value}`)
  }
}

if (singleBrace.length > 0) {
  failed = true
  console.error(`\n✗ Bitta qavsli o'rin belgisi — ${singleBrace.length} ta (i18next «{{nom}}» kutadi):`)
  for (const line of singleBrace) console.error(`    ${line}`)
} else {
  console.log('✓ o‘rin belgilari: hammasi «{{nom}}» shaklida')
}

// ────────────────────────────────────────────────────────────
//  Frontendda ishlatilgan tarjima kalitlari
// ────────────────────────────────────────────────────────────

/**
 * `web/src` dagi HAR BIR `t(...)` chaqiruvini o'qiydi va uni etalon bilan
 * solishtiradi. Ikki xil chaqiruv bor:
 *
 * <b>Oddiy holat — `t('nav.employees')`.</b> Kalit KODNING O'ZIDA yozilgan,
 * shu sababli to'g'ridan-to'g'ri o'qib solishtirsa bo'ladi. Shart ichida ham
 * («`t(collapsed ? 'a' : 'b')`») — ikkala tarmoq ham haqiqiy kalit, shu
 * sababli qavs ICHIDAGI barcha qatorli literallar olinadi, faqat birinchisi
 * emas.
 *
 * <b>Shablonli holat — `` t(`nav.${item.key}`) ``.</b> Bu YERDA kalit
 * ISHGA TUSHGANDA hisoblanadi va faylni o'qib to'g'ridan-to'g'ri topib
 * bo'lmaydi. Lekin `item.key` nima qiymat olishi mumkinligi kod
 * bazasida ALOHIDA joyda — odatda o'sha ro'yxatning o'zida — statik
 * yozilgan (`nav-items.ts` dagi `key: 'employees'` kabi). Shu sababli
 * yondashuv: HAR BIR takrorlanuvchi shablon uchun uning manba ro'yxati
 * qo'lda ko'rsatiladi (`DYNAMIC_KEY_SOURCES`), o'sha ro'yxatdagi qiymatlar
 * o'qiladi va ular bilan shablon TO'LDIRILIB kutilgan kalitlar hosil
 * qilinadi — masalan `nav-items.ts` dagi har bir `key:` qiymati uchun
 * `nav.<qiymat>` etalonda bormi tekshiriladi.
 *
 * Bu yondashuv TO'LIQ statik tahlil emas (masalan `t(STATUS_KEY[status])`
 * kabi o'zgaruvchidan o'qiladigan chaqiruvlarni ko'rmaydi — u yerda matn
 * ko'rinishda umuman yo'q), lekin haqiqiy kodda uchraydigan naqshlarning
 * katta qismini yopadi va yolg'on ijobiy natija BERMAYDI: agar ro'yxat
 * o'zgarsa (band qo'shilsa/o'chsa), tekshiruv ham AVTOMATIK yangilanadi,
 * chunki u ro'yxatni har safar qayta o'qiydi.
 */
const WEB_ROOT = new URL('../', import.meta.url).pathname

function sourceFiles(dir) {
  const found = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) found.push(...sourceFiles(path))
    else if (/\.tsx?$/.test(entry.name)) found.push(path)
  }
  return found
}

function lineOf(text, index) {
  return text.slice(0, index).split('\n').length
}

function addUsage(map, key, location) {
  if (!map.has(key)) map.set(key, new Set())
  map.get(key).add(location)
}

/**
 * `t(` dan keyingi ochiq qavsdan boshlab, mos yopiq qavsgacha bo'lgan
 * matnni qaytaradi — ICHKI qavslarni hisoblab (masalan
 * `t('x', { n: num(y) })` yoki ichma-ich `t('a', { b: t('c') })`).
 */
function callArgument(text, openParenIndex) {
  let depth = 1
  let i = openParenIndex + 1
  while (i < text.length && depth > 0) {
    if (text[i] === '(') depth++
    else if (text[i] === ')') depth--
    i++
  }
  return text.slice(openParenIndex + 1, i - 1)
}

/**
 * `t(...)` argumentlaridan FAQAT BIRINCHISINI — ya'ni kalitni — ajratadi.
 *
 * ⚠️ BU ZARUR, «shunchaki aniqroq» emas. Ilgari butun argument ro'yxati
 * skanerlanardi va undagi HAR BIR satr kalit deb hisoblanardi. Natijada
 * ikkinchi argumentdagi ORINBOSAR QIYMATLAR ham kalit bo'lib ko'rinardi:
 *
 *     t('recruiting.acceptSummary', { position: offer.vacancy ?? '—' })
 *                                                                 ↑
 *                                          «—» kalit deb o'qilardi
 *
 * va tekshiruv «kodda ishlatilgan, lekin etalonda yo'q kalit» deb
 * YIQILARDI. Bu yolg'on xato edi: `—` hech qachon kalit bo'lmagan.
 *
 * Vergul QAVS VA TIRNOQ ICHIDA hisobga olinmaydi — aks holda
 * `t(cond ? 'a' : 'b', { n: f(1, 2) })` kabi chaqiruv noto'g'ri kesilardi.
 */
function firstArgument(args) {
  let depth = 0
  let quote = null

  for (let i = 0; i < args.length; i++) {
    const ch = args[i]

    if (quote !== null) {
      if (ch === '\\') i++
      else if (ch === quote) quote = null
      continue
    }

    if (ch === "'" || ch === '"' || ch === '`') quote = ch
    else if (ch === '(' || ch === '[' || ch === '{') depth++
    else if (ch === ')' || ch === ']' || ch === '}') depth--
    else if (ch === ',' && depth === 0) return args.slice(0, i)
  }

  return args
}

/** Har bir faylni o'qib, oddiy va shablonli `t(...)` chaqiruvlarini ajratadi. */
function scanTranslationCalls() {
  const literalKeys = new Map() // kalit → joylashuvlar to'plami
  const templates = [] // { raw, location } — ichida `${...}` bor shabloncha

  for (const file of sourceFiles(join(WEB_ROOT, 'src'))) {
    const text = readFileSync(file, 'utf8')
    const relative = `src${file.slice(join(WEB_ROOT, 'src').length)}`
    const callRe = /\bt\(/g
    let match
    while ((match = callRe.exec(text))) {
      const arg = firstArgument(callArgument(text, match.index + 1))
      const location = `${relative}:${lineOf(text, match.index)}`

      for (const literal of arg.matchAll(/'([^']*)'|"([^"]*)"/g)) {
        addUsage(literalKeys, literal[1] ?? literal[2], location)
      }
      for (const template of arg.matchAll(/`([^`]*)`/g)) {
        if (template[1].includes('${')) templates.push({ raw: template[1], location })
        else addUsage(literalKeys, template[1], location)
      }
    }
  }

  return { literalKeys, templates }
}

/** `maydon: 'qiymat'` ko'rinishidagi HAR BIR yozuvni (satr bilan) chiqaradi. */
function fieldValues(text, field) {
  const re = new RegExp(`\\b${field}\\s*:\\s*'([^']+)'`, 'dg')
  const out = []
  let match
  while ((match = re.exec(text))) out.push({ value: match[1], line: lineOf(text, match.indices[1][0]) })
  return out
}

/** `export const NAME = ['a', 'b', ...] as const` massividagi qiymatlarni chiqaradi. */
function constArrayValues(text, name) {
  const block = new RegExp(`export const ${name}\\s*=\\s*\\[([^\\]]*)\\]`, 'ds').exec(text)
  if (!block) return []
  const [start] = block.indices[1]
  const out = []
  const itemRe = /'([^']+)'/dg
  let match
  while ((match = itemRe.exec(block[1]))) {
    out.push({ value: match[1], line: lineOf(text, start + match.indices[1][0]) })
  }
  return out
}

/** `export const NAME = { kalit: qiymat, ... } as const` obyektining KALIT nomlarini chiqaradi. */
function constObjectKeyNames(text, name) {
  const block = new RegExp(`export const ${name}\\s*=\\s*\\{([^}]*)\\}`, 'ds').exec(text)
  if (!block) return []
  const [start] = block.indices[1]
  const out = []
  const keyRe = /(\w+)\s*:/dg
  let match
  while ((match = keyRe.exec(block[1]))) {
    out.push({ value: match[1], line: lineOf(text, start + match.indices[1][0]) })
  }
  return out
}

/**
 * Shablonli kalitlarning manba ro'yxati.
 *
 * Yangi shablonli `` t(`prefiks.${...}`) `` chaqiruvi qo'shilsa-yu, shu
 * yerga mos yozuv qo'shilmasa — pastdagi «tanilmagan shablon» ogohlantirishi
 * buni ko'rsatadi (lekin `` roles.${code} `` kabi ATAYLAB qoldirilganlar
 * bundan mustasno, SKIPPED_DYNAMIC_PREFIXES ga qarang).
 */
const DYNAMIC_KEY_SOURCES = [
  {
    file: 'src/app/layout/nav-items.ts',
    values: (text) => fieldValues(text, 'key'),
    keysFor: (value) => [`nav.${value}`],
  },
  {
    file: 'src/features/employees/api/employees-api.ts',
    values: (text) => constArrayValues(text, 'EMPLOYMENT_EVENT_TYPES'),
    keysFor: (value) => [`employees.events.${value}`],
  },
  {
    file: 'src/features/employees/api/employees-api.ts',
    values: (text) => constArrayValues(text, 'EMPLOYEE_FLAG_TYPES'),
    keysFor: (value) => [
      `employees.flags.${value}`,
      `flags.page.${value}.title`,
      `flags.page.${value}.hint`,
      `flags.page.${value}.emptyTitle`,
    ],
  },
  /*
   * «Belgi qo'yish» oynasidagi HAVOLALI yozuvlar — baxtsiz hodisa va
   * qayta aloqa. Ular belgi EMAS (`ROUTED_MARKS` izohi), lekin ro'yxatda
   * belgilar bilan yonma-yon turadi va har biriga ikkita kalit kerak:
   * nomi va nima uchun boshqa formaga olib borilishini tushuntiruvchi matn.
   */
  {
    file: 'src/features/employees/lib/employee-presentation.ts',
    values: (text) => constArrayValues(text, 'ROUTED_MARKS'),
    keysFor: (value) => [`employees.marks.${value}`, `employees.markRouteText.${value}`],
  },
  /*
   * Til bilish — qat'iy ro'yxat KODDA yashaydi (ma'lumotnoma emas,
   * `LANGUAGE_CODES` izohi). Har bir til va daraja uchun tarjima shu
   * yerda qadaladi: ro'yxatga yangi til qo'shgan odam 4 tilga nomini
   * qo'shmaguncha tekshiruv o'tmaydi.
   */
  {
    file: 'src/features/employees/api/employee-competency-api.ts',
    values: (text) => constArrayValues(text, 'LANGUAGE_CODES'),
    keysFor: (value) => [`competency.language.${value}`],
  },
  {
    file: 'src/features/employees/api/employee-competency-api.ts',
    values: (text) => constArrayValues(text, 'LANGUAGE_LEVELS'),
    keysFor: (value) => [`competency.languageLevel.${value}`],
  },
  {
    file: 'src/features/catalog/lib/catalog-kinds.ts',
    values: (text) => fieldValues(text, 'i18nKey'),
    keysFor: (value) => [`catalog.kinds.${value}.title`, `catalog.kinds.${value}.subtitle`],
  },
  {
    file: 'src/features/probations/api/probations-api.ts',
    values: (text) => constObjectKeyNames(text, 'PROBATION_STATUSES'),
    keysFor: (value) => [`probation.status.${value}`],
  },

  /*
   * Rekruting (2-bosqich). Server sanoqlarni `"Open"`, `"InProgress"`
   * ko'rinishida qaytaradi, tarjima kalitlari esa kichik harf bilan
   * boshlanadi (`recruiting.vacancyStatus.open`) — kodda ular
   * `status.charAt(0).toLowerCase() + status.slice(1)` bilan hosil
   * qilinadi. Shu sababli bu yerda ham AYNAN o'sha o'girish qo'llanadi:
   * ro'yxat kodda o'zgarsa, tekshiruv o'zi yangilanadi.
   */
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'VACANCY_STATUSES'),
    keysFor: (value) => [`recruiting.vacancyStatus.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'CANDIDATE_STATUSES'),
    keysFor: (value) => [`recruiting.candidateStatus.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'CANDIDATE_SOURCES'),
    keysFor: (value) => [`recruiting.source.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'OFFER_STATUSES'),
    keysFor: (value) => [`recruiting.offerStatus.${lowerFirst(value)}`],
  },
  /*
   * Nomzod IZOHLARINING turi — «oxirgi nima bo'ldi» qatoridagi yorliq
   * (`candidate-stage-picker.tsx`: `` t(`recruiting.noteKind.${lowerFirst(last.kind)}`) ``).
   *
   * Nega bu yozuv ZARUR: usiz skript faqat «tanilmagan shablon»
   * ogohlantirishini chiqaradi va u ATAYLAB yiqitmaydi (pastdagi izoh) —
   * ya'ni tarjimasi qolib ketgan tur ekranga XOM KALIT bo'lib chiqardi
   * («recruiting.noteKind.reassign»), aynan shu fayl sarlavhasida
   * tasvirlangan nosozlik. Manba — `CANDIDATE_NOTE_KINDS`, ya'ni serverga
   * yangi tur qo'shilib ro'yxat o'sganda tekshiruv o'zi yangilanadi.
   */
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'CANDIDATE_NOTE_KINDS'),
    keysFor: (value) => [`recruiting.noteKind.${lowerFirst(value)}`],
  },

  /*
   * Nomzod ANKETASI (`candidate-questionnaire-card.tsx`). Uchta yopiq
   * to'plam — javoblar botda tugmadan tanlanadi («Qayerdan bilgan» endi
   * ma'lumotnoma, SPEC-qayerdan-bilgan). Beshinchisi,
   * `russianLevel`, bu yerda YO'Q va bo'lishi ham kerak emas: u xodim
   * kartasidagi bilan bir xil sanoq va yuqoridagi `LANGUAGE_LEVELS`
   * yozuvi uni allaqachon qamrab oladi (`competency.languageLevel.*`).
   */
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'EDUCATION_LEVELS'),
    keysFor: (value) => [`recruiting.education.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'MARITAL_STATUSES'),
    keysFor: (value) => [`recruiting.marital.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/recruiting/api/recruiting-api.ts',
    values: (text) => constArrayValues(text, 'SALARY_EXPECTATIONS'),
    keysFor: (value) => [`recruiting.expectedSalary.${lowerFirst(value)}`],
  },

  /*
   * Tasdiqlash oynalari uchta kalitni birga ishlatadi (`…Title`, `…Text`,
   * `…Confirm`) va amal nomi shablonda turadi. Ro'yxat kodda alohida
   * massiv bo'lib yotmaydi — u dialogning o'z tipida. Shu sababli
   * qiymatlar SHU YERDA sanab o'tilgan: yangi amal qo'shilsa, «tanilmagan
   * shablon» ogohlantirishi emas, «etalonda yo'q kalit» XATOSI chiqadi va
   * uni e'tibordan chetda qoldirib bo'lmaydi.
   */
  {
    file: 'src/features/recruiting/components/candidate-action-dialog.tsx',
    values: () =>
      ['reject', 'withdraw', 'reserve', 'returnToFunnel', 'reassign', 'delete'].map(
        (value) => ({ value, line: 0 }),
      ),
    keysFor: (value) => [
      `recruiting.candidateAction.${value}Title`,
      `recruiting.candidateAction.${value}Text`,
      `recruiting.candidateAction.${value}Confirm`,
    ],
  },
  /*
   * Vakansiya amallari uchun bu yerda yozuv YO'Q: yopish va qayta ochish
   * endi ro'yxatda bir bosishda ishlaydi, tasdiqlash oynasi faqat
   * o'chirishda qoldi (`vacancy-delete-dialog.tsx`) va u kalitlarni
   * to'g'ridan-to'g'ri yozadi — shablon yo'q, ya'ni ular yuqoridagi
   * oddiy tekshiruvga o'zi tushadi.
   */
  /*
   * Bildirishnomalar (qo'ng'iroq oynasi). Har bir yozuv turi uchun
   * sarlavha va matn kerak, tur esa shablonda turadi:
   * `` t(`notifications.type.${textKey}.title`) ``.
   *
   * ⚠️ Manba `NOTIFICATION_TYPES` (server turlari) EMAS, balki
   * `NOTIFICATION_TEXT_KEYS`: bitta tur ikkita matnga ajralishi mumkin
   * (ish taklifi turlarida shunday bo'lgan edi). Kod ham AYNAN shu
   * massivdan tanlaydi (`notificationTextKey`), ya'ni ro'yxat
   * o'zgarganda tekshiruv o'zi yangilanadi.
   */
  {
    file: 'src/features/notifications/lib/notification-presentation.ts',
    values: (text) => constArrayValues(text, 'NOTIFICATION_TEXT_KEYS'),
    keysFor: (value) => [
      `notifications.type.${value}.title`,
      `notifications.type.${value}.text`,
    ],
  },

  {
    /*
     * Nomzod kartasidagi «yopilgan» yozuvi. To'rtta holatning har biri uchun
     * alohida matn — `InProgress` bu yerda YO'Q va bo'lishi ham kerak emas:
     * jarayondagi nomzodda «yopildi» yozuvi umuman chizilmaydi. `reserve`
     * esa BOR: rezervdagi nomzod ham voronkadan chiqqan va kartada aynan
     * shu yozuv ko'rinadi.
     */
    file: 'src/features/recruiting/components/candidate-card-page.tsx',
    values: () =>
      ['rejected', 'withdrawn', 'hired', 'reserve'].map((value) => ({ value, line: 0 })),
    keysFor: (value) => [`recruiting.candidateClosed.${value}`],
  },

  /*
   * Rol muharriri (`role-editor-dialog.tsx`). Bo'limlar, darajalar va
   * amallar SERVERDAN keladi (`GET /api/roles/catalog`) va yorliq
   * yubormaydi — matnni frontend tanlaydi. Kalitlar `roles.` bilan
   * boshlanadi va shu sababli pastdagi `SKIPPED_DYNAMIC_PREFIXES` ga
   * tushadi; bu yozuvlarsiz yangi bo'lim yoki amalning tarjimasi hech
   * qachon tekshirilmasdi. Manba — `role-keys.ts` dagi massivlar: kod
   * ham AYNAN shulardan tanlaydi (`useRoleLabels`).
   */
  {
    file: 'src/features/roles/lib/role-keys.ts',
    values: (text) => constArrayValues(text, 'ROLE_MODULE_KEYS'),
    keysFor: (value) => [`roles.module.${value}`],
  },
  {
    file: 'src/features/roles/lib/role-keys.ts',
    values: (text) => constArrayValues(text, 'ACCESS_LEVELS'),
    keysFor: (value) => [`roles.level.${value}`, `roles.levelHint.${value}`],
  },
  {
    file: 'src/features/roles/lib/role-keys.ts',
    values: (text) => constArrayValues(text, 'ROLE_ACTION_CODES'),
    keysFor: (value) => [`roles.action.${value}`],
  },
  {
    file: 'src/features/roles/lib/role-keys.ts',
    values: (text) => constArrayValues(text, 'ROLE_EXTRA_CODES'),
    keysFor: (value) => [`roles.extra.${value}`],
  },
  /*
   * Audit jurnali (`features/audit/**`, SPEC-rollar-va-audit §9.9). Server
   * amal, entity, maydon va amal nomini XOM yuboradi, yorliqni frontend
   * tanlaydi — rollar muharriridagi bilan bir xil fikr. Kod ham AYNAN shu
   * massivlardan tanlaydi (`audit-format.ts`), massivda yo'q nom esa xom
   * holida chiqadi. Bu yozuvlarsiz yangi amal yoki maydonning tarjimasi
   * qolib ketsa, tekshiruv buni sezmasdi.
   */
  {
    file: 'src/features/audit/lib/audit-keys.ts',
    values: (text) => constArrayValues(text, 'AUDIT_ACTION_KEYS'),
    keysFor: (value) => [`audit.actions.${value}`],
  },
  {
    file: 'src/features/audit/lib/audit-keys.ts',
    values: (text) => constArrayValues(text, 'AUDIT_ENTITY_NAMES'),
    keysFor: (value) => [`audit.entities.${value}`],
  },
  {
    file: 'src/features/audit/lib/audit-keys.ts',
    values: (text) => constArrayValues(text, 'AUDIT_FIELD_KEYS'),
    keysFor: (value) => [`audit.fields.${value}`],
  },
  {
    file: 'src/features/audit/lib/audit-keys.ts',
    values: (text) => constArrayValues(text, 'AUDIT_OPERATION_NAMES'),
    keysFor: (value) => [`audit.operations.${value}`],
  },
  {
    file: 'src/features/audit/lib/audit-keys.ts',
    values: (text) => constArrayValues(text, 'AUDIT_LOGIN_REASONS'),
    keysFor: (value) => [`audit.reasons.${value}`],
  },
  /*
   * «AI ulanish» jurnalidagi vosita nomlari (`mcp-section.tsx`:
   * `` t(`settings.mcp.tool.${tool}`) ``). Server nomni xom yuboradi,
   * yorliqni frontend tanlaydi — rollardagi bilan bir xil fikr. Manba —
   * `MCP_TOOL_NAMES`: kod ham AYNAN shu massivdan tanlaydi.
   */
  {
    file: 'src/features/settings/api/mcp-api.ts',
    values: (text) => constArrayValues(text, 'MCP_TOOL_NAMES'),
    keysFor: (value) => [`settings.mcp.tool.${value}`],
  },
  /*
   * Public candidate questionnaire HR-F02 (`features/kiosk`, SPEC-planshet-anketa §7).
   * Option labels and validation messages are picked by value in a template
   * (`` t(`kiosk.marital.${lowerFirst(value)}`) ``); the lists live in
   * `kiosk-options.ts` / `kiosk-draft.ts`, so a new option without its four
   * translations fails here.
   */
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'HOUSING_TYPES'),
    keysFor: (value) => [`kiosk.housing.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'MARITAL_OPTIONS'),
    keysFor: (value) => [`kiosk.marital.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'MILITARY_OPTIONS'),
    keysFor: (value) => [`kiosk.military.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'START_AFTER_OPTIONS'),
    keysFor: (value) => [`kiosk.startAfter.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'EDUCATION_OPTIONS'),
    keysFor: (value) => [`kiosk.education.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'FORM_LANGUAGE_NAMES'),
    keysFor: (value) => [`kiosk.language.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'LANGUAGE_ASPECTS'),
    keysFor: (value) => [`kiosk.languages.${value}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'FAMILY_RELATIONS'),
    keysFor: (value) => [`kiosk.relation.${lowerFirst(value)}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'COMPUTER_GROUPS'),
    keysFor: (value) => [`kiosk.computer.group.${value}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'COMPUTER_SKILL_KEYS'),
    keysFor: (value) => [`kiosk.computer.skill.${value}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'ABOUT_QUESTIONS'),
    keysFor: (value) => [`kiosk.about.q.${value}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'CONSENTS'),
    keysFor: (value) => [`kiosk.consent.${value}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-options.ts',
    values: (text) => constArrayValues(text, 'WIZARD_SCREENS'),
    keysFor: (value) => [`kiosk.screen.${value}`],
  },
  {
    file: 'src/features/kiosk/lib/kiosk-draft.ts',
    values: (text) => constArrayValues(text, 'KIOSK_ERROR_CODES'),
    keysFor: (value) => [`kiosk.error.${value}`],
  },
]

/** `InProgress` → `inProgress`. Kodda ham aynan shu o'girish ishlatiladi. */
function lowerFirst(value) {
  return value.charAt(0).toLowerCase() + value.slice(1)
}

function collectDynamicKeys() {
  const dynamicKeys = new Map()
  for (const source of DYNAMIC_KEY_SOURCES) {
    const text = readFileSync(join(WEB_ROOT, source.file), 'utf8')
    for (const { value, line } of source.values(text)) {
      for (const key of source.keysFor(value)) {
        addUsage(dynamicKeys, key, `${source.file}:${line}`)
      }
    }
  }
  return dynamicKeys
}

/**
 * Shablonlari BILIB TURIB ro'yxatga qo'shilmagan prefikslar.
 *
 * `errors.*` — pastdagi backend tekshiruvida ALOHIDA ko'riladi (u yerda
 * yo'nalish teskari: backend kodi → tarjima).
 *
 * `roles.*` — backend'dan `user.roles` orqali keladigan rol kodi
 * (`settings-dialog.tsx`). Frontend'da rollarning statik ro'yxati YO'Q —
 * `./bonvi gen` kelmaguncha bo'lishi ham mumkin emas. Kod buni BILADI va
 * tarjimasi topilmagan rolni ekranga chiqarmay, o'zi filtrlab tashlaydi
 * (`` .filter((role) => role.label !== `roles.${role.code}`) ``) —
 * ya'ni yetishmagan tarjima UI'ni buzmaydi, shu sababli bu yerda majburiy
 * tekshiruv shart emas.
 */
const SKIPPED_DYNAMIC_PREFIXES = ['errors.', 'roles.']

const { literalKeys, templates } = scanTranslationCalls()
const dynamicKeys = collectDynamicKeys()

const usedKeys = new Map()
for (const [key, locations] of literalKeys) for (const location of locations) addUsage(usedKeys, key, location)
for (const [key, locations] of dynamicKeys) for (const location of locations) addUsage(usedKeys, key, location)

const missingCodeKeys = [...usedKeys]
  .filter(([key]) => !reference.has(key))
  .sort(([a], [b]) => a.localeCompare(b))

if (missingCodeKeys.length > 0) {
  failed = true
  console.error(`\n✗ Kodda ishlatilgan, lekin ${REFERENCE} da yo'q kalitlar — ${missingCodeKeys.length} ta:`)
  for (const [key, locations] of missingCodeKeys) {
    console.error(`    ${key}`)
    for (const location of [...locations].sort()) console.error(`        @ ${location}`)
  }
  console.error('\n  Foydalanuvchi ekranda tarjima o\'rniga XOM KALIT nomini ko\'radi.')
  console.error('  Har to\'rtala tilga qo\'shing: ' + files.join(', '))
} else {
  console.log(`✓ frontend tarjima kalitlari: ${usedKeys.size} ta ishlatilgan, hammasi ${REFERENCE} da bor`)
}

/*
 * Shablonli chaqiruv YUQORIDAGI manbalarning birortasi bilan ham
 * qoplanmasa — ehtimol yangi dinamik naqsh paydo bo'lgan va uni
 * `DYNAMIC_KEY_SOURCES` ga qo'lda qo'shish kerak. Tekshiruvni ATAYLAB
 * YIQITMAYDI: naqsh xato emas, faqat hali ro'yxatga olinmagan bo'lishi
 * mumkin, majburiy xato esa bunday paytda tekshiruvning o'zini
 * o'chirib qo'yishga olib keladi.
 */
const uncoveredTemplates = templates.filter(({ raw }) => {
  if (SKIPPED_DYNAMIC_PREFIXES.some((prefix) => raw.startsWith(prefix))) return false
  const prefix = raw.slice(0, raw.indexOf('${'))
  const suffix = raw.slice(raw.lastIndexOf('}') + 1)
  return ![...dynamicKeys.keys()].some((key) => key.startsWith(prefix) && key.endsWith(suffix))
})

if (uncoveredTemplates.length > 0) {
  console.log(`\n⚠ Tanilmagan shablonli \`t()\` chaqiruvi — ${uncoveredTemplates.length} ta (qo'lda tekshiring):`)
  for (const { raw, location } of uncoveredTemplates) console.log(`    \`${raw}\`  @ ${location}`)
}

/*
 * Teskari tomon: etalonda bor, lekin kod hech qanday shaklda
 * ishlatmayotgan kalitlar. Bu XATO EMAS — faqat ma'lumot, shu sababli
 * `failed` ga ta'sir qilmaydi. Sabab shu izohning yuqorisida: dinamik
 * chaqiruvlarning bir qismi (masalan `roles.*`) tamomila kuzatuvdan
 * tashqarida qoldirilgan, ya'ni «ishlatilmagan» degan xulosa ular uchun
 * YOLG'ON bo'lishi mumkin.
 */
const potentiallyUnused = [...reference]
  .filter((key) => !SKIPPED_DYNAMIC_PREFIXES.some((prefix) => key.startsWith(prefix)))
  .filter((key) => !usedKeys.has(key))
  .sort()

if (potentiallyUnused.length > 0) {
  console.log(
    `\nℹ Etalonda bor, kodda topilmagan kalitlar — ${potentiallyUnused.length} ta ` +
      "(ogohlantirish, tekshiruvni YIQITMAYDI):",
  )
  for (const key of potentiallyUnused) console.log(`    ${key}`)
}

// ────────────────────────────────────────────────────────────
//  Backend xato kodlari
// ────────────────────────────────────────────────────────────

/** `Error.Validation("kod", ...)` va uning qardoshlari. */
const ERROR_CALL = /Error\.(?:Validation|Conflict|NotFound|Forbidden|Unauthorized)\s*\(\s*"([^"]+)"/gs

/**
 * HTTP qatlamida domen `Error` siz qaytadigan kodlar: `ProblemDetails` ning
 * `title` maydoni (403, 504, ...) va tezlik cheklovining `XatoKodi`
 * konstantasi (429). Ilgari ular tekshiruvdan tashqarida qolgan va
 * ekranda xom kod chiqardi (`rate_limit.oshib_ketdi`, 2026-09-26).
 */
const PROBLEM_TITLE = /\b(?:[Tt]itle|XatoKodi)\s*=\s*"([a-z][a-z0-9_]*\.[a-z0-9_]+)"/g

function csharpFiles(dir) {
  const found = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    // `artifacts`, `bin`, `obj` — qurilish natijalari; ularda manba yo'q
    if (entry.isDirectory()) {
      if (['artifacts', 'bin', 'obj', 'TestResults'].includes(entry.name)) continue
      found.push(...csharpFiles(join(dir, entry.name)))
    } else if (entry.name.endsWith('.cs')) {
      found.push(join(dir, entry.name))
    }
  }
  return found
}

const errorCodes = new Set()
const apiReachable = existsSync(API_DIR)

for (const file of apiReachable ? csharpFiles(API_DIR) : []) {
  // Testlar hisobga olinmaydi: ular o'ylab topilgan kodlarni ham
  // ishlatishi mumkin va ular hech qachon ekranga chiqmaydi.
  if (file.includes('/tests/')) continue

  const source = readFileSync(file, 'utf8')
  for (const [, code] of source.matchAll(ERROR_CALL)) errorCodes.add(code)
  for (const [, code] of source.matchAll(PROBLEM_TITLE)) errorCodes.add(code)
}

const translated = JSON.parse(readFileSync(join(DIR, REFERENCE), 'utf8')).errors ?? {}
const untranslated = [...errorCodes].filter((code) => !(code in translated)).sort()

if (!apiReachable) {
  console.log(
    `\n⚠ backend xato kodlari TEKSHIRILMADI — «${API_DIR}» ko'rinmayapti.` +
      '\n  To\'liq tekshiruv:  ./bonvi check',
  )
} else if (untranslated.length > 0) {
  failed = true
  console.error(`\n✗ Backend xato kodlari — ${untranslated.length} tasining tarjimasi yo'q:`)
  for (const code of untranslated) console.error(`    errors.${code}`)
  console.error('\n  Bularsiz foydalanuvchi ekranda xom kodni o\'qiydi.')
  console.error('  Har to\'rtala tilga qo\'shing: ' + files.join(', '))
} else {
  console.log(`✓ backend xato kodlari: ${errorCodes.size} ta, hammasi tarjimalangan`)
}

// ────────────────────────────────────────────────────────────
//  HRMentor katalogi uchun kiosk matnlari (SPEC-blankalar-hrmentor §4)
// ────────────────────────────────────────────────────────────

/**
 * Backend HR ga katalogni `kiosk-texts.json` dan yuboradi. Kiosk tarjimasi o'zgarib, fayl
 * qayta eksport qilinmasa — HR eski matnni «asl» deb ko'radi va tahrir qiladi. Shu sababli
 * fayl hozirgi tarjimalardan qayta yig'iladi va harfma-harf solishtiriladi.
 */
if (!apiReachable) {
  console.log('⚠ kiosk-texts.json TEKSHIRILMADI — backend ko\'rinmayapti (./bonvi check).')
} else {
  try {
    const target = kioskTextsTarget(API_DIR)
    const stored = existsSync(target) ? readFileSync(target, 'utf8').replace(/\r\n/g, '\n') : null
    if (stored !== buildKioskTexts()) {
      failed = true
      console.error('\n✗ kiosk-texts.json eskirgan — kiosk tarjimasi o\'zgargan.')
      console.error('  Qayta eksport:  node web/scripts/export-kiosk-texts.mjs')
    } else {
      console.log('✓ kiosk-texts.json: kiosk tarjimalari bilan bir xil')
    }
  } catch (error) {
    failed = true
    console.error(`\n✗ ${error.message}`)
  }
}

if (failed) {
  console.error('\nTarjima tekshiruvi o\'tmadi.')
  process.exit(1)
}

console.log('Barcha tillar mos.')
