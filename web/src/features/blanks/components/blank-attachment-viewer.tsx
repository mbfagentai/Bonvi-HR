import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { saveBlobAs } from '@/shared/lib/download'
import { FileViewer, useViewerList, viewerKind, type ViewerKind } from '@/shared/ui'

import { fetchBlankAttachment } from '../api/blanks-api'
import { useBlankAttachmentBlob } from '../api/blanks-queries'
import { formatFileSize } from '../lib/blank-presentation'
import type { BlankAttachmentDto } from '../lib/definition-types'

/** Kind before the bytes arrive (from the name); the response `Content-Type` wins once loaded. */
function kindOf(attachment: BlankAttachmentDto | null, blob: Blob | undefined): ViewerKind {
  if (blob !== undefined && blob.type !== '') return viewerKind(blob.type)
  const name = attachment?.fileName.toLowerCase() ?? ''
  if (name.endsWith('.pdf')) return 'pdf'
  if (/\.(png|jpe?g|webp|gif|heic)$/.test(name)) return 'image'
  return 'other'
}

/**
 * Attachments of one blank in the full-screen `FileViewer` (← / → walks the list).
 * Returns `open` for the renderer's `openFile` and the viewer element to place once on the page.
 */
export function useBlankAttachmentViewer(submissionId: string, attachments: readonly BlankAttachmentDto[]) {
  const viewer = useViewerList(attachments)
  return {
    open: (attachment: BlankAttachmentDto) => viewer.open(attachment.id),
    element: (
      <BlankAttachmentViewer
        submissionId={submissionId}
        attachment={viewer.current}
        onClose={viewer.close}
        position={viewer.position}
        onPrevious={viewer.onPrevious}
        onNext={viewer.onNext}
      />
    ),
  }
}

function BlankAttachmentViewer({
  submissionId,
  attachment,
  onClose,
  position,
  onPrevious,
  onNext,
}: {
  submissionId: string
  /** `null` — closed, nothing is fetched. */
  attachment: BlankAttachmentDto | null
  onClose: () => void
  position: { index: number; total: number } | undefined
  onPrevious: (() => void) | undefined
  onNext: (() => void) | undefined
}) {
  const { locale, dateTime } = useLocale()
  const blob = useBlankAttachmentBlob(submissionId, attachment?.id ?? '', attachment !== null)
  const [downloading, setDownloading] = useState(false)

  const download = () => {
    if (attachment === null) return
    if (blob.data) {
      saveBlobAs(blob.data, attachment.fileName)
      return
    }
    setDownloading(true)
    void fetchBlankAttachment(submissionId, attachment.id)
      .then((data) => saveBlobAs(data, attachment.fileName))
      .catch(() => undefined)
      .finally(() => setDownloading(false))
  }

  return (
    <FileViewer
      open={attachment !== null}
      onClose={onClose}
      title={attachment?.fileName ?? ''}
      subtitle={
        attachment === null
          ? undefined
          : `${formatFileSize(attachment.sizeBytes, locale)} · ${dateTime(attachment.uploadedAt)}`
      }
      kind={kindOf(attachment, blob.data)}
      blob={blob.data ?? null}
      error={blob.isError}
      onDownload={attachment === null ? undefined : download}
      downloading={downloading}
      position={position}
      onPrevious={onPrevious}
      onNext={onNext}
    />
  )
}
