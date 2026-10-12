/**
 * Suratdan kvadrat bo'lak kesish — brauzerda, yuklashdan OLDIN.
 *
 * NEGA kerak: server suratni 512×512 kvadratga markazdan kesib keltiradi
 * (`SquarePhotoStore`). Bo'yi baland rasmda (odam butun gavdasi bilan)
 * markaz ko'krakka tushadi va bosh kesilib qoladi — kartada ham, Face ID
 * terminalida ham yuz ko'rinmaydi (foydalanuvchi talabi, 2026-10-12).
 * Endi HR kvadratni o'zi joylashtiradi; serverga allaqachon kvadrat rasm
 * boradi va uning markazdan kesishi hech narsani o'zgartirmaydi.
 */

/** Kvadrat bo'lak — asl rasm pikselida. */
export interface CropArea {
  x: number
  y: number
  size: number
}

/** Chiqish o'lchami: server baribir 512 ga tushiradi, ortig'i — sifat zaxirasi. */
const OUTPUT_MAX = 1024

/**
 * Faylning tanlangan bo'lagini JPEG faylga aylantiradi.
 *
 * Shaffof PNG oq fonga qo'yiladi (JPEG'da shaffoflik yo'q). Kichik rasm
 * kattalashtirilmaydi — sifat qo'shilmaydi, faqat hajm.
 */
export async function cropToSquareFile(file: File, area: CropArea): Promise<File> {
  const image = await loadImage(file)

  try {
    const size = Math.max(1, Math.min(OUTPUT_MAX, Math.round(area.size)))
    const canvas = document.createElement('canvas')
    canvas.width = size
    canvas.height = size

    const context = canvas.getContext('2d')
    if (context === null) throw new Error('canvas')

    context.fillStyle = '#ffffff'
    context.fillRect(0, 0, size, size)
    context.imageSmoothingQuality = 'high'
    context.drawImage(image.element, area.x, area.y, area.size, area.size, 0, 0, size, size)

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.92))
    if (blob === null) throw new Error('toBlob')

    const base = file.name.replace(/\.[^.]+$/u, '') || 'photo'
    return new File([blob], `${base}.jpg`, { type: 'image/jpeg', lastModified: Date.now() })
  } finally {
    image.release()
  }
}

async function loadImage(file: File): Promise<{ element: HTMLImageElement; release: () => void }> {
  const url = URL.createObjectURL(file)
  const element = new Image()
  element.decoding = 'async'
  element.src = url

  try {
    await element.decode()
  } catch (error) {
    URL.revokeObjectURL(url)
    throw error
  }

  return { element, release: () => URL.revokeObjectURL(url) }
}

/**
 * Yuklanadigan fayl: tanlangan kvadrat, kesib bo'lmasa — asl fayl.
 *
 * Kesish yiqilsa (buzuq fayl, eski brauzer) HR to'xtab qolmaydi: asl rasm
 * ketadi va server uni avvalgidek markazdan kesadi.
 */
export async function croppedOrOriginal(file: File, area: CropArea | null): Promise<File> {
  if (area === null) return file

  try {
    return await cropToSquareFile(file, area)
  } catch {
    return file
  }
}
