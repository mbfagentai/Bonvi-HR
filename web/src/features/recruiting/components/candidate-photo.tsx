import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Avatar, type AvatarSize } from '@/shared/ui'
import { useCandidatePhoto } from '../api/recruiting-queries'

/**
 * Nomzod surati — kartada va (kerak bo'lganda) ro'yxatda bir xil.
 *
 * `Avatar` ustidagi yupqa qatlam: surat baytlarini olib, ko'rsatiladigan
 * havolaga aylantiradi (`EmployeePhoto` bilan AYNAN bir xil naqsh —
 * autentifikatsiya sarlavha orqali yuradi, ya'ni oddiy `<img src>` 401
 * olardi). Surat yo'q, hali kelmagan yoki so'rov yiqilgan bo'lsa —
 * `Avatar` ning o'zi bosh harflarni chizadi.
 *
 * NEGA skelet yo'q: yuklanish paytida ham bosh harflar turaveradi.
 * Kulrang doiraning yuzga almashishi ekranni «qaltiratadi» — ism esa
 * birinchi kadrdanoq o'qiladi.
 */
export function CandidatePhoto({
  candidateId,
  hasPhoto,
  name,
  size,
  className,
}: {
  candidateId: string
  /** `false` — surat yuklanmagan, so'rov ham yuborilmaydi. */
  hasPhoto: boolean
  /** Bosh harflar va `alt` matni shundan olinadi. */
  name: string
  size?: AvatarSize
  className?: string
}) {
  const photo = useCandidatePhoto(candidateId, hasPhoto)
  const src = useObjectUrl(photo.data ?? null)

  return <Avatar name={name} src={src} size={size} className={className} />
}
