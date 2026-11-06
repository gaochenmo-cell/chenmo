#!/usr/bin/env python3
"""
Build PADUEX5_V3.1.bin (5-key board).

Base   : legacy/PADUEX5_V3.0.bin
Patches:
  1. SET_SENS persistence (0x08004BD8..0x08004C04, 44 bytes, in place).
     The stock SET_SENS handler (App_Config_Command_Report) copies the 20 values
     into RAM for every table, but only calls App_Set_Flash_Cofnig for type 1
     (colour, page 0x08010800) and type 2 (debounce, page 0x08011000):

         if (type == 1) App_Set_Flash_Cofnig(0x08010800, Color, 36);
         else if (type == 2) App_Set_Flash_Cofnig(0x08011000, DTick, 36);

     Type 0 (sensitivity) never reaches flash, so the thresholds survive only
     until the next power cycle, after which App_Get_Flash_Cofnig(0x08010000)
     finds no valid page and loads the built-in 800 defaults. The replacement
     uses the table pointer the handler already computed in r7 and derives the
     page address from the type:

         type = cmd[4] & 0x0F;
         if (type <= 2) App_Set_Flash_Cofnig(0x08010800 + (type - 1) * 0x800, r7, 36);

     i.e. 0 -> 0x08010000, 1 -> 0x08010800, 2 -> 0x08011000. Unknown low nibbles
     keep the stock behaviour (RAM only). The literal 0x08010800 is the one the
     stock code already used; no new literal, no new RAM, no code outside the
     original 44 bytes (the tail is NOP padding).
  2. GET_MODE reply FW minor 0 -> 1 (0x0800616A): the image reports V3.1.

Vector table, boot sequence, USB commands, flash driver and every other function
are byte-identical to V3.0. The build asserts the diff set, 0x14C == 0x080084A5
and size 37072.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from capstone import CS_ARCH_ARM, CS_MODE_THUMB, Cs

BASE = 0x08004000
SIZE = 37072
USB_VECTOR_OFF = 0x14C
USB_VECTOR = 0x080084A5

FN_SET_FLASH = 0x080053F0       # App_Set_Flash_Cofnig(addr, src, halfwords)
PATCH_START = 0x08004BD8        # ldrb r0, [r5, #4] (type dispatch before the ACK)
PATCH_END = 0x08004C04          # movs r0, #0x57 (ACK builder, shared tail)
LIT_PAGE_COLOR = 0x08004D10     # literal 0x08010800 used by the stock type-1 branch
PAGE_COLOR = 0x08010800

FW_MAJOR_SITE = 0x08006164      # movs r1, #3
FW_MINOR_SITE = 0x0800616A      # movs r1, #0
FW_MINOR_NEW = 1

STOCK_TAIL = bytes.fromhex(
    "2879" "00f00f00" "0128" "05d1" "2422" "3549" "4a48" "00f002fc" "0ae0"
    "2879" "00f00f00" "0228" "05d1" "2422" "3049" "4831" "4548" "00f0f6fb"
)


def off(va: int) -> int:
    return va - BASE


def ldr_pc(addr: int, rt: int, lit: int) -> bytes:
    pc = (addr + 4) & ~3
    d = lit - pc
    assert 0 <= d <= 1020 and d % 4 == 0, (hex(addr), hex(lit))
    return struct.pack("<H", 0x4800 | (rt << 8) | (d // 4))


def bcond(addr: int, cond: int, target: int) -> bytes:
    imm = (target - (addr + 4)) // 2
    assert -128 <= imm <= 127
    return struct.pack("<H", 0xD000 | (cond << 8) | (imm & 0xFF))


def bl(addr: int, target: int) -> bytes:
    d = target - (addr + 4)
    assert -(1 << 24) <= d < (1 << 24) and d % 2 == 0
    s = 1 if d < 0 else 0
    imm = d & 0x1FFFFFF
    i1, i2 = (imm >> 23) & 1, (imm >> 22) & 1
    j1, j2 = (~i1 ^ s) & 1, (~i2 ^ s) & 1
    hi = 0xF000 | (s << 10) | ((imm >> 12) & 0x3FF)
    lo = 0xD000 | (j1 << 13) | (j2 << 11) | ((imm >> 1) & 0x7FF)
    return struct.pack("<HH", hi, lo)


COND_HI = 0x8


def build_persist() -> bytes:
    code = bytearray()

    def here() -> int:
        return PATCH_START + len(code)

    def emit(b: bytes) -> None:
        code.extend(b)

    emit(struct.pack("<H", 0x7928))                      # ldrb r0, [r5, #4]       type byte
    emit(struct.pack("<HH", 0xF000, 0x000F))             # and.w r0, r0, #0xf
    emit(struct.pack("<H", 0x2802))                      # cmp  r0, #2
    emit(bcond(here(), COND_HI, PATCH_END))              # bhi  ack                (unknown type: RAM only)
    emit(struct.pack("<H", 0x3801))                      # subs r0, #1
    emit(struct.pack("<H", 0x02C0))                      # lsls r0, r0, #11        (type-1) * 0x800
    emit(ldr_pc(here(), 3, LIT_PAGE_COLOR))              # ldr  r3, =0x08010800
    emit(struct.pack("<H", 0x4418))                      # add  r0, r3             page address
    emit(struct.pack("<H", 0x4639))                      # mov  r1, r7             table already chosen by type
    emit(struct.pack("<H", 0x2224))                      # movs r2, #36
    emit(bl(here(), FN_SET_FLASH))                       # bl   App_Set_Flash_Cofnig
    while here() < PATCH_END:
        emit(b"\x00\xbf")                                # nop
    assert len(code) == PATCH_END - PATCH_START, len(code)
    return bytes(code)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "legacy" / "PADUEX5_V3.0.bin").read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE

    # Pre-checks: this is V3.0 and the handler tail has the stock shape
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(PATCH_START):off(PATCH_END)] == STOCK_TAIL, "SET_SENS tail differs from stock"
    assert buf[off(PATCH_END):off(PATCH_END) + 2] == bytes([0x57, 0x20]), "movs r0,#0x57 (ACK)"
    assert struct.unpack_from("<I", buf, off(LIT_PAGE_COLOR))[0] == PAGE_COLOR
    assert buf[off(FN_SET_FLASH):off(FN_SET_FLASH) + 4] == bytes.fromhex("2de9f043"), "Set_Flash push.w"
    assert buf[off(FW_MAJOR_SITE):off(FW_MAJOR_SITE) + 2] == bytes([3, 0x21]), "movs r1,#3"
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([0, 0x21]), "movs r1,#0"

    buf[off(PATCH_START):off(PATCH_END)] = build_persist()
    buf[off(FW_MINOR_SITE)] = FW_MINOR_NEW

    # Post-checks
    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200], "vector table / early code must not change"
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    for va in diffs:
        assert PATCH_START <= va < PATCH_END or va == FW_MINOR_SITE, hex(va)
    print(f"diff vs PADUEX5_V3.0: {len(diffs)} bytes, all inside {PATCH_START:#x}..{PATCH_END:#x} + FW minor")

    dst = root / "PADUEX5_V3.1.bin"
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for insn in md.disasm(bytes(buf[off(PATCH_START):off(PATCH_END)]), PATCH_START):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    for insn in md.disasm(bytes(buf[off(FW_MAJOR_SITE):off(FW_MINOR_SITE) + 2]), FW_MAJOR_SITE):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
