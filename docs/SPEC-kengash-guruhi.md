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
A new binding replaces the old one, and while a code is live the bot does not leave a new group. The bot
confirms in the group. When Telegram turns the group into a supergroup, its new id follows automatically.

## 3. Members
Owner, 2026-11-06: only the group's members are the council, and nobody presses Start. Access is being in the
bound group, checked with Telegram's `getChatMember` (the bot is an admin there, answers cached one minute).
Telegram gives a bot no way to find a person by phone or to list a group, so nothing can be entered beforehand.

`council_members`: full name, Telegram id, Telegram name and username, name-confirmed, active. The first open of the
Mini App by a group member creates the row with the Telegram name. A settings holder (`settings.manage`) writes the
real name or turns the row off. Leaving the group closes access within a minute. If Telegram does not answer, known
members keep working and new ones wait.

The earlier phone flow (`/start kengash`, `POST /members`) still exists and is unused: such a member needs to be in
the group as well.

## 4. Day-before message
One message per council day at 10:00 Tashkent the day before. Its text has the date and the count; each
candidate is one button «HH:mm · Surname Name · Vacancy», plus «All candidates». Over 90 candidates the rest are
reached through «All candidates». Candidates added or removed later edit the same message, without a new
post. A digest for tomorrow, or for today if it was missed, is only first posted 10:00–20:00.

Button target: `t.me/<bot>/<miniApp>?startapp=<id>`, opening over the chat with no Start. `bot.council.miniAppName`
defaults to `kengash`; a changed name edits the posted message. Blanked by hand, buttons fall back to
`t.me/<bot>?start=km_<id>`, where the bot replies in private with a `web_app` button, which needs Start.

## 5. Mini App (`/tg/kengash`, outside `_app`)
Auth is the signed `initData` in `X-Telegram-Init-Data`: HMAC per Telegram, `auth_date` at most 24 h old, Telegram
id must be in the bound group and not turned off. Anything else is 401/403, and every read and write is scoped by that.

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
1. Make the bot an admin of the council group; it needs no rights switched on.
2. Bind the group (§2).
3. BotFather: `/setjoingroups` off. Binding another group later needs it on again, because the bot must be added first.
4. Write the real names as members appear on the roster.
The Mini App checks signatures with the token in the system settings, so the token must be there, not only in `.env`.

## 8. Not in scope
Quorum or vote counting, opinions in HR-F04 on HRMentor, members' attendance filled from the roster.
