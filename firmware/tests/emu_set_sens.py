#!/usr/bin/env python3
"""
Closed-loop persistence check of SET_SENS, running the REAL firmware code of the
image under Unicorn:

  1. power-up with a blank config area: the three App_Get_Flash_Cofnig calls fall
     back to the built-in defaults (Sensi 800, Colour table, DTick 3)
  2. host SET_SENS type 0 / 1 / 2 with distinctive values -> real handler
     (App_Config_Command_Report) -> did it call App_Set_Flash_Cofnig, on which page?
  3. power cycle: RAM wiped, config pages re-read from the emulated flash
  4. host GET_SENS -> must equal what was written in step 2
  5. type 0x03 (unknown low nibble) must not touch flash at all

Only printf and the flash peripheral (unlock / lock / erase / write) are stubbed;
the command parser, CRC8/CRC16, the config helpers and their magic/CRC checks are
the bytes from the .bin.

  python3 firmware/tests/emu_set_sens.py firmware/PADUEX5_V3.1.bin          # expect ALL PASS
  python3 firmware/tests/emu_set_sens.py firmware/legacy/PADUEX5_V3.0.bin   # control: Sensi lost
  python3 firmware/tests/emu_set_sens.py firmware/legacy/paduex5_1.6.bin    # control: stock behaviour
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_MODE_THUMB, Uc
from unicorn.arm_const import (
    UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2,
    UC_ARM_REG_R3, UC_ARM_REG_SP,
)

BASE = 0x08004000
PAGE = 0x800
RAM = 0x20000000
STACK_TOP = 0x20006000
STOP = 0x0800FFF0
SCRATCH = 0x20004000

APPUSER = 0x200019D0
SENSI = APPUSER + 0xFE           # 0x20001ACE
COLOR = APPUSER + 0x146          # 0x20001B16
DTICK = COLOR + 0x48             # 0x20001B5E
CONFIGMODE = 0x20000008
MAX_VAL_CHAN = 36
DATA_LEN = 40

PAGES = {0: 0x08010000, 1: 0x08010800, 2: 0x08011000}
RAM_TAB = {0: SENSI, 1: COLOR, 2: DTICK}
DEFAULT_TAB = {0: 0x0800CE94, 1: 0x0800CEDC, 2: 0x0800CF24}
NAMES = {0: "Sensi", 1: "Color", 2: "DTick"}

FN_CMD_REPORT = 0x080049E0       # App_Config_Command_Report(cmd, len, rsp, rsplen)
FN_GET_FLASH = 0x08004D38        # App_Get_Flash_Cofnig(addr, dst, default, size)
FN_SET_FLASH = 0x080053F0        # App_Set_Flash_Cofnig(addr, src, size)
FN_PRINTF = 0x08004390
FN_FLASH_UNLOCK = 0x08009D00
FN_FLASH_LOCK = 0x08009C38
FN_FLASH_ERASE = 0x08009CC0
FN_FLASH_WRITE = 0x08009D18


def crc8(buf: bytes) -> int:
    crc = 0
    for b in buf:
        crc ^= b
        for _ in range(8):
            crc = ((crc >> 1) ^ 0x8C) & 0xFF if crc & 1 else (crc >> 1) & 0xFF
    return crc


class Board:
    def __init__(self, image: bytes):
        self.uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
        self.uc.mem_map(0x08000000, 0x20000)
        self.uc.mem_map(RAM, 0x10000)
        self.uc.mem_write(0x08000000, b"\xff" * 0x20000)
        self.uc.mem_write(BASE, image)
        self.uc.mem_write(CONFIGMODE, bytes([0x80]))
        self.flash_ops: list[str] = []
        self.set_flash_calls: list[tuple[int, int, int]] = []
        self.uc.hook_add(UC_HOOK_CODE, self._hook)

    def _ret(self, r0: int | None = None) -> None:
        if r0 is not None:
            self.uc.reg_write(UC_ARM_REG_R0, r0)
        self.uc.reg_write(UC_ARM_REG_PC, self.uc.reg_read(UC_ARM_REG_LR))

    def _hook(self, uc: Uc, addr: int, size: int, _):
        if addr in (FN_PRINTF, FN_FLASH_UNLOCK, FN_FLASH_LOCK):
            self._ret(0)
        elif addr == FN_SET_FLASH:
            self.set_flash_calls.append(tuple(uc.reg_read(r) for r in (UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2)))
        elif addr == FN_FLASH_ERASE:
            a = uc.reg_read(UC_ARM_REG_R0) & ~(PAGE - 1)
            uc.mem_write(a, b"\xff" * PAGE)
            self.flash_ops.append(f"erase {a:#x}")
            self._ret(0)
        elif addr == FN_FLASH_WRITE:
            a, src, n = (uc.reg_read(r) for r in (UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2))
            data = bytes(uc.mem_read(src, n * 2))
            cur = bytes(uc.mem_read(a, n * 2))
            uc.mem_write(a, bytes(x & y for x, y in zip(cur, data)))   # NOR: bits only clear
            self.flash_ops.append(f"write {a:#x} {n} halfwords")
            self._ret(0)

    def call(self, fn: int, *args: int, count: int = 400000) -> None:
        for reg, val in zip((UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3), args):
            self.uc.reg_write(reg, val)
        self.uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
        self.uc.reg_write(UC_ARM_REG_LR, STOP | 1)
        self.uc.emu_start(fn | 1, STOP, count=count)

    # --- power cycle: wipe RAM, run the three config loads the boot code performs -------
    def power_up(self) -> None:
        self.uc.mem_write(APPUSER, b"\x00" * 0x200)
        for t in (0, 1, 2):
            self.call(FN_GET_FLASH, PAGES[t], RAM_TAB[t], DEFAULT_TAB[t], MAX_VAL_CHAN)

    def ram(self, t: int) -> list[int]:
        return list(struct.unpack("<36H", self.uc.mem_read(RAM_TAB[t], MAX_VAL_CHAN * 2)))

    def usb_command(self, pkt: bytes) -> bytes:
        cmd, rsp = SCRATCH, SCRATCH + 0x100
        self.uc.mem_write(cmd, pkt.ljust(64, b"\0"))
        self.uc.mem_write(rsp, b"\0" * 64)
        self.call(FN_CMD_REPORT, cmd, 64, rsp, 64)
        return bytes(self.uc.mem_read(rsp, 64))


# ---- host side, byte-for-byte what App_DDR.Read_Config_Value / Write_Config_Value send ----
def host_get(board: Board, ctype: int) -> list[int] | None:
    rsp = board.usb_command(bytes([0x52, 0x45, 0x41, 0x44, ctype]))
    if rsp[:5] != bytes([0x52, 0x52, 0x53, 0x50, ctype]):
        return None
    if crc8(rsp[:5 + DATA_LEN]) != rsp[5 + DATA_LEN]:
        return None
    return [(rsp[5 + 2 * i] << 8) | rsp[6 + 2 * i] for i in range(20)]


def host_set(board: Board, ctype: int, values: list[int]) -> bytes:
    pkt = bytearray([0x57, 0x52, 0x49, 0x54, ctype])
    for v in values:
        pkt += bytes([(v >> 8) & 0xFF, v & 0xFF])
    pkt.append(crc8(bytes(pkt)))
    return board.usb_command(bytes(pkt))


def main() -> int:
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/PADUEX5_V3.1.bin")
    img = path.read_bytes()
    fails = 0

    def check(name: str, ok: bool, detail: str = "") -> None:
        nonlocal fails
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}{('  ' + detail) if detail else ''}")

    board = Board(img)

    # 1) blank flash -> defaults
    board.power_up()
    check("blank flash: Sensi defaults to 800", board.ram(0)[:20] == [800] * 20, str(board.ram(0)[:4]))
    check("blank flash: DTick defaults to 3", board.ram(2)[:20] == [3] * 20)
    check("GET_SENS(Sensi) returns defaults", host_get(board, 0x00) == [800] * 20)

    # 2) write all three tables, each with a recognisable pattern
    want = {
        0: [300 + 7 * i for i in range(20)],                 # sensitivity thresholds
        1: [[1, 0, 1, 1], [4, 0, 4, 4], [7, 0, 7, 7], [1, 0, 1, 1], [4, 0, 4, 4]],
        2: [10 + i for i in range(20)],
    }
    want[1] = [v for key in want[1] for v in key]
    for t in (0, 1, 2):
        board.flash_ops.clear()
        board.set_flash_calls.clear()
        rsp = host_set(board, t, want[t])
        check(f"SET_SENS({NAMES[t]}) ACK 57 4F 4B", rsp[:3] == bytes([0x57, 0x4F, 0x4B]) and rsp[4] == t, rsp[:5].hex())
        check(f"SET_SENS({NAMES[t]}) RAM updated", board.ram(t)[:20] == want[t], str(board.ram(t)[:4]))
        check(f"SET_SENS({NAMES[t]}) calls App_Set_Flash_Cofnig({PAGES[t]:#x}, {NAMES[t]}, 36)",
              board.set_flash_calls == [(PAGES[t], RAM_TAB[t], MAX_VAL_CHAN)],
              str([(hex(a), hex(s), n) for a, s, n in board.set_flash_calls]) or "no call")
        check(f"SET_SENS({NAMES[t]}) erase + write page {PAGES[t]:#x}",
              board.flash_ops == [f"erase {PAGES[t]:#x}", f"write {PAGES[t]:#x} 128 halfwords"],
              "; ".join(board.flash_ops) or "no flash access")
        page = bytes(board.uc.mem_read(PAGES[t], 2 + MAX_VAL_CHAN * 2))
        check(f"page {PAGES[t]:#x} magic A55A + data", page[:2] == b"\x5a\xa5" and
              list(struct.unpack("<20H", page[2:42])) == want[t], page[:6].hex())
        check(f"GET_SENS({NAMES[t]}) immediately reads back", host_get(board, t) == want[t])

    # 3) unknown type: RAM table (Sensi) is written by stock code, flash must stay untouched
    board.flash_ops.clear()
    board.set_flash_calls.clear()
    rsp = host_set(board, 0x03, [1] * 20)
    check("SET_SENS(type 3) ACK", rsp[:3] == bytes([0x57, 0x4F, 0x4B]))
    check("SET_SENS(type 3) no flash access", not board.flash_ops and not board.set_flash_calls,
          "; ".join(board.flash_ops))
    # restore Sensi RAM contents for the reboot comparison below (type 3 aliases the Sensi table)
    host_set(board, 0x00, want[0])

    # 4) power cycle and read back
    board.power_up()
    for t in (0, 1, 2):
        check(f"after power cycle: {NAMES[t]} RAM == written values", board.ram(t)[:20] == want[t],
              str(board.ram(t)[:4]))
        check(f"after power cycle: GET_SENS({NAMES[t]}) == written values", host_get(board, t) == want[t])

    # 5) bad CRC8 from the host must be rejected (57 46 49 = "WFI") and not persist
    board.flash_ops.clear()
    pkt = bytearray([0x57, 0x52, 0x49, 0x54, 0x00]) + b"\x01\x02" * 20 + b"\x00"
    rsp = board.usb_command(bytes(pkt))
    check("SET_SENS with wrong CRC8 -> 57 46 49, no flash access",
          rsp[:3] == bytes([0x57, 0x46, 0x49]) and not board.flash_ops, rsp[:5].hex())
    board.power_up()
    check("after power cycle: Sensi still the good values", board.ram(0)[:20] == want[0])

    print(f"\n{path.name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
