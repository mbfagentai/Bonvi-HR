/**
 * Bugungi sana `YYYY-MM-DD` ko'rinishida — MAHALLIY vaqt bo'yicha.
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
 * mahalliy: 30-avgust 00:49   →  toISOString(): «2026-08-29»  ✗
 *                                todayIso():    «2026-08-30»  ✓
 * ```
 *
 * Oqibati faqat ko'rinish emas edi: kechqurun ishlayotgan HR ochgan
 * formada sana kechagi kun bo'lib turardi va u shu holicha saqlanardi —
 * belgi, hodisa va buyruq sanalari bir kunga surilardi. Tug'ilgan sana
 * tekshiruvi ham («kelajakdagi sana bo'lmasin») o'sha soatlarda bir kunga
 * qattiqroq ishlardi.
 *
 * <b>Nega <c>Intl</c> yoki kutubxona emas.</b> Bu yerda vaqt mintaqasini
 * O'GIRISH kerak emas — brauzerning o'z kunini olish kifoya, va
 * `getFullYear`/`getMonth`/`getDate` aynan shuni beradi.
 */
export function todayIso(): string {
  const now = new Date()
  const pad = (value: number) => String(value).padStart(2, '0')

  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}
