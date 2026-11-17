#!/usr/bin/env python3
"""
PADUEX V3.4 pins the release-hold table at 0.

Against the matching V3.2 image:

  * the only differing bytes are the SET_SENS type-2 path, the boot clear loop,
    the default DTick table and the FVer minor
  * a DTick page saved by V3.2 is ignored at power-up; RAM and GET_SENS stay 0
  * SET_SENS type 2 is ACKed, does not program flash, and GET_SENS still returns 0
  * pressing and releasing a key produces the same keyboard-report bytes as V3.2
    with its DTick table forced to 0, and not the same sequence as DTick = 3

  python3 firmware/tests/emu_dtick_fixed.py
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_MODE_THUMB, Uc
from unicorn.arm_const import (
    UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_SP,
)

sys.path.insert(0, str(Path(__file__).resolve().parent))
from emu_keyscan_paduex4 import DTICK, Board  # noqa: E402
from emu_report_normal import COLOR, CONFIGMODE, SENDPACKET_BL_SITE  # noqa: E402
from emu_set_sens import PAGES, Board as FlashBoard, host_get, host_set  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
BASE = 0x08004000
SET_SITE, SET_END = 0x08004BDE, 0x08004C04
BOOT_SITE, BOOT_END = 0x080064DE, 0x080064F4
DEFAULT_TAB = 0x0800CF24
FW_MINOR = 0x0800616A
FN_TICK = 0x08006D6E
FN_TICK_STOP = 0x08006D9A
STOP = 0x0800FFF0
REPORT_NORMAL = 0x08005D48


def report_bytes(image: bytes, keycode: int) -> bytes:
    uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
    uc.mem_map(0x08000000, 0x20000)
    uc.mem_map(0x20000000, 0x10000)
    uc.mem_write(BASE, image)
    uc.mem_write(CONFIGMODE, bytes([0x10]))
    uc.mem_write(COLOR, struct.pack("<36H", *([7] * 36)))
    captured: dict[str, bytes] = {}

    def hook(uc: Uc, addr: int, size: int, _):
        if addr == SENDPACKET_BL_SITE:
            buf = uc.reg_read(UC_ARM_REG_R0)
            n = uc.reg_read(UC_ARM_REG_R1)
            captured["report"] = bytes(uc.mem_read(buf, n))
            uc.reg_write(UC_ARM_REG_PC, STOP | 1)
            uc.emu_stop()

    uc.hook_add(UC_HOOK_CODE, hook)
    uc.reg_write(UC_ARM_REG_SP, 0x20004000)
    uc.reg_write(UC_ARM_REG_R0, keycode)
    uc.reg_write(UC_ARM_REG_LR, STOP | 1)
    uc.emu_start(REPORT_NORMAL | 1, STOP, count=5000)
    if "report" not in captured:
        raise RuntimeError("SendPacket never reached")
    return captured["report"]


def tick(board: Board) -> None:
    board.uc.reg_write(UC_ARM_REG_R0, 0)
    board.uc.reg_write(UC_ARM_REG_SP, 0x20006000)
    board.uc.reg_write(UC_ARM_REG_LR, STOP | 1)
    board.uc.emu_start(FN_TICK | 1, FN_TICK_STOP, count=5000)


def play(image: bytes, dtick: int) -> list[tuple[int, list[bytes]]]:
    board = Board(image)
    board.uc.mem_write(DTICK, struct.pack("<36H", *([dtick] * 36)))
    out: list[tuple[int, list[bytes]]] = []

    def step(sensors: set[int], ticks: int = 0) -> None:
        for _ in range(ticks):
            tick(board)
        board.reports.clear()
        code = board.scan(sensors)
        payloads = [report_bytes(image, k) for t, k in board.reports if t == "KBD"]
        out.append((code, payloads))

    step({0})          # press
    step(set())        # sample already below the threshold
    step(set(), 1)
    step(set(), 1)
    step(set(), 1)
    step({0})          # press again
    step({0})          # still above: reloads Tck
    step(set())        # drop
    return out


def main() -> int:
    fails = 0

    def check(name: str, ok: bool, detail: str = "") -> None:
        nonlocal fails
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f": {detail}" if detail else ""))

    for hver, name in ((0x13, "PADUEX5"), (0x14, "PADUEX4")):
        old = (ROOT / "firmware" / f"{name}_V3.2.bin").read_bytes()
        new = (ROOT / "firmware" / f"{name}_V3.4.bin").read_bytes()
        check(f"{name} size and USB vector", len(new) == 37072 and struct.unpack_from("<I", new, 0x14C)[0] == 0x080084A5)
        check(f"{name} HVer unchanged, FVer 3.4", new[0x0800615E - BASE] == hver and new[FW_MINOR - BASE] == 4)
        diffs = [BASE + i for i, (a, b) in enumerate(zip(old, new)) if a != b]
        allowed = set(range(SET_SITE, SET_END)) | set(range(BOOT_SITE, BOOT_END))
        allowed.update(range(DEFAULT_TAB, DEFAULT_TAB + 72))
        allowed.add(FW_MINOR)
        check(f"{name} diff only at the DTick sites", set(diffs) <= allowed,
              " ".join(hex(d) for d in diffs if d not in allowed) or f"{len(diffs)} bytes")
        check(f"{name} default table is 36 zeros",
              struct.unpack_from("<36H", new, DEFAULT_TAB - BASE) == (0,) * 36)

        fresh = FlashBoard(new)
        fresh.power_up()
        check(f"{name} blank boot DTick is 0 and GET returns 0",
              fresh.ram(2)[:20] == [0] * 20 and host_get(fresh, 2) == [0] * 20,
              str(fresh.ram(2)[:4]))

        donor = FlashBoard(old)
        donor.power_up()
        host_set(donor, 2, [9 + i for i in range(20)])
        saved = bytes(donor.uc.mem_read(PAGES[2], 0x800))
        loaded = FlashBoard(new)
        loaded.uc.mem_write(PAGES[2], saved)
        loaded.power_up()
        check(f"{name} a saved DTick page is discarded at boot",
              loaded.ram(2)[:20] == [0] * 20 and host_get(loaded, 2) == [0] * 20,
              str(loaded.ram(2)[:4]))
        page_before = bytes(loaded.uc.mem_read(PAGES[2], 0x800))
        rsp = host_set(loaded, 2, [40] * 20)
        page_after = bytes(loaded.uc.mem_read(PAGES[2], 0x800))
        check(f"{name} SET DTick ACKs, RAM stays 0, flash page unchanged",
              rsp[:3] == bytes([0x57, 0x4F, 0x4B]) and loaded.ram(2)[:20] == [0] * 20
              and page_after == page_before and host_get(loaded, 2) == [0] * 20,
              rsp[:5].hex())

        same = play(new, 0)
        ref = play(old, 0)
        held = play(old, 3)
        check(f"{name} report bytes match V3.2 with DTick 0", same == ref, f"v34={same} v32={ref}")
        check(f"{name} DTick 3 on V3.2 holds the key longer", same != held)
        # the reload still reads the table: forcing 3 on V3.4 matches V3.2 with 3
        check(f"{name} reload still reads DTick RAM", play(new, 3) == held)

    print(f"\n{'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
