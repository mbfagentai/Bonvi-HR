/*
 * `HrWarning`: what a successful write still wants HR to know. In warn mode the process gates let a hire or a termination
 * through and name what was missing only in that one reply, so a dialog that just closes loses it for good.
 */

export interface HrWarning {
  /** Translated through `useErrorMessage`. */
  code: string
  /** The server's own words, for a code with no translation yet. */
  message: string
}

export function readHrWarnings(raw: unknown): HrWarning[] {
  if (!Array.isArray(raw)) return []

  return raw.flatMap((item: unknown) => {
    const { code, message } = (item ?? {}) as { code?: unknown; message?: unknown }
    if (typeof code !== 'string' || code === '') return []
    return [{ code, message: typeof message === 'string' ? message : code }]
  })
}

/**
 * The reply of a confirmed termination repeats the flags HR has just read and accepted on the 409. Those would stop a
 * finished dialog for nothing, so only what HR has not seen yet is shown.
 */
export function warningsToShow(
  warnings: readonly HrWarning[],
  acknowledged: readonly string[] | null,
): HrWarning[] {
  return warnings.filter((warning) => acknowledged?.includes(warning.code) !== true)
}
