#!/usr/bin/env python3
"""
Build grok1.7.bin = grok1.6.bin (stock V2.13 + SendPacket timeout) + keyboard remap.

Design — zero new USB commands, zero new flash code, zero new RAM:
  The firmware only ever reads AppUser.Color[key*4] for the LED colour; the
  three sibling slots Color[key*4+1..3] are transferred, persisted (page
  0x08010800) and loaded at boot by the stock GET/SET_SENS(type 1) path but
  never used. grok1.7 reads the keyboard usage from Color[key*4+1]:

      value = 0x4B00 | usage   ->  use `usage`            ("K" tag)
      anything else            ->  stock 1P/2P table      (old hosts wrote 0..7 here)

  Only Report_Normal (main.c, the 1P/2P if/else chain) is rewritten. The new
  code + both stock tables fit inside the old 1P chain (0x08005D72..0x08005DCA);
  the old 2P chain becomes unreachable and is left untouched. The vector table,
  boot sequence and every other function are byte-identical to grok1.6/stock.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from capstone import CS_ARCH_ARM, CS_MODE_THUMB, Cs

BASE = 0x08004000
APPUSER = 0x200019D0
COLOR_OFF = 0x146           # AppUser.Color  (== 0x20001B16 - APPUSER)
CONFIGMODE = 0x20000008

LOOKUP = 0x08005D72         # start of "ldr r0,=ConfigMode / and 0xf0 / bne 2P"
JOIN = 0x08005DCA           # and.w r0, r5, #0x80  (shift-bit handling, shared tail)
CHAIN1P_END = 0x08005DCA
LIT_APPUSER = 0x08005E64
LIT_CONFIGMODE = 0x08005E68

TAB_1P = [0x1A, 0x1B, 0x04, 0x07, 0x16, 0x14, 0x08, 0x1D, 0x06, 0x55]  # w x a d s q e z c *
TAB_2P = [0x0B, 0x0E, 0x0D, 0x11, 0x10, 0x14, 0x08, 0x1D, 0x06, 0x55]  # h k j n m q e z c *
MAP_TAG = 0x4B

# GET_MODE reply: stock "V2.13" still hard-codes FW_VERSION_MINOR = 12 (movs r1,#0xc @ 0x0800616A),
# so the host cannot tell stock / grok apart. grok1.7 reports 2.17.
FW_MINOR_SITE = 0x0800616A
FW_MINOR_NEW = 17


def off(va: int) -> int:
    return va - BASE


def ldr_pc(addr: int, rt: int, lit: int) -> bytes:
    pc = (addr + 4) & ~3
    d = lit - pc
    assert 0 <= d <= 1020 and d % 4 == 0, (hex(addr), hex(lit))
    return struct.pack("<H", 0x4800 | (rt << 8) | (d // 4))


def adr(addr: int, rd: int, target: int) -> bytes:
    pc = (addr + 4) & ~3
    d = target - pc
    assert 0 <= d <= 1020 and d % 4 == 0, (hex(addr), hex(target))
    return struct.pack("<H", 0xA000 | (rd << 8) | (d // 4))


def b_n(addr: int, target: int) -> bytes:
    imm = (target - (addr + 4)) // 2
    assert -1024 <= imm <= 1023
    return struct.pack("<H", 0xE000 | (imm & 0x7FF))


def bcond(addr: int, cond: int, target: int) -> bytes:
    imm = (target - (addr + 4)) // 2
    assert -128 <= imm <= 127
    return struct.pack("<H", 0xD000 | (cond << 8) | (imm & 0xFF))


COND_EQ, COND_NE = 0x0, 0x1


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = root / "grok1.6.bin"
    buf = bytearray(src.read_bytes())
    assert len(buf) == 37072

    # Pre-checks: stock Report_Normal shape
    assert struct.unpack_from("<I", buf, off(LIT_APPUSER))[0] == APPUSER
    assert struct.unpack_from("<I", buf, off(LIT_CONFIGMODE))[0] == CONFIGMODE
    assert buf[off(JOIN):off(JOIN) + 4] == bytes.fromhex("05f08000")        # and.w r0, r5, #0x80
    assert buf[off(LOOKUP):off(LOOKUP) + 2] == bytes.fromhex("3d48")        # ldr r0, [pc, #0xf4]
    assert buf[off(0x08005D7E):off(0x08005D7E) + 4] == bytes.fromhex("0cb91a25")  # cbnz r4 / movs r5,#0x1a
    assert struct.unpack_from("<I", buf, 0x14C)[0] == 0x080084A5            # USB vector intact

    code = bytearray()

    def here() -> int:
        return LOOKUP + len(code)

    def emit(b: bytes) -> None:
        code.extend(b)

    emit(ldr_pc(here(), 0, LIT_APPUSER))                 # ldr  r0, =AppUser
    emit(struct.pack("<H", 0x00E1))                      # lsls r1, r4, #3        (Index*8)
    emit(struct.pack("<H", 0x1840))                      # adds r0, r0, r1
    emit(struct.pack("<HH", 0xF8B0, 0x5000 | (COLOR_OFF + 2)))  # ldrh.w r5, [r0, #0x148]  Color[Index*4+1]
    emit(struct.pack("<H", 0x0A28))                      # lsrs r0, r5, #8
    emit(struct.pack("<H", 0x2800 | MAP_TAG))            # cmp  r0, #0x4B
    bne_at = len(code)
    emit(b"\x00\x00")                                    # bne  fallback (patched below)
    emit(struct.pack("<H", 0xB2ED))                      # uxtb r5, r5
    emit(b_n(here(), JOIN))                              # b    join
    fallback = here()
    struct.pack_into("<H", code, bne_at, struct.unpack("<H", bcond(LOOKUP + bne_at, COND_NE, fallback))[0])
    emit(ldr_pc(here(), 0, LIT_CONFIGMODE))              # ldr  r0, =ConfigMode
    emit(struct.pack("<H", 0x7800))                      # ldrb r0, [r0]
    # table must be 4-aligned: compute where it will land first
    # layout after this point: adr(2) lsrs(2) beq(2) adds(2) ldrb(2) b(2) [pad] tab
    tab = (here() + 12 + 3) & ~3
    emit(adr(here(), 1, tab))                            # adr  r1, tab_1p
    emit(struct.pack("<H", 0x0900))                      # lsrs r0, r0, #4        ((ConfigMode & 0xF0) >> 4)
    pick = here() + 4
    emit(bcond(here(), COND_EQ, pick))                   # beq  pick (1P)
    emit(struct.pack("<H", 0x310A))                      # adds r1, #10           (2P table)
    assert here() == pick
    emit(struct.pack("<H", 0x5D0D))                      # ldrb r5, [r1, r4]
    emit(b_n(here(), JOIN))                              # b    join
    while here() < tab:
        emit(b"\x00\xbf")                                # nop
    assert here() == tab
    emit(bytes(TAB_1P) + bytes(TAB_2P))
    while here() < CHAIN1P_END:
        emit(b"\x00\xbf" if CHAIN1P_END - here() >= 2 else b"\x00")
    assert len(code) == CHAIN1P_END - LOOKUP, len(code)

    buf[off(LOOKUP):off(CHAIN1P_END)] = code

    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([0x0C, 0x21])   # movs r1, #12
    buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] = bytes([FW_MINOR_NEW, 0x21])     # movs r1, #17

    # Post-checks
    assert len(buf) == 37072
    assert buf[:0x200] == src.read_bytes()[:0x200]
    base = src.read_bytes()
    diffs = [BASE + i for i, (a, b) in enumerate(zip(base, buf)) if a != b]
    for va in diffs:
        assert LOOKUP <= va < CHAIN1P_END or va == FW_MINOR_SITE, hex(va)
    print(f"diff vs grok1.6: {len(diffs)} bytes, all inside {LOOKUP:#x}..{CHAIN1P_END:#x} + FW minor byte")

    dst = root / "grok1.7.bin"
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes)")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for insn in md.disasm(bytes(buf[off(LOOKUP):off(tab)]), LOOKUP):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    print(f"  {tab:#010x}: tab_1p {bytes(TAB_1P).hex()}  tab_2p {bytes(TAB_2P).hex()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
