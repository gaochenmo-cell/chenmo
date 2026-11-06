#!/usr/bin/env python3
"""
Build PADUEX5_V3.2.bin (5-key board).

Base   : legacy/PADUEX5_V3.1.bin
Patches:
  1. Boot: stop overwriting the sensitivity table after it was loaded from flash.
     The config-load routine (0x08006470, called once from main) runs

         App_Get_Flash_Cofnig(0x08010000, Sensi, Default800, 36);   // SENSI page
         App_Get_Flash_Cofnig(0x08010800, Color, DefaultColor, 36);
         App_Get_Flash_Cofnig(0x08011000, DTick, DefaultDTick, 36);
         ...print the three tables...
         for (i = 0; i < 36; i++) Sensi[i] = 200;                  // 0x080064DE..0x080064F4

     The last loop was the stock way of forcing the 200 threshold (the stock firmware
     never wrote the SENSI page, so the loaded value was always the 800 default and
     the loop replaced it). V3.1 made SET_SENS write the page, but this loop still
     replaced the loaded values on every power-up, which is exactly what the user
     sees: save -> re-plug -> every sensor back to 200. The 11 halfwords of the loop
     become NOPs; the function returns through the stock `pop {r4, pc}` that follows.
  2. Default sensitivity table 0x0800CE94 (36 x 800 -> 36 x 200).
     With the loop gone the table App_Get_Flash_Cofnig falls back to when the SENSI
     page is blank or fails its A55A/CRC16 check becomes the effective default. It is
     only referenced from that one call (literal at 0x08006500), so changing the data
     keeps a never-saved board at the same 200 it has always booted with.
  3. GET_MODE reply FW minor 1 -> 2 (0x0800616A): the image reports V3.2.

Vector table, boot sequence, USB commands, flash driver and every other function
are byte-identical to V3.1. The build asserts the diff set, 0x14C == 0x080084A5
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

FN_CONFIG_LOAD = 0x08006470
FILL_START = 0x080064DE         # movs r4, #0 ; b ... (Sensi[i] = 200 loop)
FILL_END = 0x080064F4           # pop {r4, pc}
FILL_STOCK = bytes.fromhex("002406e0c8210948483820f81410601cc4b2242cf6db")

DEFAULT_SENSI_TAB = 0x0800CE94  # 36 x u16
DEFAULT_SENSI_LIT = 0x08006500  # the only literal pointing at the table
SENSI_OLD = 800
SENSI_NEW = 200
TAB_ENTRIES = 36

FW_MAJOR_SITE = 0x08006164      # movs r1, #3
FW_MINOR_SITE = 0x0800616A      # movs r1, #1
FW_MINOR_NEW = 2


def off(va: int) -> int:
    return va - BASE


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "legacy" / "PADUEX5_V3.1.bin").read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE

    # Pre-checks: this is V3.1 and the boot routine has the stock shape
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(FN_CONFIG_LOAD):off(FN_CONFIG_LOAD) + 2] == bytes.fromhex("10b5"), "config load push {r4,lr}"
    assert buf[off(FILL_START):off(FILL_END)] == FILL_STOCK, "Sensi = 200 loop differs from stock"
    assert buf[off(FILL_END):off(FILL_END) + 2] == bytes.fromhex("10bd"), "pop {r4,pc}"
    assert struct.unpack_from("<I", buf, off(DEFAULT_SENSI_LIT))[0] == DEFAULT_SENSI_TAB
    refs = [BASE + i for i in range(0, SIZE - 3, 2) if struct.unpack_from("<I", buf, i)[0] == DEFAULT_SENSI_TAB]
    assert refs == [DEFAULT_SENSI_LIT], [hex(r) for r in refs]
    assert struct.unpack_from(f"<{TAB_ENTRIES}H", buf, off(DEFAULT_SENSI_TAB)) == (SENSI_OLD,) * TAB_ENTRIES
    assert buf[off(FW_MAJOR_SITE):off(FW_MAJOR_SITE) + 2] == bytes([3, 0x21]), "movs r1,#3"
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([1, 0x21]), "movs r1,#1"

    buf[off(FILL_START):off(FILL_END)] = b"\x00\xbf" * ((FILL_END - FILL_START) // 2)
    struct.pack_into(f"<{TAB_ENTRIES}H", buf, off(DEFAULT_SENSI_TAB), *([SENSI_NEW] * TAB_ENTRIES))
    buf[off(FW_MINOR_SITE)] = FW_MINOR_NEW

    # Post-checks
    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200], "vector table / early code must not change"
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    for va in diffs:
        assert (FILL_START <= va < FILL_END or DEFAULT_SENSI_TAB <= va < DEFAULT_SENSI_TAB + 2 * TAB_ENTRIES
                or va == FW_MINOR_SITE), hex(va)
    print(f"diff vs PADUEX5_V3.1: {len(diffs)} bytes (boot loop {FILL_START:#x}..{FILL_END:#x}, "
          f"default table {DEFAULT_SENSI_TAB:#x}, FW minor)")

    dst = root / "PADUEX5_V3.2.bin"
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for insn in md.disasm(bytes(buf[off(FILL_START):off(FILL_END) + 2]), FILL_START):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    for insn in md.disasm(bytes(buf[off(FW_MAJOR_SITE):off(FW_MINOR_SITE) + 2]), FW_MAJOR_SITE):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
