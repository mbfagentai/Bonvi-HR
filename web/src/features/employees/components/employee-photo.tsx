import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Avatar, type AvatarSize } from '@/shared/ui'
import { useEmployeePhoto } from '../api/employees-queries'

/**
 * Xodim rasmi — jadvalda, kartochkada va profilda bir xil ko'rinadi.
 *
 * `Avatar` ustidagi yupqa qatlam: rasm baytlarini olib, ko'rsatiladigan
 * havolaga aylantiradi. Rasm yo'q, hali kelmagan yoki so'rov yiqilgan
 * bo'lsa — `Avatar` ning o'zi bosh harflarni chizadi.
 *
 * NEGA skelet yo'q: yuklanish paytida ham bosh harflar turaveradi. 25
 * qatorli ro'yxatda 25 ta kulrang doira paydo bo'lib keyin yuzga
 * almashishi ekranni «qaltiratadi» — ism esa birinchi kadrdanoq o'qiladi.
 */
export function EmployeePhoto({
  employeeId,
  photoKey,
  name,
  size,
  className,
}: {
  employeeId: string
  /** `null` — rasm yuklanmagan, so'rov ham yuborilmaydi. */
  photoKey: string | null
  /** Bosh harflar va `alt` matni shundan olinadi. */
  name: string
  size?: AvatarSize
  className?: string
}) {
  const photo = useEmployeePhoto(employeeId, photoKey)
  const src = useObjectUrl(photo.data ?? null)

  return <Avatar name={name} src={src} size={size} className={className} />
}
