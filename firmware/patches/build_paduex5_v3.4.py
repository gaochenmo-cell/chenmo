#!/usr/bin/env python3
"""
Build PADUEX5_V3.4.bin from PADUEX5_V3.2.bin.

Release hold (DTick) is fixed at 0 and can no longer be changed.

  1. Default table 0x0800CF24 (36 x u16)  3 -> 0.
     App_Get_Flash_Cofnig falls back to this table when the DTick page is blank
     or fails its A55A/CRC16 check. It is referenced only from 0x08006514.

  2. After that load, the 22-byte NOP pad at 0x080064DE (the old "Sensi = 200"
     loop, already removed in V3.2) becomes a loop that writes DTick[0..35] = 0.
     A page previously saved by the host is loaded and then discarded, so the
     value in flash 0x08011000 does not survive power-up. The loop is the stock
     one with the immediate changed from 200 to 0 and the base from Sensi
     (Color-0x48) to DTick (Color+0x48):

         movs r4, #0
         b    cmp
       body:
         movs r1, #0
         ldr  r0, [pc, #0x24]     ; 0x20001B16, the colour table
         adds r0, #0x48           ; DTick
         strh.w r1, [r0, r4, lsl #1]
         adds r0, r4, #1
         uxtb r4, r0
       cmp:
         cmp  r4, #0x24
         blt  body

  3. SET_SENS of the DTick table (type low nibble 2) no longer programs flash.
     0x08004BDE..0x08004C04: type 0 and type 1 still call App_Set_Flash_Cofnig;
     type 2 branches to a 36-halfword clear of the table the copy loop just
     wrote (r7 is DTick) and then falls into the existing ACK. GET_SENS reads
     that RAM, so it returns 0. The scan still reloads Tck from DTick, which
     is now always 0 — the same path as a V3.2 board whose DTick table is 0.

  4. 0x0800616A  movs r1,#2 -> movs r1,#4. GET_MODE reports V3.4. HVer stays 0x13.

Scan, threshold compare, LEDs, key remap and the sensitivity / colour flash
pages are not in the diff. 0x14C stays 0x080084A5. Size stays 37072.
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

SRC_NAME = "PADUEX5_V3.2.bin"
DST_NAME = "PADUEX5_V3.4.bin"
HVER = 0x13

SET_SITE = 0x08004BDE
SET_END = 0x08004C04
SET_BYTES = bytes.fromhex(
    "0228"        # cmp r0, #2
    "10d8"        # bhi ack
    "08d0"        # beq zero
    "0138"        # subs r0, #1
    "c002"        # lsls r0, r0, #11
    "494b"        # ldr r3, [pc, #0x124]   ; 0x08010800
    "1844"        # add r0, r3
    "3946"        # mov r1, r7
    "2422"        # movs r2, #0x24
    "00f0fefb"    # bl App_Set_Flash_Cofnig
    "06e0"        # b ack
    "0021"        # movs r1, #0
    "0024"        # movs r4, #0
    "27f81410"    # strh.w r1, [r7, r4, lsl #1]
    "0134"        # adds r4, #1
    "242c"        # cmp r4, #0x24
    "fadb"        # blt loop
)
BOOT_SITE = 0x080064DE
BOOT_END = 0x080064F4
BOOT_BYTES = bytes.fromhex(
    "0024" "06e0" "0021" "0948" "4830" "20f81410" "601c" "c4b2" "242c" "f6db"
)
DEFAULT_TAB = 0x0800CF24
TAB_ENTRIES = 36
FW_MINOR_SITE = 0x0800616A
HVER_SITE = 0x0800615E


def off(va: int) -> int:
    return va - BASE


def build(src_name: str = SRC_NAME, dst_name: str = DST_NAME, hver: int = HVER) -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / src_name).read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE
    assert len(SET_BYTES) == SET_END - SET_SITE
    assert len(BOOT_BYTES) == BOOT_END - BOOT_SITE
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(HVER_SITE)] == hver, "HVer"
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([2, 0x21])
    assert struct.unpack_from(f"<{TAB_ENTRIES}H", buf, off(DEFAULT_TAB)) == (3,) * TAB_ENTRIES
    assert buf[off(BOOT_SITE):off(BOOT_END)] == b"\x00\xbf" * ((BOOT_END - BOOT_SITE) // 2)
    # type 0/1 flash call shape, before this patch rewrites it
    assert buf[off(0x08004BDE):off(0x08004BE2)] == bytes.fromhex("022810d8")

    buf[off(SET_SITE):off(SET_END)] = SET_BYTES
    buf[off(BOOT_SITE):off(BOOT_END)] = BOOT_BYTES
    struct.pack_into(f"<{TAB_ENTRIES}H", buf, off(DEFAULT_TAB), *([0] * TAB_ENTRIES))
    buf[off(FW_MINOR_SITE)] = 4

    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200]
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(HVER_SITE)] == hver
    assert buf[off(BOOT_END):off(BOOT_END) + 2] == bytes.fromhex("10bd"), "pop {r4,pc}"
    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    allowed = set(range(SET_SITE, SET_END))
    allowed.update(range(BOOT_SITE, BOOT_END))
    allowed.update(range(DEFAULT_TAB, DEFAULT_TAB + 2 * TAB_ENTRIES))
    allowed.add(FW_MINOR_SITE)
    stray = [hex(d) for d in diffs if d not in allowed]
    assert not stray, stray

    dst = root / dst_name
    dst.write_bytes(buf)
    print(f"diff vs {src_name}: {len(diffs)} bytes")
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")
    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for blob, addr in ((SET_BYTES, SET_SITE), (BOOT_BYTES, BOOT_SITE)):
        print(f"-- {addr:#010x} --")
        for insn in md.disasm(blob, addr):
            print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


def main() -> int:
    return build()


if __name__ == "__main__":
    sys.exit(main())
