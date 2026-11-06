#!/usr/bin/env python3
"""
Build paduex4_1.0.bin = paduex3_1.7.bin + "no fifth key" (for the 4-key pad
that uses the same 5KEY_2P board with only connectors K1..K4 populated).

What changes — exactly TWO bytes, no code moves, no new code:

  1. 0x08006BE2  cmp r4, #0x24  ->  cmp r4, #0x10
     The key press/release detector (0x080069CC, called from the main loop) walks
     every sensor 0..35 (9 keys x 4 ADC channels; the 5-key board uses 0..19) and
     calls SetKeyBit(key = sensor/4, pressed) -> KeyCode |= / &= ~(1 << key).
     Capping the loop at sensor 16 means keys 4..8 are never evaluated, so bit 4
     (K5) can never be set: no keyboard usage, no gamepad button, no LED flash,
     no RAW-stream key bit, in every USB mode. Keys 0..3 run the identical code.
     The two other 36-entry loops in that function (new-sample detection and the
     auto-calibration printout) are untouched.

  2. 0x0800616A  movs r1, #17   ->  movs r1, #18
     GET_MODE minor version: host shows "FVer=2.18 (paduex4_1.0)".
     (paduex3 = 5-key series, paduex4 = 4-key series.)

Everything else (vector table, boot, USB commands, Flash code, Report_Normal remap,
SendPacket timeouts) is byte-identical to paduex3_1.7. No new loops, waits, RAM or
Flash writes. The build asserts the diff set, 0x14C == 0x080084A5 and size 37072.
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

FW_MINOR_SITE = 0x0800616A      # movs r1, #minor (GET_MODE reply)
FW_MINOR_OLD = 17
FW_MINOR_NEW = 18


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
    src_path = root / "paduex3_1.7.bin"
    src = src_path.read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE, len(buf)

    # Pre-checks: this really is paduex3_1.7 and the scan loop looks like we expect
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(SCAN_FN):off(SCAN_FN) + 4] == bytes.fromhex("2de9f843"), "scan fn push.w"
    assert buf[off(SCAN_LIMIT_SITE):off(SCAN_LIMIT_SITE) + 2] == bytes([SCAN_LIMIT_OLD, 0x2C]), "cmp r4,#0x24"
    assert buf[off(SCAN_LIMIT_SITE + 2):off(SCAN_LIMIT_SITE + 2) + 4] == bytes.fromhex("fff651af"), "blt.w loop head"
    assert buf[off(0x08006A86):off(0x08006A86) + 4] == bytes.fromhex("0024abe0"), "movs r4,#0 / b limit"
    for site in SETKEYBIT_BL_SITES:
        assert bl_target(buf, site) == SETKEYBIT_FN, hex(site)
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([FW_MINOR_OLD, 0x21]), "movs r1,#17"

    buf[off(SCAN_LIMIT_SITE)] = SCAN_LIMIT_NEW
    buf[off(FW_MINOR_SITE)] = FW_MINOR_NEW

    # Post-checks
    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200], "vector table / early code must not change"
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    assert diffs == [FW_MINOR_SITE, SCAN_LIMIT_SITE], [hex(d) for d in diffs]

    dst = root / "paduex4_1.0.bin"
    dst.write_bytes(buf)
    print(f"diff vs paduex3_1.7: {len(diffs)} bytes at {', '.join(hex(d) for d in diffs)}")
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for insn in md.disasm(bytes(buf[off(SCAN_LIMIT_SITE):off(SCAN_LIMIT_SITE) + 6]), SCAN_LIMIT_SITE):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    for insn in md.disasm(bytes(buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2]), FW_MINOR_SITE):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
