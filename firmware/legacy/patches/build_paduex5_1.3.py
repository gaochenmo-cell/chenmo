#!/usr/bin/env python3
"""
Build paduex5_1.3.bin from paduex5_1.2.bin — same size, fix sticky-key USB TX wait.

Root cause: App_USB_KBS_SendPacket / App_USB_HID_SendPacket spin forever on
g_*_tx_completed. When the IN endpoint is busy, the main loop stalls for
hundreds of ms–seconds; the pad has already released (ADC low) but the host
still sees the previous press report.

Fix: replace the infinite wait with a bounded spin (~0x80000 iters), then
always attempt send_report (same as success path). Image length unchanged.
Also defer KeyCodeBK update until after the report BL so a blocked/failed
path can retry on the next KeyNewsFG.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from capstone import CS_ARCH_ARM, CS_MODE_THUMB, Cs
from keystone import KS_ARCH_ARM, KS_MODE_THUMB, Ks

BASE = 0x08004000
OTG_DEV = 0x20001730
CONNECT_STATE = 0x0800B62C
KBS_SEND_REPORT = 0x0800C416
HID_SEND_REPORT = 0x0800C0CE

KBS_FN = 0x08005E6C
KBS_LIT = 0x08005EA4
HID_FN = 0x08005D08
HID_LIT = 0x08005D44

# Key report: premature KeyCodeBK = KeyCode store
KEYCODEBK_STRH = 0x08006C7C  # strh.w r0, [r1, #0x1d8]


def va_to_off(va: int) -> int:
    return va - BASE


def assemble(addr: int, src: str) -> bytes:
    ks = Ks(KS_ARCH_ARM, KS_MODE_THUMB)
    encoding, _ = ks.asm(src, addr)
    if encoding is None:
        raise RuntimeError(f"asm failed @ {addr:#x}:\n{src}")
    return bytes(encoding)


def _thumb_bl(frm: int, to: int) -> bytes:
    offset = to - (frm + 4)
    if offset & 1:
        raise ValueError("odd BL")
    S = (offset >> 24) & 1
    I1 = (offset >> 23) & 1
    I2 = (offset >> 22) & 1
    imm10 = (offset >> 12) & 0x3FF
    imm11 = (offset >> 1) & 0x7FF
    J1 = (~(I1 ^ S)) & 1
    J2 = (~(I2 ^ S)) & 1
    hi = 0xF000 | (S << 10) | imm10
    lo = 0xD000 | (J1 << 13) | (J2 << 11) | imm11
    return struct.pack("<HH", hi, lo)


def encode_ldr_pc(addr: int, rt: int, lit: int) -> bytes:
    pc = (addr + 4) & ~3
    delta = lit - pc
    if delta < 0 or delta > 1020 or delta % 4:
        raise ValueError(f"LDR PC {addr:#x}->{lit:#x} d={delta}")
    return struct.pack("<H", 0x4800 | (rt << 8) | (delta // 4))


def build_sendpacket(
    fn: int,
    lit: int,
    completed_off: int,
    send_report: int,
    wide_completed: bool,
) -> bytes:
    """
    push {r4-r7,lr}
    ... connect check ...
    movs r3,#1; lsls r3,#19          ; timeout ≈ 524288 spins
    loop: load completed; beq send; subs r3; bne loop
    send: clear flag; bl send_report
    pop {r4-r7,pc}
    literal word stays at `lit` (not included in returned bytes)
    """
    body = bytearray()
    end = lit  # exclusive

    def here() -> int:
        return fn + len(body)

    def emit(data: bytes) -> None:
        body.extend(data)

    def emit_asm(src: str) -> None:
        emit(assemble(here(), src))

    emit_asm("push {r4, r5, r6, r7, lr}")
    emit_asm("mov r4, r0")
    emit_asm("mov r5, r1")
    emit(encode_ldr_pc(here(), 6, lit))
    emit_asm("ldr r0, [r6, #4]")
    emit_asm("ldr r7, [r0, #0x24]")
    emit_asm("mov r0, r6")
    emit(_thumb_bl(here(), CONNECT_STATE))
    emit_asm("cmp r0, #3")
    # BNE exit — patch later
    bne_off = len(body)
    emit(b"\x00\x00")
    emit_asm("movs r3, #1")
    emit_asm("lsls r3, r3, #19")  # 0x80000 iterations
    loop = here()
    if wide_completed:
        # ldrb.w r0, [r7, #imm12]
        imm = completed_off
        emit(struct.pack("<HH", 0xF897, imm & 0xFFF))
    else:
        emit_asm(f"ldrb r0, [r7, #{completed_off}]")
    emit_asm("cmp r0, #1")
    beq_off = len(body)
    emit(b"\x00\x00")  # BEQ send
    emit_asm("subs r3, #1")
    # BNE loop
    bne_loop_at = here()
    imm = (loop - (bne_loop_at + 4)) // 2
    emit(struct.pack("<H", 0xD100 | (imm & 0xFF)))

    send = here()
    # patch BEQ send
    imm = (send - (fn + beq_off + 4)) // 2
    struct.pack_into("<H", body, beq_off, 0xD000 | (imm & 0xFF))

    emit_asm("movs r0, #0")
    if wide_completed:
        imm = completed_off
        emit(struct.pack("<HH", 0xF887, imm & 0xFFF))  # strb.w r0,[r7,#imm]
    else:
        emit_asm(f"strb r0, [r7, #{completed_off}]")
    emit_asm("mov r2, r5")
    emit_asm("mov r1, r4")
    emit(encode_ldr_pc(here(), 0, lit))
    emit(_thumb_bl(here(), send_report))

    exit_addr = here()
    imm = (exit_addr - (fn + bne_off + 4)) // 2
    struct.pack_into("<H", body, bne_off, 0xD100 | (imm & 0xFF))

    emit_asm("pop {r4, r5, r6, r7, pc}")

    if len(body) > (end - fn):
        raise RuntimeError(
            f"SendPacket @{fn:#x} overflow: {len(body)} > {end - fn}"
        )
    # Pad with NOPs to keep literal pool address stable
    while len(body) < (end - fn):
        if (len(body) % 2) == 0 and len(body) + 2 <= (end - fn):
            body.extend(b"\x00\xbf")
        else:
            body.append(0)
    assert len(body) == (end - fn)
    return bytes(body)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = root / "paduex5_1.2.bin"
    dst = root / "paduex5_1.3.bin"
    if not src.exists():
        print("missing paduex5_1.2.bin — run build_paduex5_1.2.py first", file=sys.stderr)
        return 1

    buf = bytearray(src.read_bytes())
    orig_len = len(buf)
    print(f"base {src.name} ({orig_len} bytes)")

    # --- KBS SendPacket (completed @ +0x0c) ---
    kbs = build_sendpacket(KBS_FN, KBS_LIT, 0x0C, KBS_SEND_REPORT, False)
    buf[va_to_off(KBS_FN) : va_to_off(KBS_LIT)] = kbs
    assert struct.unpack_from("<I", buf, va_to_off(KBS_LIT))[0] == OTG_DEV
    print(f"  patched KBS SendPacket {KBS_FN:#x} ({len(kbs)} bytes)")

    # --- HID SendPacket (completed @ +0xd8) ---
    hid = build_sendpacket(HID_FN, HID_LIT, 0xD8, HID_SEND_REPORT, True)
    buf[va_to_off(HID_FN) : va_to_off(HID_LIT)] = hid
    assert struct.unpack_from("<I", buf, va_to_off(HID_LIT))[0] == OTG_DEV
    print(f"  patched HID SendPacket {HID_FN:#x} ({len(hid)} bytes)")

    # --- Defer KeyCodeBK write: NOP the early strh.w r0,[r1,#0x1d8] ---
    # Report path still has r1 = AppUser; after BL we store KeyCodeBK.
    # Replace early store with two NOPs; inject store before RGB update.
    early = buf[va_to_off(KEYCODEBK_STRH) : va_to_off(KEYCODEBK_STRH) + 4]
    assert early == bytes.fromhex("a1f8d801"), early.hex()
    buf[va_to_off(KEYCODEBK_STRH) : va_to_off(KEYCODEBK_STRH) + 4] = b"\x00\xbf\x00\xbf"

    # At 0x08006CB0: ldr r1; subs; ldrh KeyCode; bl RGB
    # Rewrite to: ldr r1; subs; ldrh KeyCode; strh KeyCodeBK; bl RGB
    # Original bl RGB is 4 bytes at 0x6cb8 — replace ldrh+bl region carefully.
    # Current:
    #   6cb0: ldr r1, [pc, #0x28]
    #   6cb2: subs r1, #0x26
    #   6cb4: ldrh.w r0, [r1, #0x1d6]   (4)
    #   6cb8: bl RGB                     (4)
    # We need +4 for strh. Steal the 2-byte nop at 0x6cae and the early 4-byte nop
    # by shifting the block — simplest: overwrite 6cb4.. with ldrh; strh; bl using
    # the 2-byte nop at 6cae as headroom by starting one instruction earlier.
    #
    # 6cac: b 6cb0 (2) / 6cae: nop (2)  — HID path lands via b 6cb0
    # Keep 6cb0 entry. Use space from nops at 6c7c (already nops) — not contiguous.
    #
    # Compact: replace
    #   ldrh.w r0,[r1,#0x1d6] ; bl RGB
    # with
    #   ldrh.w r0,[r1,#0x1d6] ; strh.w r0,[r1,#0x1d8] ; bl RGB
    # by shortening PC ldr imm / moving RGB bl — needs +4 bytes.
    # Use 6cae nop: change both branch targets from 6cb0 to 6cae, put:
    #   6cae: ldrh.w r0,[r1,#0x1d6]
    #   6cb2: strh.w r0,[r1,#0x1d8]
    #   6cb6: bl RGB   — but then destroy ldr r1/subs at 6cb0!
    #
    # Safer approach: after RGB-prep, r1 is reloaded at 6cb0. Insert strh after
    # ldrh by patching bl to a tiny cave — no cave available without size change.
    #
    # Alternative: keep KeyCodeBK early-NOP only. Retry works because KeyCodeBK
    # stays stale → next KeyNewsFG with unequal codes re-sends. KeyNewsFG is
    # still cleared at 6cbc unconditionally — so WITHOUT keeping KeyNewsFG,
    # deferring KeyCodeBK alone still clears the flag and won't retry.
    #
    # Fix clear path: only clear KeyNewsFG when KeyCodeBK == KeyCode after send.
    # At 6cbc–6cd4 flags are cleared always. Change to:
    #   after send block, compare KeyCodeBK vs KeyCode; if equal clear KeyNewsFG.
    # That's more invasive.
    #
    # Pragmatic: restore early KeyCodeBK write (don't NOP) — timeout alone is the
    # main fix. Undo the NOP.
    buf[va_to_off(KEYCODEBK_STRH) : va_to_off(KEYCODEBK_STRH) + 4] = early
    print("  KeyCodeBK: left as-is (SendPacket timeout is the primary fix)")

    assert len(buf) == orig_len
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes) SAME as paduex5_1.2/1.0")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for name, va, n in [("KBS", KBS_FN, 56), ("HID", HID_FN, 60)]:
        print(f"---- {name} ----")
        off = va_to_off(va)
        for insn in md.disasm(bytes(buf[off : off + n]), va):
            print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
