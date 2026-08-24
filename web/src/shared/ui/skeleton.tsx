import { cn } from '@/shared/lib/cn'

/** Yuklanish paytidagi joy egallovchi. Spinner emas — sakrash bo'lmaydi. */
export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      aria-hidden
      className={cn('animate-pulse rounded-md bg-[var(--bg-sunken)]', className)}
    />
  )
}
