import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { useSession } from '@/app/session'
import { APP_VERSION } from '@/app/version'
import { useMyPhoto } from '@/data/photo'
import { useMe } from '@/data/queries'
import { TabScreen } from '@/ui/app-shell'
import { Avatar } from '@/ui/avatar'
import { Card, CardRow } from '@/ui/card'
import { ErrorState, Skeleton, SkeletonLines } from '@/ui/feedback'
import { ListGroup, ListRow } from '@/ui/list'
import { ConfirmSheet } from '@/ui/sheet'
import { useToast } from '@/ui/toast'

/**
 * PROFIL (UX §A.5).
 *
 * «Telefondan belgilash» holati ATAYLAB shu yerda, sozlamalarda emas:
 * bu ilova tug'diradigan eng ehtimolli savolning javobi («nega
 * tugmam ishlamayapti»), va u ko'rinib turishi kerak.
 */
export function ProfileScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const session = useSession()
  const me = useMe()
  const photoUrl = useMyPhoto(me.data ? me.data.photoKey : null)

  const [signOutOpen, setSignOutOpen] = useState(false)

  return (
    <TabScreen title={t('profile.title')} staleSince={me.fetchedAt}>
      <Card>
        {me.isLoading ? (
          <div className="flex items-center">
            <Skeleton className="h-16 w-16 rounded-pill" />
            <div className="ml-4 flex-1 space-y-2">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-4 w-28" />
            </div>
          </div>
        ) : me.error && !me.data ? (
          <ErrorState onRetry={me.refetch} />
        ) : me.data ? (
          <div className="flex items-center">
            <Avatar photoUrl={photoUrl} fullName={me.data.fullName} size="lg" />
            <div className="ml-4 min-w-0">
              <p className="text-title3 font-semibold text-fg server-text">{me.data.fullName}</p>
              <p className="mt-1 text-footnote text-fg-secondary server-text">
                {me.data.position ?? ''}
                {me.data.position && me.data.department ? ' · ' : ''}
                {me.data.department ?? t('profile.noDepartment')}
              </p>
            </div>
          </div>
        ) : null}
      </Card>

      {/* «Rahbar» (SPEC-jamoam §D) — bo'limlar zanjiri bo'ylab birinchi boshliq;
          topilmasa qator umuman yo'q: «Rahbar: —» xodimga hech narsa demaydi */}
      {me.data && me.data.manager ? (
        <Card>
          <CardRow label={t('profile.manager')} value={me.data.manager.fullName} />
        </Card>
      ) : null}

      <Card>
        {me.isLoading ? (
          <SkeletonLines rows={1} />
        ) : (
          <ListRow
            label={t('profile.mobileCheckIn')}
            secondary={me.data && !me.data.mobileCheckInAllowed ? t('profile.mobileOffText') : undefined}
            value={
              <span className={me.data?.mobileCheckInAllowed ? 'text-hue-success' : 'text-fg-tertiary'}>
                {me.data?.mobileCheckInAllowed ? t('profile.mobileOn') : t('profile.mobileOff')}
              </span>
            }
            chevron={false}
          />
        )}
      </Card>

      <ListGroup>
        <ListRow label={t('profile.settings')} to="/profil/sozlamalar" />
        <ListRow label={t('profile.locationHelp')} to="/joylashuv" />
      </ListGroup>

      <ListGroup>
        <ListRow label={t('profile.signOut')} danger chevron={false} onClick={() => setSignOutOpen(true)} />
      </ListGroup>

      <p className="pt-2 text-center text-caption1 text-fg-tertiary">
        {t('app.version', { version: APP_VERSION })}
      </p>

      <ConfirmSheet
        open={signOutOpen}
        title={t('auth.signOutTitle')}
        text={t('auth.signOutText')}
        confirmLabel={t('common.signOut')}
        danger
        onConfirm={() => {
          setSignOutOpen(false)
          session.signOut()
          toast.show(t('auth.signedOut'), 'info')
          navigate('/kirish', { replace: true })
        }}
        onClose={() => setSignOutOpen(false)}
      />
    </TabScreen>
  )
}
