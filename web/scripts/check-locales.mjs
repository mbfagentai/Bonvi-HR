/**
 * Barcha tarjima fayllarida bir xil kalitlar borligini tekshiradi.
 *
 * Nega kerak: yangi kalit qo'shilganda bitta tilda unutilishi oson.
 * U holda foydalanuvchi kalit nomini ko'radi («nav.employees»), matnni emas.
 * Bu skript CI'da ishlaydi va shunday holatni o'tkazmaydi.
 *
 * Ishga tushirish:  ./bonvi pnpm check:locales
 */
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const DIR = new URL('../src/shared/i18n/locales/', import.meta.url).pathname
const REFERENCE = 'uz-Latn.json'

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

if (failed) {
  console.error('\nTarjima fayllari mos emas.')
  process.exit(1)
}

console.log('Barcha tillar mos.')
