#!/usr/bin/env python3
"""
Build PADUEX4_V3.2.bin (4-key board: same 5KEY_2P PCB with only K1..K4 populated).

Base   : PADUEX5_V3.2.bin
Patches: two bytes, no code moves, no new code.

  1. 0x08006BE2  cmp r4, #0x24  ->  cmp r4, #0x10
     The key press/release detector (0x080069CC, called from the main loop) walks
     every sensor 0..35 (9 keys x 4 ADC channels; the 5-key board uses 0..19) and
     calls SetKeyBit(key = sensor/4, pressed). Capping the loop at sensor 16 means
     keys 4..8 are never evaluated, so bit 4 (K5) can never be set: no keyboard
     usage, no gamepad button, no LED flash, no RAW-stream key bit, in every USB
     mode. Keys 0..3 run the identical code. The two other 36-entry loops in that
     function (new-sample detection and the auto-calibration printout) are untouched.

  2. 0x0800615E  movs r1, #0x13  ->  movs r1, #0x14
     HVer byte of the GET_MODE reply. The constant is only used to build that reply,
     so it is a safe place to tell the host which board variant it is talking to:
     0x13 = 5-key, 0x14 = 4-key. Both images report FW version 3.2.

Everything else (vector table, boot, USB commands, flash code, Report_Normal remap,
SET_SENS persistence, boot fix, SendPacket timeouts) is byte-identical to PADUEX5_V3.2. The build asserts the diff
set, 0x14C == 0x080084A5 and size 37072.
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

SCAN_FN = 0x080069CC            # key scan (push.w {r3,r4,r5,r6,r7,r8,sb,lr})
SCAN_LIMIT_SITE = 0x08006BE2    # cmp r4, #0x24   (sensor loop bound of the press/release pass)
SCAN_LIMIT_OLD = 0x24           # 36 sensors = 9 keys
SCAN_LIMIT_NEW = 0x10           # 16 sensors = keys 0..3 only
SETKEYBIT_FN = 0x08006810
SETKEYBIT_BL_SITES = (0x08006B08, 0x08006BDA)

HVER_SITE = 0x0800615E          # movs r1, #hver (GET_MODE reply)
HVER_5KEY = 0x13
HVER_4KEY = 0x14
FW_MAJOR_SITE = 0x08006164
FW_MINOR_SITE = 0x0800616A


def off(va: int) -> int:
    return va - BASE


def bl_target(buf: bytearray, addr: int) -> int:
    hi, lo = struct.unpack_from("<HH", buf, off(addr))
    s = (hi >> 10) & 1
    j1, j2 = (lo >> 13) & 1, (lo >> 11) & 1
    i1, i2 = (~(j1 ^ s)) & 1, (~(j2 ^ s)) & 1
    imm = (s << 24) | (i1 << 23) | (i2 << 22) | ((hi & 0x3FF) << 12) | ((lo & 0x7FF) << 1)
    if imm & (1 << 24):
        imm -= 1 << 25
    return addr + 4 + imm


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "PADUEX5_V3.2.bin").read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE, len(buf)

    # Pre-checks: this really is PADUEX5_V3.2 and the scan loop looks like we expect
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(SCAN_FN):off(SCAN_FN) + 4] == bytes.fromhex("2de9f843"), "scan fn push.w"
    assert buf[off(SCAN_LIMIT_SITE):off(SCAN_LIMIT_SITE) + 2] == bytes([SCAN_LIMIT_OLD, 0x2C]), "cmp r4,#0x24"
    assert buf[off(SCAN_LIMIT_SITE + 2):off(SCAN_LIMIT_SITE + 2) + 4] == bytes.fromhex("fff651af"), "blt.w loop head"
    assert buf[off(0x08006A86):off(0x08006A86) + 4] == bytes.fromhex("0024abe0"), "movs r4,#0 / b limit"
    for site in SETKEYBIT_BL_SITES:
        assert bl_target(buf, site) == SETKEYBIT_FN, hex(site)
    assert buf[off(HVER_SITE):off(HVER_SITE) + 2] == bytes([HVER_5KEY, 0x21]), "movs r1,#0x13"
    assert buf[off(FW_MAJOR_SITE):off(FW_MAJOR_SITE) + 2] == bytes([3, 0x21]), "movs r1,#3"
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([2, 0x21]), "movs r1,#2"

    buf[off(SCAN_LIMIT_SITE)] = SCAN_LIMIT_NEW
    buf[off(HVER_SITE)] = HVER_4KEY

    # Post-checks
    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200], "vector table / early code must not change"
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    assert diffs == [HVER_SITE, SCAN_LIMIT_SITE], [hex(d) for d in diffs]

    prev = root / "legacy" / "PADUEX4_V3.1.bin"
    if prev.exists():
        pd = [BASE + i for i, (a, b) in enumerate(zip(prev.read_bytes(), buf)) if a != b]
        assert all(0x080064DE <= d < 0x080064F4 or 0x0800CE94 <= d < 0x0800CE94 + 72 or d == FW_MINOR_SITE for d in pd), [hex(d) for d in pd]
        print("diff vs PADUEX4_V3.1: boot fix + default table + FW minor only")

    dst = root / "PADUEX4_V3.2.bin"
    dst.write_bytes(buf)
    print(f"diff vs PADUEX5_V3.2: {len(diffs)} bytes at {', '.join(hex(d) for d in diffs)}")
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for insn in md.disasm(bytes(buf[off(SCAN_LIMIT_SITE):off(SCAN_LIMIT_SITE) + 6]), SCAN_LIMIT_SITE):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    for insn in md.disasm(bytes(buf[off(HVER_SITE):off(FW_MINOR_SITE) + 2]), HVER_SITE):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
