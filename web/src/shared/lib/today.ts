import { tashkentParts } from './timezone'

/**
 * Bugungi sana `YYYY-MM-DD` ko'rinishida — TOSHKENT vaqti bo'yicha.
 *
 * <b>Nega alohida funksiya.</b> Loyihada bu qiymat o'n ikkita joyda kerak
 * (belgi, hodisa, suhbat, rag'bat, ishga qabul, bo'shatish formalari va
 * tug'ilgan sana tekshiruvi) va hammasida bir xil bo'lishi shart.
 *
 * <b>Nega <c>toISOString()</c> EMAS.</b> Ilgari har bir joyda
 * `new Date().toISOString().slice(0, 10)` yozilgan edi va u sanani
 * <b>UTC</b> bo'yicha qaytaradi. Toshkent UTC+5 da, ya'ni har kuni
 * yarim tundan ertalab soat 5 gacha bu ifoda KECHAGI sanani beradi:
 *
 * ```
 * Toshkent: 30-avgust 00:49   →  toISOString(): «2026-08-29»  ✗
 *                                todayIso():    «2026-08-30»  ✓
 * ```
 *
 * Oqibati faqat ko'rinish emas edi: kechqurun ishlayotgan HR ochgan
 * formada sana kechagi kun bo'lib turardi va u shu holicha saqlanardi —
 * belgi, hodisa va buyruq sanalari bir kunga surilardi. Tug'ilgan sana
 * tekshiruvi ham («kelajakdagi sana bo'lmasin») o'sha soatlarda bir kunga
 * qattiqroq ishlardi.
 *
 * <b>Nega BRAUZERNING kuni ham yaramaydi.</b> Ilgari bu yerda
 * `getFullYear`/`getMonth`/`getDate` turardi — ular brauzer qanday
 * mintaqada bo'lsa, o'sha kunni beradi. O'zbekistonda bu to'g'ri
 * chiqadi, lekin sayohatdagi noutbukda, serverda ochilgan brauzerda
 * yoki mintaqasi noto'g'ri qo'yilgan mashinada AYNAN o'sha bir kunlik
 * siljish qaytadi — endi UTC tomonga emas, ixtiyoriy tomonga. Sana esa
 * bu yerda ko'rsatilmaydi, forma orqali BAZAGA YOZILADI.
 *
 * Shu sababli mintaqa qat'iy qadalgan (`TASHKENT_TIME_ZONE`) —
 * backend'dagi `SystemClock.TashkentOffset` bilan bitta qoida.
 */
export function todayIso(): string {
  const { year, month, day } = tashkentParts(new Date())

  return `${year}-${month}-${day}`
}
