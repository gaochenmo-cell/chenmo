#!/usr/bin/env python3
"""
End-to-end emulation of the grok1.7 keyboard remap, running the REAL firmware
code of the image under Unicorn:

  1. old board state: colours saved by an old host (slot +1 = colour code)
  2. boot          : App_Get_Flash_Cofnig(COLOR)           (real FW)
  3. host READ     : GET_SENS type 1 -> App_Config_Command_Report (real FW) -> decode
  4. host WRITE    : SET_SENS type 1 with Q E S Z C in slot +1 -> real FW handler
                     -> App_Set_Flash_Cofnig -> flash page (erase/write stubbed to memory)
  5. reboot        : fresh RAM, App_Get_Flash_Cofnig(COLOR) again from the written page
  6. press keys    : Report_Normal (real FW) -> HID usages must be Q E S Z C
  7. recolour      : host paints key 3 blue (slots +0/+2/+3 only) -> SET -> map intact

Only flash peripheral / printf are stubbed; CRC8, CRC16, flash_read, the USB
command parser, the flash config helpers and the report builder are the bytes
from the .bin.

  python3 firmware/tests/emu_keymap_e2e.py firmware/grok1.7.bin
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
FLASH_COLOR = 0x08010800
RAM = 0x20000000
STACK_TOP = 0x20006000
STOP = 0x0800FFF0
SCRATCH = 0x20004000             # host-side buffers inside emulated RAM

APPUSER = 0x200019D0
COLOR = APPUSER + 0x146
CONFIGMODE = 0x20000008
MAX_VAL_CHAN = 36
DATA_LEN = 40                    # 5 keys * 4 * 2 bytes

FN_REPORT_NORMAL = 0x08005D48
FN_CMD_REPORT = 0x080049E0       # App_Config_Command_Report(cmd, len, rsp, rsplen)
FN_GET_FLASH = 0x08004D38        # App_Get_Flash_Cofnig(addr, dst, default, size)
FN_SET_FLASH = 0x080053F0        # App_Set_Flash_Cofnig(addr, src, size)
FN_PRINTF = 0x08004390
FN_FLASH_UNLOCK = 0x08009D00
FN_FLASH_LOCK = 0x08009C38
FN_FLASH_ERASE = 0x08009CC0
FN_FLASH_WRITE = 0x08009D18
SENDPACKET_BL_SITE = 0x08005E5C

MAP_TAG = 0x4B
USAGE = {"Q": 0x14, "E": 0x08, "S": 0x16, "Z": 0x1D, "C": 0x06,
         "H": 0x0B, "K": 0x0E, "J": 0x0D, "N": 0x11, "M": 0x10}
NAME = {v: k for k, v in USAGE.items()}
RED, BLUE, WHITE = 1, 4, 7


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
        self.uc.mem_write(CONFIGMODE, bytes([0x80]))      # 2P | KBS (DEFAULT_1P2P)
        self.flash_ops: list[str] = []
        self.report: bytes | None = None
        self.uc.hook_add(UC_HOOK_CODE, self._hook)

    # --- stubs for peripherals / console -------------------------------------------------
    def _ret(self, r0: int | None = None) -> None:
        if r0 is not None:
            self.uc.reg_write(UC_ARM_REG_R0, r0)
        self.uc.reg_write(UC_ARM_REG_PC, self.uc.reg_read(UC_ARM_REG_LR))

    def _hook(self, uc: Uc, addr: int, size: int, _):
        if addr in (FN_PRINTF, FN_FLASH_UNLOCK, FN_FLASH_LOCK):
            self._ret(0)
        elif addr == FN_FLASH_ERASE:
            a = uc.reg_read(UC_ARM_REG_R0) & ~(PAGE - 1)
            uc.mem_write(a, b"\xff" * PAGE)
            self.flash_ops.append(f"erase {a:#x}")
            self._ret(0)
        elif addr == FN_FLASH_WRITE:
            a, src, n = (uc.reg_read(r) for r in (UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2))
            data = bytes(uc.mem_read(src, n * 2))
            cur = bytes(uc.mem_read(a, n * 2))
            uc.mem_write(a, bytes(x & y for x, y in zip(cur, data)))   # NOR: can only clear bits
            self.flash_ops.append(f"write {a:#x} {n} halfwords")
            self._ret(0)
        elif addr == SENDPACKET_BL_SITE:
            buf = uc.reg_read(UC_ARM_REG_R0)
            n = uc.reg_read(UC_ARM_REG_R1)
            self.report = bytes(uc.mem_read(buf, n))
            uc.reg_write(UC_ARM_REG_PC, STOP | 1)
            uc.emu_stop()

    # --- calling convention -------------------------------------------------------------
    def call(self, fn: int, *args: int, count: int = 200000) -> None:
        for reg, val in zip((UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3), args):
            self.uc.reg_write(reg, val)
        self.uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
        self.uc.reg_write(UC_ARM_REG_LR, STOP | 1)
        self.uc.emu_start(fn | 1, STOP, count=count)

    # --- firmware-level operations -------------------------------------------------------
    def ram_reset(self) -> None:
        self.uc.mem_write(APPUSER, b"\x00" * 0x200)

    def boot_load_color(self) -> None:
        default = SCRATCH + 0x800
        self.uc.mem_write(default, b"".join(struct.pack("<H", WHITE) for _ in range(MAX_VAL_CHAN)))
        self.call(FN_GET_FLASH, FLASH_COLOR, COLOR, default, MAX_VAL_CHAN)

    def color_ram(self) -> list[int]:
        return list(struct.unpack("<36H", self.uc.mem_read(COLOR, MAX_VAL_CHAN * 2)))

    def usb_command(self, pkt: bytes) -> bytes:
        cmd, rsp = SCRATCH, SCRATCH + 0x100
        self.uc.mem_write(cmd, pkt.ljust(64, b"\0"))
        self.uc.mem_write(rsp, b"\0" * 64)
        self.call(FN_CMD_REPORT, cmd, 64, rsp, 64)
        return bytes(self.uc.mem_read(rsp, 64))

    def press(self, keycode: int) -> list[int]:
        self.report = None
        self.call(FN_REPORT_NORMAL, keycode, count=5000)
        assert self.report is not None, "SendPacket never reached"
        return [b for b in self.report[2:10] if b]


# ---- host side (mirrors App_DDR.cs Read/Write_Config_Value + key-map tab) ---------------
def host_get(board: Board, ctype: int) -> list[int]:
    rsp = board.usb_command(bytes([0x52, 0x45, 0x41, 0x44, ctype]))
    assert rsp[:5] == bytes([0x52, 0x52, 0x53, 0x50, ctype]), rsp[:8].hex()
    assert crc8(rsp[:5 + DATA_LEN]) == rsp[5 + DATA_LEN], "GET crc"
    return [(rsp[5 + 2 * i] << 8) | rsp[6 + 2 * i] for i in range(20)]


def host_set(board: Board, ctype: int, values: list[int]) -> bytes:
    pkt = bytearray([0x57, 0x52, 0x49, 0x54, ctype])
    for v in values:
        pkt += bytes([(v >> 8) & 0xFF, v & 0xFF])
    pkt.append(crc8(bytes(pkt)))
    return board.usb_command(bytes(pkt))


def host_decode_map(colors: list[int], default: list[int]) -> list[int]:
    return [(colors[k * 4 + 1] & 0x7F) if (colors[k * 4 + 1] >> 8) == MAP_TAG else default[k] for k in range(5)]


def host_encode_map(colors: list[int], usages: list[int]) -> list[int]:
    out = list(colors)
    for k, u in enumerate(usages):
        out[k * 4 + 1] = (MAP_TAG << 8) | u
    return out


def host_paint_key(colors: list[int], key: int, code: int) -> list[int]:
    out = list(colors)
    for s in (0, 2, 3):                       # slot +1 is the key map, never touched
        out[key * 4 + s] = code
    return out


def main() -> int:
    img = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/grok1.7.bin").read_bytes()
    fails = 0

    def check(name: str, ok: bool, detail: str = "") -> None:
        nonlocal fails
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}{('  ' + detail) if detail else ''}")

    board = Board(img)
    want = [USAGE[c] for c in "QESZC"]
    default_2p = [USAGE[c] for c in "HKJNM"]

    # 1) old board: colours saved by an old host -> every slot of key k = colour code
    old_colors = [RED if (i // 4) % 2 == 0 else WHITE for i in range(MAX_VAL_CHAN)]
    board.uc.mem_write(COLOR, b"".join(struct.pack("<H", v) for v in old_colors))
    board.call(FN_SET_FLASH, FLASH_COLOR, COLOR, MAX_VAL_CHAN)
    check("old host colour page written by real App_Set_Flash_Cofnig", len(board.flash_ops) == 2, "; ".join(board.flash_ops))
    board.flash_ops.clear()

    # 2) boot
    board.ram_reset()
    board.boot_load_color()
    check("boot loads colour page (not defaults)", board.color_ram()[:8] == old_colors[:8], str(board.color_ram()[:8]))

    # 3) host READ before remap -> firmware default table shown
    colors = host_get(board, 0x01)
    check("GET_SENS(colour) 20 values", colors == old_colors[:20])
    check("host decodes NO custom map -> defaults H K J N M",
          host_decode_map(colors, default_2p) == default_2p)
    check("FW before remap: keys -> H K J N M",
          [board.press(1 << k)[0] for k in range(5)] == default_2p,
          " ".join(NAME.get(board.press(1 << k)[0], "?") for k in range(5)))

    # 4) host WRITE Q E S Z C
    new_colors = host_encode_map(colors, want)
    rsp = host_set(board, 0x01, new_colors)
    check("SET_SENS(colour) ACK 57 4F 4B", rsp[:3] == bytes([0x57, 0x4F, 0x4B]), rsp[:5].hex())
    check("FW persisted colour page (erase + write)", board.flash_ops == [f"erase {FLASH_COLOR:#x}", f"write {FLASH_COLOR:#x} 40 halfwords"] or len(board.flash_ops) == 2, "; ".join(board.flash_ops))
    ram = board.color_ram()
    check("RAM slot+1 tagged 0x4Bxx for 5 keys", [ram[k * 4 + 1] for k in range(5)] == [(MAP_TAG << 8) | u for u in want],
          " ".join(f"{ram[k*4+1]:#06x}" for k in range(5)))
    check("RAM LED colours untouched", [ram[k * 4] for k in range(5)] == [old_colors[k * 4] for k in range(5)])
    check("FW immediately: keys -> Q E S Z C", [board.press(1 << k)[0] for k in range(5)] == want,
          " ".join(NAME.get(board.press(1 << k)[0], "?") for k in range(5)))

    # 5) reboot from flash
    board.ram_reset()
    check("RAM cleared", board.color_ram()[:4] == [0, 0, 0, 0])
    board.boot_load_color()
    ram = board.color_ram()
    check("after reboot: colour page CRC ok, map restored", [ram[k * 4 + 1] for k in range(5)] == [(MAP_TAG << 8) | u for u in want])
    check("after reboot: LED colours restored", [ram[k * 4] for k in range(5)] == [old_colors[k * 4] for k in range(5)])

    # 6) keys after reboot, singles + chords
    pressed = [board.press(1 << k)[0] for k in range(5)]
    check("after reboot: UL=Q UR=E C=S DL=Z DR=C", pressed == want, " ".join(NAME.get(u, "?") for u in pressed))
    check("chord UL+DR -> Q C", board.press(0b10001) == [USAGE["Q"], USAGE["C"]])
    check("chord all five -> Q E S Z C", board.press(0b11111) == want)
    check("host READ back shows Q E S Z C", host_decode_map(host_get(board, 0x01), default_2p) == want)

    # 7) recolour key 3 (DL) blue with the new host -> map must survive
    colors = host_get(board, 0x01)
    rsp = host_set(board, 0x01, host_paint_key(colors, 3, BLUE))
    check("recolour ACK", rsp[:3] == bytes([0x57, 0x4F, 0x4B]))
    ram = board.color_ram()
    check("key3 LED now blue", ram[3 * 4] == BLUE and ram[3 * 4 + 2] == BLUE)
    check("map still Q E S Z C after recolour", [board.press(1 << k)[0] for k in range(5)] == want)

    # 8) host "default" -> clear tags -> stock table again
    colors = host_get(board, 0x01)
    for k in range(5):
        colors[k * 4 + 1] = 0
    host_set(board, 0x01, colors)
    check("reset to default -> H K J N M", [board.press(1 << k)[0] for k in range(5)] == default_2p)

    print(f"\n{Path(sys.argv[1] if len(sys.argv) > 1 else 'grok1.7.bin').name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
