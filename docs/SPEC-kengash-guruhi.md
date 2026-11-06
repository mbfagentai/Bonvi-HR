# SPEC — Council group in Telegram

Owner's decisions, 2026-11-06. The bot sits in the council's Telegram group. Questions and vacancy
work stay in the private chat; the group only gets the day-before list, and each member gives a
personal conclusion on each candidate from a Mini App.

## 1. Group rules
- Only a private chat reaches the questionnaire, menu, `/start` and candidate matching.
- In a group the bot answers nothing except the binding command (§2). It stays silent on the rest.
- Once the council group is bound, the bot leaves any other group, on being added or on its next update there.

## 2. Binding the group
Settings holder opens «Kengash a'zolari» on `/kengash-xulosasi`, presses «Guruhni ulash» and pastes
the shown `/kengash_ulash@<bot> <code>` into the group. The code is single-use and lives 30 minutes.
A new binding replaces the old one. The bot confirms in the group.

## 3. Members
`council_members`: full name, phone, Telegram user id, linked-at, active. Managed by `settings.manage`.
The roster is the only source of access, not group membership.

A member links once: the Mini App (or the group's first-time hint) opens `t.me/<bot>?start=kengash`, the
bot asks for the contact with one button, `contact.user_id` must equal the sender, and the phone must match an
active member. There is no separate link token: if Telegram holds another number, the admin corrects the
phone in the roster. Unlinking is one button.

## 4. Day-before message
One message per council day at 10:00 Tashkent the day before. Its text has the date and the count; each
candidate is one button «Surname Name · HH:mm», plus «All candidates». Over 90 candidates the rest are
reached through «All candidates». Candidates added or removed later edit the same message, without a new
post. A digest for tomorrow, or for today if it was missed, is only first posted 10:00–20:00.

Button target: `t.me/<bot>/<miniApp>?startapp=<id>` when `bot.council.miniAppName` is set, otherwise
`t.me/<bot>?start=km_<id>`, where the bot replies in private with a `web_app` button.

## 5. Mini App (`/tg/kengash`, outside `_app`)
Auth is the signed `initData` in `X-Telegram-Init-Data`: HMAC per Telegram, `auth_date` at most 24 h old, Telegram
id must belong to an active member. Anything else is 401/403, and every read and write is scoped by that.

- List: candidates invited from 14 days ago on, with whether I gave a conclusion.
- Candidate: photo, name, vacancy, invitation time, contact details, questionnaire, HR-F02 PDF, read only.
- A candidate is reachable only with an invitation no older than 60 days.

## 6. Conclusions
`council_opinions`: candidate, member, meeting day (the invitation's day when written), decision (accept,
conditional, reserve, reject), comment, created-at. Rows are never changed.

- No time limit: a member may write any time, any number of times. Each write is a new row.
- On reopening, the member's earlier conclusions stay visible as read-only notes.
- A member sees only their own. HR sees everyone's on `/kengash-xulosasi` and on the printed sheet, one block
  per member, latest on top.
- A conditional decision needs a comment.
- The final decision is the chief's; HR enters it in the existing `council_decisions` form. Opinions never set it.

## 7. Owner's one-time steps after deploy
1. BotFather: `/setjoingroups` off, keep privacy on, `/newapp` for the bot with short name `kengash` and URL
   `https://hr.bonvi.uz/tg/kengash`; enter the short name in the settings on `/kengash-xulosasi`.
2. Add members with their Telegram phone numbers.
3. Bind the group.
Until step 1 the buttons go through the private chat (§4), which needs nothing from BotFather.

## 8. Not in scope
Quorum or vote counting, opinions in HR-F04 on HRMentor, members' attendance filled from the roster.
