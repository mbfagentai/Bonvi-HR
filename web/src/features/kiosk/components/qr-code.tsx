import qrcode from 'qrcode-generator'
import { useMemo } from 'react'

/** A QR code as one SVG path, so it stays sharp at any size and needs no canvas. */
export function QrCode({ value, className }: { value: string; className?: string }) {
  const { size, path } = useMemo(() => {
    const code = qrcode(0, 'M')
    code.addData(value)
    code.make()

    const cells = code.getModuleCount()
    let d = ''
    for (let row = 0; row < cells; row += 1) {
      for (let column = 0; column < cells; column += 1) {
        if (code.isDark(row, column)) d += `M${column + QUIET} ${row + QUIET}h1v1h-1z`
      }
    }
    return { size: cells + QUIET * 2, path: d }
  }, [value])

  return (
    <svg viewBox={`0 0 ${size} ${size}`} role="img" aria-label="QR" className={className}>
      <rect width={size} height={size} fill="#fff" rx={1} />
      <path d={path} fill="#000" />
    </svg>
  )
}

/** The blank margin a scanner needs, in cells. */
const QUIET = 2
