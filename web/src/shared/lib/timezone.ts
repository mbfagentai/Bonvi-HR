/**
 * ⭐ TIZIMNING YAGONA VAQT MINTAQASI — Toshkent (UTC+5).
 *
 * <b>Nega brauzerning mintaqasi YARAMAYDI.</b> Ilgari sana brauzer
 * qanday mintaqada tursa, o'shanda hisoblanardi. O'zbekistonda bu
 * amalda to'g'ri chiqadi (brauzer ham +5 da), lekin uchta real holatda
 * sana bir kunga siljiydi:
 *   · sayohatdagi rahbarning noutbugi boshqa mintaqada;
 *   · brauzer serverda ochilgan (odatda UTC);
 *   · operatsion tizimda mintaqa qo'lda noto'g'ri qo'yilgan.
 *
 * Va bu faqat ko'rinish emas: `todayIso()` FORMA maydonini oldindan
 * to'ldiradi, ya'ni noto'g'ri kun bazaga YOZILADI.
 *
 * <b>Nega aynan shu qiymat.</b> Backend allaqachon shunday ishlaydi:
 * `SystemClock.TashkentOffset` (Infrastructure) va `BotClock` (bot)
 * ikkalasi ham UTC+5 da. Frontend uchinchi qoida bo'lib qolsa,
 * «bugun» degan tushuncha tizimda ikki xil bo'lardi.
 *
 * <b>Nega `'+05:00'` emas, IANA nomi.</b> Nom mintaqaning butun
 * tarixini biladi (Toshkent 1991 gacha yozgi vaqtga o'tardi), qattiq
 * siljish esa faqat bugungi kunni. Tug'ilgan sanalar 1950-yillargacha
 * boradi va ular uchun farq realdir.
 */
export const TASHKENT_TIME_ZONE = 'Asia/Tashkent'

/** Toshkent mintaqasidagi sana va vaqt bo'laklari — hammasi to'ldirilgan satr. */
export interface TashkentParts {
  /** «2026» */
  year: string
  /** «09» */
  month: string
  /** «06» */
  day: string
  /** «09» — 24 soatlik shkala. */
  hour: string
  /** «30» */
  minute: string
}

/**
 * Formatlagich BIR MARTA yaratiladi.
 *
 * `Intl.DateTimeFormat` konstruktori qimmat (mintaqa jadvali o'qiladi),
 * `formatToParts` esa arzon. Ro'yxatlarda bu funksiya yuzlab marta
 * chaqiriladi — har chaqiruvda yangi formatlagich yasash sezilarli
 * yuk bo'lardi.
 *
 * `en-GB` + `hourCycle: 'h23'` — bo'laklar ARAB raqamlarida va soat
 * 24 lik shkalada kelishi uchun. Til bu yerda ahamiyatsiz: natija
 * bo'laklarga ajratiladi va tartibi chaqiruvchida yig'iladi.
 */
const FORMATTER = new Intl.DateTimeFormat('en-GB', {
  timeZone: TASHKENT_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

/**
 * Sanani Toshkent mintaqasida bo'laklarga ajratadi.
 *
 * ⚠️ `date.getDate()` va qardoshlari ATAYLAB ishlatilmaydi: ular
 * BRAUZER mintaqasini oladi va aynan shu narsadan qutulmoqchimiz.
 * `formatToParts` esa mintaqani hisobga olib, tayyor bo'laklarni
 * beradi — o'rtada qo'lda soat qo'shish yoki ayirish yo'q (unday
 * hisob yozgi vaqt va tarixiy o'zgarishlarda buzilardi).
 *
 * ⚠️ Chaqiruvchi sananing YAROQLI ekanini oldindan tekshirishi shart:
 * `Invalid Date` da `formatToParts` istisno tashlaydi.
 */
export function tashkentParts(date: Date): TashkentParts {
  const found = new Map<string, string>()
  for (const part of FORMATTER.formatToParts(date)) {
    if (part.type !== 'literal') found.set(part.type, part.value)
  }

  return {
    year: found.get('year') ?? '',
    month: found.get('month') ?? '',
    day: found.get('day') ?? '',
    hour: found.get('hour') ?? '',
    minute: found.get('minute') ?? '',
  }
}
