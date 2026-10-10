# ISUP gateway — operations runbook

Terminals (office, factories) connect **to** Bonvi over ISUP 5.0; one gateway
container on the production server serves all of them. Nothing is installed at
the site, no router port is opened, the terminal is never exposed to the internet.

Design: `docs/SPEC-verifikatsiya-isup.md`. The LAN agent (`AGENT-RUNBOOK.md`) is
the same code; only the transport differs.

> ⚠️ **A terminal works with Verifix OR Bonvi, never both** (one ISUP slot, one
> HTTP push slot — read from the terminals on 2026-10-10). Switching a terminal
> to Bonvi stops Verifix on it.
>
> ⚠️ **Switching back to Verifix needs Verifix's ISUP key**, and the terminal does
> not reveal it (`GET /ISAPI/System/Network/Ehome` has no `<key>`). Only Verifix
> support / the original installer has it. Before switching a production
> terminal, either get that key or accept that the switch is final. Prove the
> gateway on a spare terminal first.

---

## 1. Enable the gateway on the server (once)

1. Native SDK files in `api/native/hikvision-isup/linux-x64/` with `SHA256SUMS`
   (see that folder's `README.md`). The image build fails without them.
2. `.env` on the server: add `isup` to `COMPOSE_PROFILES`
   (e.g. `COMPOSE_PROFILES=isup`). Optional: `ISUP_FACE_URL_BASE`
   (default `https://hr.bonvi.uz/isup-face/`).
3. `.env.isup` next to it, `chmod 600`, never in git — template:
   `.env.isup.example`.
4. Firewall: allow inbound **TCP and UDP 7660** (nothing else is new; faces go
   through Caddy on 443). The port is published by `isup-proxy`; the gateway
   itself has no route to the internet (SPEC G8).
5. Deploy as usual (`DEPLOY-CHECKLIST.md`); the `web` image is rebuilt too
   (Caddyfile gained `/isup-face/*`).
6. Check: `docker compose -f docker-compose.prod.yml logs isup-gateway | head`
   must show `ISUP CMS tinglamoqda: 0.0.0.0:7660` and `isup-proxy` must be
   running. No internet from the gateway, by design:
   `docker compose -f docker-compose.prod.yml exec isup-gateway bash -c 'timeout 3 bash -c "</dev/tcp/1.1.1.1/443" && echo OCHIQ || echo yopiq'`
   must print `yopiq`.

## 2. Add a terminal

1. **Bonvi:** Verifikatsiya → Qurilmalar → *Qurilma qo'shish* (Hikvision,
   Token). Copy the token (shown once). Leave *allowed IPs* **empty** — all
   gateway traffic comes from one internal address.
2. **Key:** 16 random characters (terminal limit 8–16):
   `openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 16`
3. **`.env.isup`** — next free index `N`:
   ```
   Isup__Devices__N__DeviceId=zavod-1        # 1–32: letters, digits, - _
   Isup__Devices__N__Key=<16 chars>
   Isup__Devices__N__Token=<token from step 1>
   ```
   Then: `docker compose -f docker-compose.prod.yml up -d isup-gateway`
   (a bad entry stops the gateway with a clear message — check the logs).
4. **Terminal** (its web page from the site LAN, or the screen menu):
   *Configuration → Network → Device Access → Platform Access (ISUP)*
   — names vary by firmware:
   | Field | Value |
   |---|---|
   | Enable | ✔ |
   | Protocol version | ISUP 5.0 |
   | Address type | Domain name |
   | Server address | `hr.bonvi.uz` |
   | Port | `7660` |
   | Device ID | same as `DeviceId` |
   | Encryption key | same as `Key` |
   Photograph the page **before** changing it (Verifix's values, minus the key).
5. **Check:** gateway log `Terminal ULANDI: zavod-1, seriya …` within a minute;
   on the device card in Bonvi the agent's last contact updates and employees
   start to be written (roster cycle: 60 s).

## 3. Troubleshooting

| Symptom (gateway log) | Cause |
|---|---|
| `Noma'lum terminal rad etildi: ID '…'` | Device ID on the terminal ≠ any `Isup__Devices__*__DeviceId` |
| `Terminal kaliti mos kelmadi` | Encryption key on the terminal ≠ `Key` |
| Nothing at all from the terminal | Terminal cannot reach `hr.bonvi.uz:7660` (site internet, DNS, server firewall) |
| `ULANDI` but agent errors `ISAPI 503` | Terminal dropped right after; watch for `UZILDI` (unstable site link) |
| Agent `ISAPI 400 … downloadPicture…` on faces | Terminal could not fetch `faceURL`. Try `ISUP_FACE_URL_BASE=http://hr.bonvi.uz/isup-face/` (needs an `http://` Caddy block for that path) |
| Agent `davomat.terminal_mos_emas` (409) | Token is bound to another terminal's serial (SPEC-agent §L2) — new token on the card |
| `ISAPI 504` repeating | SDK timeouts — slow site link; raise `Isup__PassthroughTimeoutMs` |

`Isup__SdkLogLevel=3` writes the vendor SDK log to `/data/sdk-logs` in the
`isup-data` volume.

## 4. Remove / roll back a terminal

* Remove its three lines from `.env.isup`, restart the gateway — the terminal is
  then rejected at registration.
* Back to Verifix: on the terminal restore Verifix's ISUP values
  (`hikvision.verifix.com`, `6368`, Verifix device ID) **plus Verifix's key**
  (see the warning at the top), and Verifix's HTTP push host.
