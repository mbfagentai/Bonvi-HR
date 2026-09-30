/**
 * `localStorage` ustidagi xavfsiz qobiq.
 *
 * ⚠️ Har bir o'qish va yozish `try/catch` ichida: maxfiy rejimda va
 * «saytlarga ma'lumot saqlashni taqiqlash» sozlamasida `localStorage`
 * ning O'ZIGA murojaat qilish ham istisno tashlaydi. Sozlama
 * saqlanmasa ilova baribir ishlashi kerak — faqat keyingi safar
 * eslab qolmaydi.
 *
 * ⚠️ Safari'da yozuvlar 7 kun faolsizlikdan keyin o'chirilishi mumkin,
 * ya'ni token yo'qolishi ODATIY hol (UX §G.4). Shu sababli hech bir
 * joy yo'qolgan qiymatni xato deb hisoblamaydi.
 */
export function readStorage(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null
  }
}

export function writeStorage(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value)
  } catch {
    // ataylab jim: saqlay olmaslik xato emas
  }
}

export function removeStorage(key: string): void {
  try {
    window.localStorage.removeItem(key)
  } catch {
    // ataylab jim
  }
}

export function readJson<T>(key: string, isValid: (value: unknown) => value is T): T | null {
  const raw = readStorage(key)
  if (!raw) return null

  try {
    const parsed: unknown = JSON.parse(raw)
    return isValid(parsed) ? parsed : null
  } catch {
    return null
  }
}

export function writeJson(key: string, value: unknown): void {
  try {
    writeStorage(key, JSON.stringify(value))
  } catch {
    // ataylab jim
  }
}
