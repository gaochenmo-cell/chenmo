#!/usr/bin/env python3
"""
Emulate the REAL sampler (0x08006E48), key scan (0x080069CC) and LED refresh
(0x08005018) of a 5-key image with Unicorn, with a bit-level I2C slave model on
each pad port's SCL/SDA pair, and check where key 5 reads its sensors and clocks
its LED strip.

  python3 firmware/tests/emu_port6_remap.py firmware/PADUEX5_P6_V3.5.bin   # key 5 on port 6
  python3 firmware/tests/emu_port6_remap.py firmware/PADUEX5_P9_V3.5.bin   # key 5 on port 9 (split bus)
  python3 firmware/tests/emu_port6_remap.py firmware/PADUEX5_V3.2.bin      # control: key 5 on port 5

Only the internal ADC read (0x080078B4), printf and the two USB report builders
are stubbed. The I2C bit-banging runs the bytes from the .bin against the slave
model (START/STOP detection, address ACK, register pointer, 16-bit big-endian
read, master ACK/NACK), the soft-float conversion runs as is, and the WS2812 bit
routines are executed while every GPIO set/clear write is recorded.

The Px images probe 8-bit addresses 0x80..0x9E on the first sample and keep the
first four that ACK. The checks below cover the stock address set, a set that
shares no address with port 5, and a bus where nothing ACKs (key 5 must read as
released, and ports 1..4 must keep working).
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from unicorn import UC_ARCH_ARM, UC_HOOK_CODE, UC_HOOK_MEM_READ, UC_HOOK_MEM_WRITE, UC_MODE_THUMB, Uc
from unicorn.arm_const import UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_R0, UC_ARM_REG_SP

BASE = 0x08004000
RAM = 0x20000000
PERIPH = 0x40000000
STACK_TOP = 0x20006000
STOP = 0x0800FFF0

GPIOA, GPIOB, GPIOC, GPIOF = 0x40010800, 0x40010C00, 0x40011000, 0x40011C00
GPIO_HOOK_END = GPIOF + 0x400
CRM_APB2EN = 0x40021018
# one bus per pad port. Port 5 is the pair the stock sampler drives; 6..9 follow
# the init order of the remaining open-drain pairs (low pin = SCL).
PORT_PINS = {
    5: ((GPIOC, 11), (GPIOC, 12)),
    6: ((GPIOB, 3), (GPIOB, 4)),
    7: ((GPIOF, 6), (GPIOF, 7)),
    8: ((GPIOB, 10), (GPIOB, 11)),
    9: ((GPIOB, 2), (GPIOA, 10)),
}
# key-5 LED strip per image, and the CRM APB2 bit the probe must turn on
PROFILES = {
    "V3.2": dict(port=5, led=(GPIOC, 8), clocks=(), probe=False),
    "P6": dict(port=6, led=(GPIOC, 9), clocks=(3,), probe=True),
    "P7": dict(port=7, led=(GPIOA, 8), clocks=(7,), probe=True),
    "P8": dict(port=8, led=(GPIOB, 13), clocks=(3,), probe=True),
    "P9": dict(port=9, led=(GPIOB, 12), clocks=(3, 2), probe=True),
}

APPUSER = 0x200019D0
RAW = APPUSER + 0x26          # u16[36]
SENSI = APPUSER + 0xFE        # u16[36]
DTICK = APPUSER + 0x18E       # u16[36]
KEYCODE = APPUSER + 0x1D6
CALIB = APPUSER + 0x1DA
COLOR = 0x20001B16            # u16[36], LED colour = Color[key*4]
ADC_NEW = 0x20001BAE          # u16[36] written by the sampler
SAMPLER_ARG = 0x20001BAC      # r0 main passes to the sampler (ADC_NEW - 2)
CONFIGMODE = 0x20000008

FN_SAMPLE = 0x08006E48
FN_SCAN = 0x080069CC
FN_LED = 0x08005018
FN_ADC_READ = 0x080078B4
FN_PRINTF = 0x08004390
FN_REPORT_NORMAL = 0x08005D48
FN_REPORT_HID = 0x08005C20
FN_DBG_DUMP = 0x080065EC

THRESHOLD = 200
# sampler: ADC_NEW[16..19] = 3915 - min(raw * 0.625, 3915); internal: 0xFFF - adc
I2C_PRESSED = 0x0000          # -> 3915
I2C_RELEASED = 0x1F00         # -> 0
ADC_PRESSED = 0x000           # -> 4095
ADC_RELEASED = 0xFFF          # -> 0
I2C_ADDRS = (0x40, 0x44, 0x43, 0x42)   # 7-bit, in RAW[16..19] order (0x80, 0x88, 0x86, 0x84 on the wire)
REG_BUS_VOLTAGE = 2


class I2CSlaveBus:
    """Open-drain I2C slaves with an 8-bit register pointer and 16-bit big-endian registers."""

    def __init__(self, name: str, scl: tuple[int, int], sda: tuple[int, int], devices: dict[int, dict[int, int]]):
        self.name, self.scl, self.sda, self.devices = name, scl, sda, devices
        self.m_scl = self.m_sda = self.s_sda = 1
        self.prev_scl = self.prev_line = 1
        self.phase, self.clk, self.shift = "idle", 0, 0
        self.addr, self.rw, self.reg = None, 0, 0
        self.ack_pending = self.master_ack = False
        self.tx: list[int] = []
        self.txbyte = 0
        self.log: list[str] = []

    def line(self) -> int:
        return self.m_sda & self.s_sda

    def on_write(self, port: int, mask: int, level: int) -> None:
        hit = False
        if port == self.scl[0] and mask & self.scl[1]:
            self.m_scl, hit = level, True
        if port == self.sda[0] and mask & self.sda[1]:
            self.m_sda, hit = level, True
        if hit:
            self.step()

    def idt(self, port: int) -> int:
        v = 0
        if port == self.scl[0] and self.m_scl:
            v |= self.scl[1]
        if port == self.sda[0] and self.line():
            v |= self.sda[1]
        return v

    def step(self) -> None:
        scl, line = self.m_scl, self.line()
        if scl and self.prev_scl:
            if self.prev_line and not line:                     # START
                self.phase, self.clk, self.shift, self.s_sda, self.tx = "addr", 0, 0, 1, []
                self.log.append("S")
            elif not self.prev_line and line:                   # STOP
                self.phase, self.s_sda = "idle", 1
                self.log.append("P")
        elif scl and not self.prev_scl and self.phase != "idle":  # rising edge: sample
            self.clk += 1
            if self.phase in ("addr", "write"):
                if self.clk <= 8:
                    self.shift = ((self.shift << 1) | line) & 0xFF
                    if self.clk == 8:
                        self.byte_done()
            elif self.phase == "read" and self.clk == 9:
                self.master_ack = line == 0
        elif not scl and self.prev_scl:                         # falling edge: drive
            if self.phase in ("addr", "write"):
                if self.clk == 8:
                    self.s_sda = 0 if self.ack_pending else 1
                elif self.clk == 9:
                    self.s_sda, self.clk = 1, 0
                    if not self.ack_pending:
                        self.phase = "idle"
                    elif self.phase == "addr":
                        if self.rw:
                            self.phase = "read"
                            self.load_tx()
                            self.out_bit()
                        else:
                            self.phase = "write"
            elif self.phase == "read":
                if self.clk < 8:
                    self.out_bit()
                elif self.clk == 8:
                    self.s_sda = 1                              # release for master ACK/NACK
                elif self.clk == 9:
                    self.clk = 0
                    if self.master_ack:
                        self.load_tx()
                        self.out_bit()
                    else:
                        self.phase, self.s_sda = "idle", 1
        self.prev_scl, self.prev_line = scl, self.line()

    def byte_done(self) -> None:
        b = self.shift
        if self.phase == "addr":
            a7, self.rw = b >> 1, b & 1
            self.ack_pending = a7 in self.devices
            self.addr = a7 if self.ack_pending else None
            self.log.append(f"{b:02X}{'r' if self.rw else 'w'}{'' if self.ack_pending else '!'}")
        else:
            self.reg, self.ack_pending = b, True
            self.log.append(f"reg{b}")

    def load_tx(self) -> None:
        if not self.tx:
            val = self.devices.get(self.addr, {}).get(self.reg, 0xFFFF)
            self.tx = [(val >> 8) & 0xFF, val & 0xFF]
        self.txbyte = self.tx.pop(0)

    def out_bit(self) -> None:
        self.s_sda = (self.txbyte >> 7) & 1
        self.txbyte = (self.txbyte << 1) & 0xFF


class Board:
    def __init__(self, image: bytes, configmode: int = 0x10):
        self.uc = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
        self.uc.mem_map(0x08000000, 0x20000)
        self.uc.mem_map(RAM, 0x10000)
        self.uc.mem_map(PERIPH, 0x30000)
        self.uc.mem_write(BASE, image)
        self.uc.mem_write(CONFIGMODE, bytes([configmode]))
        self.uc.mem_write(SENSI, struct.pack("<36H", *([THRESHOLD] * 36)))
        self.uc.mem_write(DTICK, b"\x00" * 72)
        self.uc.mem_write(CALIB, b"\x00")
        self.uc.mem_write(COLOR, struct.pack("<36H", *([1] * 36)))      # every key red
        self.uc.mem_write(0x20001CC4 + 0xD, bytes([0x40]))             # LED brightness byte
        self.buses_by_port = {}
        for port, ((sp, sb), (dp, db)) in PORT_PINS.items():
            self.buses_by_port[port] = I2CSlaveBus(
                f"port{port}", (sp, 1 << sb), (dp, 1 << db),
                {a: {REG_BUS_VOLTAGE: I2C_RELEASED} for a in I2C_ADDRS})
        self.buses = tuple(self.buses_by_port.values())
        self.adc = [ADC_RELEASED] * 16
        self.reports: list[tuple[str, int]] = []
        self.gpio_toggles: dict[tuple[int, int], int] = {}
        self.uc.hook_add(UC_HOOK_CODE, self._code)
        self.uc.hook_add(UC_HOOK_MEM_WRITE, self._mem_write, begin=GPIOA, end=GPIO_HOOK_END)
        self.uc.hook_add(UC_HOOK_MEM_READ, self._mem_read, begin=GPIOA, end=GPIO_HOOK_END)

    # -- stubs ---------------------------------------------------------------------
    def _code(self, uc: Uc, addr: int, size: int, _) -> None:
        if addr == FN_ADC_READ:
            uc.reg_write(UC_ARM_REG_R0, self.adc[uc.reg_read(UC_ARM_REG_R0) & 0xF])
        elif addr == FN_REPORT_NORMAL:
            self.reports.append(("KBD", uc.reg_read(UC_ARM_REG_R0)))
            uc.reg_write(UC_ARM_REG_R0, 0)
        elif addr == FN_REPORT_HID:
            self.reports.append(("HID", uc.reg_read(UC_ARM_REG_R0)))
            uc.reg_write(UC_ARM_REG_R0, 0)
        elif addr in (FN_PRINTF, FN_DBG_DUMP):
            uc.reg_write(UC_ARM_REG_R0, 0)
        else:
            return
        uc.reg_write(UC_ARM_REG_PC, uc.reg_read(UC_ARM_REG_LR))

    # -- GPIO model ----------------------------------------------------------------
    def _mem_write(self, uc: Uc, access, addr: int, size: int, value: int, _) -> None:
        port, reg = addr & ~0x3FF, addr & 0x3FF
        if reg in (0x10, 0x14):                                  # SCR / CLR
            level = 1 if reg == 0x10 else 0
            for bit in range(16):
                if value >> bit & 1:
                    self.gpio_toggles[(port, bit)] = self.gpio_toggles.get((port, bit), 0) + 1
            for bus in self.buses:
                bus.on_write(port, value, level)

    def _mem_read(self, uc: Uc, access, addr: int, size: int, value: int, _) -> None:
        port, reg = addr & ~0x3FF, addr & 0x3FF
        if reg == 0x08:                                          # IDT
            v = 0
            for bus in self.buses:
                v |= bus.idt(port)
            uc.mem_write(addr, struct.pack("<I", v))

    # -- runs ----------------------------------------------------------------------
    def _run(self, fn: int, r0: int = 0) -> None:
        self.uc.reg_write(UC_ARM_REG_R0, r0)
        self.uc.reg_write(UC_ARM_REG_SP, STACK_TOP)
        self.uc.reg_write(UC_ARM_REG_LR, STOP | 1)
        self.uc.emu_start(fn | 1, STOP, count=50_000_000)

    def set_port(self, port: int, pressed: bool) -> None:
        """Press/release all four sensors of pad port 1..6 (ports 1..4 internal ADC, 5/6 I2C)."""
        if port <= 4:
            for ch in range((port - 1) * 4, port * 4):
                self.adc[ch] = ADC_PRESSED if pressed else ADC_RELEASED
        else:
            bus = self.buses_by_port[port]
            for regs in bus.devices.values():
                regs[REG_BUS_VOLTAGE] = I2C_PRESSED if pressed else I2C_RELEASED

    def sample(self) -> list[int]:
        for bus in self.buses:
            bus.log.clear()
        self._run(FN_SAMPLE, SAMPLER_ARG)
        return list(struct.unpack("<36H", self.uc.mem_read(ADC_NEW, 72)))

    def scan(self) -> int:
        """One main-loop iteration: real sampler, then real scan (which dispatches reports and LEDs)."""
        self.sample()
        self.gpio_toggles.clear()
        self.reports.clear()
        self._run(FN_SCAN)
        return struct.unpack("<H", self.uc.mem_read(KEYCODE, 2))[0]

    def led_toggles(self, port: int, pin: int) -> int:
        return self.gpio_toggles.get((port, pin), 0)


def profile_of(name: str) -> dict:
    upper = name.upper()
    for key, prof in PROFILES.items():
        if key != "V3.2" and f"_{key}_" in upper:
            return prof
    if "V3.2" in upper:
        return PROFILES["V3.2"]
    raise SystemExit(f"no port profile for {name}")


def pin_name(port: int, pin: int) -> str:
    return f"P{'ABCDEF'[(port - GPIOA) // 0x400]}{pin}"


def main() -> int:
    img_path = Path(sys.argv[1] if len(sys.argv) > 1 else "firmware/PADUEX5_P6_V3.5.bin")
    img = img_path.read_bytes()
    prof = profile_of(img_path.name)
    k5_port = prof["port"]
    led_port, led_pin = prof["led"]
    fails = 0

    def check(name: str, got, want) -> None:
        nonlocal fails
        ok = got == want
        fails += 0 if ok else 1
        fmt = hex if isinstance(got, int) and not isinstance(got, bool) else str
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: got {fmt(got)} want {fmt(want)}")

    def load_raws(board: Board, port: int, raws: tuple[int, ...], addrs=I2C_ADDRS) -> I2CSlaveBus:
        bus = board.buses_by_port[port]
        bus.devices = {a: {REG_BUS_VOLTAGE: raw} for a, raw in zip(addrs, raws)}
        for other in board.buses:
            if other is bus:
                continue
            for regs in other.devices.values():
                regs[REG_BUS_VOLTAGE] = 0x0400          # 3275 if this bus were the one being read
        return bus

    # 1) sampler: which bus feeds RAW[16..19], and in which order
    b = Board(img)
    bus = load_raws(b, k5_port, (0x0000, 0x0800, 0x1000, 0x1F00))
    b.adc = [0xFFF - 10 * ch for ch in range(16)]
    vals = b.sample()
    check("ADC_NEW[0..15] = 0xFFF - adc (ports 1..4 untouched)", vals[:16], [10 * ch for ch in range(16)])
    check(f"ADC_NEW[16..19] from port {k5_port} in 0x80/0x88/0x86/0x84 order", vals[16:20], [3915, 2635, 1355, 0])
    check(f"port {k5_port} saw 4 reads of reg 2", [e for e in bus.log if e.startswith("reg")], ["reg2"] * 4)
    if prof["probe"]:
        # probe ACKs the four stock addresses and stops; the reads repeat them
        check("probe stopped at the 4th ACK (no 0x82)", "82w" not in bus.log and "82w!" not in bus.log, True)
        check("probe plus 4 reads", (bus.log.count("S"), bus.log.count("P")), (12, 8))
        bus.log.clear()
        vals = b.sample()
        check("second sample reuses the probed addresses", vals[16:20], [3915, 2635, 1355, 0])
        check("second sample does not probe again", (bus.log.count("S"), bus.log.count("P")), (8, 4))
        en = struct.unpack("<I", b.uc.mem_read(CRM_APB2EN, 4))[0]
        check("GPIO clock bit set by the probe", en, sum(1 << bit for bit in prof["clocks"]))
    else:
        check("port 5 addresses", [e for e in bus.log if e.endswith("w")], ["80w", "88w", "86w", "84w"])
        check("4 reads, no probe", (bus.log.count("S"), bus.log.count("P")), (8, 4))
    for port, other in b.buses_by_port.items():
        if port == k5_port:
            continue
        check(f"port {port} idle (no START)", other.log, [])
    check("ADC_NEW[20..35] stay 0", vals[20:], [0] * 16)

    # 2) scan: key 5 follows the mapped port, every other port does nothing
    b = Board(img)
    check("idle: KeyCode 0", b.scan(), 0)
    b.set_port(k5_port, True)
    check(f"press port {k5_port} -> K5 bit", b.scan(), 0x10)
    check("K5 keyboard report", [k for t, k in b.reports if t == "KBD"], [0x10])
    check(f"K5 LED strip clocked on {pin_name(led_port, led_pin)}", b.led_toggles(led_port, led_pin) > 0, True)
    if (led_port, led_pin) != (GPIOC, 8):
        check("no LED data on PC8", b.led_toggles(GPIOC, 8), 0)
    if (led_port, led_pin) != (GPIOC, 9):
        check("no LED data on PC9", b.led_toggles(GPIOC, 9), 0)
    check("K1..K4 strips quiet",
          [b.led_toggles(GPIOB, 14), b.led_toggles(GPIOB, 15), b.led_toggles(GPIOC, 6), b.led_toggles(GPIOC, 7)],
          [0, 0, 0, 0])
    b.set_port(k5_port, False)
    check(f"release port {k5_port} -> KeyCode 0", b.scan(), 0)
    check("release report", [k for t, k in b.reports if t == "KBD"], [0x00])

    for dead in (5, 6, 7, 8, 9):
        if dead == k5_port:
            continue
        b = Board(img)
        b.set_port(dead, True)
        check(f"press port {dead} -> nothing", b.scan(), 0)
        check(f"port {dead} press: no report", b.reports, [])
        check(f"port {dead} press: K5 LED dark", b.led_toggles(led_port, led_pin), 0)

    # 3) keys 1..4 unchanged, chords with K5
    b = Board(img)
    for key, (port, pin) in enumerate(((GPIOB, 14), (GPIOB, 15), (GPIOC, 6), (GPIOC, 7))):
        b.set_port(key + 1, True)
        check(f"press port {key + 1} -> K{key + 1}", b.scan(), 1 << key)
        check(f"K{key + 1} LED on {pin_name(port, pin)}", b.led_toggles(port, pin) > 0, True)
        b.set_port(key + 1, False)
        check(f"release port {key + 1}", b.scan(), 0)
    b.set_port(1, True)
    b.set_port(k5_port, True)
    check(f"chord port 1 + port {k5_port} -> K1|K5", b.scan(), 0x11)
    check("chord report", [k for t, k in b.reports if t == "KBD"], [0x11])
    check("chord LEDs: K1 and K5",
          (b.led_toggles(GPIOB, 14) > 0, b.led_toggles(led_port, led_pin) > 0), (True, True))
    b.set_port(1, False)
    check("release port 1 -> K5 only", b.scan(), 0x10)
    b.set_port(k5_port, False)
    check("release all", b.scan(), 0)

    # 4) gamepad mode dispatch
    b = Board(img, configmode=0x11)
    b.set_port(k5_port, True)
    b.scan()
    check("HID report after K5 press", [k for t, k in b.reports if t == "HID"], [0x10])

    # 5) probe images only: addresses that are not the port-5 set, and a silent bus
    if prof["probe"]:
        alt = (0x41, 0x45, 0x47, 0x4A)          # wire bytes 0x82, 0x8A, 0x8E, 0x94
        b = Board(img)
        bus = load_raws(b, k5_port, (0x0000, 0x0800, 0x1000, 0x1F00), addrs=alt)
        check("non-stock addresses still convert in probe order", b.sample()[16:20], [3915, 2635, 1355, 0])
        check("probe used 0x82/0x8A/0x8E/0x94",
              [e for e in bus.log if e in ("82w", "8Aw", "8Ew", "94w")],
              ["82w", "8Aw", "8Ew", "94w"] * 2)
        b.set_port(k5_port, True)
        check("press on the non-stock chips -> K5", b.scan(), 0x10)

        b = Board(img)
        b.buses_by_port[k5_port].devices = {}
        check("nothing ACKs: key 5 reads released", b.sample()[16:20], [0, 0, 0, 0])
        check("nothing ACKs: KeyCode 0 (scan returns)", b.scan(), 0)
        b.set_port(1, True)
        check("nothing ACKs: port 1 still presses", b.scan(), 0x01)
        b.set_port(1, False)
        check("nothing ACKs: port 1 still releases", b.scan(), 0)

    where = f"key 5 on port {k5_port}, LED {pin_name(led_port, led_pin)}"
    print(f"\n{img_path.name}: {'ALL PASS' if fails == 0 else f'{fails} FAIL'}  ({where})")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
