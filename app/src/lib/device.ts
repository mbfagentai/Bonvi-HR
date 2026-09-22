/**
 * Qurilmani taxmin qilish.
 *
 * ⚠️ Bu ANIQ emas va aniq bo'lolmaydi ham: UA satri yolg'on bo'lishi
 * mumkin. Shu sababli natija hech qayerda YAKUNIY qaror sifatida
 * ishlatilmaydi — joylashuv yordami ekranida to'rtala variant ham
 * «Boshqa qurilma» ostida ochilib turadi (UX §C.7), o'rnatish kartasi
 * esa noto'g'ri taxminda shunchaki ortiqcha maslahat bo'lib qoladi.
 */
export type DeviceKind = 'ios-standalone' | 'ios-safari' | 'android-app' | 'android-chrome'

interface NavigatorWithStandalone extends Navigator {
  /** iOS Safari'da bor, boshqa joyda yo'q. */
  standalone?: boolean
}

/** Bosh ekranga qo'shilgan holda ishga tushirilganmi. */
export function isStandalone(): boolean {
  const nav = window.navigator as NavigatorWithStandalone
  if (nav.standalone === true) return true

  // Android/Chromium: media so'rovi. Eski brauzerda `matchMedia` bor,
  // lekin bu so'rovni bilmaydi — u holda `false` qaytadi, bu to'g'ri.
  try {
    return window.matchMedia('(display-mode: standalone)').matches
  } catch {
    return false
  }
}

export function isIos(): boolean {
  const ua = window.navigator.userAgent
  if (/iPad|iPhone|iPod/.test(ua)) return true

  // iPadOS 13+ o'zini Mac deb ko'rsatadi; teginish nuqtalari bilan ajratiladi
  return /Macintosh/.test(ua) && window.navigator.maxTouchPoints > 1
}

export function isAndroid(): boolean {
  return /Android/.test(window.navigator.userAgent)
}

/**
 * Telegram, Instagram va shunga o'xshash ilova ichidagi brauzer.
 * U yerda PWA o'rnatib bo'lmaydi va joylashuv ruxsati ham ilovaniki
 * bo'ladi — shu sababli kirish ekranida ochiq aytiladi (UX §F.1).
 */
export function isInAppBrowser(): boolean {
  const ua = window.navigator.userAgent
  return /Telegram|Instagram|FBAN|FBAV|Line\/|MicroMessenger/i.test(ua)
}

export function deviceKind(): DeviceKind {
  if (isIos()) return isStandalone() ? 'ios-standalone' : 'ios-safari'
  return isStandalone() ? 'android-app' : 'android-chrome'
}

/**
 * Ilova umuman ishlay oladimi (UX §F.5 — «gate screen»).
 *
 * Uchtasidan biri yo'q bo'lsa ilova ishga TUSHMAYDI va bitta statik
 * ekran chiziladi: yarim ishlaydigan ilova aniq rad javobidan ko'ra
 * battar qo'ng'iroqlar tug'diradi.
 */
export function isSupportedBrowser(): boolean {
  if (!('geolocation' in window.navigator)) return false

  try {
    const probe = '__bonvi__'
    window.localStorage.setItem(probe, '1')
    window.localStorage.removeItem(probe)
  } catch {
    return false
  }

  // Service worker HTTPS (yoki localhost) talab qiladi — dev'da ham,
  // production'da ham bor. Uni tekshirish ilovani oflayn ishlashini
  // kafolatlaydi, lekin yo'qligi FALOKAT emas: shu sababli faqat
  // ogohlantirish uchun ajratib qo'yilgan.
  return true
}

/** Oflayn qobiq bormi — `Sozlamalar` da holatni ko'rsatish uchun. */
export function hasServiceWorker(): boolean {
  return 'serviceWorker' in window.navigator
}
