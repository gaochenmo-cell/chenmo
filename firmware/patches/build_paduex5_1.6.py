#!/usr/bin/env python3
"""
Build paduex5_1.6.bin = OFFICIAL 5KEY_2P_V2.13.bin + ONLY the SendPacket timeout.

Why start from stock instead of paduex5_1.x:
  paduex5_1.0 (and every image derived from it: 1.1–1.5) placed a code cave at
  0x08004130..0x0800415F — which is INSIDE the vector table. Offset 0x14C is
  IRQ67 = OTGFS1_IRQn (USB). The real handler 0x080084A5 was replaced by
  0x0000BD5B, so the first USB interrupt jumps into junk → no enumeration,
  no plug-in sound. That is the brick seen after every "successful" OTA.

What paduex5_1.6 changes (and nothing else):
  * App_USB_KBS_SendPacket / App_USB_HID_SendPacket: bounded wait on
    g_*_tx_completed (~0x80000 spins) instead of infinite loop, keeping the
    original 6-register push.w/pop.w (8-byte SP alignment).

Not included (by design, to keep risk minimal):
  * key remap (GET/SET_MAPP), sensitivity flash write-back, DTick index fix.
    Color / debounce / ~1000 Hz are already in stock V2.13.
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

STOCK = Path("/tmp/uandancedance/V2.13/5KEY_2P_V2.13.bin")


def va_to_off(va: int) -> int:
    return va - BASE


def assemble(addr: int, src: str) -> bytes:
    ks = Ks(KS_ARCH_ARM, KS_MODE_THUMB)
    enc, _ = ks.asm(src, addr)
    if enc is None:
        raise RuntimeError(f"asm failed @ {addr:#x}: {src}")
    return bytes(enc)


def thumb_bl(frm: int, to: int) -> bytes:
    offset = to - (frm + 4)
    S = (offset >> 24) & 1
    I1 = (offset >> 23) & 1
    I2 = (offset >> 22) & 1
    imm10 = (offset >> 12) & 0x3FF
    imm11 = (offset >> 1) & 0x7FF
    J1 = (~(I1 ^ S)) & 1
    J2 = (~(I2 ^ S)) & 1
    return struct.pack("<HH", 0xF000 | (S << 10) | imm10, 0xD000 | (J1 << 13) | (J2 << 11) | imm11)


def ldr_pc(addr: int, rt: int, lit: int) -> bytes:
    pc = (addr + 4) & ~3
    d = lit - pc
    assert 0 <= d <= 1020 and d % 4 == 0, (hex(addr), hex(lit))
    return struct.pack("<H", 0x4800 | (rt << 8) | (d // 4))


def build_sendpacket(fn: int, lit: int, done_off: int, send_report: int, wide: bool) -> bytes:
    body = bytearray()

    def here() -> int:
        return fn + len(body)

    def emit(b: bytes) -> None:
        body.extend(b)

    def asm(s: str) -> None:
        emit(assemble(here(), s))

    emit(bytes.fromhex("2de9f041"))  # push.w {r4-r8, lr}
    asm("mov r4, r0")
    asm("mov r5, r1")
    emit(ldr_pc(here(), 6, lit))
    asm("ldr r0, [r6, #4]")
    asm("ldr r7, [r0, #0x24]")
    asm("mov r0, r6")
    emit(thumb_bl(here(), CONNECT_STATE))
    asm("cmp r0, #3")
    bne_at = len(body)
    emit(b"\x00\x00")
    asm("movs r3, #1")
    asm("lsls r3, r3, #19")
    loop = here()
    if wide:
        emit(struct.pack("<HH", 0xF897, done_off & 0xFFF))  # ldrb.w r0,[r7,#off]
    else:
        asm(f"ldrb r0, [r7, #{done_off}]")
    asm("cmp r0, #1")
    beq_at = len(body)
    emit(b"\x00\x00")
    asm("subs r3, #1")
    imm = (loop - (here() + 4)) // 2
    emit(struct.pack("<H", 0xD100 | (imm & 0xFF)))  # bne loop
    send = here()
    struct.pack_into("<H", body, beq_at, 0xD000 | (((send - (fn + beq_at + 4)) // 2) & 0xFF))
    asm("movs r0, #0")
    if wide:
        emit(struct.pack("<HH", 0xF887, done_off & 0xFFF))  # strb.w
    else:
        asm(f"strb r0, [r7, #{done_off}]")
    asm("mov r2, r5")
    asm("mov r1, r4")
    emit(ldr_pc(here(), 0, lit))
    emit(thumb_bl(here(), send_report))
    exit_ = here()
    struct.pack_into("<H", body, bne_at, 0xD100 | (((exit_ - (fn + bne_at + 4)) // 2) & 0xFF))
    emit(bytes.fromhex("bde8f081"))  # pop.w {r4-r8, pc}

    room = lit - fn
    assert len(body) <= room, (len(body), room)
    while len(body) < room:
        body.extend(b"\x00\xbf" if room - len(body) >= 2 else b"\x00")
    return bytes(body)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    if not STOCK.exists():
        print(f"missing {STOCK}", file=sys.stderr)
        return 1
    buf = bytearray(STOCK.read_bytes())
    assert len(buf) == 37072

    # Pre-checks: stock must have the expected function prologues + literals.
    assert buf[va_to_off(HID_FN) : va_to_off(HID_FN) + 4] == bytes.fromhex("2de9f041")
    assert buf[va_to_off(KBS_FN) : va_to_off(KBS_FN) + 4] == bytes.fromhex("2de9f041")
    assert struct.unpack_from("<I", buf, va_to_off(HID_LIT))[0] == OTG_DEV
    assert struct.unpack_from("<I", buf, va_to_off(KBS_LIT))[0] == OTG_DEV
    # Vector table must be stock (IRQ67 = USB)
    assert struct.unpack_from("<I", buf, 0x14C)[0] == 0x080084A5

    hid = build_sendpacket(HID_FN, HID_LIT, 0xD8, HID_SEND_REPORT, True)
    kbs = build_sendpacket(KBS_FN, KBS_LIT, 0x0C, KBS_SEND_REPORT, False)
    buf[va_to_off(HID_FN) : va_to_off(HID_LIT)] = hid
    buf[va_to_off(KBS_FN) : va_to_off(KBS_LIT)] = kbs

    # Post-checks
    assert len(buf) == 37072
    assert struct.unpack_from("<I", buf, 0x14C)[0] == 0x080084A5
    stock = STOCK.read_bytes()
    diffs = [BASE + i for i, (a, b) in enumerate(zip(stock, buf)) if a != b]
    for va in diffs:
        assert HID_FN <= va < HID_LIT or KBS_FN <= va < KBS_LIT, hex(va)
    print(f"diff vs stock: {len(diffs)} bytes, all inside the two SendPacket bodies")

    dst = root / "paduex5_1.6.bin"
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes)")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for name, va, n in [("HID", HID_FN, 0x3C), ("KBS", KBS_FN, 0x38)]:
        print(f"---- {name} ----")
        for insn in md.disasm(bytes(buf[va_to_off(va) : va_to_off(va) + n]), va):
            print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
