/** Longest edge of the uploaded selfie (SPEC §7). */
const PHOTO_MAX_EDGE = 1600

/** Server cap is 5 MB; a 1600 px JPEG at this quality is ~300–600 KB. */
const PHOTO_QUALITY = 0.85

/**
 * Downscales a camera photo in the browser before upload. Tablet cameras
 * produce 8–12 MB files; sending them over office Wi-Fi is slow and the
 * server would shrink them anyway. `imageOrientation: 'from-image'` applies
 * the EXIF rotation, so a portrait selfie does not arrive sideways.
 */
export async function downscalePhoto(file: Blob): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })

  try {
    const scale = Math.min(1, PHOTO_MAX_EDGE / Math.max(bitmap.width, bitmap.height))
    const width = Math.max(1, Math.round(bitmap.width * scale))
    const height = Math.max(1, Math.round(bitmap.height * scale))

    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height

    const context = canvas.getContext('2d')
    if (context === null) return file

    context.drawImage(bitmap, 0, 0, width, height)

    return await canvasBlob(canvas, 'image/jpeg', PHOTO_QUALITY)
  } finally {
    bitmap.close()
  }
}

export function canvasBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob === null ? reject(new Error('Canvas export failed')) : resolve(blob)),
      type,
      quality,
    )
  })
}
