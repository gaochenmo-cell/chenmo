#!/usr/bin/env python3
"""
Emulate the REAL key scan of a PADUEX5 image (0x080069CC: new-sample detect ->
press/release per sensor -> SetKeyBit -> dispatch Report_Normal / HID / LED) with
Unicorn and check which key bits end up in AppUser.KeyCode and which KeyCode is
handed to the USB report builders.

Used to validate PADUEX4 images (fifth key never reported) and to show that
keys 0..3 behave exactly as on the 5-key image.

  python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX4_V3.1.bin     # expect 4-key behaviour
  python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX5_V3.1.bin    # control: K5 must still report

Only printf, the USB packet senders and the LED driver are stubbed (they touch
peripherals); the scan loop, SetKeyBit, the debounce helper and the dispatcher
are the bytes from the .bin.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_MODE_THUMB, Uc
from unicorn.arm_const import UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_SP

BASE = 0x08004000
RAM = 0x20000000
STACK_TOP = 0x20006000
STOP = 0x0800FFF0

APPUSER = 0x200019D0
RAW = APPUSER + 0x26          # u16[36] last accepted ADC sample  (0x200019F6)
PREV = APPUSER + 0x6E         # u16[36] previous sample
HOLD = APPUSER + 0xB6         # u16[9]  per-key release hold counter
SENSI = APPUSER + 0xFE        # u16[36] thresholds
DTICK = APPUSER + 0x18E       # u16[36] debounce ticks (FW uses [key])
KEYCODE = APPUSER + 0x1D6     # u16 current key bitmask
LASTKEY = APPUSER + 0x1D8
CALIB = APPUSER + 0x1DA       # >0 = auto-calibration print mode (must be 0)
ADC_NEW = 0x20001BAE          # u16[36] fresh ADC buffer compared against RAW
CONFIGMODE = 0x20000008

FN_SCAN = 0x080069CC
FN_PRINTF = 0x08004390
FN_REPORT_NORMAL = 0x08005D48   # keyboard
FN_REPORT_HID = 0x08005C20      # gamepad
FN_LED = 0x08005018
FN_DBG_DUMP = 0x080065EC
STUBS = (FN_PRINTF, FN_REPORT_NORMAL, FN_REPORT_HID, FN_LED, FN_DBG_DUMP)

THRESHOLD = 200
PRESSED = 1000


class Board:
    def __init__(self, image: bytes, configmode: int = 0x10):
        self.uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
        self.uc.mem_map(0x08000000, 0x20000)
        self.uc.mem_map(RAM, 0x10000)
        self.uc.mem_write(BASE, image)
        self.uc.mem_write(CONFIGMODE, bytes([configmode]))
        self.uc.mem_write(SENSI, b"".join(struct.pack("<H", THRESHOLD) for _ in range(36)))
        self.uc.mem_write(DTICK, b"\x00" * 72)          # no release hold -> release in the same scan
        self.reports: list[tuple[str, int]] = []
        self.uc.hook_add(UC_HOOK_CODE, self._hook)

    def _hook(self, uc: Uc, addr: int, size: int, _):
        if addr in STUBS:
            if addr == FN_REPORT_NORMAL:
                self.reports.append(("KBD", uc.reg_read(UC_ARM_REG_R0)))
            elif addr == FN_REPORT_HID:
                self.reports.append(("HID", uc.reg_read(UC_ARM_REG_R0)))
            elif addr == FN_LED:
                self.reports.append(("LED", uc.reg_read(UC_ARM_REG_R0)))
            uc.reg_write(UC_ARM_REG_R0, 0)
            uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))

    def scan(self, pressed_sensors: set[int]) -> int:
        """One main-loop scan with the given sensors above threshold. Returns KeyCode."""
        adc = [PRESSED if i in pressed_sensors else 0 for i in range(36)]
        self.uc.mem_write(ADC_NEW, b"".join(struct.pack("<H", v) for v in adc))
        self.uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
        self.uc.reg_write(UC_ARM_REG_LR, STOP | 1)
        self.uc.emu_start(FN_SCAN | 1, STOP, count=200000)
        return self.keycode()

    def keycode(self) -> int:
        return struct.unpack("<H", self.uc.mem_read(KEYCODE, 2))[0]


def main() -> int:
    img_path = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/PADUEX4_V3.1.bin")
    img = img_path.read_bytes()
    four_key = "paduex4" in img_path.name.lower() or "4key" in img_path.name.lower()
    fails = 0

    def check(name: str, got, want) -> None:
        nonlocal fails
        ok = got == want
        fails += 0 if ok else 1
        fmt = hex if isinstance(got, int) else str
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: got {fmt(got)} want {fmt(want)}")

    def expect_bit(key: int) -> int:
        return 0 if (four_key and key >= 4) else (1 << key)

    # 1) each sensor alone (keys 0..4): press, then release
    for s in range(20):
        key = s // 4
        b = Board(img)
        check(f"sensor {s:2d} (K{key + 1}) press", b.scan({s}), expect_bit(key))
        check(f"sensor {s:2d} (K{key + 1}) release", b.scan(set()), 0)

    # 2) chords including the fifth key
    b = Board(img)
    check("chord K1+K5 (sensors 0,16)", b.scan({0, 16}), 0x01 | expect_bit(4))
    check("chord K1+K5 release K1 only", b.scan({16}), expect_bit(4))
    check("chord K1+K5 release all", b.scan(set()), 0)
    b = Board(img)
    check("all 20 sensors", b.scan(set(range(20))), 0x0F | expect_bit(4))
    b = Board(img)
    check("K2+K3+K4 (sensors 5,10,15)", b.scan({5, 10, 15}), 0x0E)

    # 3) dispatch: what the USB report builders actually receive
    for mode, tag in ((0x10, "KBD"), (0x11, "HID")):
        b = Board(img, configmode=mode)
        b.scan({17})                       # K5 sensor
        got = [k for t, k in b.reports if t == tag]
        check(f"{tag} report after K5 press", got, [] if four_key else [0x10])
        b.reports.clear()
        b.scan({17, 2})                    # K5 + K1 -> only K1 must change the report
        got = [k for t, k in b.reports if t == tag]
        check(f"{tag} report after K5+K1 press", got, [0x01] if four_key else [0x11])
        leds = [k for t, k in b.reports if t == "LED"]
        check(f"LED update after K5+K1 ({tag} mode)", leds, [0x01] if four_key else [0x11])

    # 4) keys 0..3 are untouched: same KeyCode sequence as a stock scan on any image
    b = Board(img)
    seq = [b.scan({0}), b.scan({0, 4}), b.scan({4}), b.scan({4, 8, 12}), b.scan(set())]
    check("K1..K4 sequence", seq, [0x1, 0x3, 0x2, 0xE, 0x0])

    print(f"\n{img_path.name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}"
          f"  (expectations: {'4-key, K5 never reported' if four_key else '5-key control, K5 reported'})")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
