# Hikvision HCISUPSDK (ISUP 5.0) — native files for the ISUP gateway

`Bonvi.IsupGateway` loads Hikvision's ISUP SDK (CMS module only) from this
folder. `Dockerfile.isup` copies `linux-x64/` into the image and refuses to
build unless every file matches `linux-x64/SHA256SUMS`.

Owner's decision (2026-10-10): use the community-mirror copy until the
official package is available (TPP registration pending).

## Files (Linux x64) — the minimum

| File | Why | Verifiable? |
|---|---|---|
| `libHCISUPCMS.so` | CMS: registration listener + ISAPI pass-through | mirror only |
| `libhpr.so` | direct dependency of `libHCISUPCMS.so` (`RPATH=$ORIGIN`) | mirror only |
| `libcrypto.so.1.1`, `libssl.so.1.1` | OpenSSL paths passed to `NET_ECMS_SetSDKInitCfg` (ISUP 5.0 encryption) | **= official HCNetSDK** |
| `HCAapSDKCom/libiconv2.so` | component folder (`NET_ECMS_SetSDKLocalCfg(COM_PATH)`) | **= official HCNetSDK** |
| `libz.so` | not shipped — a symlink to Ubuntu's `zlib1g`, created in the image | distro package |

Deliberately left out: `libHCNetUtils.so`, `libSystemTransform.so` (not
referenced by `libHCISUPCMS.so`), Alarm, Stream, SS, NPQos, sqlite — the
gateway uses neither the alarm channel nor the picture storage server
(`docs/SPEC-verifikatsiya-isup.md`, G2/G3). If a live terminal shows the SDK
needs one of them, add it here with its hash.

## Safety review (2026-10-10, before first use)

| Check | Result |
|---|---|
| Two mirrors, independent repos | byte-identical (git blob SHAs) |
| Shared libs vs official `EN-HCNetSDKV6.1.9.4_build20220412_linux64.zip` | `libcrypto`, `libssl`, `libiconv2` identical |
| Imported libc functions (`readelf --dyn-syms`) | no `system`/`popen`/`exec*`/`fork`/`ptrace`/`setuid`/`mprotect`; networking and `dlopen` only in `libhpr` (Hikvision's portability layer) |
| Strings | no foreign domains (only the `hikvision.com` XML namespace), no IPs except 0.0.0.0/127.0.0.1, no shell commands, no `/etc/passwd`, `.ssh`, `/proc/self/environ` etc. |
| ClamAV 1.2.2, signatures 2026-10-10 (3.6 M) | 0 infected of 10 files |
| Runtime: started in a network-isolated container, traffic captured | see `docs/SPEC-verifikatsiya-isup.md` §F |
| Containment in production | gateway on an internal Docker network **without internet egress**; read-only filesystem, no capabilities, non-root (`docker-compose.prod.yml`) |

## Source

1. **Official (preferred):** Hikvision Technology Partner Program,
   <https://tpp.hikvision.com> → SDK → *ISUP SDK*, Linux 64-bit. Requires a
   partner account. Copy the files above into `linux-x64/`, then
   `cd linux-x64 && sha256sum <files…> > SHA256SUMS`.
2. **Community mirrors (owner's call):** the same package is re-hosted on
   GitHub (`oldweipro/hik-isup` → `sdk/linux/`, `ejeonghun/hikvision_isup` →
   `linux/`). On 2026-10-10 both mirrors held **byte-identical** files (equal git
   blob SHAs). SHA-256 of that copy:

```
3440f80e3d58642262d93abbdb75fc6a375ca29049cbb76c7bda92afb2300a3f  libHCISUPCMS.so
cf79fc312ae0df8d40d4e1c0831aac807dda4569ab256842a335b3b248df437f  libhpr.so
ff1a99b25d9fab0db3eab688a74d0a8ae4e4a8f01ca3b76bb8d246e4f0e66e68  libHCNetUtils.so
60696c14fee856caad926cc48a3ae8f75e75b941c8b84b6651102244d75d4934  libcrypto.so.1.1
1faff223fcb6a81fe97a2b74ab900c1903e0a23c32f7557ca7d411880299d8a6  libssl.so.1.1
e26c4459b51b2abb7ee7441c3c00e45650a3993ee96c9a0105a8314fbfba7918  libz.so
da5d9957f5a00214ced49874bcee189659208f0b988e465f391264697f8b45c4  HCAapSDKCom/libiconv2.so
a437f2ba4e8a106403133bf016fd1bb4760f7588c644575b0cb08eaa7a5f3f03  HCAapSDKCom/libSystemTransform.so
```

   Cross-check (2026-10-10): `libcrypto.so.1.1`, `libssl.so.1.1` and
   `libiconv2.so` of this copy are byte-identical to the same files in the
   **official** Device Network SDK (`EN-HCNetSDKV6.1.9.4_build20220412_linux64.zip`,
   hikvision.com). `libHCISUPCMS.so` itself is not in that package (it has no
   ISUP module at all), so it cannot be verified this way.

Whatever the source: the gateway runs as a non-root user in its own container,
has no database access, and the only thing it can reach on the API is the
agent endpoints with device tokens (`SPEC-verifikatsiya-isup.md`, G7).
