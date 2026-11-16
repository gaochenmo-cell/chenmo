#!/usr/bin/env python3
"""
PADUEX4_V3.3 arms the keyboard report only after a UID-derived busy wait.

The wait is the first thing App_USB_KBS_SendPacket does: entry 0x08005E6C
branches to 0x08005DE8, which delays (UID0^UID1^UID2) & 0xF steps and then
continues at 0x08005E70 with the original register arguments.

Checks, against firmware/PADUEX4_V3.2.bin:

  * the only differing bytes are the helper, the entry branch and FVer minor
  * 0x14C is still 0x080084A5, HVer is still 0x14, size is still 37072
  * instruction count from entry to 0x08005E70 grows by 99 per delay step
    (movs + 48x subs/bne + subs + bne), and 0 steps skips the loop
  * r0 (report pointer) and r1 (length) survive the wait
  * Report_Normal's report bytes match V3.2 for the same KeyCode

  python3 firmware/tests/emu_report_stagger.py
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_MODE_THUMB, Uc
from unicorn.arm_const import (
    UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_SP,
)

sys.path.insert(0, str(Path(__file__).resolve().parent))
from emu_report_normal import run as report_normal  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
V32 = ROOT / "firmware" / "PADUEX4_V3.2.bin"
V33 = ROOT / "firmware" / "PADUEX4_V3.3.bin"

BASE = 0x08004000
SEND_ENTRY = 0x08005E6C
SEND_RESUME = 0x08005E70
STAGGER = 0x08005DE8
STAGGER_LEN = 50
FW_MINOR_SITE = 0x0800616A
UID = 0x1FFFF7E8
STACK = 0x20005000
BUF = 0x20003000
# one delay step: movs + 48 * (subs, bne) + subs + bne
STEP_INSNS = 1 + 48 * 2 + 2


def insn_count(image: bytes, uid0: int, uid1: int, uid2: int) -> tuple[int, int, int]:
    uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
    uc.mem_map(0x08000000, 0x20000)
    uc.mem_map(0x20000000, 0x10000)
    uc.mem_map(0x1FFF0000, 0x10000)
    uc.mem_write(BASE, image)
    uc.mem_write(UID, struct.pack("<III", uid0, uid1, uid2))
    uc.mem_write(BUF, bytes(range(10)))
    seen = {"n": 0}

    def hook(uc: Uc, addr: int, size: int, _):
        if addr == SEND_RESUME:
            uc.emu_stop()
            return
        seen["n"] += 1

    uc.hook_add(UC_HOOK_CODE, hook)
    uc.reg_write(UC_ARM_REG_R0, BUF)
    uc.reg_write(UC_ARM_REG_R1, 10)
    uc.reg_write(UC_ARM_REG_SP, STACK)
    uc.emu_start(SEND_ENTRY | 1, SEND_RESUME | 1, count=100000)
    assert uc.reg_read(UC_ARM_REG_PC) == SEND_RESUME, hex(uc.reg_read(UC_ARM_REG_PC))
    return seen["n"], uc.reg_read(UC_ARM_REG_R0), uc.reg_read(UC_ARM_REG_R1)


def main() -> int:
    old, new = V32.read_bytes(), V33.read_bytes()
    fails = 0

    def check(name: str, ok: bool, detail: str = "") -> None:
        nonlocal fails
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f": {detail}" if detail else ""))

    check("size 37072", len(new) == 37072 == len(old))
    check("USB vector 0x14C", struct.unpack_from("<I", new, 0x14C)[0] == 0x080084A5)
    check("HVer still 0x14", new[0x0800615E - BASE] == 0x14)
    check("FVer minor is 3", new[FW_MINOR_SITE - BASE] == 3 and old[FW_MINOR_SITE - BASE] == 2)

    diffs = [BASE + i for i, (a, b) in enumerate(zip(old, new)) if a != b]
    allowed = set(range(STAGGER, STAGGER + STAGGER_LEN))
    allowed.update((SEND_ENTRY, SEND_ENTRY + 1, SEND_ENTRY + 2, SEND_ENTRY + 3, FW_MINOR_SITE))
    check("diff only helper, entry branch, FVer", set(diffs) <= allowed,
          " ".join(hex(d) for d in diffs if d not in allowed) or f"{len(diffs)} bytes")
    check("SendPacket body from 0x08005E70 unchanged",
          new[SEND_RESUME - BASE:0x08005EA4 - BASE] == old[SEND_RESUME - BASE:0x08005EA4 - BASE])

    base_n, r0, r1 = insn_count(new, 0, 0, 0)
    check("zero delay keeps r0/r1", r0 == BUF and r1 == 10, f"r0={r0:#x} r1={r1}")
    counts = {0: base_n}
    for step in (1, 7, 15):
        n, r0, r1 = insn_count(new, step, 0, 0)
        counts[step] = n
        check(f"delay step {step} is +{step * STEP_INSNS} insns and keeps args",
              n - base_n == step * STEP_INSNS and r0 == BUF and r1 == 10,
              f"delta={n - base_n}")
    # low nibbles of the three words are mixed, not UID0 alone
    mixed, _, _ = insn_count(new, 0x10, 0x01, 0x00)   # xor = 0x11 -> step 1
    check("UID0^UID1^UID2, not UID0 alone", mixed == counts[1], f"count={mixed} want {counts[1]}")
    cancelled, _, _ = insn_count(new, 0x0F, 0x0F, 0x00)  # xor = 0
    check("matching low nibbles cancel", cancelled == counts[0])

    color = [7] * 36
    same = True
    for key in (0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F):
        if report_normal(old, key, color, 0x10) != report_normal(new, key, color, 0x10):
            same = False
            break
    check("Report_Normal bytes match V3.2", same)

    print(f"\n{'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
