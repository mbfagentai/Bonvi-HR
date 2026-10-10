# SPEC — Verifikatsiya: ISUP gateway (terminal dials Bonvi)

Status: **built and unit-tested; not yet run against a live terminal** (2026-10-10).
Owner decision: Bonvi connects to every face terminal (office and factories) from
**one server**, with **no computer on site**, **no router work** and **no terminal
exposed to the internet**. Verifix is dropped once Bonvi is proven.

Related: `SPEC-verifikatsiya-agent.md` (the LAN agent this reuses), `AGENT-RUNBOOK.md`,
`ISUP-RUNBOOK.md` (operations).

---

## A. Facts this design rests on (read from the office terminals, 2026-10-10)

| Fact | Source |
|---|---|
| Verifix uses two **outbound** channels: HTTPS push → `listen-app.verifix.com:443` (events) and **ISUP 5.0** → `hikvision.verifix.com:6368` (management) | `GET /ISAPI/Event/notification/httpHosts`, `GET /ISAPI/System/Network/Ehome` |
| ISUP centre slots: **one** (`<id min="1" max="1"/>`) | `/ISAPI/System/Network/Ehome/capabilities` |
| HTTP push slots: effectively **one** (`hostNumber=2`, the second is the EHome-type entry; writing a second HTTP host replaced Verifix's on 2026-09-21) | `/ISAPI/Event/notification/httpHosts/capabilities` |
| ISUP key 8–16 chars, device ID 1–32 chars, host name ≤ 64 | `/ISAPI/System/Network/Ehome/capabilities` |
| 435 persons / ~434 faces, capacity 3000 | `UserInfo/Count`, `FDLib/Count`, capabilities |
| Verifix's ISUP key is **not readable** (`GET …/Ehome` has no `<key>`) — switching back to Verifix needs the key from Verifix | `GET /ISAPI/System/Network/Ehome` |

⇒ **A terminal talks to Verifix OR to Bonvi, never both.** There is no parallel
run in ISUP mode. The office terminals stay on Verifix until the owner switches
them; the gateway is proven on a spare terminal first.

## B. Architecture

```
terminal (any network) ──ISUP 5.0, outbound TCP──▶ hr.bonvi.uz:7660 ┐
                                                                    │ isup-gateway (container)
                         ┌──────────────────────────────────────────┘
                         │ CMS listener: per-device key, ISUP ≥ 4.0 only (safe mode)
                         │ device registry: deviceId → login handle
                         │ per device: AgentWorker  (the SAME code as the LAN agent)
                         │     HikvisionClient ─▶ IsupPassthroughHandler ─▶ NET_ECMS_ISAPIPassThrough
                         │ face picture host: one-time URLs
                         ▼
               http://api:8080/api/attendance/agent/*   (internal network, device token)

terminal ──GET faceURL──▶ https://hr.bonvi.uz/isup-face/{token}.jpg  (Caddy → gateway:8091)
```

| # | Decision | Why |
|---|---|---|
| G1 | **Reuse `AgentWorker` unchanged**, one instance per terminal inside the gateway process. Only the transport differs: `HikvisionClient`'s `HttpClient` gets `IsupPassthroughHandler` instead of a digest `HttpClientHandler`. | Reconcile, cursor, mass-delete guard, serial binding (SPEC-agent §E–§L) are already proven; a second copy would drift. One process scales to many factories (one .NET runtime, not one container per terminal). |
| G2 | **Events by polling** `AcsEvent` through the tunnel (existing logic). No alarm channel, no HTTPS push. | The terminal keeps its log; polling after a reconnect loses nothing (SPEC-agent §K). Push is fire-and-forget. Fewer ports and moving parts. The push slot stays free as a later real-time option (the server already parses Hikvision push). |
| G3 | **Faces by URL.** ISAPI over ISUP carries text only (no `Content-Type`, so no multipart). The handler turns the agent's multipart `FaceDataRecord` / `FDSetUp` into the documented JSON form with `faceURL`, and serves the JPEG at a one-time URL the terminal downloads. | Hikvision's own ISUP demo does exactly this (`AddFaceInfoParam.json`). Registration leaves the picture-server type at 0 (Tomcat = plain HTTP GET). |
| G4 | **Security:** per-device ISUP key (16 random chars), unknown device IDs rejected at auth and at online, SDK in safe access mode, keys only in the gateway env (never logged, never in git), the only new public port is 7660. The server still binds each device token to the terminal's serial (SPEC-agent §L2), so a stolen key + device ID on another terminal gets `davomat.terminal_mos_emas`. Face URLs: 128-bit token, 5-minute TTL, deleted as soon as the write call returns. | |
| G5 | **Configuration:** `Isup__Devices__N__DeviceId / Key / Token` in `.env`. Adding a terminal = a device card in Bonvi (token) + three lines + gateway restart + the ISUP form on the terminal. | Simple; a UI can come later. |
| G6 | **Native SDK:** Hikvision HCISUPSDK (CMS module only), Linux x64, vendored in `api/native/hikvision-isup/linux-x64/` with `SHA256SUMS`. Image is `linux/amd64`. | Same bytes found in two independent mirrors (git blob SHAs equal); the official copy is behind Hikvision's partner portal (TPP). Replace with the official package when available. |
| G7 | The gateway has **no database access**; it calls the API's agent endpoints with device tokens over the internal network. | Same trust boundary as the LAN agent. |
| G8 | **No internet egress.** The gateway runs the vendor's closed native code, so it lives only on the `isup` Docker network (`internal: true`, members: gateway, proxy, api, web). Port 7660 (TCP+UDP) is published by `isup-proxy` (socat), which only relays bytes. Plus: read-only root FS, `cap_drop: ALL`, `no-new-privileges`, non-root, pids/memory limits. Its only secrets are terminal keys and device tokens. | Even hidden code in the SDK could not send anything anywhere (§F). |

## C. Status mapping (`IsupPassthroughHandler`)

`AgentWorker` decides retry vs. permanent rejection from HTTP status
(SPEC-agent §L1.5: 4xx = rejected, else transient; 401/403 = auth back-off).
ISUP returns only a body, so the status is derived from it:

| Situation | HTTP | Agent sees |
|---|---|---|
| Device not registered (offline) | 503 | transient, retried |
| SDK call failed / timed out | 504 | transient |
| Body has no `ResponseStatus` (data: `deviceInfo`, `AcsEvent`, `UserInfoSearch`, `time`) | 200 | data |
| `statusCode` 1 (OK) or 7 (reboot required) | 200 | success |
| `statusCode` 2 (busy) | 503 | transient |
| `statusCode` 3 (device error) | 500 | transient |
| `statusCode` 4, 5, 6 (invalid operation / format / content) | 400 | rejected — body kept (`employeeNoAlreadyExist` still detected) |
| Binary endpoint (`/Streaming/.../picture`) | 501 | no snapshot (SnapshotChannel is empty for ISUP devices) |

Never 401/403: ISUP has already authenticated the device.

## D. Registration (CMS callback)

* `ENUM_DEV_AUTH` — write the configured key for this device ID (ASCII + NUL,
  within `dwInLen`); unknown ID → `false`.
* `ENUM_DEV_SESSIONKEY` — `NET_ECMS_SetDeviceSessionKey`.
* `ENUM_DEV_ON` — unknown ID → `false`. Otherwise read the SDK-prefilled
  `NET_EHOME_SERVER_INFO_V50`, set keep-alive, leave alarm / NTP / picture server
  empty (no device setting is pushed), write it back; registry → online.
* `ENUM_DEV_DAS_REQ` — answer with our public address only if configured.
* `ENUM_DEV_OFF` — registry → offline.
* Struct layouts are transcribed from `HCISUPCMS.h` / `HCISUPPublic.h` and their
  sizes are asserted in tests (132, 184, 524, 912, 1488, 96, 272 bytes on x64).

## E. Known limits / open items

1. **Not run against a live terminal yet.** Risky points to verify first:
   registration with key, passthrough of JSON bodies, `faceURL` over HTTPS (fallback:
   plain HTTP on the same path), concurrent passthrough on one device (serialised
   per device in the handler for now).
2. Live snapshot photo is not available over ISUP (binary). The alarm channel can
   carry event pictures — later, if wanted.
3. The API's ingest rate limit is per source IP (`RateLimiting:Ingest`, 300/min).
   All gateway agents share one IP; raise the limit before ~10 terminals.
4. No UI for ISUP devices yet: online/offline is visible through the existing
   agent status on the device card.
5. Behind `isup-proxy` the gateway sees the proxy's address, not the
   terminal's public one; the terminal's own address (from its registration)
   is what the log shows.

## F. SDK safety review and runtime test (2026-10-10)

Owner approved the community-mirror SDK copy (TPP registration pending) on the
condition "no viruses, no data-stealing code". Done before first use:

* **Provenance:** two independent mirrors byte-identical; `libcrypto`,
  `libssl`, `libiconv2` byte-identical to the official HCNetSDK V6.1.9.4 the
  owner downloaded from hikvision.com. SDK reports itself as
  *HCISUPCMS 2.5.1.35, 2024-11-01*.
* **Minimal set:** only `libHCISUPCMS.so` + `libhpr.so` are unverifiable;
  `libz` is Ubuntu's; utility/stream libraries left out (not referenced).
* **Static:** no `system`/`popen`/`exec*`/`fork`/`ptrace`/`setuid`/`mprotect`
  imports; networking and `dlopen` only in `libhpr`; no foreign domains, IPs,
  shell commands or credential-file paths in strings.
* **ClamAV** 1.2.2 with that day's signatures: 0 of 10 files infected.
* **Runtime:** image started in an internal-only network, read-only,
  `cap_drop ALL`, traffic of its network namespace captured with tcpdump for
  ~3 minutes, including fuzzing 7660 with random bytes, an HTTP request and
  1 MB of zeros, and 8091 with bad tokens and a path-traversal attempt.
  Result: **0 DNS queries, 0 outbound connections, 0 outbound UDP**; the
  process never crashed or restarted; 8091 returned 404 for everything but a
  valid token. The SDK's own log showed it loaded only `libcrypto.so.1.1` and
  `libssl.so.1.1` and opened only 7660 (MQTT/TCP + HRUDP/UDP).
* A 3-minute window cannot rule out a delayed beacon — that is what G8 is for.
