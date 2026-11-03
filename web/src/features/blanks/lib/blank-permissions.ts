/** Permission codes of the «Blankalar» module (SPEC-blankalar §6.3). */
export const BLANKS_VIEW = 'blanks.view'
export const BLANKS_FILL = 'blanks.fill'
export const BLANKS_MANAGE = 'blanks.manage'
export const BLANKS_APPROVE = 'blanks.approve'

/** «HR-F06 № 12»; a draft has no number yet → «HR-F06». */
export function blankNumberLabel(templateCode: string, number: number | null): string {
  return number === null ? templateCode : `${templateCode} № ${number}`
}
