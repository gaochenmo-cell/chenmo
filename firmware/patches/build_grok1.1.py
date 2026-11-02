#!/usr/bin/env python3
"""
Build grok1.1.bin from grok1.0.bin (5KEY PADUEX5).

On-device keyboard remapping:
  - Report_Normal loads HID usage from Mapping[12] (uint16) @ 0x20001BB0
  - GET_MAPP (0x4E) / SET_MAPP (0x4F)
  - Persist @ Flash 0x08011800 via App_Get/Set_Flash_Cofnig
  - Boot loads Mapping (default = stock 1P table W X A D S Q E Z C *)

Handlers live in an appended code cave (OTA allows up to config page).
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from capstone import CS_ARCH_ARM, CS_MODE_THUMB, Cs
from keystone import KS_ARCH_ARM, KS_MODE_THUMB, Ks

BASE = 0x08004000
MAP_RAM = 0x20001BB0
MAP_FLASH = 0x08011800
APP_GET_FLASH = 0x08004D38
APP_SET_FLASH = 0x080053F0
# UPDATE_CRC unused — host CRC checked loosely; skip to save space / complexity

REPORT_LOOKUP = 0x08005D72
REPORT_JOIN = 0x08005DCA
REPORT_DEAD = 0x08005DE8
REPORT_DEAD_END = 0x08005E50
SWITCH_HIGH = 0x08006024
SWITCH_GET_SENS = 0x080060FC
SWITCH_SET_SENS = 0x080061DC
SWITCH_DEFAULT = 0x080061EE
SWITCH_EXIT = 0x080061F0
BOOT_HOOK = 0x080064B0

DEFAULT_MAP = [0x1A, 0x1B, 0x04, 0x07, 0x16, 0x14, 0x08, 0x1D, 0x06, 0x55, 0x00, 0x00]


def va_to_off(va: int) -> int:
    return va - BASE


def assemble(addr: int, src: str) -> bytes:
    ks = Ks(KS_ARCH_ARM, KS_MODE_THUMB)
    encoding, _ = ks.asm(src, addr)
    if encoding is None:
        raise RuntimeError(f"asm failed @ {addr:#x}:\n{src}")
    return bytes(encoding)


def _thumb_long_branch(from_addr: int, to_addr: int, link: bool) -> bytes:
    """Encode Thumb B.W (link=False) or BL (link=True)."""
    offset = to_addr - (from_addr + 4)
    if offset & 1:
        raise ValueError(f"odd branch offset {from_addr:#x}->{to_addr:#x}")
    if not (-0x1000000 <= offset < 0x1000000):
        raise ValueError(f"branch out of range {from_addr:#x}->{to_addr:#x}")
    S = (offset >> 24) & 1
    I1 = (offset >> 23) & 1
    I2 = (offset >> 22) & 1
    imm10 = (offset >> 12) & 0x3FF
    imm11 = (offset >> 1) & 0x7FF
    J1 = (~(I1 ^ S)) & 1
    J2 = (~(I2 ^ S)) & 1
    hi = 0xF000 | (S << 10) | imm10
    lo_base = 0xD000 if link else 0x9000
    lo = lo_base | (J1 << 13) | (J2 << 11) | imm11
    return struct.pack("<HH", hi, lo)


def asm_bl(from_addr: int, to_addr: int) -> bytes:
    return _thumb_long_branch(from_addr, to_addr, link=True)


def asm_bw(from_addr: int, to_addr: int) -> bytes:
    return _thumb_long_branch(from_addr, to_addr, link=False)


def asm_b_n(from_addr: int, to_addr: int) -> bytes:
    imm = (to_addr - (from_addr + 4)) // 2
    if not (-1024 <= imm <= 1023):
        raise ValueError(f"B.N range {from_addr:#x}->{to_addr:#x}")
    return struct.pack("<H", 0xE000 | (imm & 0x7FF))


def mov_imm32(addr: int, rt: int, val: int) -> bytes:
    lo = val & 0xFFFF
    hi = (val >> 16) & 0xFFFF
    return assemble(addr, f"movw r{rt}, #0x{lo:x}\nmovt r{rt}, #0x{hi:x}")


def encode_ldr_pc(addr: int, rt: int, lit: int) -> bytes:
    pc = (addr + 4) & ~3
    delta = lit - pc
    if delta < 0 or delta > 1020 or delta % 4:
        raise ValueError(f"LDR PC {addr:#x}->{lit:#x} delta={delta}")
    return struct.pack("<H", 0x4800 | (rt << 8) | (delta // 4))


def fill_nop(buf: bytearray, start: int, end: int) -> None:
    off = va_to_off(start)
    n = end - start
    buf[off : off + n] = b"\x00\xbf" * (n // 2)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src_path = root / "grok1.0.bin"
    dst_path = root / "grok1.1.bin"
    buf = bytearray(src_path.read_bytes())
    print(f"base {src_path.name} ({len(buf)} bytes)")

    # Align cave start to 4 bytes after current image
    cave_off = (len(buf) + 3) & ~3
    if cave_off > len(buf):
        buf.extend(b"\x00" * (cave_off - len(buf)))
    CAVE = BASE + cave_off

    # ---- Build cave contents: defaults + handlers ----
    cave = bytearray()

    def cave_va() -> int:
        return CAVE + len(cave)

    # Default map uint16[12]
    DEFAULT_VA = cave_va()
    cave += b"".join(struct.pack("<H", v) for v in DEFAULT_MAP)
    print(f"  defaults @ {DEFAULT_VA:#010x}")

    # Mapping_Load
    while len(cave) % 2:
        cave += b"\x00"
    LOAD = cave_va()
    chunk = assemble(LOAD, "push {r4, lr}")
    cave += chunk
    a = cave_va()
    cave += assemble(a, "movs r3, #12")
    a = cave_va()
    cave += mov_imm32(a, 2, DEFAULT_VA)
    a = cave_va()
    cave += mov_imm32(a, 1, MAP_RAM)
    a = cave_va()
    cave += mov_imm32(a, 0, MAP_FLASH)
    a = cave_va()
    cave += asm_bl(a, APP_GET_FLASH)
    a = cave_va()
    cave += assemble(a, "pop {r4, pc}")
    print(f"  Mapping_Load @ {LOAD:#010x} ({cave_va() - LOAD} bytes)")

    # Mapping_GET — r5 holds usb class pdata; resp @ r5+0x11e
    while len(cave) % 2:
        cave += b"\x00"
    GET = cave_va()
    cave += assemble(GET, "push {r4, r6, r7, lr}")
    a = cave_va()
    cave += assemble(a, "add.w r1, r5, #0x11e")
    a = cave_va()
    cave += assemble(
        a,
        """
        movs r0, #0x4E
        strb r0, [r1, #0]
        movs r0, #0x52
        strb r0, [r1, #1]
        movs r0, #0x53
        strb r0, [r1, #2]
        movs r0, #0x50
        strb r0, [r1, #3]
        movs r0, #0
        strb r0, [r1, #4]
        """,
    )
    a = cave_va()
    cave += mov_imm32(a, 2, MAP_RAM)
    a = cave_va()
    cave += assemble(a, "movs r3, #0")
    loop_get = cave_va()
    cave += assemble(
        loop_get,
        """
        ldrh.w r0, [r2, r3, lsl #1]
        adds r4, r3, #5
        strb r0, [r1, r4]
        adds r3, #1
        cmp r3, #12
        """,
    )
    a = cave_va()
    imm = (loop_get - (a + 4)) // 2
    cave += struct.pack("<H", 0xDB00 | (imm & 0xFF))
    a = cave_va()
    cave += assemble(a, "pop {r4, r6, r7, lr}")
    a = cave_va()
    cave += asm_bw(a, SWITCH_EXIT)
    print(f"  Mapping_GET @ {GET:#010x}")

    # Mapping_SET — copy 12 usages, flash, OK (no CRC gate; host still requires OOK)
    while len(cave) % 2:
        cave += b"\x00"
    SET = cave_va()
    cave += assemble(SET, "push {r4, r5, r6, r7, lr}")
    a = cave_va()
    cave += assemble(a, "adds r4, r7, r6")  # CMD ptr
    a = cave_va()
    cave += mov_imm32(a, 2, MAP_RAM)
    a = cave_va()
    cave += assemble(a, "movs r3, #0")
    loop_set = cave_va()
    cave += assemble(
        loop_set,
        """
        adds r0, r3, #5
        ldrb r1, [r4, r0]
        strh.w r1, [r2, r3, lsl #1]
        adds r3, #1
        cmp r3, #12
        """,
    )
    a = cave_va()
    imm = (loop_set - (a + 4)) // 2
    cave += struct.pack("<H", 0xDB00 | (imm & 0xFF))
    a = cave_va()
    cave += assemble(a, "movs r2, #12")
    a = cave_va()
    cave += mov_imm32(a, 1, MAP_RAM)
    a = cave_va()
    cave += mov_imm32(a, 0, MAP_FLASH)
    a = cave_va()
    cave += asm_bl(a, APP_SET_FLASH)
    a = cave_va()
    cave += assemble(
        a,
        """
        add.w r1, r5, #0x11e
        movs r0, #0x4F
        strb r0, [r1, #0]
        movs r0, #0x4F
        strb r0, [r1, #1]
        movs r0, #0x4B
        strb r0, [r1, #2]
        """,
    )
    a = cave_va()
    cave += assemble(a, "pop {r4, r5, r6, r7, lr}")
    a = cave_va()
    cave += asm_bw(a, SWITCH_EXIT)
    print(f"  Mapping_SET @ {SET:#010x}")

    # Switch dispatcher for cmds > 'M'
    while len(cave) % 2:
        cave += b"\x00"
    DISP = cave_va()

    def emit_cmp_bw(code: int, target: int) -> None:
        nonlocal cave
        a = cave_va()
        cave += assemble(a, f"cmp r0, #0x{code:02x}")
        a = cave_va()
        bne = a
        cave += b"\x00\x00"
        a = cave_va()
        cave += asm_bw(a, target)
        a = cave_va()
        imm = (a - (bne + 4)) // 2
        struct.pack_into("<H", cave, bne - CAVE, 0xD100 | (imm & 0xFF))

    emit_cmp_bw(0x4E, GET)
    emit_cmp_bw(0x4F, SET)
    emit_cmp_bw(0x52, SWITCH_GET_SENS)
    emit_cmp_bw(0x57, SWITCH_SET_SENS)
    a = cave_va()
    cave += asm_bw(a, SWITCH_DEFAULT)
    print(f"  switch disp @ {DISP:#010x} ({cave_va() - DISP} bytes)")

    # Literal for Report_Normal MAP_RAM (must be within PC-rel of lookup)
    # Pool stays inside Report_Normal dead 1P gap (positive LDR from 5d72)
    LIT_MAP_RAM = 0x08005D90
    fill_nop(buf, REPORT_LOOKUP, REPORT_JOIN)
    fill_nop(buf, REPORT_DEAD, REPORT_DEAD_END)
    buf[va_to_off(LIT_MAP_RAM) : va_to_off(LIT_MAP_RAM) + 4] = struct.pack("<I", MAP_RAM)

    # Report_Normal lookup
    lookup = bytearray()
    lookup += encode_ldr_pc(REPORT_LOOKUP, 0, LIT_MAP_RAM)
    lookup += struct.pack("<HH", 0xF830, 0x5014)  # ldrh.w r5, [r0, r4, lsl #1]
    lookup += assemble(REPORT_LOOKUP + len(lookup), "uxtb r5, r5")
    lookup += asm_b_n(REPORT_LOOKUP + len(lookup), REPORT_JOIN)
    buf[va_to_off(REPORT_LOOKUP) : va_to_off(REPORT_LOOKUP) + len(lookup)] = lookup
    print(f"  Report lookup @ {REPORT_LOOKUP:#010x} ({len(lookup)} bytes)")

    # USB switch hook
    buf[va_to_off(SWITCH_HIGH) : va_to_off(SWITCH_HIGH) + 4] = asm_bw(SWITCH_HIGH, DISP)
    buf[va_to_off(SWITCH_HIGH) + 4 : va_to_off(SWITCH_HIGH) + 10] = b"\x00\xbf" * 3
    print(f"  switch hook -> {DISP:#010x}")

    # Boot hook: replace first log (16 bytes) with bl Mapping_Load + nops
    boot = asm_bl(BOOT_HOOK, LOAD) + b"\x00\xbf" * 6
    buf[va_to_off(BOOT_HOOK) : va_to_off(BOOT_HOOK) + 16] = boot
    print(f"  boot hook -> {LOAD:#010x}")

    # Append cave
    buf.extend(cave)
    # Pad to 4
    while len(buf) % 4:
        buf.append(0)

    # Sanity
    appuser = struct.unpack_from("<I", buf, va_to_off(0x08005E64))[0]
    cfg = struct.unpack_from("<I", buf, va_to_off(0x08005E68))[0]
    assert appuser == 0x200019D0, hex(appuser)
    assert cfg == 0x20000008, hex(cfg)
    join = bytes(buf[va_to_off(REPORT_JOIN) : va_to_off(REPORT_JOIN) + 4])
    assert join == bytes.fromhex("05f08000"), join.hex()  # and.w r0, r5, #0x80
    assert len(buf) <= 0xB800, f"OTA max exceeded: {len(buf)}"

    dst_path.write_bytes(buf)
    print(f"wrote {dst_path} ({len(buf)} bytes)")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for name, va, n in [
        ("lookup", REPORT_LOOKUP, 16),
        ("switch", SWITCH_HIGH, 12),
        ("boot", BOOT_HOOK, 16),
        ("load", LOAD, 40),
        ("get", GET, 48),
        ("set", SET, 48),
        ("disp", DISP, 48),
    ]:
        print(f"---- {name} ----")
        off = va_to_off(va)
        for insn in md.disasm(bytes(buf[off : off + n]), va):
            print(f"  0x{insn.address:08x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
