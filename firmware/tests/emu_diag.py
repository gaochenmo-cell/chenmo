#!/usr/bin/env python3
"""
Unicorn checks for the two images that replaced the one-port-one-pair guess.

PADUEX5_DIAG.bin scans 0x80..0x9E on both polarities of every open-drain pair and
publishes the bitmaps on RAW channels 0..15 (the only ones the host draws) plus
a copy of the port-5 bus on channels 16 and 17. It must not emit a key report.

PADUEX5_BUS5EXT.bin keeps key 5 on PC11/PC12 but ignores 0x80/0x88/0x86/0x84 and
uses the first four other even addresses that ACK. An address that does not ACK
reads as released, so a short probe cannot stick the key.

  python3 firmware/tests/emu_diag.py
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

import emu_port6_remap
from emu_port6_remap import (
    GPIOC,
    I2CSlaveBus,
    Board,
)


def _idt_with_pullups(self, uc, access, addr, size, value, _) -> None:
    """Released SDA reads high. A pin with no slave is a NACK, not an ACK."""
    port, reg = addr & ~0x3FF, addr & 0x3FF
    if reg != 0x08:
        return
    v = 0xFFFF
    for bus in self.buses:
        if port == bus.sda[0] and bus.line() == 0:
            v &= ~bus.sda[1]
        if port == bus.scl[0] and not bus.m_scl:
            v &= ~bus.scl[1]
    uc.mem_write(addr, struct.pack("<I", v))


emu_port6_remap.Board._mem_read = _idt_with_pullups

ROOT = Path(__file__).resolve().parents[1]
REG = 2


def bitmap(addrs: list[int]) -> int:
    bits = 0
    for a in addrs:
        bits |= 1 << ((a - 0x80) // 2)
    return bits


def low(bits: int) -> int:
    return bits & 0xFFF


def high(bits: int) -> int:
    return bits >> 12


def popcount(bits: int) -> int:
    return bin(bits).count("1")


def first_addr(bits: int) -> int:
    if bits == 0:
        return 0
    return 0x80 + 2 * (bits & -bits).bit_length() - 2


def attach(board: Board, port: int, addrs: list[int]) -> I2CSlaveBus:
    bus = board.buses_by_port[port]
    bus.devices = {a >> 1: {REG: 0x1F00} for a in addrs}
    return bus


def silence(board: Board, keep: I2CSlaveBus | None = None) -> None:
    for bus in board.buses:
        if bus is not keep:
            bus.devices = {}


def expect_diag(vals: list[int], pair_bits: dict[int, int]) -> list[int]:
    """pair_bits: pair index -> 16-bit ACK bitmap. Unlisted pairs are 0."""
    bits = [pair_bits.get(i, 0) for i in range(12)]
    # layout in build_paduex5_diag.py: (pair, mode) with mode 0 low, 1 high, 2 count, 3 first, 4 highs
    layout = [
        (0, 0), (0, 1), (0, 2), (0, 3),
        (2, 0), (4, 0), (6, 0), (8, 0),
        (10, 0), (1, 0), (5, 0), (7, 0),
        (9, 0), (11, 0), (3, 0), (0, 4),
    ]
    out = []
    for pair, mode in layout:
        b = bits[pair]
        if mode == 0:
            out.append(low(b))
        elif mode == 1:
            out.append(high(b))
        elif mode == 2:
            out.append(popcount(b))
        elif mode == 3:
            out.append(first_addr(b))
        else:
            out.append(sum(1 for x in bits if high(x)))
    out.append(low(bits[0]))
    out.append(high(bits[0]))
    return out


def main() -> int:
    fails = 0

    def check(name: str, got, want) -> None:
        nonlocal fails
        ok = got == want
        fails += 0 if ok else 1
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: got {got} want {want}")

    diag = (ROOT / "PADUEX5_DIAG.bin").read_bytes()
    ext = (ROOT / "PADUEX5_BUS5EXT.bin").read_bytes()

    # --- diagnostic: stock four chips on the port-5 bus -------------------------
    b = Board(diag)
    stock = [0x80, 0x84, 0x86, 0x88]
    keep = attach(b, 5, stock)
    silence(b, keep)
    vals = b.sample()
    want = expect_diag(vals, {0: bitmap(stock)})
    check("port5 stock four -> channels 0..17", vals[:18], want)
    check("channel 0 is the 0x80..0x96 bitmap (29)", vals[0], 29)
    check("channel 16 repeats that bitmap", vals[16], 29)
    check("no ACK on any other pair", vals[4:16], [0] * 12)
    # cached: unplugging the chips after the first sample changes nothing
    keep.devices = {}
    for bus in b.buses:
        bus.log.clear()
    vals2 = b.sample()
    check("second sample does not rescan", vals2[:18], vals[:18])
    check("second sample touches no bus", any(bus.log for bus in b.buses), False)

    # --- 0x90 and 0x98 on port 5, plus 0x90 on PB3/PB4 and 0x82 on PF6/PF7 ------
    b = Board(diag)
    p5 = attach(b, 5, [0x90, 0x98])
    p6 = attach(b, 6, [0x90])
    p7 = attach(b, 7, [0x82])
    silence(b, None)
    # silence wiped them; put them back
    attach(b, 5, [0x90, 0x98])
    attach(b, 6, [0x90])
    attach(b, 7, [0x82])
    for bus in b.buses:
        if bus not in (p5, p6, p7):
            bus.devices = {}
    vals = b.sample()
    want = expect_diag(vals, {0: bitmap([0x90, 0x98]), 4: bitmap([0x90]), 6: bitmap([0x82])})
    check("0x90/0x98 on port 5, 0x90 on PB3, 0x82 on PF6", vals[:18], want)
    check("port5 low bar is bit 8 (0x90) = 256", vals[0], 256)
    check("port5 high bar is 0x98", vals[1], 1)
    check("smallest ACK address is 144 (0x90)", vals[3], 144)
    check("PB3/PB4 bar", vals[5], 256)
    check("PF6/PF7 bar", vals[6], 2)

    # --- swapped SCL/SDA on the port-5 pair --------------------------------------
    b = Board(diag)
    forward = b.buses_by_port[5]
    swapped = I2CSlaveBus("port5swap", (GPIOC, 1 << 12), (GPIOC, 1 << 11), {0x90 >> 1: {REG: 0x1F00}})
    b.buses = tuple(bus for bus in b.buses if bus is not forward) + (swapped,)
    for bus in b.buses:
        if bus is not swapped:
            bus.devices = {}
    vals = b.sample()
    want = expect_diag(vals, {1: bitmap([0x90])})
    check("swapped PC12/PC11 answers on its own bar", vals[:18], want)
    check("forward port-5 bar stays 0", vals[0], 0)
    check("swapped bar is 256", vals[9], 256)

    # --- a bitmap above the key threshold must not produce a report -------------
    b = Board(diag)
    keep = attach(b, 5, list(range(0x80, 0x98, 2)))
    silence(b, keep)
    b.sample()
    b.reports.clear()
    b.scan()
    check("diag scan sends no keyboard or HID report", b.reports, [])
    raw = list(struct.unpack("<36H", b.uc.mem_read(0x200019D0 + 0x26, 72)))
    check("the same bitmap is what RAW[0] holds", raw[0], 0xFFF)
    check("every published bar fits the host progress range", max(raw[:18]) <= 4095, True)

    # --- bus extension: stock addresses are present and must be ignored ---------
    b = Board(ext)
    bus = b.buses_by_port[5]
    bus.devices = {
        0x80 >> 1: {REG: 0x0000},
        0x88 >> 1: {REG: 0x0000},
        0x86 >> 1: {REG: 0x0000},
        0x84 >> 1: {REG: 0x0000},
        0x90 >> 1: {REG: 0x0800},
        0x92 >> 1: {REG: 0x1000},
        0x94 >> 1: {REG: 0x0000},
        0x98 >> 1: {REG: 0x1F00},
    }
    for other in b.buses:
        if other is not bus:
            other.devices = {}
    vals = b.sample()
    check("key 5 reads 0x90/0x92/0x94/0x98, not the stock four", vals[16:20], [2635, 1355, 3915, 0])
    tried = [e for e in bus.log if e[:2] in ("80", "88", "86", "84") and not e.endswith("!")]
    check("probe never ACKs 0x80/0x88/0x86/0x84", tried, [])
    for other in b.buses:
        if other is bus:
            continue
        check(f"{other.name} idle during busext", other.log, [])

    # one extra chip, three empty slots: those slots read as released, not stuck
    b = Board(ext)
    bus = b.buses_by_port[5]
    bus.devices = {0x90 >> 1: {REG: 0x0800}, 0x80 >> 1: {REG: 0x0000}}
    for other in b.buses:
        if other is not bus:
            other.devices = {}
    vals = b.sample()
    check("lone 0x90, empty slots released", vals[16:20], [2635, 0, 0, 0])
    bus.devices[0x90 >> 1][REG] = 0x0000
    # probe result is cached, so changing the raw value is seen; the address is not
    check("0x90 pressed -> K5, and the stock chip does not matter", b.scan(), 0x10)
    check("K5 lamp still on PC8", b.led_toggles(GPIOC, 8) > 0, True)

    # nothing but the stock chips: key 5 stays released (a NACK must not read as 0)
    b = Board(ext)
    bus = b.buses_by_port[5]
    bus.devices = {a >> 1: {REG: 0x0000} for a in (0x80, 0x88, 0x86, 0x84)}
    for other in b.buses:
        if other is not bus:
            other.devices = {}
    vals = b.sample()
    check("stock chips only -> key 5 reads released", vals[16:20], [0, 0, 0, 0])
    check("stock chips only -> KeyCode 0", b.scan(), 0)
    check("no report", b.reports, [])

    # ports 1..4 still sampled by the ADC
    b = Board(ext)
    for bus in b.buses:
        bus.devices = {}
    b.set_port(1, True)
    check("port 1 still asserts K1", b.scan(), 0x01)
    check("K1 report", [k for t, k in b.reports if t == "KBD"], [0x01])

    print(f"\n{'ALL PASS' if fails == 0 else f'{fails} FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
