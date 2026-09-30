/**
 * Eskirgan brauzer ekrani (UX §F.5).
 *
 * ⚠️ Bu ekran i18n DAN OLDIN chiziladi va tarjimaga tayanmaydi: agar
 * brauzer ilovani ko'tara olmasa, i18n ham ishlamasligi mumkin. Matn
 * shu sababli faqat o'zbekcha va faylning ichida.
 *
 * Yarim ishlaydigan ilova aniq rad javobidan ko'ra battar
 * qo'ng'iroqlar tug'diradi — shuning uchun ilova ishga tushmaydi.
 */
export function GateScreen() {
  return (
    <div className="mx-auto flex min-h-screen w-full max-w-app flex-col justify-center px-6 text-center">
      <h1 className="text-title1 font-bold text-fg">Brauzeringiz eskirgan</h1>
      <p className="mt-3 text-subhead text-fg-secondary">
        Ilova ishlashi uchun Safari yoki Chrome’ning yangi versiyasi kerak. Turniketdan o‘ting yoki
        HR bo‘limiga murojaat qiling.
      </p>
    </div>
  )
}
