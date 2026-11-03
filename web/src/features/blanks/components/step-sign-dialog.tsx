import { LoaderCircle, PenLine } from 'lucide-react'
import { useState } from 'react'

import { SignaturePad } from '@/features/kiosk/components/signature-pad'
import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Dialog, TextField } from '@/shared/ui'

import { useSignBlankStepInPerson } from '../api/blanks-queries'
import { useActorLabel } from './blank-steps-panel'
import { StepActFields, useStepAct, type StepDialogProps } from './step-approve-dialog'

/** Person role whose signer is typed by hand — nobody to resolve (SPEC §6.1, §6.2). */
const WITNESS = 'witness'

/**
 * In-person signature on the HR device — POST #13 multipart: `payload` (the #12 body +
 * `signerName`) and `signature` (PNG from the kiosk `SignaturePad`). The witness types
 * the name (required); for every other role the server takes the resolved person and the
 * name is an optional clarification.
 */
export function StepSignDialog({ submission, definition, step, onClose, onDone, uploadFile, removeFile, openFile }: StepDialogProps) {
  const { t } = useLocale()
  const sign = useSignBlankStepInPerson()
  const act = useStepAct(submission, definition, step)
  const actorLabel = useActorLabel(definition)

  const [signerName, setSignerName] = useState('')
  const [signature, setSignature] = useState<Blob | null>(null)
  const [tried, setTried] = useState(false)

  const isWitness = step.actorRole === WITNESS
  const nameMissing = isWitness && signerName.trim() === ''

  const run = () => {
    setTried(true)
    const body = act.build()
    if (body === null || nameMissing || signature === null) return
    sign.mutate(
      {
        id: submission.id,
        stepCode: step.stepCode,
        body: { ...body, signerName: signerName.trim() === '' ? null : signerName.trim() },
        signature,
      },
      { onSuccess: onDone, onError: act.fail },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!sign.isPending}
      size="lg"
      title={t('blanks.steps.signTitle', { step: step.title })}
      description={t('blanks.steps.signText', { actor: actorLabel(step.actorRole) })}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={sign.isPending}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={run} disabled={sign.isPending}>
            {sign.isPending ? <LoaderCircle className="animate-spin" /> : <PenLine />}
            {t('blanks.steps.sign')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <StepActFields
          act={act}
          submission={submission}
          definition={definition}
          disabled={sign.isPending}
          uploadFile={uploadFile}
          removeFile={removeFile}
          openFile={openFile}
        />
        <TextField
          label={isWitness ? t('blanks.steps.signerName') : t('blanks.steps.signerNameOptional')}
          value={signerName}
          maxLength={200}
          autoComplete="off"
          disabled={sign.isPending}
          error={tried && nameMissing ? t('blanks.steps.signerNameRequired') : undefined}
          onChange={(event) => setSignerName(event.target.value)}
        />
        <SignaturePad
          hasSignature={signature !== null}
          onChange={setSignature}
          error={tried && signature === null ? t('blanks.steps.signatureRequired') : undefined}
        />
      </div>
    </Dialog>
  )
}
