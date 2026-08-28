/**
 * HR qo'llanmasini Markdown manbasidan PDF ga aylantiradi.
 *
 * <b>NEGA ish vaqtida emas, oldindan.</b> Qo'llanma matni har so'rovda bir xil —
 * u foydalanuvchiga ham, sanaga ham, bazaga ham bog'liq emas. Shunday matnni
 * har bosishda serverda qayta yasash bekorga ish: backend'ga PDF kutubxonasi,
 * shrift fayllari va yangi endpoint qo'shilardi, natija esa har safar bir xil
 * bayt bo'lardi. Shu sababli PDF SHU YERDA, qo'lda, bir marta yasaladi va
 * repozitoriyga tushadi — tugma uni oddiy statik fayl sifatida yuklaydi.
 *
 * <b>NEGA build vaqtida ham emas.</b> `pnpm build` ni PDF ga bog'lash har bir
 * qurilishga bir necha soniya va shrift o'qishni qo'shardi, holbuki qo'llanma
 * yiliga bir necha marta o'zgaradi. Generator qo'lda chaqiriladi:
 *
 *     ./bonvi pnpm build:manual
 *
 * <b>NEGA `pdfkit`.</b> Litsenziyasi MIT — bu loyihada shart (ImageSharp aynan
 * pullik bo'lgani uchun rad etilgan). Butun bog'liqlik daraxti ham toza:
 * pdfkit/fontkit/linebreak/png-js — MIT, `@swc/helpers` — Apache-2.0,
 * `tslib` — 0BSD. Sof JavaScript, tizim kutubxonasi talab qilmaydi va
 * TrueType shriftni Identity-H kodlashi bilan JOYLASHTIRADI — kirill uchun
 * yagona to'g'ri yo'l.
 *
 * NEGA puppeteer/playwright emas: ular Chromium'ni tortadi — yuzlab megabayt
 * va bir talay tizim kutubxonasi. Bir varaq matn uchun butun brauzer.
 *
 * NEGA `pdf-lib` emas (u ham MIT): u matnni qatorlarga O'ZI bo'lmaydi.
 * Ko'p sahifali oqar matnda satrga sig'dirish, ko'chirish va sahifa
 * chegarasini hisoblashni qo'lda yozish kerak bo'lardi — pdfkit buni
 * `linebreak` (UAX #14) orqali qiladi.
 *
 * <b>NEGA Noto Sans joylashtiriladi.</b> PDF ning 14 ta «standart» shrifti
 * (Helvetica va h.k.) WinAnsi kodlashda ishlaydi va kirillni UMUMAN
 * ko'tarmaydi — `қ ғ ҳ ў` o'rniga bo'sh quti chiqardi. Noto Sans SIL OFL 1.1
 * ostida (joylashtirishga ochiq ruxsat beradi) va o'zbekchaning ikkala
 * yozuvini ham to'liq qoplaydi. Fayllar `manual/fonts/` da — qaramlik
 * sifatida olinmagan, chunki npm'dagi shrift paketlari `woff2` beradi,
 * fontkit esa `ttf`/`otf` o'qiydi.
 *
 * <b>NEGA manba Markdown.</b> Qo'llanmani keyin ODAM tahrirlaydi — HR
 * bo'limining o'zi ham. Matnni kod ichiga yozib qo'ysak, har tuzatish uchun
 * dasturchi kerak bo'lardi. Ikki yozuv ikki alohida faylda va QO'LDA
 * yuritiladi — `locales/uz-Latn.json` va `uz-Cyrl.json` bilan bir xil
 * qoida. NEGA lotindan kirillga avtomatik o'girilmaydi: o'girish qoidalari
 * bir qiymatli emas («sh» bitta harfmi yoki ikkitami, «ye/e»), xato esa
 * butun hujjat bo'ylab tarqalardi; qolaversa avtomatik fayl tahrirlab
 * bo'lmaydigan fayl — keyingi generatsiyada tuzatish yo'qolardi.
 *
 * <b>Oxirida PDF O'ZINI tekshiradi.</b> Yasalgan fayl qayta o'qiladi, ichidagi
 * matn qatlami (`ToUnicode` jadvali orqali) HARFMA-HARF ajratib olinadi va
 * manbadagi matn bilan solishtiriladi. Shundagina «kirill chiqdi» degan
 * gapga ishonsa bo'ladi: ekranda to'g'ri ko'rinib turgan harf PDF ichida
 * noto'g'ri kodlangan bo'lishi mumkin — u holda nusxa olinganda yoki qidiruv
 * qilinganda axlat chiqadi. Tekshiruv yiqilsa, fayl YOZILMAYDI.
 */
// `Buffer` global sifatida ham mavjud, lekin ATAYLAB modul orqali olinadi:
// eslint sozlamasi bu papkani brauzer muhiti deb biladi va global `Buffer`ni
// «e'lon qilinmagan» deb belgilaydi. Aniq import lintni ham, o'quvchini ham
// qanoatlantiradi — bu fayl faqat Node'da ishlashi ochiq ko'rinib turadi.
import { Buffer } from 'node:buffer'
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { inflateSync } from 'node:zlib'

import PDFDocument from 'pdfkit'

const ROOT = new URL('../', import.meta.url).pathname
const SOURCE_DIR = `${ROOT}manual/`
const FONT_DIR = `${SOURCE_DIR}fonts/`
const OUT_DIR = `${ROOT}public/qollanma/`

/**
 * Qaysi manbadan qaysi fayl chiqadi.
 *
 * NEGA faqat ikki til, `ru` va `en` yo'q: qo'llanmaning auditoriyasi —
 * Bonvi HR bo'limi, ular o'zbek tilida ishlaydi va tizimga faqat shular
 * kiradi (xodimlar umuman kirmaydi). Rus yoki ingliz interfeysini yoqib
 * qo'ygan odam ham amalda o'sha HR — unga lotin variantini berish
 * tarjima qilinmagan yoki eskirgan uchinchi faylni yuritishdan afzal.
 * Tugmadagi tanlov ham shu qoidaga amal qiladi (`settings-dialog.tsx`).
 */
const EDITIONS = [
  { locale: 'uz-Latn', source: 'uz-Latn.md', out: 'bonvi-hr-qollanma-uz-Latn.pdf' },
  { locale: 'uz-Cyrl', source: 'uz-Cyrl.md', out: 'bonvi-hr-qollanma-uz-Cyrl.pdf' },
]

/* ────────────────────────────────────────────────────────────
   Sahifa va tipografika
   ──────────────────────────────────────────────────────────── */

/**
 * A4, punktlarda (1 pt = 1/72 dyuym). Chekkalar keng: qo'llanma bosib
 * chiqarilishi mumkin, tikuv chetida matn qolib ketmasin.
 */
const PAGE = { width: 595.28, height: 841.89 }
const MARGIN = { top: 62, bottom: 68, left: 64, right: 64 }
const CONTENT_WIDTH = PAGE.width - MARGIN.left - MARGIN.right

/**
 * Ranglar `styles/globals.css` dagi YORUG' mavzu tokenlaridan olingan —
 * qo'llanma tizimning o'zi bilan bir xil ko'rinsin. Tungi mavzu bu yerda
 * yo'q: PDF bosib chiqariladigan hujjat, uning foni har doim oq.
 */
const COLOR = {
  navy: '#154067', // logotip rangi — sarlavhalar
  fg: '#16191D', // --fg
  secondary: '#5C636E', // --fg-secondary
  tertiary: '#98A0AC', // --fg-tertiary
  accent: '#0071E3', // --accent
  danger: '#D70015', // --danger
  border: '#E3E8EF', // --border
  sunken: '#EDF0F4', // --bg-sunken
  dangerSoft: '#FDECEE', // --danger ning yumshoq foni (globals.css: --danger-soft)
}

const SIZE = {
  coverTitle: 30,
  coverSub: 13,
  h1: 19,
  h2: 13,
  h3: 11,
  body: 10,
  small: 9,
  footer: 8,
}

/** Satrlar orasidagi qo'shimcha bo'shliq — 10pt matn uchun qulay zichlik. */
const LINE_GAP = 3.2

/* ────────────────────────────────────────────────────────────
   Markdown — ATAYLAB kichik qism
   ──────────────────────────────────────────────────────────── */

/**
 * To'liq Markdown parseri KERAK EMAS va ataylab yozilmagan.
 *
 * Sabab: manbani yozadigan ham, o'qiydigan ham biz va HR — bu yerda
 * rasm, havola, ichma-ich ro'yxat yoki HTML ishlatilmaydi. To'liq
 * `markdown-it` ni tortish esa qo'llanmaga o'z bog'liqligini qo'shardi,
 * holbuki quyidagi olti belgidan boshqasi hech qachon kerak bo'lmagan.
 * Agar manbada tanilmagan belgi uchrasa — u ODDIY MATN sifatida chiqadi,
 * jimgina yo'qolmaydi.
 *
 * Qo'llab-quvvatlanadigan belgilar:
 *
 *   # Sarlavha        — bob; HAR DOIM yangi sahifadan boshlanadi
 *   ## Kichik sarlavha — bo'lim (mundarijaga tushadi)
 *   ### Eng kichik     — ichki sarlavha
 *   - band             — belgili ro'yxat
 *   1. band            — raqamli ro'yxat (raqam qayta hisoblanadi)
 *   > matn             — eslatma qutisi (kulrang)
 *   >! matn            — diqqat qutisi (qizil)
 *   | a | b |          — jadval; ikkinchi qator `|---|-----|` ajratkich
 *   ---                — nozik chiziq
 *   **qalin**          — qalin matn
 *   `kod`              — tint rangidagi matn (yo'l, tugma nomi, xato kodi)
 *
 * NEGA jadval kerak: rollar matritsasi va xato kodlari ro'yxati aynan
 * jadval — ularni ro'yxat ko'rinishida yozish o'qishni qiyinlashtiradi.
 *
 * NEGA `#` bob har doim yangi sahifada: HR qo'llanmani boshdan-oyoq
 * o'qimaydi, mundarijadan kerakli bobni topib o'sha yerdan boshlaydi.
 * Bob o'rtadan boshlansa, u sahifadagi oldingi bobning dumi bilan
 * chalkashadi.
 */
function parseMarkdown(text) {
  const lines = text.replace(/\r\n/g, '\n').split('\n')
  const blocks = []
  let index = 0

  /** Ochiq paragrafni yopadi. Paragraf bo'sh qator yoki boshqa blok bilan tugaydi. */
  let paragraph = []
  const flushParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push({ type: 'p', text: paragraph.join(' ') })
      paragraph = []
    }
  }

  while (index < lines.length) {
    const line = lines[index]
    const trimmed = line.trim()

    if (trimmed === '') {
      flushParagraph()
      index += 1
      continue
    }

    const heading = /^(#{1,3})\s+(.*)$/.exec(trimmed)
    if (heading) {
      flushParagraph()
      blocks.push({ type: `h${heading[1].length}`, text: heading[2] })
      index += 1
      continue
    }

    if (/^-{3,}$/.test(trimmed)) {
      flushParagraph()
      blocks.push({ type: 'rule' })
      index += 1
      continue
    }

    /*
      Jadval: birinchi qator — sarlavha, ikkinchisi — ajratkich. Ustun
      KENGLIGI ajratkichdagi chiziqchalar soniga proporsional (`|--|------|`
      — ikkinchi ustun uch barobar keng). NEGA shunday: matn uzunligidan
      avtomatik hisoblash «Rol» kabi qisqa sarlavhali, lekin uzun izohli
      ustunlarda doim noto'g'ri chiqadi; manbani yozgan odam esa nisbatni
      o'sha yerning o'zida, ko'rib turib beradi.
    */
    if (trimmed.startsWith('|') && /^\|[\s:|-]+\|$/.test(lines[index + 1]?.trim() ?? '')) {
      flushParagraph()
      const cells = (row) =>
        row
          .trim()
          .replace(/^\||\|$/g, '')
          .split('|')
          .map((cell) => cell.trim())

      const header = cells(trimmed)
      const weights = cells(lines[index + 1]).map((spec) => Math.max(spec.replace(/[^-]/g, '').length, 1))
      index += 2

      const rows = []
      while (index < lines.length && lines[index].trim().startsWith('|')) {
        rows.push(cells(lines[index]))
        index += 1
      }
      blocks.push({ type: 'table', header, weights, rows })
      continue
    }

    const note = /^>(!?)\s?(.*)$/.exec(trimmed)
    if (note) {
      flushParagraph()
      // Ko'p qatorli qutini bitta blokka yig'amiz — chegara bir marta chizilsin.
      const parts = []
      let danger = note[1] === '!'
      while (index < lines.length) {
        const next = /^>(!?)\s?(.*)$/.exec(lines[index].trim())
        if (!next) break
        if (next[1] === '!') danger = true
        parts.push(next[2])
        index += 1
      }
      blocks.push({ type: 'note', danger, text: parts.join(' ').trim() })
      continue
    }

    const bullet = /^[-*]\s+(.*)$/.exec(trimmed)
    const numbered = /^\d+[.)]\s+(.*)$/.exec(trimmed)
    if (bullet || numbered) {
      flushParagraph()
      const ordered = numbered !== null
      const items = []
      while (index < lines.length) {
        const current = lines[index].trim()
        const match = ordered ? /^\d+[.)]\s+(.*)$/.exec(current) : /^[-*]\s+(.*)$/.exec(current)
        if (!match) break
        items.push(match[1])
        index += 1
      }
      blocks.push({ type: 'list', ordered, items })
      continue
    }

    paragraph.push(trimmed)
    index += 1
  }

  flushParagraph()
  return blocks
}

/**
 * Bir qator matnni uslub bo'laklariga ajratadi: oddiy, `**qalin**`, `` `kod` ``.
 *
 * NEGA alohida funksiya: pdfkit inline uslubni bilmaydi — har bir bo'lak
 * `continued: true` bilan alohida chiziladi. Ajratishni chizishdan
 * ajratmasak, paragraf, ro'yxat bandi va jadval katagi uchun bir xil
 * mantiq uch marta takrorlanardi.
 */
function parseRuns(text) {
  const runs = []
  const re = /\*\*([^*]+)\*\*|`([^`]+)`/g
  let last = 0
  let match
  while ((match = re.exec(text))) {
    if (match.index > last) runs.push({ text: text.slice(last, match.index), style: 'normal' })
    runs.push(
      match[1] !== undefined
        ? { text: match[1], style: 'bold' }
        : { text: match[2], style: 'code' },
    )
    last = match.index + match[0].length
  }
  if (last < text.length) runs.push({ text: text.slice(last), style: 'normal' })
  return runs.length > 0 ? runs : [{ text, style: 'normal' }]
}

/** Belgilarsiz xom matn — mundarija va tekshiruv uchun. */
function plain(text) {
  return text.replace(/\*\*([^*]+)\*\*/g, '$1').replace(/`([^`]+)`/g, '$1')
}

/* ────────────────────────────────────────────────────────────
   Chizish
   ──────────────────────────────────────────────────────────── */

function createDocument(meta) {
  const doc = new PDFDocument({
    size: [PAGE.width, PAGE.height],
    margins: MARGIN,
    autoFirstPage: false,
    /*
      NEGA `bufferPages`: sahifa raqami «N sahifadan M-si» ko'rinishida
      chiziladi, jami sahifa soni esa oxirida ma'lum bo'ladi. Buferlash
      hujjat tugagach har bir sahifaga qaytib, pastki kolontitulni
      yozish imkonini beradi.
    */
    bufferPages: true,
    info: meta,
  })

  doc.registerFont('body', `${FONT_DIR}NotoSans-Regular.ttf`)
  doc.registerFont('bold', `${FONT_DIR}NotoSans-Bold.ttf`)
  return doc
}

/** Joriy sahifada shuncha joy qolmasa — yangi sahifa ochadi. */
function ensureSpace(doc, needed) {
  if (doc.y + needed > PAGE.height - MARGIN.bottom) doc.addPage()
}

/**
 * Bo'laklarni ketma-ket, bitta paragraf sifatida chizadi.
 * `indent` — chapdan surish (ro'yxat bandlari uchun).
 */
function drawRuns(doc, runs, { size = SIZE.body, color = COLOR.fg, width = CONTENT_WIDTH, indent = 0, align = 'left' } = {}) {
  doc.fontSize(size)
  runs.forEach((run, i) => {
    const last = i === runs.length - 1
    doc
      .font(run.style === 'bold' ? 'bold' : 'body')
      .fillColor(run.style === 'code' ? COLOR.accent : color)
      .text(run.text, doc.x + (i === 0 ? indent : 0), doc.y, {
        continued: !last,
        width: width - indent,
        align,
        lineGap: LINE_GAP,
      })
  })
  // `continued` zanjiri `doc.x` ni surib qo'yadi — keyingi blok uchun qaytaramiz.
  doc.x = MARGIN.left
}

/**
 * Matnning balandligini uslubni HISOBGA OLIB o'lchaydi.
 *
 * NEGA kerak: eslatma qutisi va jadval katagi uchun avval FON chiziladi,
 * keyin matn. Fon balandligini bilmasdan turib chizib bo'lmaydi.
 * Qalin bo'lak kengroq joy egallaydi, shu sababli o'lchov ham bo'lak-bo'lak.
 */
function measureRuns(doc, runs, width, size = SIZE.body) {
  doc.fontSize(size)
  // Bo'laklar bitta oqim bo'lgani uchun kengliklarni qo'shib, umumiy
  // uzunlikni bitta satrga aylantiramiz — bu pdfkit ning o'z hisobiga
  // eng yaqin natija (aniq o'lchov faqat chizishdan keyin ma'lum).
  const text = runs.map((r) => r.text).join('')
  const boldShare = runs.filter((r) => r.style === 'bold').reduce((n, r) => n + r.text.length, 0) / Math.max(text.length, 1)
  doc.font(boldShare > 0.5 ? 'bold' : 'body')
  return doc.heightOfString(text, { width, lineGap: LINE_GAP })
}

/** Muqova — bitta sahifa: nom, tizim nomi va sana. */
function drawCover(doc, manual) {
  doc.addPage()

  doc.rect(0, 0, PAGE.width, 6).fill(COLOR.navy)

  doc.y = 250
  doc.font('bold').fontSize(SIZE.coverTitle).fillColor(COLOR.navy)
  doc.text(manual.title, MARGIN.left, doc.y, { width: CONTENT_WIDTH, lineGap: 6 })

  doc.moveDown(0.6)
  doc.font('body').fontSize(SIZE.coverSub).fillColor(COLOR.secondary)
  doc.text(manual.subtitle, { width: CONTENT_WIDTH, lineGap: 4 })

  doc.y = PAGE.height - 150
  doc.moveTo(MARGIN.left, doc.y).lineTo(PAGE.width - MARGIN.right, doc.y).lineWidth(0.75).stroke(COLOR.border)
  doc.y += 14
  doc.font('body').fontSize(SIZE.small).fillColor(COLOR.tertiary)
  doc.text(manual.footer, MARGIN.left, doc.y, { width: CONTENT_WIDTH })
}

/**
 * Mundarija. Sahifa raqamlari BIRINCHI o'tishdan olinadi (pastga qarang).
 *
 * NEGA raqamlar kerak: qo'llanma yigirmadan ortiq sahifa — raqamsiz
 * mundarija bosib chiqarilgan nusxada foydasiz.
 */
function drawContents(doc, manual, entries) {
  doc.addPage()
  doc.font('bold').fontSize(SIZE.h1).fillColor(COLOR.navy)
  doc.text(manual.contents, MARGIN.left, MARGIN.top, { width: CONTENT_WIDTH })
  doc.moveDown(1)

  for (const entry of entries) {
    ensureSpace(doc, 22)
    const chapter = entry.level === 1
    const y = doc.y
    doc
      .font(chapter ? 'bold' : 'body')
      .fontSize(chapter ? SIZE.h3 : SIZE.body)
      .fillColor(chapter ? COLOR.navy : COLOR.secondary)

    const number = String(entry.page)
    const numberWidth = doc.widthOfString(number)
    const indent = chapter ? 0 : 16
    const labelWidth = CONTENT_WIDTH - indent - numberWidth - 12

    doc.text(entry.text, MARGIN.left + indent, y, { width: labelWidth, lineBreak: false, ellipsis: true })
    doc.text(number, PAGE.width - MARGIN.right - numberWidth, y, { width: numberWidth, lineBreak: false })

    // Nuqtali yo'lakcha — ko'z sarlavhadan raqamgacha adashmay boradi.
    const dotsFrom = MARGIN.left + indent + Math.min(doc.widthOfString(entry.text), labelWidth) + 4
    const dotsTo = PAGE.width - MARGIN.right - numberWidth - 4
    if (dotsTo > dotsFrom) {
      doc
        .moveTo(dotsFrom, y + (chapter ? 8 : 7))
        .lineTo(dotsTo, y + (chapter ? 8 : 7))
        .lineWidth(0.5)
        .dash(1, { space: 2.5 })
        .stroke(COLOR.border)
        .undash()
    }

    doc.y = y + (chapter ? 20 : 16)
    doc.x = MARGIN.left
  }
}

/**
 * Hujjat tanasi. `onHeading` har bir sarlavha uchun chaqiriladi —
 * mundarija shu orqali yig'iladi.
 */
function drawBody(doc, blocks, onHeading) {
  doc.addPage()

  for (const block of blocks) {
    switch (block.type) {
      case 'h1': {
        // Bob doim yangi sahifadan. Birinchi bob esa endigina ochilgan
        // sahifada qoladi — bo'sh varaq chiqmasin.
        if (doc.y > MARGIN.top) doc.addPage()
        onHeading({ level: 1, text: plain(block.text), page: doc.bufferedPageRange().count - 1 })
        doc.font('bold').fontSize(SIZE.h1).fillColor(COLOR.navy)
        doc.text(block.text, MARGIN.left, doc.y, { width: CONTENT_WIDTH, lineGap: 2 })
        doc.y += 4
        doc.moveTo(MARGIN.left, doc.y).lineTo(PAGE.width - MARGIN.right, doc.y).lineWidth(1.5).stroke(COLOR.navy)
        doc.y += 14
        break
      }
      case 'h2': {
        // Sarlavha sahifa oxirida yolg'iz qolmasin: o'zi + kamida ikki satr.
        ensureSpace(doc, 58)
        doc.y += 8
        onHeading({ level: 2, text: plain(block.text), page: doc.bufferedPageRange().count - 1 })
        doc.font('bold').fontSize(SIZE.h2).fillColor(COLOR.fg)
        doc.text(block.text, MARGIN.left, doc.y, { width: CONTENT_WIDTH })
        doc.y += 5
        break
      }
      case 'h3': {
        ensureSpace(doc, 48)
        doc.y += 6
        doc.font('bold').fontSize(SIZE.h3).fillColor(COLOR.secondary)
        doc.text(block.text, MARGIN.left, doc.y, { width: CONTENT_WIDTH })
        doc.y += 3
        break
      }
      case 'p': {
        ensureSpace(doc, 26)
        drawRuns(doc, parseRuns(block.text))
        doc.y += 5
        break
      }
      case 'list': {
        block.items.forEach((item, i) => {
          ensureSpace(doc, 24)
          const marker = block.ordered ? `${i + 1}.` : '•'
          const y = doc.y
          doc.font('body').fontSize(SIZE.body).fillColor(block.ordered ? COLOR.secondary : COLOR.accent)
          doc.text(marker, MARGIN.left + 4, y, { width: 18, lineBreak: false })
          doc.y = y
          doc.x = MARGIN.left
          drawRuns(doc, parseRuns(item), { indent: 22 })
          doc.y += 2
        })
        doc.y += 5
        break
      }
      case 'note': {
        const padding = 10
        const innerWidth = CONTENT_WIDTH - padding * 2 - 4
        const runs = parseRuns(block.text)
        const height = measureRuns(doc, runs, innerWidth) + padding * 2
        ensureSpace(doc, height + 6)

        const top = doc.y
        doc.rect(MARGIN.left, top, CONTENT_WIDTH, height).fill(block.danger ? COLOR.dangerSoft : COLOR.sunken)
        doc.rect(MARGIN.left, top, 3, height).fill(block.danger ? COLOR.danger : COLOR.tertiary)

        doc.x = MARGIN.left + padding + 4
        doc.y = top + padding
        drawRuns(doc, runs, {
          color: block.danger ? COLOR.danger : COLOR.fg,
          width: innerWidth + (MARGIN.left - doc.x) + padding + 4,
        })
        doc.x = MARGIN.left
        doc.y = top + height + 10
        break
      }
      case 'table': {
        drawTable(doc, block)
        break
      }
      case 'rule': {
        ensureSpace(doc, 16)
        doc.y += 4
        doc.moveTo(MARGIN.left, doc.y).lineTo(PAGE.width - MARGIN.right, doc.y).lineWidth(0.75).stroke(COLOR.border)
        doc.y += 10
        break
      }
      default:
        break
    }
  }
}

function drawTable(doc, block) {
  const total = block.weights.reduce((a, b) => a + b, 0)
  const widths = block.weights.map((w) => (w / total) * CONTENT_WIDTH)
  const padding = 7

  /** Bitta qatorni chizadi; `head` — sarlavha qatori. */
  const drawRow = (cells, head) => {
    const runs = cells.map((cell) => parseRuns(cell))
    const heights = runs.map((r, i) => measureRuns(doc, r, widths[i] - padding * 2, SIZE.small))
    const height = Math.max(...heights) + padding * 2
    ensureSpace(doc, height)

    const top = doc.y
    if (head) doc.rect(MARGIN.left, top, CONTENT_WIDTH, height).fill(COLOR.sunken)

    let x = MARGIN.left
    runs.forEach((cellRuns, i) => {
      doc.x = x + padding
      doc.y = top + padding
      doc.fontSize(SIZE.small)
      cellRuns.forEach((run, j) => {
        const last = j === cellRuns.length - 1
        doc
          .font(head || run.style === 'bold' ? 'bold' : 'body')
          .fillColor(run.style === 'code' ? COLOR.accent : head ? COLOR.fg : COLOR.secondary)
          .text(run.text, doc.x, doc.y, {
            continued: !last,
            width: widths[i] - padding * 2,
            lineGap: 2,
          })
      })
      x += widths[i]
    })

    doc.x = MARGIN.left
    doc.y = top + height
    doc
      .moveTo(MARGIN.left, doc.y)
      .lineTo(PAGE.width - MARGIN.right, doc.y)
      .lineWidth(0.5)
      .stroke(COLOR.border)
  }

  ensureSpace(doc, 60)
  doc.y += 4
  drawRow(block.header, true)
  for (const row of block.rows) drawRow(row, false)
  doc.y += 10
}

/**
 * Pastki kolontitul — hamma sahifaga, MUQOVADAN tashqari.
 *
 * NEGA oxirida: jami sahifa soni faqat hujjat tugagach ma'lum bo'ladi,
 * `bufferPages` esa sahifalarga qaytishga ruxsat beradi.
 */
function drawFooters(doc, manual) {
  const range = doc.bufferedPageRange()
  for (let i = 1; i < range.count; i += 1) {
    doc.switchToPage(range.start + i)

    /*
      Kolontitul PASTKI CHEKKA ichiga yoziladi — ya'ni pdfkit «matn maydoni
      tugadi» deb hisoblaydigan chiziqdan pastga. Chekka o'z holicha qolsa,
      har bir kolontitul yozuvi YANGI SAHIFA ochib yuboradi (hujjat uch
      barobar shishadi va oxirida bo'sh varaqlar qoladi). Shu sababli shu
      sahifaning pastki chekkasi vaqtincha nolga tushiriladi.
    */
    const bottom = doc.page.margins.bottom
    doc.page.margins.bottom = 0

    const y = PAGE.height - MARGIN.bottom + 22

    doc.moveTo(MARGIN.left, y - 10).lineTo(PAGE.width - MARGIN.right, y - 10).lineWidth(0.5).stroke(COLOR.border)
    doc.font('body').fontSize(SIZE.footer).fillColor(COLOR.tertiary)
    doc.text(manual.title, MARGIN.left, y, { width: CONTENT_WIDTH * 0.7, lineBreak: false })
    doc.text(
      manual.page.replace('{n}', String(i)).replace('{total}', String(range.count - 1)),
      MARGIN.left,
      y,
      { width: CONTENT_WIDTH, align: 'right', lineBreak: false },
    )

    doc.page.margins.bottom = bottom
  }
}

/**
 * Hujjatni to'liq yasaydi.
 *
 * Qaytaradi: `{ buffer, pages, bodyStart, headings }`, bunda `bodyStart` —
 * tana MATNI boshlangan sahifaning 0 dan sanaladigan indeksi, `headings` —
 * har bir sarlavha va u tushgan sahifa indeksi.
 *
 * `entries` berilmasa, mundarija chizilmaydi — bu shunchaki o'lchov o'tishi.
 */
function build(manual, blocks, entries) {
  return new Promise((resolve, reject) => {
    const doc = createDocument({
      Title: manual.title,
      Subject: manual.subtitle,
      Author: 'Bonvi',
      Creator: 'bonvi/web/scripts/build-manual.mjs',
    })

    const chunks = []
    const headings = []
    let bodyStart = 0
    let pages = 0

    doc.on('data', (chunk) => chunks.push(chunk))
    doc.on('error', reject)
    doc.on('end', () => resolve({ buffer: Buffer.concat(chunks), pages, bodyStart, headings }))

    drawCover(doc, manual)
    if (entries) drawContents(doc, manual, entries)
    bodyStart = doc.bufferedPageRange().count
    drawBody(doc, blocks, (entry) => headings.push(entry))
    drawFooters(doc, manual)

    // Sahifa soni `end()` DAN OLDIN olinadi: hujjat yopilgach bufer
    // bo'shatiladi va `bufferedPageRange()` nolga tushadi.
    pages = doc.bufferedPageRange().count
    doc.end()
  })
}

/**
 * Mundarijali hujjatni yasaydi va sahifa raqamlari TO'G'RI bo'lguncha
 * qayta yasaydi.
 *
 * Muammo tovuq-tuxum: mundarijadagi raqamlar tana matni qaysi sahifadan
 * boshlanishiga bog'liq, tana esa mundarija necha varaqni egallashiga —
 * ya'ni har biri ikkinchisini kutadi.
 *
 * NEGA baholash formulasi emas: mundarija balandligini oldindan hisoblash
 * (sarlavha soni × qator balandligi) doim taxminiy bo'ladi — uzun sarlavha
 * kesiladi, oxirgi qator sahifaga sig'ib-sig'masligi chegarada hal bo'ladi.
 * Bir varaqqa adashsa, BUTUN mundarija bir sahifaga surilib xato bo'lardi.
 *
 * NEGA «joy band qilib, keyin to'ldirish» emas: band qilingan varaqlar
 * soni ham o'sha taxminga tayanadi — yo yetmaydi, yo bo'sh varaq qoladi.
 *
 * Shu sababli yo'l eng ishonchlisi: yasab ko'rib, HAQIQIY natijani o'lchash
 * va raqamlar o'zgarmay qolgunicha takrorlash. Amalda ikki o'tish yetadi,
 * uchinchisi faqat mundarija chegarada turganda kerak bo'ladi. Har o'tish
 * bir necha o'n millisekund — bu narx umuman sezilmaydi.
 */
async function buildWithContents(manual, blocks) {
  // Birinchi o'lchov: mundarijasiz. Faqat sarlavhalarning o'zaro tartibi
  // va ular tana ichida nechanchi varaqqa tushgani kerak.
  const draft = await build(manual, blocks, null)

  let offset = draft.bodyStart + 1 // taxmin: mundarija bitta varaq
  let result = null

  for (let attempt = 0; attempt < 5; attempt += 1) {
    const entries = draft.headings.map((entry) => ({
      ...entry,
      // Pastki kolontitulda sahifa raqami 0 dan sanaladigan indeksga teng
      // (muqova raqamlanmaydi), shu sababli qo'shimcha «+1» kerak emas.
      page: offset + (entry.page - draft.bodyStart),
    }))

    result = await build(manual, blocks, entries)
    if (result.bodyStart === offset) return result
    offset = result.bodyStart
  }

  // Bu yerga yetib kelish — mundarija uzunligi ikki qiymat orasida
  // «sakrab» turgani. Kutilmagan holat, lekin hujjatni yo'qotmaymiz:
  // oxirgi natija qaytadi va ogohlantirish beriladi.
  console.error('⚠ mundarija sahifalari barqarorlashmadi — raqamlarni tekshiring')
  return result
}

/* ────────────────────────────────────────────────────────────
   Tekshiruv: PDF ichidagi matnni qayta o'qish
   ──────────────────────────────────────────────────────────── */

/**
 * PDF dagi `ToUnicode` jadvallarini o'qib, sahifa oqimlaridagi glif
 * kodlarini HARFGA qaytaradi — ya'ni hujjatdan matnni AJRATIB OLADI,
 * xuddi «nusxa olish» yoki qidiruv qilgandek.
 *
 * NEGA bu kerak: shrift joylashtirilgani PDF ichida to'g'ri matn
 * borligini KAFOLATLAMAYDI. Kirill harfi noto'g'ri kodlansa, ekranda
 * to'g'ri ko'rinib turaveradi, lekin nusxa olinganda axlat chiqadi va
 * qidiruv topmaydi. Buni faqat teskari yo'ldan — kodni harfga qaytarib —
 * tekshirsa bo'ladi.
 *
 * NEGA tayyor kutubxona emas: yagona jiddiy nomzod `pdfjs-dist` — o'nlab
 * megabayt va butun brauzer render qatlami, bu yerda esa faqat ikki
 * jadval o'qiladi. Yozilganicha atigi bir necha o'nlab qator.
 */
function extractText(buffer) {
  const raw = buffer.toString('latin1')

  /**
   * `12 0 obj … endobj` — obyektlarni raqami bo'yicha yig'adi:
   * `{ dict, data }`, bunda `data` — siqilgan oqimning xom baytlari.
   *
   * NEGA oddiy `indexOf('endobj')` emas: hujjatda joylashtirilgan shrift
   * bor va u SIQILGAN IKKILIK ma'lumot. Uning baytlari orasida tasodifan
   * `endobj` yoki `7 0 obj` ketma-ketligi uchrashi mumkin — u holda soddaroq
   * qidiruv obyekt chegarasini xato joyda kesadi yoki mavjud obyektni
   * soxtasi bilan almashtiradi (bu aynan shu yerda yuz bergan: sahifa
   * obyekti «yo'qolib», tekshiruv bo'sh matn ko'rgan edi). Shu sababli
   * o'qish CHIZIQLI va oqim uzunligi lug'atdagi `/Length` dan olinadi.
   */
  const objects = new Map()
  {
    let pos = 0
    const header = /(\d+)\s+0\s+obj/g
    for (;;) {
      header.lastIndex = pos
      const found = header.exec(raw)
      if (!found) break

      const bodyStart = found.index + found[0].length
      const streamAt = raw.indexOf('stream', bodyStart)
      const objEnd = raw.indexOf('endobj', bodyStart)
      if (objEnd === -1) break

      if (streamAt !== -1 && streamAt < objEnd) {
        const dict = raw.slice(bodyStart, streamAt)
        const length = Number(/\/Length\s+(\d+)/.exec(dict)?.[1] ?? 0)
        // `stream` dan keyin CRLF yoki LF keladi, so'ng aynan `length` bayt.
        const dataFrom = streamAt + (raw[streamAt + 6] === '\r' ? 8 : 7)
        objects.set(Number(found[1]), { dict, data: raw.slice(dataFrom, dataFrom + length) })
        pos = raw.indexOf('endobj', dataFrom + length)
        pos = pos === -1 ? raw.length : pos + 6
      } else {
        objects.set(Number(found[1]), { dict: raw.slice(bodyStart, objEnd), data: null })
        pos = objEnd + 6
      }
    }
  }

  /** `5 0 R` ko'rinishidagi havolani obyektga aylantiradi. */
  const deref = (reference) => {
    const found = /^(\d+)\s+0\s+R$/.exec(reference.trim())
    return found ? objects.get(Number(found[1])) : undefined
  }

  /** Obyekt oqimini ochadi (pdfkit `FlateDecode` bilan siqadi). */
  const streamOf = (object) => {
    if (!object?.data) return null
    if (!/\/FlateDecode/.test(object.dict)) return object.data
    try {
      return inflateSync(Buffer.from(object.data, 'latin1')).toString('latin1')
    } catch {
      return null
    }
  }

  /**
   * `beginbfchar`/`beginbfrange` — glif kodi » Unicode.
   *
   * `bfrange` ning IKKI shakli bor va ikkalasi ham qo'llab-quvvatlanadi:
   *
   *   <0041> <0043> <0061>                       — ketma-ket: 41»61, 42»62, 43»63
   *   <0000> <0002> [<0053> <049b> <2018>]       — ro'yxat: har kod alohida
   *
   * NEGA ikkalasi ham kerak: pdfkit aynan IKKINCHISINI yozadi (gliflar
   * shriftdan tanlab olingani uchun ularning tartibi Unicode tartibiga
   * mos kelmaydi). Faqat birinchi shaklni bilgan o'quvchi ro'yxatning
   * BIRINCHI elementini «boshlang'ich qiymat» deb o'qiydi va butun
   * alifboni surib yuboradi.
   *
   * ⚠️ Ichida BO'SHLIQ bo'lgan yozuv ham bor va u aynan shu yerda tuzoq
   * qo'yadi:
   *
   *   [<0066> <0069> <0066 0069>]
   *                  ^^^^^^^^^^^ bitta glif, IKKI harf: «fi» ligaturasi
   *
   * Bir nechta harfga yoyiladigan glif (ligatura) uchun PDF standarti
   * o'n oltilik raqamlarni bo'shliq bilan ajratib yozishga ruxsat beradi.
   * Bo'shliqni hisobga olmagan o'quvchi bu yozuvni UMUMAN KO'RMAYDI —
   * ro'yxatdagi bitta katak tushib qoladi va undan KEYINGI hamma harf
   * bittaga suriladi. Natija ayniqsa yomon: matn o'qiladigandek tuyuladi
   * («Grafiklar» o'rniga «GraAklar», «Manzil» o'rniga «’anzil»), ya'ni xato
   * darrov ko'zga tashlanmaydi. Shu sababli hamma joyda `[0-9A-Fa-f\s]`
   * ishlatiladi va raqam o'qishdan oldin bo'shliqlar olib tashlanadi.
   */
  const parseCMap = (text) => {
    const map = new Map()

    /** `<00660069>` yoki `<0066 0069>` » «fi». */
    const fromUtf16 = (hex) => {
      const clean = hex.replace(/\s+/g, '')
      let out = ''
      for (let i = 0; i + 3 < clean.length; i += 4) {
        out += String.fromCharCode(parseInt(clean.slice(i, i + 4), 16))
      }
      return out
    }

    const codeOf = (hex) => parseInt(hex.replace(/\s+/g, ''), 16)

    const put = (code, hex) => {
      const value = fromUtf16(hex)
      // `<0000>` — `.notdef` glifi; uni matnga qo'shish faqat axlat beradi.
      if (value.length > 0 && value.charCodeAt(0) !== 0) map.set(code, value)
    }

    const HEX = '[0-9A-Fa-f\\s]+'

    for (const block of text.matchAll(/beginbfchar([\s\S]*?)endbfchar/g)) {
      for (const pair of block[1].matchAll(new RegExp(`<(${HEX})>\\s*<(${HEX})>`, 'g'))) {
        put(codeOf(pair[1]), pair[2])
      }
    }

    for (const block of text.matchAll(/beginbfrange([\s\S]*?)endbfrange/g)) {
      const entryRe = new RegExp(`<(${HEX})>\\s*<(${HEX})>\\s*(\\[[^\\]]*\\]|<${HEX}>)`, 'g')
      for (const row of block[1].matchAll(entryRe)) {
        const from = codeOf(row[1])
        const to = codeOf(row[2])

        if (row[3].startsWith('[')) {
          const targets = [...row[3].matchAll(new RegExp(`<(${HEX})>`, 'g'))].map((m) => m[1])
          targets.forEach((hex, i) => put(from + i, hex))
        } else {
          const base = codeOf(row[3].slice(1, -1))
          for (let code = from; code <= to; code += 1) {
            put(code, (base + (code - from)).toString(16).padStart(4, '0'))
          }
        }
      }
    }
    return map
  }

  // Shrift obyekti » uning ToUnicode jadvali.
  const cmapByFont = new Map()
  for (const [number, object] of objects) {
    const toUnicode = /\/ToUnicode\s+(\d+)\s+0\s+R/.exec(object.dict)
    if (!toUnicode) continue
    const stream = streamOf(objects.get(Number(toUnicode[1])))
    if (stream) cmapByFont.set(number, parseCMap(stream))
  }

  let text = ''
  for (const [, object] of objects) {
    const body = object.dict
    if (!/\/Type\s*\/Page\b/.test(body)) continue

    /*
      Sahifa resurslari: `/F2 8 0 R` » qaysi shrift qaysi nom ostida.

      pdfkit `/Resources` ni ALOHIDA obyektga chiqaradi (`/Resources 6 0 R`),
      shu sababli avval havola ochiladi. Sahifaning o'zida ham to'g'ridan-
      to'g'ri lug'at bo'lishi mumkin — ikkala holat ham qo'llab-quvvatlanadi.
    */
    const fonts = new Map()
    const resourcesRef = /\/Resources\s+(\d+\s+0\s+R)/.exec(body)
    const resources = resourcesRef ? (deref(resourcesRef[1])?.dict ?? '') : body
    const fontDict = /\/Font\s*<<([\s\S]*?)>>/.exec(resources)
    if (fontDict) {
      for (const entry of fontDict[1].matchAll(/\/(\w+)\s+(\d+)\s+0\s+R/g)) {
        fonts.set(entry[1], cmapByFont.get(Number(entry[2])))
      }
    }

    const contents = /\/Contents\s+(\d+)\s+0\s+R/.exec(body)
    const stream = contents ? streamOf(objects.get(Number(contents[1]))) : null
    if (!stream) continue

    let current = null
    /*
      `/F2 10 Tf` shriftni almashtiradi; `<hex> Tj` va `[<hex> 20 <hex>] TJ`
      matn chizadi — ikkinchisida bitta amalda bir nechta hex bo'lak bo'ladi
      (oradagi raqamlar — kerning), shu sababli hex bo'laklar ALOHIDA
      qidiriladi. `Tm`/`Td`/`T*`/`ET` — yangi satr yoki yangi bo'lak,
      ular o'rniga bo'shliq qo'yiladi.
    */
    for (const op of stream.matchAll(
      /\/(\w+)\s+[\d.]+\s+Tf|<([0-9A-Fa-f\s]+)>|\bTm\b|\bTd\b|\bTD\b|\bT\*\b|\bET\b/g,
    )) {
      if (op[1] !== undefined) {
        current = fonts.get(op[1]) ?? null
      } else if (op[2] !== undefined && current) {
        // Bo'shliq PDF ning o'n oltilik satrida hech narsani anglatmaydi
        // (`ToUnicode` dagi bilan bir xil qoida) — u shunchaki tashlanadi.
        const hex = op[2].replace(/\s+/g, '')
        for (let i = 0; i + 3 < hex.length; i += 4) {
          text += current.get(parseInt(hex.slice(i, i + 4), 16)) ?? ''
        }
      } else {
        text += ' '
      }
    }
    text += '\n'
  }
  return text
}

/* ────────────────────────────────────────────────────────────
   Ishga tushirish
   ──────────────────────────────────────────────────────────── */

if (!existsSync(`${FONT_DIR}NotoSans-Regular.ttf`)) {
  console.error(`✗ Shrift topilmadi: ${FONT_DIR}NotoSans-Regular.ttf`)
  process.exit(1)
}
if (!existsSync(OUT_DIR)) mkdirSync(OUT_DIR, { recursive: true })

let failed = false

for (const edition of EDITIONS) {
  const source = readFileSync(`${SOURCE_DIR}${edition.source}`, 'utf8')

  /*
    Manba faylining boshidagi `<!-- kalit: qiymat -->` qatorlari —
    muqova va kolontitul matnlari. NEGA shu yerda, skriptda emas: ular
    ham tarjima qilinadigan MATN, ya'ni ularning o'rni matn faylida.
  */
  const meta = Object.fromEntries(
    [...source.matchAll(/<!--\s*(\w+)\s*:\s*(.*?)\s*-->/g)].map((m) => [m[1], m[2]]),
  )
  const manual = {
    title: meta.title ?? 'Bonvi HR',
    subtitle: meta.subtitle ?? '',
    contents: meta.contents ?? 'Mundarija',
    footer: meta.footer ?? '',
    page: meta.page ?? '{n} / {total}',
  }

  const blocks = parseMarkdown(source.replace(/<!--[\s\S]*?-->/g, ''))

  const { buffer, pages } = await buildWithContents(manual, blocks)

  // ── Tekshiruv ──
  const extracted = extractText(buffer)
  // Tekshiruv nima ko'rganini ko'rish uchun: MANUAL_DEBUG=1 ./bonvi pnpm build:manual
  if (process.env.MANUAL_DEBUG) console.log(`\n──── ${edition.out} ────\n${extracted}\n────\n`)
  const normalize = (s) => s.replace(/\s+/g, '')
  const haystack = normalize(extracted)

  /*
    Manbadagi HAR BIR sarlavha PDF ichidan qayta topilishi shart. Sarlavha
    tanlangani bejiz emas: ular butun hujjat bo'ylab tarqalgan, ikkala
    shriftda ham (qalin) chizilgan va o'zbekcha maxsus harflarni o'z ichiga
    oladi — ya'ni bitta tekshiruv butun matn qatlamini qamrab oladi.
  */
  const headings = blocks.filter((b) => b.type === 'h1' || b.type === 'h2').map((b) => plain(b.text))
  const lost = headings.filter((h) => !haystack.includes(normalize(h)))

  /*
    Alohida harf tekshiruvi: manbada uchraydigan har bir «xavfli» belgi
    (kirillning o'zbekcha harflari, lotinning `o‘`/`g‘` apostrofi, tire)
    ajratilgan matnda ham bo'lishi kerak. Sarlavha tekshiruvi buni
    qamrab olmasligi mumkin — harf faqat tana matnida uchrashi mumkin.
  */
  const risky = [...new Set(source.match(/[Ѐ-ӿ‘’ʻ–—«»]/g) ?? [])]
  const lostChars = risky.filter((c) => !extracted.includes(c))

  if (lost.length > 0 || lostChars.length > 0) {
    failed = true
    console.error(`✗ ${edition.out}`)
    if (lost.length > 0) console.error(`    matn qatlamida topilmagan sarlavhalar: ${lost.join(' | ')}`)
    if (lostChars.length > 0) console.error(`    matn qatlamida topilmagan harflar: ${lostChars.join(' ')}`)
    console.error('    Fayl YOZILMADI — shrift yoki kodlash muammosi.')
    continue
  }

  writeFileSync(`${OUT_DIR}${edition.out}`, buffer)
  console.log(
    `✓ ${edition.out.padEnd(34)} ${String(pages).padStart(2)} sahifa · ` +
      `${(buffer.length / 1024).toFixed(0)} KB · ${headings.length} sarlavha · ` +
      `${risky.length} maxsus harf tekshirildi`,
  )
}

if (failed) process.exit(1)

console.log(`\nManba: web/manual/  ·  Natija: web/public/qollanma/`)
console.log(`Shrift: Noto Sans (SIL OFL 1.1) — ${readdirSync(FONT_DIR).filter((f) => f.endsWith('.ttf')).join(', ')}`)
