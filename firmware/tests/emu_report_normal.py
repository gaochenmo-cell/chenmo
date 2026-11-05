#!/usr/bin/env python3
"""
Emulate Report_Normal (keyboard report builder) of a PADUEX5 image with Unicorn
and check which HID usages are produced for a given KeyCode / Color table /
ConfigMode. Used to validate the paduex3_1.7 keyboard remap patch offline.

  python3 firmware/tests/emu_report_normal.py firmware/paduex3_1.7.bin
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_MODE_THUMB, Uc
from unicorn.arm_const import (
    UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_SP,
)

BASE = 0x08004000
REPORT_NORMAL = 0x08005D48
SENDPACKET_BL_SITE = 0x08005E5C   # bl App_USB_KBS_SendPacket inside Report_Normal
APPUSER = 0x200019D0
COLOR = APPUSER + 0x146
CONFIGMODE = 0x20000008
RAM = 0x20000000
STACK_TOP = 0x20004000
STOP = 0x0800FFF0

TAB_1P = [0x1A, 0x1B, 0x04, 0x07, 0x16]   # w x a d s
TAB_2P = [0x0B, 0x0E, 0x0D, 0x11, 0x10]   # h k j n m


def run(image: bytes, keycode: int, color: list[int], configmode: int) -> list[int]:
    uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
    uc.mem_map(0x08000000, 0x20000)
    uc.mem_map(RAM, 0x10000)
    uc.mem_write(BASE, image)
    uc.mem_write(CONFIGMODE, bytes([configmode]))
    uc.mem_write(COLOR, b"".join(struct.pack("<H", v) for v in color))

    captured: dict[str, bytes] = {}

    def hook(uc: Uc, addr: int, size: int, _):
        if addr == SENDPACKET_BL_SITE:
            buf = uc.reg_read(UC_ARM_REG_R0)
            n = uc.reg_read(UC_ARM_REG_R1)
            captured["report"] = uc.mem_read(buf, n)
            uc.reg_write(UC_ARM_REG_PC, STOP | 1)
            uc.emu_stop()

    uc.hook_add(UC_HOOK_CODE, hook)
    uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
    uc.reg_write(UC_ARM_REG_R0, keycode)
    uc.reg_write(UC_ARM_REG_LR, STOP | 1)
    uc.emu_start(REPORT_NORMAL | 1, STOP, count=5000)
    rep = captured.get("report")
    if rep is None:
        raise RuntimeError("SendPacket never reached")
    return [b for b in rep[2:10] if b]


def main() -> int:
    img_path = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/paduex3_1.7.bin")
    img = img_path.read_bytes()
    fails = 0

    def check(name: str, got: list[int], want: list[int]) -> None:
        nonlocal fails
        ok = got == want
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: got {[hex(x) for x in got]} want {[hex(x) for x in want]}")

    default_color = [7] * 36                      # white LEDs, slots +1 hold old colour codes
    # 1) untouched colour table -> stock tables, 1P and 2P
    for k in range(5):
        check(f"stock 1P key{k}", run(img, 1 << k, default_color, 0x01), [TAB_1P[k]])
        check(f"stock 2P key{k}", run(img, 1 << k, default_color, 0x11), [TAB_2P[k]])
    # 2) remapped: K tag in Color[k*4+1]
    remap = [0x28, 0x2C, 0x50, 0x4F, 0x51]        # Enter Space Left Right Down
    color = list(default_color)
    for k in range(5):
        color[k * 4 + 1] = 0x4B00 | remap[k]
    for k in range(5):
        check(f"remap key{k} (2P mode)", run(img, 1 << k, color, 0x11), [remap[k]])
        check(f"remap key{k} (1P mode)", run(img, 1 << k, color, 0x01), [remap[k]])
    # 3) partial remap: only key2 tagged, others stock
    color = list(default_color)
    color[2 * 4 + 1] = 0x4B00 | 0x2C
    check("partial: key2 remapped", run(img, 1 << 2, color, 0x11), [0x2C])
    check("partial: key0 stock", run(img, 1 << 0, color, 0x11), [TAB_2P[0]])
    # 4) chords and LED colours untouched (slot +0 still the colour)
    check("chord key0+key4 remap", run(img, 0b10001, [0x4B00 | 0x04 if i % 4 == 1 else 1 for i in range(36)], 0x11), [0x04, 0x04])
    check("chord stock 2P key1+key3", run(img, 0b01010, default_color, 0x11), [TAB_2P[1], TAB_2P[3]])
    # 5) a wrong tag (e.g. host wrote 0x0100) must NOT remap
    color = list(default_color)
    color[1] = 0x0100 | 0x04
    check("bad tag -> stock", run(img, 1, color, 0x11), [TAB_2P[0]])

    print(f"\n{img_path.name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
