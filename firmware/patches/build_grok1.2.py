#!/usr/bin/env python3
"""
Build grok1.2.bin from grok1.0.bin — remappable keys, SAME file size as grok1.0.

Safety: never append past the original image. Handlers live inside a rewritten
App_USB_KBS_Report_Normal region so ISP tools that flash by file length cannot
truncate a dangling boot cave (the grok1.1 brick mode).

Features:
  - Report_Normal reads Mapping[12] uint16 @ 0x20001BB0
  - GET_MAPP / SET_MAPP + Flash page 0x08011800
  - Boot loads Mapping via App_Get_Flash (defaults = W X A D S …)
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
APP_USER = 0x200019D0
APP_GET_FLASH = 0x08004D38
APP_SET_FLASH = 0x080053F0
SEND_PACKET = 0x08005E6C

# Entire Report_Normal body we may rewrite (keep literals at 5e64/5e68)
RN_START = 0x08005D48
RN_LITERALS = 0x08005E64  # AppUser, ConfigMode — DO NOT TOUCH
LIT_APPUSER = 0x08005E64
BOOT_HOOK = 0x080064B0
SWITCH_HIGH = 0x08006024
SWITCH_GET_SENS = 0x080060FC
SWITCH_SET_SENS = 0x080061DC
SWITCH_DEFAULT = 0x080061EE
SWITCH_EXIT = 0x080061F0

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
    offset = to_addr - (from_addr + 4)
    if offset & 1:
        raise ValueError("odd branch")
    if not (-0x1000000 <= offset < 0x1000000):
        raise ValueError(f"branch OOR {from_addr:#x}->{to_addr:#x}")
    S = (offset >> 24) & 1
    I1 = (offset >> 23) & 1
    I2 = (offset >> 22) & 1
    imm10 = (offset >> 12) & 0x3FF
    imm11 = (offset >> 1) & 0x7FF
    J1 = (~(I1 ^ S)) & 1
    J2 = (~(I2 ^ S)) & 1
    hi = 0xF000 | (S << 10) | imm10
    lo = (0xD000 if link else 0x9000) | (J1 << 13) | (J2 << 11) | imm11
    return struct.pack("<HH", hi, lo)


def asm_bl(frm: int, to: int) -> bytes:
    return _thumb_long_branch(frm, to, True)


def asm_bw(frm: int, to: int) -> bytes:
    return _thumb_long_branch(frm, to, False)


def mov_imm32(addr: int, rt: int, val: int) -> bytes:
    return assemble(
        addr,
        f"movw r{rt}, #0x{val & 0xFFFF:x}\nmovt r{rt}, #0x{(val >> 16) & 0xFFFF:x}",
    )


def encode_ldr_pc(addr: int, rt: int, lit: int) -> bytes:
    pc = (addr + 4) & ~3
    delta = lit - pc
    if delta < 0 or delta > 1020 or delta % 4:
        raise ValueError(f"LDR PC {addr:#x}->{lit:#x} d={delta}")
    return struct.pack("<H", 0x4800 | (rt << 8) | (delta // 4))


def encode_b_n(frm: int, to: int) -> bytes:
    imm = (to - (frm + 4)) // 2
    if not (-1024 <= imm <= 1023):
        raise ValueError(f"B.N OOR {frm:#x}->{to:#x}")
    return struct.pack("<H", 0xE000 | (imm & 0x7FF))


def encode_bcond(frm: int, to: int, cond: int) -> bytes:
    """cond: 0=eq 1=ne 3=lt(signed)  etc. Standard ARM cond codes for B<cond>."""
    imm = (to - (frm + 4)) // 2
    if not (-128 <= imm <= 127):
        raise ValueError(f"Bcond OOR {frm:#x}->{to:#x} imm={imm}")
    return struct.pack("<H", 0xD000 | ((cond & 0xF) << 8) | (imm & 0xFF))


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src_path = root / "grok1.0.bin"
    dst_path = root / "grok1.2.bin"
    buf = bytearray(src_path.read_bytes())
    orig_len = len(buf)
    print(f"base {src_path.name} ({orig_len} bytes) — size will be preserved")

    region_end = RN_LITERALS
    avail = region_end - RN_START

    # Reserve 8 bytes at end of region for MAP_RAM + MAP_FLASH literals
    lit_map_ram = region_end - 8
    lit_map_flash = region_end - 4
    code_end = lit_map_ram

    workspace = bytearray()

    def here() -> int:
        return RN_START + len(workspace)

    def emit(data: bytes) -> int:
        addr = here()
        workspace.extend(data)
        return addr

    def emit_asm(src: str) -> int:
        addr = here()
        workspace.extend(assemble(addr, src))
        return addr

    def align(n: int = 2) -> None:
        while (RN_START + len(workspace)) % n:
            workspace.append(0)

    # ------------------------------------------------------------------
    # Compact Report_Normal — always use Mapping[] (no 1P/2P split)
    # ------------------------------------------------------------------
    RN = here()
    emit_asm("stmdb sp!, {r1, r2, r3, r4, r5, r6, r7, r8, r9, lr}")
    emit_asm("mov r7, r0")
    emit_asm("movs r6, #0")
    emit_asm("movs r4, #0")
    emit_asm("movs r0, #0")
    emit_asm("str r0, [sp, #0]")
    emit_asm("str r0, [sp, #4]")
    emit_asm("str r0, [sp, #8]")
    patch_b_to_test = len(workspace)
    emit(b"\x00\x00")  # B.N loop_test

    loop_body = here()
    emit(encode_ldr_pc(here(), 0, LIT_APPUSER))
    emit_asm("strh r7, [r0, #26]")
    emit_asm("movs r1, #1")
    emit_asm("lsls r1, r4")
    emit_asm("tst r7, r1")
    patch_beq_next = len(workspace)
    emit(b"\x00\x00")  # BEQ next

    emit(encode_ldr_pc(here(), 0, lit_map_ram))
    emit(struct.pack("<HH", 0xF830, 0x5014))  # ldrh.w r5, [r0, r4, lsl #1]
    emit_asm("uxtb r5, r5")
    # shift bit 7 → modifier LeftShift in report[0]
    emit_asm("movs r0, #0x80")
    emit_asm("tst r5, r0")
    patch_beq_noshift = len(workspace)
    emit(b"\x00\x00")  # BEQ noshift
    emit_asm("movs r0, #2")
    emit(struct.pack("<HH", 0xF88D, 0x0000))  # strb.w r0, [sp]
    emit_asm("movs r0, #0x7f")
    emit_asm("ands r5, r5, r0")

    noshift = here()
    workspace[patch_beq_noshift : patch_beq_noshift + 2] = encode_bcond(
        RN_START + patch_beq_noshift, noshift, 0
    )

    emit_asm("adds r0, r6, #2")
    emit(struct.pack("<HH", 0xF80D, 0x5000))  # strb.w r5, [sp, r0]
    emit_asm("adds r6, #1")  # max 9 keys — no uxtb needed (saves 2B to fit)

    next_i = here()
    workspace[patch_beq_next : patch_beq_next + 2] = encode_bcond(
        RN_START + patch_beq_next, next_i, 0
    )

    emit_asm("adds r4, #1")
    emit_asm("uxtb r4, r4")
    loop_test = here()
    workspace[patch_b_to_test : patch_b_to_test + 2] = encode_b_n(
        RN_START + patch_b_to_test, loop_test
    )

    emit_asm("cmp r4, #9")
    emit(encode_bcond(here(), loop_body, 0xB))  # BLT
    emit_asm("movs r1, #10")
    emit_asm("mov r0, sp")
    emit(asm_bl(here(), SEND_PACKET))
    emit_asm("ldmia.w sp!, {r1, r2, r3, r4, r5, r6, r7, r8, r9, pc}")
    rn_end = here()
    print(f"  compact Report_Normal {RN:#x}..{rn_end:#x} ({rn_end - RN} bytes)")

    # ------------------------------------------------------------------
    # defaults + handlers (fit before literal pool)
    # ------------------------------------------------------------------
    align(4)
    DEFAULTS = here()
    workspace.extend(b"".join(struct.pack("<H", v) for v in DEFAULT_MAP))
    print(f"  defaults @ {DEFAULTS:#x}")

    align(2)
    LOAD = here()
    emit_asm("push {r4, lr}")
    emit_asm("movs r3, #12")
    # ADR r2, DEFAULTS — encode manually for exact PC
    # ADR (add rd, pc, #imm) T1: encoding depends on range; use movw/movt if far
    if 0 <= (DEFAULTS - ((here() + 4) & ~3)) <= 1020 and (
        (DEFAULTS - ((here() + 4) & ~3)) % 4 == 0
    ):
        # ADR via ADD rd, pc, #imm encoded as adr
        emit_asm(f"adr r2, #{DEFAULTS - ((here() + 4) & ~3)}")
    else:
        emit(mov_imm32(here(), 2, DEFAULTS))
    emit(encode_ldr_pc(here(), 1, lit_map_ram))
    emit(encode_ldr_pc(here(), 0, lit_map_flash))
    emit(asm_bl(here(), APP_GET_FLASH))
    emit_asm("pop {r4, pc}")
    print(f"  Mapping_Load @ {LOAD:#x} ({here() - LOAD} bytes)")

    align(2)
    GET = here()
    emit_asm("push {r4, r6, r7, lr}")
    emit_asm("add.w r1, r5, #0x11e")
    emit_asm(
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
        """
    )
    emit(encode_ldr_pc(here(), 2, lit_map_ram))
    emit_asm("movs r3, #0")
    loop_g = here()
    emit(struct.pack("<HH", 0xF832, 0x0013))  # ldrh.w r0, [r2, r3, lsl #1]
    emit_asm("adds r4, r3, #5")
    emit_asm("strb r0, [r1, r4]")
    emit_asm("adds r3, #1")
    emit_asm("cmp r3, #12")
    emit(encode_bcond(here(), loop_g, 0xB))  # BLT
    emit_asm("pop {r4, r6, r7, lr}")
    emit(asm_bw(here(), SWITCH_EXIT))
    print(f"  Mapping_GET @ {GET:#x} ({here() - GET} bytes)")

    align(2)
    SET = here()
    emit_asm("push {r4, r5, r6, r7, lr}")
    emit_asm("adds r4, r7, r6")
    emit(encode_ldr_pc(here(), 2, lit_map_ram))
    emit_asm("movs r3, #0")
    loop_s = here()
    emit_asm("adds r0, r3, #5")
    emit_asm("ldrb r1, [r4, r0]")
    emit(struct.pack("<HH", 0xF822, 0x1013))  # strh.w r1, [r2, r3, lsl #1]
    emit_asm("adds r3, #1")
    emit_asm("cmp r3, #12")
    emit(encode_bcond(here(), loop_s, 0xB))
    emit_asm("movs r2, #12")
    emit(encode_ldr_pc(here(), 1, lit_map_ram))
    emit(encode_ldr_pc(here(), 0, lit_map_flash))
    emit(asm_bl(here(), APP_SET_FLASH))
    emit_asm(
        """
        add.w r1, r5, #0x11e
        movs r0, #0x4F
        strb r0, [r1, #0]
        movs r0, #0x4F
        strb r0, [r1, #1]
        movs r0, #0x4B
        strb r0, [r1, #2]
        """
    )
    emit_asm("pop {r4, r5, r6, r7, lr}")
    emit(asm_bw(here(), SWITCH_EXIT))
    print(f"  Mapping_SET @ {SET:#x} ({here() - SET} bytes)")

    align(2)
    DISP = here()

    def emit_cmp_bw(code: int, target: int) -> None:
        emit_asm(f"cmp r0, #0x{code:02x}")
        bne_off = len(workspace)
        emit(b"\x00\x00")  # BNE after
        emit(asm_bw(here(), target))
        after = here()
        workspace[bne_off : bne_off + 2] = encode_bcond(
            RN_START + bne_off, after, 1
        )  # BNE

    emit_cmp_bw(0x4E, GET)
    emit_cmp_bw(0x4F, SET)
    emit_cmp_bw(0x52, SWITCH_GET_SENS)
    emit_cmp_bw(0x57, SWITCH_SET_SENS)
    emit(asm_bw(here(), SWITCH_DEFAULT))
    print(f"  switch disp @ {DISP:#x} ({here() - DISP} bytes)")

    end = here()
    used = end - RN_START
    print(f"  code used {used} / {code_end - RN_START} before lit pool "
          f"(region {avail} incl. 8-byte lits)")
    if end > code_end:
        print(
            f"ERROR: overflow into literal pool by {end - code_end} bytes",
            file=sys.stderr,
        )
        return 1

    # Pad with NOPs up to literal pool
    while len(workspace) < (code_end - RN_START):
        if len(workspace) + 2 <= (code_end - RN_START) and (len(workspace) % 2) == 0:
            workspace.extend(b"\x00\xbf")
        else:
            workspace.append(0)

    assert len(workspace) == (code_end - RN_START)
    # Append MAP_RAM + MAP_FLASH literals
    workspace.extend(struct.pack("<II", MAP_RAM, MAP_FLASH))
    assert len(workspace) == avail
    assert RN_START + (lit_map_ram - RN_START) == lit_map_ram

    buf[va_to_off(RN_START) : va_to_off(region_end)] = workspace

    # Preserve following literals
    appuser = struct.unpack_from("<I", buf, va_to_off(0x08005E64))[0]
    cfg = struct.unpack_from("<I", buf, va_to_off(0x08005E68))[0]
    assert appuser == APP_USER, hex(appuser)
    assert cfg == 0x20000008, hex(cfg)
    assert struct.unpack_from("<I", buf, va_to_off(lit_map_ram))[0] == MAP_RAM
    assert struct.unpack_from("<I", buf, va_to_off(lit_map_flash))[0] == MAP_FLASH

    # Switch hook → dispatcher
    buf[va_to_off(SWITCH_HIGH) : va_to_off(SWITCH_HIGH) + 4] = asm_bw(SWITCH_HIGH, DISP)
    buf[va_to_off(SWITCH_HIGH) + 4 : va_to_off(SWITCH_HIGH) + 10] = b"\x00\xbf" * 3

    # Boot hook → Mapping_Load (still inside original image)
    boot = asm_bl(BOOT_HOOK, LOAD) + b"\x00\xbf" * 6
    buf[va_to_off(BOOT_HOOK) : va_to_off(BOOT_HOOK) + 16] = boot

    assert len(buf) == orig_len, f"size changed {orig_len} -> {len(buf)}"
    dst_path.write_bytes(buf)
    print(f"wrote {dst_path} ({len(buf)} bytes) SAME as grok1.0")

    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for name, va, n in [
        ("RN", RN, 64),
        ("load", LOAD, 40),
        ("get", GET, 48),
        ("set", SET, 48),
        ("disp", DISP, 48),
        ("boot", BOOT_HOOK, 16),
        ("sw", SWITCH_HIGH, 12),
        ("lits", lit_map_ram, 8),
    ]:
        print(f"---- {name} @ {va:#x} ----")
        off = va_to_off(va)
        if name == "lits":
            print(f"  MAP_RAM={struct.unpack_from('<I', buf, off)[0]:#x} "
                  f"MAP_FLASH={struct.unpack_from('<I', buf, off+4)[0]:#x}")
            continue
        for insn in md.disasm(bytes(buf[off : off + n]), va):
            print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
