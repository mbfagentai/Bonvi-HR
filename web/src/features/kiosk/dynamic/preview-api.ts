import { revealProblemDetails } from '@/features/employees/api/employees-api'
import { api } from '@/shared/api/http-client'
import { fileNameFromDisposition } from '@/shared/lib/download'
import { parseKioskSession, type KioskLanguage, type KioskSession } from '../api/kiosk-api'

/*
 * `/anketa/korinish` — HR sees the constructor's DRAFT as candidates will
 * (SPEC-anketa-konstruktori §2.6, §9). Unlike the public form these calls go
 * through the HR client: the server requires a signed-in HR user with
 * `recruiting.view`, and an expired session takes the usual refresh / sign-in
 * path. Nothing is stored on the server.
 */

/** The session with the live draft as `definition` (version 0, label «draft» or the draft's). */
export async function fetchPreviewSession(signal?: AbortSignal): Promise<KioskSession> {
  const { data } = await api.get<KioskSession>('/api/kiosk/questionnaire/preview', { signal, timeout: 60_000 })
  return parseKioskSession(data)
}

export interface PreviewPdfRequest {
  answers: Record<string, unknown>
  language: KioskLanguage
  /** Optional in the preview — the sample PDF leaves the boxes empty. */
  photo: Blob | null
  signature: Blob | null
}

/** The sample PDF («Anketa № Namuna»), drawn from the draft exactly as a submission would be. */
export async function fetchPreviewPdf(request: PreviewPdfRequest): Promise<{ blob: Blob; fileName: string }> {
  const form = new FormData()
  form.append('answers', JSON.stringify(request.answers))
  form.append('language', request.language)
  if (request.photo !== null) form.append('photo', request.photo, 'photo.jpg')
  if (request.signature !== null) form.append('signature', request.signature, 'signature.png')

  try {
    const response = await api.post<Blob>('/api/kiosk/questionnaire/preview/pdf', form, {
      responseType: 'blob',
      timeout: 60_000,
    })
    const disposition = response.headers['content-disposition']

    return {
      blob: response.data,
      fileName: fileNameFromDisposition(typeof disposition === 'string' ? disposition : null) ?? 'Anketa_namuna.pdf',
    }
  } catch (error) {
    // The error body arrives as a Blob too — read it so the code and the field path show
    await revealProblemDetails(error)
    throw error
  }
}
