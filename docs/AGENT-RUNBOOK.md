# Device agent — office runbook

The device agent (`api/src/Bonvi.DeviceAgent`) connects one Hikvision face
terminal to Bonvi. It runs on a machine **inside the office LAN**, next to the
terminal — not on the Bonvi server. It only makes outbound connections: to the
terminal's ISAPI and to the Bonvi API. No port is opened anywhere.

Design and rules: `docs/SPEC-verifikatsiya-agent.md` (§C, §E, §H–§L).

> ⚠️ The agent never changes terminal settings. The terminal's HTTP push
> stays Verifix's (SPEC-agent A1). The agent only reads the event log, reads
> the clock / device info / a live snapshot, and writes Bonvi persons and faces
> (numbers `9` + 8 digits).

---

## 1. Prerequisites

* A small always-on machine on the same network as the terminal (Linux with
  Docker Engine + Compose v2; a mini PC or NUC is enough — the agent uses
  ~60 MB RAM).
* The terminal's LAN address and its **admin** password. ⚠️ Wrong passwords
  lock the terminal account (`retryLoginTime`). Do not guess — copy it from
  the Verifix installer's records.
* A device record in Bonvi: **Verifikatsiya → Qurilmalar → Qurilma qo'shish**.
  The dialog shows the token **once**, together with ready `.env` lines
  (`AGENT__APIBASEURL`, `AGENT__DEVICETOKEN`).
* A checkout of this repository on the office machine (the image is built
  from `api/Dockerfile.agent`; there is no registry image).

## 2. Configure

Create `.env.agent` in the repository root (it is git-ignored; keep it
`chmod 600`):

```
AGENT__APIBASEURL=https://hr.bonvi.uz
AGENT__DEVICETOKEN=<token from the device card>
AGENT__DEVICEHOST=192.168.1.50
AGENT__DEVICEUSER=admin
AGENT__DEVICEPASSWORD=<terminal admin password>

# Optional (defaults shown)
# AGENT__SNAPSHOTCHANNEL=101      # live photo channel; empty = no photo
# AGENT__EVENTSINTERVAL=2         # seconds
# AGENT__ROSTERINTERVAL=60        # seconds
# AGENT__COLDSTARTHOURS=48        # read-back when there is no state file
```

## 3. Start / update / stop

```bash
# first start and every update (after git pull)
docker compose -f docker-compose.agent.yml --env-file .env.agent up -d --build

# logs (rotated: 5 × 10 MB)
docker compose -f docker-compose.agent.yml logs -f --tail=200

# stop (the state volume is kept)
docker compose -f docker-compose.agent.yml stop
```

Never run `down -v`: it deletes the event cursor volume. That is not fatal
(the server drops duplicates), but the agent then re-reads 48 hours of the
terminal log.

## 4. Check that it works

Within two minutes of starting:

1. **Qurilmalar** page: the device row shows «Oxirgi aloqa» a few seconds ago
   and the terminal serial under the name («Terminal: DS-K1T…»). The first
   agent call binds the token to that serial.
2. The **Qurilmaga yozish** card for the device: counters fill in; «Navbatda»
   drops to zero within a few minutes (the first rollout of ~500 people takes
   longer — each person is a photo download plus two terminal writes).
3. Walk through the terminal: the pass appears in **O'tishlar** within ~5
   seconds, with a live snapshot.
4. Stop the agent for a few minutes, make 40–50 passes, start it again: all of
   them must appear in the pass log (SPEC-agent §K).

## 5. Alerts and what to do

| What HR sees | Meaning | Action |
|---|---|---|
| Red «… bilan aloqa uzilgan» — *Agent … dan beri javob bermayapti* (Statistika and Qurilmalar) | The agent has not called Bonvi for > 10 minutes. Passes stay on the terminal and will arrive later; departed employees are **not** being removed. | Is the office machine on? Internet? `docker compose … ps` / `logs`. |
| Red — *Terminal … dan beri javob bermayapti* | The agent is alive but cannot reach the terminal for > 10 minutes. | Terminal powered? IP changed (DHCP)? Cable / Wi-Fi? Update `AGENT__DEVICEHOST` if the IP moved. |
| Red — *Agent ishlayapti, lekin terminalga hali ulana olmagan* | Wrong `AGENT__DEVICEHOST`, or the terminal is unreachable from the agent machine. | Check the address from the agent machine (`curl -m 3 http://<ip>/`). |
| Agent log: `401`/`403` from ISAPI, retries every 10 minutes | Wrong terminal user or password. The agent deliberately waits 10 minutes between attempts so the account is not locked. | Fix `AGENT__DEVICEPASSWORD`, then `up -d`. |
| Agent log: `davomat.terminal_mos_emas`, retries every 10 minutes | The token is bound to a different terminal serial. | If the terminal was replaced: **Qurilmalar → token yangilash**, put the new token in `.env.agent`, `up -d`. The next call binds the new serial. |
| Yellow «N kishini o'chirish to'xtatildi» on the device card | The mass-removal guard held removals: the roster was empty or more than a fifth of the Bonvi people on the terminal would be removed at once. | Usually a data problem — check employments. If it is real (e.g. a branch closed), press the confirm button; it allows removals for 10 minutes. |
| Yellow «Terminalda begona N ta Bonvi raqamli odam bor» | Another Bonvi installation (e.g. a test server) writes to this terminal. | Stop the other agent. Two servers allocate numbers independently and can overwrite each other's people. |
| «Xato» badge on a person, hover shows e.g. *face quality too low* | The terminal rejected that photo. The agent will not retry the same photo. | Upload a better photo (the new photo retries automatically) or press «Hammasini sinxronlash» after fixing the cause. |
| Yellow «… soati adashgan» | Terminal clock is off by > 5 minutes. | Fix the terminal's NTP / time in its web UI (the agent never changes settings). |

## 6. Rules to keep

* **One agent per terminal, one server per terminal.** Never point a dev or
  test agent at the production terminal.
* Do not edit or delete people with `9xxxxxxxx` numbers in the terminal's own
  UI or in Verifix — the agent recreates them within a minute, and deleting
  their mapping in Bonvi is blocked on purpose (the number must never change).
* The agent is stateless apart from the cursor volume; reinstalling the
  machine only needs `.env.agent` and step 3.
