#!/usr/bin/env python3
"""
Build paduex3_1.4.bin from paduex3_1.2.bin — same size.

Fixes vs paduex3_1.3:
  paduex3_1.3 replaced `push.w {r4-r8,lr}` (6 regs = 24 bytes, 8-byte aligned)
  with `push {r4-r7,lr}` (5 regs = 20 bytes). That breaks AAPCS 8-byte SP
  alignment before BL into the USB stack → HardFault → USB gone
  ("plug in, no sound, no response").

  paduex3_1.4 keeps the TX timeout from 1.3 but restores the original 6-register
  push/pop so the stack stays 8-byte aligned.
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
    push.w {r4-r8,lr}     ; 6 regs — keep 8-byte SP align (same as stock/1.2)
    ... connect check ...
    timeout spin ~0x80000 then send_report
    pop.w {r4-r8,pc}
    """
    body = bytearray()
    end = lit

    def here() -> int:
        return fn + len(body)

    def emit(data: bytes) -> None:
        body.extend(data)

    def emit_asm(src: str) -> None:
        emit(assemble(here(), src))

    # Must be 6 regs (24 bytes) for AAPCS 8-byte alignment — do NOT drop r8.
    push = assemble(here(), "push {r4, r5, r6, r7, r8, lr}")
    if push != bytes.fromhex("2de9f041"):
        # fall back to known encoding if keystone differs
        if len(push) != 4:
            push = bytes.fromhex("2de9f041")
    emit(push)

    emit_asm("mov r4, r0")
    emit_asm("mov r5, r1")
    emit(encode_ldr_pc(here(), 6, lit))
    emit_asm("ldr r0, [r6, #4]")
    emit_asm("ldr r7, [r0, #0x24]")
    emit_asm("mov r0, r6")
    emit(_thumb_bl(here(), CONNECT_STATE))
    emit_asm("cmp r0, #3")
    bne_off = len(body)
    emit(b"\x00\x00")
    emit_asm("movs r3, #1")
    emit_asm("lsls r3, r3, #19")
    loop = here()
    if wide_completed:
        emit(struct.pack("<HH", 0xF897, completed_off & 0xFFF))
    else:
        emit_asm(f"ldrb r0, [r7, #{completed_off}]")
    emit_asm("cmp r0, #1")
    beq_off = len(body)
    emit(b"\x00\x00")
    emit_asm("subs r3, #1")
    bne_loop_at = here()
    imm = (loop - (bne_loop_at + 4)) // 2
    emit(struct.pack("<H", 0xD100 | (imm & 0xFF)))

    send = here()
    imm = (send - (fn + beq_off + 4)) // 2
    struct.pack_into("<H", body, beq_off, 0xD000 | (imm & 0xFF))

    emit_asm("movs r0, #0")
    if wide_completed:
        emit(struct.pack("<HH", 0xF887, completed_off & 0xFFF))
    else:
        emit_asm(f"strb r0, [r7, #{completed_off}]")
    emit_asm("mov r2, r5")
    emit_asm("mov r1, r4")
    emit(encode_ldr_pc(here(), 0, lit))
    emit(_thumb_bl(here(), send_report))

    exit_addr = here()
    imm = (exit_addr - (fn + bne_off + 4)) // 2
    struct.pack_into("<H", body, bne_off, 0xD100 | (imm & 0xFF))

    pop = assemble(here(), "pop {r4, r5, r6, r7, r8, pc}")
    if len(pop) != 4:
        pop = bytes.fromhex("bde8f081")
    emit(pop)

    if len(body) > (end - fn):
        raise RuntimeError(
            f"SendPacket @{fn:#x} overflow: {len(body)} > {end - fn}"
        )
    while len(body) < (end - fn):
        if (len(body) % 2) == 0 and len(body) + 2 <= (end - fn):
            body.extend(b"\x00\xbf")
        else:
            body.append(0)
    assert len(body) == (end - fn)
    # Sanity: push/pop are 4 bytes each (wide), total body fills to lit
    assert body[:4] == bytes.fromhex("2de9f041"), body[:4].hex()
    assert body[exit_addr - fn : exit_addr - fn + 4] == bytes.fromhex("bde8f081")
    return bytes(body)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = root / "paduex3_1.2.bin"
    dst = root / "paduex3_1.4.bin"
    if not src.exists():
        print("missing paduex3_1.2.bin", file=sys.stderr)
        return 1

    buf = bytearray(src.read_bytes())
    orig_len = len(buf)
    print(f"base {src.name} ({orig_len} bytes)")

    kbs = build_sendpacket(KBS_FN, KBS_LIT, 0x0C, KBS_SEND_REPORT, False)
    buf[va_to_off(KBS_FN) : va_to_off(KBS_LIT)] = kbs
    assert struct.unpack_from("<I", buf, va_to_off(KBS_LIT))[0] == OTG_DEV
    print(f"  patched KBS SendPacket {KBS_FN:#x} ({len(kbs)} bytes, 8-byte SP align)")

    hid = build_sendpacket(HID_FN, HID_LIT, 0xD8, HID_SEND_REPORT, True)
    buf[va_to_off(HID_FN) : va_to_off(HID_LIT)] = hid
    assert struct.unpack_from("<I", buf, va_to_off(HID_LIT))[0] == OTG_DEV
    print(f"  patched HID SendPacket {HID_FN:#x} ({len(hid)} bytes, 8-byte SP align)")

    assert len(buf) == orig_len
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes) SAME as stock/1.2 — DO NOT use paduex3_1.3")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for name, va, n in [("KBS", KBS_FN, 60), ("HID", HID_FN, 64)]:
        print(f"---- {name} ----")
        off = va_to_off(va)
        for insn in md.disasm(bytes(buf[off : off + n]), va):
            print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
