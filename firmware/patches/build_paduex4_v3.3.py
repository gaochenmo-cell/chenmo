#!/usr/bin/env python3
"""
Build PADUEX4_V3.3.bin from PADUEX4_V3.2.bin.

One change, plus the version byte. Before App_USB_KBS_SendPacket writes the
keyboard IN endpoint it busy-waits a fixed 0..15 us derived from the MCU unique
ID, so two boards (which cannot see each other) arm their reports a few
microseconds apart. Report rate, scan, release, DTICK and LEDs are untouched.

  1. 0x08005E6C  push.w {r4-r8,lr}  ->  b.w stagger
     The branch replaces the first instruction of App_USB_KBS_SendPacket.
     stagger lives in the unreachable stock 2P chain at 0x08005DE8
     (Report_Normal jumps over it at 0x08005DE6) and ends with the original
     push plus b.w back to 0x08005E70, so the rest of SendPacket is byte-identical.

        movw r2, #0xF7E8          ; UID base 0x1FFFF7E8
        movt r2, #0x1FFF
        ldr  r3, [r2]             ; UID0
        ldr  r12, [r2, #4]        ; UID1
        eor  r3, r12
        ldr  r12, [r2, #8]        ; UID2
        eor  r3, r12
        and  r3, r3, #0xF         ; 0..15
        cbz  r3, done
     outer:
        movs r2, #48              ; 48 * 3 cycles = 1 us at 144 MHz
     inner:
        subs r2, #1
        bne  inner
        subs r3, #1
        bne  outer
     done:
        push.w {r4, r5, r6, r7, r8, lr}
        b.w  0x08005E70

     The system clock is 144 MHz (the image's own clock setup stores 144000000
     at 0x20000000). On Cortex-M4, subs+bne-taken is 3 cycles, so 48 iterations
     are 1 us. Flash wait states can stretch that; the spread stays 0..15 steps.

  2. 0x0800616A  movs r1, #2  ->  movs r1, #3
     GET_MODE reports FVer 3.3. HVer stays 0x14.

bInterval, the scan loop, DTICK and the LED driver are not in the diff.
0x14C stays 0x080084A5. Size stays 37072.
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

SEND_ENTRY = 0x08005E6C          # first instruction of App_USB_KBS_SendPacket
SEND_RESUME = 0x08005E70
STAGGER = 0x08005DE8             # first byte of the unreachable 2P chain
SKIP_SITE = 0x08005DE6           # b 0x08005E50, the live instruction that jumps over it
LIVE_AFTER = 0x08005E50          # adds r0, r4, #1  (still executed)

# Assembled at STAGGER. 50 bytes, ends at 0x08005E1A, short of LIVE_AFTER.
STAGGER_BYTES = bytes.fromhex(
    "4ff2e872"  # movw r2, #0xf7e8
    "c1f6ff72"  # movt r2, #0x1fff
    "1368"      # ldr r3, [r2]
    "d2f804c0"  # ldr.w ip, [r2, #4]
    "83ea0c03"  # eor.w r3, r3, ip
    "d2f808c0"  # ldr.w ip, [r2, #8]
    "83ea0c03"  # eor.w r3, r3, ip
    "03f00f03"  # and r3, r3, #0xf
    "23b1"      # cbz r3, done
    "3022"      # movs r2, #48
    "013a"      # subs r2, #1
    "fdd1"      # bne inner
    "013b"      # subs r3, #1
    "fad1"      # bne outer
    "2de9f041"  # push.w {r4, r5, r6, r7, r8, lr}
    "00f02bb8"  # b.w 0x08005E70
)
ENTRY_BYTES = bytes.fromhex("fff7bcbf")  # b.w STAGGER, assembled at SEND_ENTRY

HVER_SITE = 0x0800615E
FW_MAJOR_SITE = 0x08006164
FW_MINOR_SITE = 0x0800616A


def off(va: int) -> int:
    return va - BASE


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "PADUEX4_V3.2.bin").read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE, len(buf)
    assert len(STAGGER_BYTES) == 50
    assert STAGGER + len(STAGGER_BYTES) <= LIVE_AFTER

    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(SEND_ENTRY):off(SEND_ENTRY) + 4] == bytes.fromhex("2de9f041"), "SendPacket push.w"
    assert buf[off(SKIP_SITE):off(SKIP_SITE) + 2] == bytes.fromhex("33e0"), "b over the dead chain"
    assert buf[off(LIVE_AFTER):off(LIVE_AFTER) + 2] == bytes.fromhex("601c"), "live adds"
    assert buf[off(HVER_SITE):off(HVER_SITE) + 2] == bytes([0x14, 0x21]), "movs r1,#0x14"
    assert buf[off(FW_MAJOR_SITE):off(FW_MAJOR_SITE) + 2] == bytes([3, 0x21]), "movs r1,#3"
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([2, 0x21]), "movs r1,#2"

    buf[off(STAGGER):off(STAGGER) + len(STAGGER_BYTES)] = STAGGER_BYTES
    buf[off(SEND_ENTRY):off(SEND_ENTRY) + 4] = ENTRY_BYTES
    buf[off(FW_MINOR_SITE)] = 3

    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200], "vector table must not change"
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(SKIP_SITE):off(SKIP_SITE) + 2] == src[off(SKIP_SITE):off(SKIP_SITE) + 2]
    assert buf[off(LIVE_AFTER):off(LIVE_AFTER) + 2] == src[off(LIVE_AFTER):off(LIVE_AFTER) + 2]
    assert buf[off(SEND_RESUME):off(0x08005EA4)] == src[off(SEND_RESUME):off(0x08005EA4)], "SendPacket body"

    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    allowed = set(range(STAGGER, STAGGER + len(STAGGER_BYTES)))
    allowed.update(range(SEND_ENTRY, SEND_ENTRY + 4))
    allowed.add(FW_MINOR_SITE)
    assert set(diffs) <= allowed, [hex(d) for d in diffs if d not in allowed]
    assert FW_MINOR_SITE in diffs and SEND_ENTRY in diffs

    dst = root / "PADUEX4_V3.3.bin"
    dst.write_bytes(buf)
    print(f"diff vs PADUEX4_V3.2: {len(diffs)} bytes")
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    print("-- stagger --")
    for insn in md.disasm(STAGGER_BYTES, STAGGER):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    print("-- entry --")
    for insn in md.disasm(bytes(buf[off(SEND_ENTRY):off(SEND_ENTRY) + 4]), SEND_ENTRY):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
        assert insn.mnemonic == "b.w" and insn.op_str.lower().endswith("8005de8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
