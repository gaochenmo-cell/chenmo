#!/usr/bin/env python3
"""
Run the real USB command dispatcher (0x08005FAC) of a PADUEX5 / PADUEX4 image under
Unicorn with a GET_MODE (0x47) packet and check the reply the firmware writes into
its shared reply buffer (AppUser + 0x11E):

    47 mode A2 HVer FWmajor FWminor

Expected per image (given on the command line, defaults below):

    PADUEX5_V3.0.bin   -> HVer 0x13, FW 3.0
    PADUEX4_V3.0.bin   -> HVer 0x14, FW 3.0
    legacy/paduex5_1.7 -> HVer 0x13, FW 2.17
    legacy/paduex4_1.0 -> HVer 0x13, FW 2.18
    legacy/paduex5_1.6 -> HVer 0x13, FW 2.12 (same as stock V2.13)

Only printf is stubbed; the dispatcher and reply builder are the bytes from the .bin.

  python3 firmware/tests/emu_get_mode.py firmware/PADUEX5_V3.0.bin 13 3 0
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_MODE_THUMB, Uc
from unicorn.arm_const import UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_SP

BASE = 0x08004000
RAM = 0x20000000
STACK_TOP = 0x20006000
STOP = 0x0800FFF0
APPUSER = 0x200019D0
CONFIGMODE = 0x20000008
REPLY = APPUSER + 0x11E

FN_DISPATCH = 0x08005FAC         # (buf, len): USB OUT packet handler
FN_PRINTF = 0x08004390
LIT_CTX = 0x080061F8             # ldr.w sb, [pc, #0x240] @ 0x08005FB4 -> ctx; AppUser = *(*(ctx+4) + 0x24)
SCRATCH = 0x20004000

EXPECT = {
    "PADUEX5_V3.0.bin": (0x13, 3, 0),
    "PADUEX4_V3.0.bin": (0x14, 3, 0),
    "paduex5_1.7.bin": (0x13, 2, 17),
    "paduex4_1.0.bin": (0x13, 2, 18),
    "paduex5_1.6.bin": (0x13, 2, 12),
}


def get_mode(image: bytes, config_mode: int) -> bytes:
    uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
    uc.mem_map(0x08000000, 0x20000)
    uc.mem_map(RAM, 0x10000)
    uc.mem_write(0x08000000, b"\xff" * 0x20000)
    uc.mem_write(BASE, image)

    ctx = struct.unpack_from("<I", image, LIT_CTX - BASE)[0]
    inner = SCRATCH + 0x200
    uc.mem_write(ctx + 4, struct.pack("<I", inner))
    uc.mem_write(inner + 0x24, struct.pack("<I", APPUSER))
    uc.mem_write(CONFIGMODE, bytes([config_mode]))
    uc.mem_write(SCRATCH, bytes([0x47]).ljust(64, b"\0"))

    def hook(uc: Uc, addr: int, size: int, _):
        if addr == FN_PRINTF:
            uc.reg_write(UC_ARM_REG_R0, 0)
            uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))

    uc.hook_add(UC_HOOK_CODE, hook)
    uc.reg_write(UC_ARM_REG_R0, SCRATCH)
    uc.reg_write(UC_ARM_REG_R1, 64)
    uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
    uc.reg_write(UC_ARM_REG_LR, STOP | 1)
    uc.emu_start(FN_DISPATCH | 1, STOP, count=20000)
    return bytes(uc.mem_read(REPLY, 6))


def main() -> int:
    img_path = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/PADUEX5_V3.0.bin")
    if len(sys.argv) >= 5:
        want = (int(sys.argv[2], 16), int(sys.argv[3]), int(sys.argv[4]))
    else:
        want = EXPECT[img_path.name]
    image = img_path.read_bytes()
    assert len(image) == 37072
    assert struct.unpack_from("<I", image, 0x14C)[0] == 0x080084A5
    assert image[FN_DISPATCH - BASE:FN_DISPATCH - BASE + 4] == bytes.fromhex("2de9fc47"), "dispatcher push.w"

    fails = 0
    for mode in (0x80, 0x81):                      # 2P|KBS, 2P|HID
        rsp = get_mode(image, mode)
        got = (rsp[3], rsp[4], rsp[5])
        ok = rsp[0] == 0x47 and rsp[1] == mode and rsp[2] == 0xA2 and got == want
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] mode={mode:#04x}: reply {rsp.hex(' ')}  "
              f"-> HVer={rsp[3]:#04x} FW={rsp[4]}.{rsp[5]}  want HVer={want[0]:#04x} FW={want[1]}.{want[2]}")
    print(f"\n{img_path.name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
