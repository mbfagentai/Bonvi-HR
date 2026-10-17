/**
 * Tarjimalarni UCH tomondan tekshiradi — HR ilovasidagi tekshiruvning
 * shu ilovaga moslangan, ATAYLAB soddalashtirilgan nusxasi.
 *
 * <b>1. To'rt til bir-biriga mos.</b> Bitta tilda unutilgan kalit
 * ekranda matn emas, KALIT NOMI bo'lib chiqadi («tab.home»).
 *
 * <b>2. Backend xato kodlari tarjimasi bor.</b> `useErrorText` tarjima
 * topmasa xom kodni ko'rsatadi. Belgilashning to'rtta rad kodi —
 * aynan shu tekshiruv uchun yozilgan ro'yxat (UX §C.5): ular xodim
 * ko'chada, shoshib turganda chiqadi.
 *
 * <b>3. Apostrof qoidasi.</b> `o‘`/`g‘` — U+2018, `ma’lumot` — U+2019,
 * ASCII `'` esa UI matnida UMUMAN uchramaydi (UX sarlavhasi). Bu
 * qoidani ko'z bilan ushlab bo'lmaydi: ikkala belgi ham bir xil
 * ko'rinadi.
 *
 * Ishga tushirish:  ./bonvi pnpm --dir app check:locales
 */
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const DIR = new URL('../src/i18n/locales/', import.meta.url).pathname
const REFERENCE = 'uz-Latn.json'

/**
 * Belgilash rad kodlari — SPEC §E dagi ro'yxat. Ular kartada SARLAVHA bilan
 * chiqadi, ya'ni `<kod>_sarlavha` ham shart.
 */
const REQUIRED_CODES = [
  'davomat.mobil_ruxsat_yoq',
  'davomat.joylashuv_aniq_emas',
  'davomat.obyektdan_uzoq',
  'davomat.takroriy_belgi',
]

/**
 * Ilova serverdan olishi mumkin bo'lgan BOSHQA kodlar — ilova chaqiradigan
 * endpointlarning (`/api/employee-auth/*`, `/api/me/*`) barcha rad javoblari
 * va tezlik cheklovi. Ro'yxat qo'lda: ilova backend manbasini ko'rmaydi, va
 * bu yerdan tushib qolgan kod ekranda xom «xodim.bloklangan» bo'lib chiqadi.
 */
const APP_CODES = [
  'auth.token_yaroqsiz',
  'rate_limit.oshib_ketdi',
  'xodim.notogri_malumot',
  'xodim.bloklangan',
  'xodim.hisob_faol_emas',
  'xodim.token_yaroqsiz',
  'davomat.yonalish_notogri',
  'davomat.joylashuv_notogri',
  'davomat.obyektdan_uzoq_obyektsiz',
  'davomat.oy_notogri',
  'davomat.sorov_topilmadi',
  'davomat.sorov_bekor_qilingan',
  'davomat.sorov_qaror_qabul_qilingan',
  'davomat.sorov_takroriy',
  'davomat.sorov_davri_notogri',
  'davomat.sorov_uzun',
  'davomat.sorov_vaqti_notogri',
  'davomat.sorov_turi_yoq',
  'davomat.yoqlik_turi_topilmadi',
  'davomat.yoqlik_turi_faolsiz',
  'davomat.yoqlik_turi_xodimga_yopiq',
  // Jadval so'rovlari (`/api/me/schedule-requests`, SPEC-jadval-sorovi §C3)
  'davomat.jadval_sorovi_takroriy',
  'davomat.jadval_sorovi_jadval_yoq',
  'davomat.jadval_sorovi_bir_xil_sana',
  'davomat.jadval_sorovi_kun_yoq',
  'davomat.jadval_sorovi_kop',
  'davomat.jadval_sorovi_sana_takror',
  'davomat.jadval_sorovi_turi',
  'davomat.jadval_sorovi_sana',
  'davomat.jadval_sorovi_davri',
  // Xodim uchun sanalar oynasi — `/schedule-requests` va `/plan` ikkalasida
  'davomat.jadval_sorovi_sana_oynasi',
  'davomat.izoh_uzun',
  // So'rov kunining smena va tanaffus qoidasi — jadval kunidagi bilan bir xil
  'davomat.jadval_vaqt_bosh',
  'davomat.jadval_vaqt_teng',
  'davomat.jadval_tanaffus_toliq_emas',
  'davomat.jadval_tanaffus_oynasi',
  'davomat.jadval_tanaffus_uzunligi',
]

/** Lotin yozuvidagi matnda ASCII apostrof bo'lmasligi kerak. */
const LATIN_LOCALES = ['uz-Latn.json']

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

function values(object, prefix = '') {
  const found = []
  for (const [key, value] of Object.entries(object)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      found.push(...values(value, path))
    } else if (typeof value === 'string') {
      found.push([path, value])
    }
  }
  return found
}

const files = readdirSync(DIR).filter((name) => name.endsWith('.json'))
const referenceData = JSON.parse(readFileSync(join(DIR, REFERENCE), 'utf8'))
const reference = new Set(flatten(referenceData))

let failed = false

// ── 1. Kalitlar mosligi ──────────────────────────────────────
for (const file of files) {
  if (file === REFERENCE) continue

  const keys = new Set(flatten(JSON.parse(readFileSync(join(DIR, file), 'utf8'))))
  const missing = [...reference].filter((key) => !keys.has(key))
  const extra = [...keys].filter((key) => !reference.has(key))

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

// ── 2. Xato kodlari ──────────────────────────────────────────
for (const file of files) {
  const data = JSON.parse(readFileSync(join(DIR, file), 'utf8'))
  const errors = data.errors ?? {}

  for (const code of REQUIRED_CODES) {
    if (typeof errors[code] !== 'string') {
      failed = true
      console.error(`✗ ${file}: «${code}» xato kodi tarjima qilinmagan`)
    }
    if (typeof errors[`${code}_sarlavha`] !== 'string') {
      failed = true
      console.error(`✗ ${file}: «${code}» uchun sarlavha yo'q`)
    }
  }

  for (const code of APP_CODES) {
    if (typeof errors[code] !== 'string') {
      failed = true
      console.error(`✗ ${file}: «${code}» xato kodi tarjima qilinmagan`)
    }
  }
}

// ── 3. Apostrof ──────────────────────────────────────────────
for (const file of LATIN_LOCALES) {
  const data = JSON.parse(readFileSync(join(DIR, file), 'utf8'))

  for (const [path, value] of values(data)) {
    if (value.includes("'")) {
      failed = true
      console.error(`✗ ${file}: «${path}» ichida ASCII apostrof bor — U+2018 yoki U+2019 kerak`)
    }
  }
}

if (failed) {
  console.error('\nTarjima tekshiruvi o‘tmadi.')
  process.exit(1)
}

console.log('Tarjima tekshiruvi o‘tdi.')
