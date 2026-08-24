/**
 * Tarjimalar to'liqligini ikki tomondan tekshiradi.
 *
 * <b>1. To'rt til bir-biriga mos.</b> Yangi kalit qo'shilganda bitta tilda
 * unutilishi oson. U holda foydalanuvchi kalit NOMINI ko'radi
 * («nav.employees»), matnni emas.
 *
 * <b>2. Backend'ning har bir xato kodi tarjima qilingan.</b> Bu ikkinchi
 * tekshiruv keyinroq qo'shildi va sababi aniq: `useErrorMessage` tarjima
 * topmasa XOM KODNI ekranga chiqaradi. Bir vaqtlar 39 ta kod shu holatda
 * edi va HR «mehnat.tugagan» degan matnni o'qirdi — hech qanday xato
 * chiqmagani uchun buni hech kim sezmasdi. Endi yangi `Error.Validation(...)`
 * yozgan odam tarjimasini ham qo'shmaguncha tekshiruv o'tmaydi.
 *
 * Ishga tushirish:  ./bonvi pnpm check:locales
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

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
//  Backend xato kodlari
// ────────────────────────────────────────────────────────────

/** `Error.Validation("kod", ...)` va uning qardoshlari. */
const ERROR_CALL = /Error\.(?:Validation|Conflict|NotFound|Forbidden|Unauthorized)\s*\(\s*"([^"]+)"/gs

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

  for (const [, code] of readFileSync(file, 'utf8').matchAll(ERROR_CALL)) {
    errorCodes.add(code)
  }
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

if (failed) {
  console.error('\nTarjima tekshiruvi o\'tmadi.')
  process.exit(1)
}

console.log('Barcha tillar mos.')
