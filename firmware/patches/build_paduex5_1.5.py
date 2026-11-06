#!/usr/bin/env python3
"""
Build paduex5_1.5.bin from paduex5_1.2.bin — same size.

Root cause of paduex5_1.2/1.3/1.4 brick after OTA jump:
  Boot hook at 0x080064B0 was written as BL + 6 NOPs = 16 bytes, which
  overwrote not only the first memcpy(0x24) block but also the next
  `movs r2,#0x24` / `ldr r1,[pc,#…]` of the *second* memcpy. Execution
  then fell into `adr r0; bl 0x080048A4` with garbage r1/r2 → RAM corruption
  → HardFault before USB enum (no plug-in sound).

Fix: shrink boot hook to BL + 4 NOPs = 12 bytes (covers only the first
memcpy), restore stock bytes at 0x080064BC..0x080064BF.

Also restore SendPacket bodies to paduex5_1.2 (drop 1.3/1.4 timeout rewrite)
so this image is “1.2 remappable + boot fix only”.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

BASE = 0x08004000
BOOT_HOOK = 0x080064B0
MAPPING_LOAD = 0x08005DB4


def va_to_off(va: int) -> int:
    return va - BASE


def thumb_bl(frm: int, to: int) -> bytes:
    offset = to - (frm + 4)
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


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = root / "paduex5_1.2.bin"
    stock_path = Path("/tmp/uandancedance/V2.13/5KEY_2P_V2.13.bin")
    dst = root / "paduex5_1.5.bin"
    if not src.exists():
        print("missing paduex5_1.2.bin", file=sys.stderr)
        return 1
    if not stock_path.exists():
        # fall back: paduex5_1.0 has stock boot bytes
        stock_path = root / "paduex5_1.0.bin"
    stock = stock_path.read_bytes()
    buf = bytearray(src.read_bytes())
    assert len(buf) == len(stock) == 37072

    # --- Fix boot hook: 12 bytes only ---
    # Stock first memcpy block is 12 bytes at 64B0..64BB.
    # Restore 64BC..64BF from stock (movs r2,#0x24; ldr r1,[pc,#0x4c]).
    boot = thumb_bl(BOOT_HOOK, MAPPING_LOAD) + b"\x00\xbf" * 4  # 4+8=12
    assert len(boot) == 12
    buf[va_to_off(BOOT_HOOK) : va_to_off(BOOT_HOOK) + 12] = boot
    restored = stock[va_to_off(0x080064BC) : va_to_off(0x080064C0)]
    assert restored == bytes.fromhex("24221349"), restored.hex()
    buf[va_to_off(0x080064BC) : va_to_off(0x080064C0)] = restored
    print("  boot hook: BL Mapping_Load + 4 NOPs (12B); restored 64BC..64BF")

    # --- Ensure SendPacket == paduex5_1.2 (undo any 1.3/1.4 edit if present) ---
    # Building from paduex5_1.2 already has stock wait loops; nothing to do.
    # Sanity: push.w present
    assert buf[va_to_off(0x08005D08) : va_to_off(0x08005D08) + 4] == bytes.fromhex(
        "2de9f041"
    )

    assert len(buf) == 37072
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes)")

    # Verify disasm of boot
    from capstone import CS_ARCH_ARM, CS_MODE_THUMB, Cs

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    print("---- boot ----")
    for insn in md.disasm(bytes(buf[va_to_off(BOOT_HOOK) : va_to_off(BOOT_HOOK) + 28]), BOOT_HOOK):
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
