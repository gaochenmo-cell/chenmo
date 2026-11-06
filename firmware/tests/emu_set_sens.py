#!/usr/bin/env python3
"""
Closed-loop persistence check of SET_SENS on the REAL firmware code of an image:

  power-up (blank config area) -> host SET_SENS type 0/1/2 -> power cycle -> host GET_SENS

What runs from the image (nothing of it is stubbed):
  * the USB command dispatcher (0x08005FAC) and App_Config_Command_Report
  * App_Set_Flash_Cofnig / App_Get_Flash_Cofnig, CRC8 / CRC16
  * the flash driver: unlock, erase, program, lock, the 2 KB read-modify-write in
    flash_write (0x08009D18) and its busy / status polling
  * the whole boot config-load routine (0x08006470): the three page loads, their
    default fall-backs and everything the routine does to the tables afterwards

What is modelled:
  * printf -> no-op
  * the FLASH controller at 0x40022000 with real NOR semantics: writes are rejected
    while LOCK is set or PG is clear, a halfword write can only clear bits (a 1->0
    only AND; trying to set a bit raises PRGMERR), erase (PER+START) fills the 2 KB
    page selected by ADDR with 0xFF. Every store into the flash address range is
    intercepted and replaced by what the controller would really have produced.

  python3 firmware/tests/emu_set_sens.py firmware/PADUEX5_V3.2.bin          # expect ALL PASS
  python3 firmware/tests/emu_set_sens.py firmware/legacy/PADUEX5_V3.1.bin   # control: page written, boot resets to 200
  python3 firmware/tests/emu_set_sens.py firmware/legacy/PADUEX5_V3.0.bin   # control: page never written
  python3 firmware/tests/emu_set_sens.py firmware/legacy/paduex5_1.6.bin    # control: stock behaviour (same as V3.0)
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_HOOK_MEM_WRITE, UC_MODE_THUMB, Uc
from unicorn.arm_const import (UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2,
                               UC_ARM_REG_R3, UC_ARM_REG_SP)

BASE = 0x08004000
FLASH_BASE = 0x08000000
FLASH_SIZE = 0x20000             # AT32F415RBT7: 128 KB
PAGE = 0x800                     # flash_write / erase work on 2 KB pages (addr >> 11)
RAM = 0x20000000
STACK_TOP = 0x20006000
STOP = 0x0800FFF0
SCRATCH = 0x20004000

FLASH_REG = 0x40022000
REG_KEYR, REG_STS, REG_CTRL, REG_ADDR = 0x04, 0x0C, 0x10, 0x14
KEY1, KEY2 = 0x45670123, 0xCDEF89AB
CTRL_PG, CTRL_PER, CTRL_START, CTRL_LOCK = 0x01, 0x02, 0x40, 0x80
STS_BSY, STS_PRGMERR, STS_EPPERR = 0x01, 0x04, 0x10

APPUSER = 0x200019D0
SENSI = APPUSER + 0xFE           # 0x20001ACE
COLOR = APPUSER + 0x146          # 0x20001B16
DTICK = COLOR + 0x48             # 0x20001B5E
CONFIGMODE = 0x20000008
USB_CLASS = SCRATCH + 0x800      # stands in for the USB class struct the dispatcher writes replies into
REPLY = USB_CLASS + 0x11E
MAX_VAL_CHAN = 36
DATA_LEN = 40

PAGES = {0: 0x08010000, 1: 0x08010800, 2: 0x08011000}
RAM_TAB = {0: SENSI, 1: COLOR, 2: DTICK}
NAMES = {0: "Sensi", 1: "Color", 2: "DTick"}

FN_DISPATCH = 0x08005FAC         # (buf, len): USB OUT packet handler
LIT_CTX = 0x080061F8             # dispatcher: AppUser = *(*(ctx+4) + 0x24)
FN_CONFIG_LOAD = 0x08006470      # boot: load the three pages, apply defaults
FN_SET_FLASH = 0x080053F0
FN_FLASH_ERASE = 0x08009CC0
FN_FLASH_WRITE = 0x08009D18
FN_PRINTF = 0x08004390


def crc8(buf: bytes) -> int:
    crc = 0
    for b in buf:
        crc ^= b
        for _ in range(8):
            crc = ((crc >> 1) ^ 0x8C) & 0xFF if crc & 1 else (crc >> 1) & 0xFF
    return crc


class Board:
    """One emulated board: image + flash controller model + RAM."""

    def __init__(self, image: bytes):
        self.image = image
        uc = self.uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
        uc.mem_map(FLASH_BASE, FLASH_SIZE)
        uc.mem_map(RAM, 0x10000)
        uc.mem_map(0x40000000, 0x30000)                       # APB peripherals (FLASH ctrl, UART, ...)
        uc.mem_write(FLASH_BASE, b"\xff" * FLASH_SIZE)
        uc.mem_write(BASE, image)
        self.ctx = struct.unpack_from("<I", image, LIT_CTX - BASE)[0]
        self.locked = True
        self.ctrl = CTRL_LOCK
        self.addr = 0
        self.key_step = 0
        self.flash_ops: list[str] = []
        self.set_flash_calls: list[tuple[int, int, int]] = []
        self.errors: list[str] = []
        self.pending: tuple[int, bytes] | None = None       # (address, bytes the flash really holds now)
        uc.mem_write(FLASH_REG + REG_CTRL, struct.pack("<I", self.ctrl))
        uc.hook_add(UC_HOOK_CODE, self._code_hook)
        uc.hook_add(UC_HOOK_MEM_WRITE, self._periph_write, begin=FLASH_REG, end=FLASH_REG + 0x1C)
        uc.hook_add(UC_HOOK_MEM_WRITE, self._flash_program, begin=FLASH_BASE, end=FLASH_BASE + FLASH_SIZE - 1)

    # ---- controller model -------------------------------------------------------------
    def _set_sts(self, bit: int) -> None:
        v = struct.unpack_from("<I", self.uc.mem_read(FLASH_REG + REG_STS, 4))[0] | bit
        self.uc.mem_write(FLASH_REG + REG_STS, struct.pack("<I", v))

    def _periph_write(self, uc: Uc, access, address, size, value, _):
        reg = address - FLASH_REG
        if reg == REG_KEYR:
            if self.key_step == 0 and value == KEY1:
                self.key_step = 1
            elif self.key_step == 1 and value == KEY2:
                self.locked = False
                self.ctrl &= ~CTRL_LOCK
                self.key_step = 0
                self.flash_ops.append("unlock")
                uc.mem_write(FLASH_REG + REG_CTRL, struct.pack("<I", self.ctrl))
            else:
                self.key_step = 0
                self.errors.append(f"bad unlock key {value:#x}")
        elif reg == REG_ADDR:
            self.addr = value
        elif reg == REG_STS:
            pass                                             # write-1-to-clear flags; let the store land
        elif reg == REG_CTRL:
            if self.locked:
                value = CTRL_LOCK                            # every bit is read-only while locked
            if value & CTRL_LOCK:
                self.locked = True
                value = CTRL_LOCK
                self.flash_ops.append("lock")
            if value & CTRL_START and value & CTRL_PER and not self.locked:
                page = self.addr & ~(PAGE - 1)
                if FLASH_BASE <= page < FLASH_BASE + FLASH_SIZE:
                    uc.mem_write(page, b"\xff" * PAGE)
                    self.flash_ops.append(f"erase {page:#x}")
                else:
                    self.errors.append(f"erase outside flash {page:#x}")
                    self._set_sts(STS_EPPERR)
                value &= ~CTRL_START                         # erase is instantaneous here
            self.ctrl = value
        # the store itself still lands in the mapped register page, which is what reads see
        uc.mem_write(address, struct.pack("<I", self.ctrl if reg == REG_CTRL else value)[:size])

    def _flash_program(self, uc: Uc, access, address, size, value, _):
        # Called before the store lands; the real result is put back by _code_hook before the
        # next instruction runs, so the CPU never sees a value the flash array could not hold.
        old = bytes(uc.mem_read(address, size))
        if self.locked or not (self.ctrl & CTRL_PG):
            self.errors.append(f"write {address:#x} while {'locked' if self.locked else 'PG clear'}")
            self._set_sts(STS_PRGMERR)
            self.pending = (address, old)
            return
        if size != 2:
            self.errors.append(f"{size}-byte program at {address:#x} (halfword expected)")
            self.pending = (address, old)
            return
        cur = struct.unpack("<H", old)[0]
        if value & ~cur:
            self.errors.append(f"program {address:#x}: {cur:#06x} -> {value:#06x} needs 0->1")
            self._set_sts(STS_PRGMERR)
        self.pending = (address, struct.pack("<H", cur & value))
        self.flash_ops.append(f"pgm {address:#x}")

    # ---- firmware hooks ---------------------------------------------------------------
    def _code_hook(self, uc: Uc, addr: int, size: int, _):
        if self.pending is not None:
            uc.mem_write(*self.pending)
            self.pending = None
        if addr == FN_PRINTF:
            uc.reg_write(UC_ARM_REG_R0, 0)
            uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))
        elif addr == FN_SET_FLASH:
            self.set_flash_calls.append(tuple(uc.reg_read(r) for r in (UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2)))

    def call(self, fn: int, *args: int, count: int = 4_000_000) -> None:
        for reg, val in zip((UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3), args):
            self.uc.reg_write(reg, val)
        self.uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
        self.uc.reg_write(UC_ARM_REG_LR, STOP | 1)
        self.uc.emu_start(fn | 1, STOP, count=count)

    def power_up(self, config_mode: int = 0x80) -> None:
        """Reset: RAM wiped, flash kept, boot config-load routine run as the firmware does."""
        self.uc.mem_write(RAM, b"\x00" * 0x4000)
        self.uc.mem_write(CONFIGMODE, bytes([config_mode]))
        inner = SCRATCH + 0x200
        self.uc.mem_write(self.ctx + 4, struct.pack("<I", inner))
        self.uc.mem_write(inner + 0x24, struct.pack("<I", USB_CLASS))
        self.locked, self.ctrl, self.key_step = True, CTRL_LOCK, 0
        self.uc.mem_write(FLASH_REG + REG_CTRL, struct.pack("<I", CTRL_LOCK))
        self.uc.mem_write(FLASH_REG + REG_STS, b"\0\0\0\0")
        self.call(FN_CONFIG_LOAD)

    def ram(self, t: int) -> list[int]:
        return list(struct.unpack("<36H", self.uc.mem_read(RAM_TAB[t], MAX_VAL_CHAN * 2)))

    def page(self, t: int) -> bytes:
        return bytes(self.uc.mem_read(PAGES[t], 2 + MAX_VAL_CHAN * 2 + 2))

    def usb_command(self, pkt: bytes) -> bytes:
        """Deliver one 64-byte OUT report to the real dispatcher, return the reply buffer."""
        self.uc.mem_write(SCRATCH, pkt.ljust(64, b"\0"))
        self.uc.mem_write(REPLY, b"\0" * 64)
        self.call(FN_DISPATCH, SCRATCH, 64)
        return bytes(self.uc.mem_read(REPLY, 64))


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
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/PADUEX5_V3.2.bin")
    img = path.read_bytes()
    assert len(img) == 37072 and struct.unpack_from("<I", img, 0x14C)[0] == 0x080084A5
    fails = 0

    def check(name: str, ok: bool, detail: str = "") -> None:
        nonlocal fails
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}{('  ' + detail) if detail else ''}")

    board = Board(img)

    # 1) first power-up on a blank config area: the board's own defaults
    board.power_up()
    check("blank flash: boot Sensi == 200 (board default)", board.ram(0)[:20] == [200] * 20, str(board.ram(0)[:4]))
    check("blank flash: boot DTick == 3", board.ram(2)[:20] == [3] * 20, str(board.ram(2)[:4]))
    check("blank flash: boot Color == default colour table",
          board.ram(1)[:20] == [5] * 8 + [3] * 4 + [4] * 8, str(board.ram(1)[:20]))
    check("GET_SENS(Sensi) via dispatcher returns the boot values", host_get(board, 0x00) == [200] * 20)
    check("no flash access during boot", not board.flash_ops and not board.errors, "; ".join(board.flash_ops + board.errors))

    # 2) write all three tables through the real dispatcher
    want = {
        0: [300 + 7 * i for i in range(20)],
        1: [v for key in ([1, 0, 1, 1], [4, 0, 4, 4], [7, 0, 7, 7], [1, 0, 1, 1], [4, 0, 4, 4]) for v in key],
        2: [10 + i for i in range(20)],
    }
    for t in (0, 1, 2):
        board.flash_ops.clear(); board.set_flash_calls.clear(); board.errors.clear()
        rsp = host_set(board, t, want[t])
        check(f"SET_SENS({NAMES[t]}) ACK 57 4F 4B", rsp[:3] == bytes([0x57, 0x4F, 0x4B]) and rsp[4] == t, rsp[:5].hex())
        check(f"SET_SENS({NAMES[t]}) RAM updated", board.ram(t)[:20] == want[t], str(board.ram(t)[:4]))
        check(f"SET_SENS({NAMES[t]}) calls App_Set_Flash_Cofnig({PAGES[t]:#x}, {NAMES[t]}, 36)",
              board.set_flash_calls == [(PAGES[t], RAM_TAB[t], MAX_VAL_CHAN)],
              str([(hex(a), hex(s), n) for a, s, n in board.set_flash_calls]) or "no call")
        ops = board.flash_ops
        pgm = [o for o in ops if o.startswith("pgm")]
        check(f"SET_SENS({NAMES[t]}) unlock -> erase {PAGES[t]:#x} -> program -> lock, no controller errors",
              "unlock" in ops and f"erase {PAGES[t]:#x}" in ops and len(pgm) == 128 and ops[-1] == "lock"
              and not board.errors and ops.index(f"erase {PAGES[t]:#x}") < ops.index(pgm[0]) if pgm else False,
              (f"{len(pgm)} programs, ops={ops[:3]}..{ops[-2:]}, errors={board.errors[:2]}") if ops else "no flash access")
        pg = board.page(t)
        check(f"page {PAGES[t]:#x}: A55A + data + CRC16 as App_Get_Flash_Cofnig expects",
              pg[:2] == b"\x5a\xa5" and list(struct.unpack("<20H", pg[2:42])) == want[t], pg[:6].hex())
        check(f"GET_SENS({NAMES[t]}) immediately reads back", host_get(board, t) == want[t])

    # 3) unknown type: RAM only, flash untouched
    board.flash_ops.clear(); board.set_flash_calls.clear()
    rsp = host_set(board, 0x03, [1] * 20)
    check("SET_SENS(type 3) ACK, no flash access", rsp[:3] == bytes([0x57, 0x4F, 0x4B]) and not board.flash_ops
          and not board.set_flash_calls, "; ".join(board.flash_ops))
    host_set(board, 0x00, want[0])                   # type 3 aliases the Sensi table: restore it

    # 4) power cycle: real boot routine on the flash we just wrote
    board.power_up()
    for t in (0, 1, 2):
        check(f"after power cycle: {NAMES[t]} RAM == saved values", board.ram(t)[:20] == want[t], str(board.ram(t)[:4]))
        check(f"after power cycle: GET_SENS({NAMES[t]}) == saved values", host_get(board, t) == want[t],
              str((host_get(board, t) or [])[:4]))

    # 5) a second power cycle must not change anything either (page stays valid, nothing rewrites it)
    board.flash_ops.clear()
    board.power_up()
    check("second power cycle: Sensi still the saved values", board.ram(0)[:20] == want[0], str(board.ram(0)[:4]))
    check("second power cycle: no flash writes at boot", not board.flash_ops, "; ".join(board.flash_ops))

    # 6) bad CRC8 from the host -> 57 46 49, nothing written
    board.flash_ops.clear()
    rsp = board.usb_command(bytes([0x57, 0x52, 0x49, 0x54, 0x00]) + b"\x01\x02" * 20 + b"\x00")
    check("SET_SENS with wrong CRC8 -> 57 46 49, no flash access",
          rsp[:3] == bytes([0x57, 0x46, 0x49]) and not board.flash_ops, rsp[:5].hex())
    board.power_up()
    check("after that: Sensi still the saved values", board.ram(0)[:20] == want[0], str(board.ram(0)[:4]))

    # 7) re-save over an already written page (erase-before-program path of flash_write)
    board.flash_ops.clear(); board.errors.clear()
    new = [450 - 5 * i for i in range(20)]
    host_set(board, 0x00, new)
    check("re-save Sensi over a valid page: erased again, no 0->1 programming errors",
          f"erase {PAGES[0]:#x}" in board.flash_ops and not board.errors, "; ".join(board.errors[:2]))
    board.power_up()
    check("after power cycle: Sensi == re-saved values", board.ram(0)[:20] == new, str(board.ram(0)[:4]))

    print(f"\n{path.name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
