import { createContext, useContext } from 'react'

import type { FieldError, FieldErrors } from '../../lib/answers-zod'
import type {
  Answers,
  BlankAttachmentDto,
  ComputedValues,
  DocRefAnswer,
  RefAnswer,
  RefKind,
} from '../../lib/definition-types'

/** Employee / department / position search — injected; the renderer never fetches. */
export type RefSearch = (kind: RefKind, query: string) => Promise<RefAnswer[]>
/** Linked-blank search for `docRef` fields (approved submissions of these templates). */
export type DocRefSearch = (templateCodes: readonly string[], query: string) => Promise<DocRefAnswer[]>
/** Upload one file into a field path; resolves with the stored attachment (API #17). */
export type UploadFile = (fieldPath: string, file: File) => Promise<BlankAttachmentDto>
/** Delete an attachment (API #18). */
export type RemoveFile = (attachment: BlankAttachmentDto) => Promise<void>

export interface RendererContextValue {
  answers: Answers
  /** Server values overlaid with the live client evaluation. */
  computed: ComputedValues
  errors: FieldErrors
  attachments: readonly BlankAttachmentDto[]
  searchRef: RefSearch | undefined
  searchDocRef: DocRefSearch | undefined
  uploadFile: UploadFile | undefined
  removeFile: RemoveFile | undefined
  openFile: ((attachment: BlankAttachmentDto) => void) | undefined
  /** Translated text of a field error. */
  errorText: (error: FieldError | undefined) => string | undefined
}

export const RendererContext = createContext<RendererContextValue | null>(null)

export function useRenderer(): RendererContextValue {
  const value = useContext(RendererContext)
  if (!value) throw new Error('Blank renderer components must be rendered inside <BlankForm>.')
  return value
}
