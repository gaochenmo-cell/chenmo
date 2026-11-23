#!/usr/bin/env python3
"""
Build PADUEX5_P6_V3.5.bin: 5-key image whose fifth key lives on pad port 6.

For boards whose port-5 connector is damaged. Everything the firmware does for
key 5 (sensor sampling, LED strip, Sensi/Color/DTick slots 16..19, key code,
host protocol) stays "key 5"; only the two hardware paths move to port 6:

  * sensors : the four INA219-style I2C readers that feed RAW[16..19] now
              bit-bang the port-6 bus (PB3 = SCL, PB4 = SDA) instead of the
              port-5 bus (PC11 = SCL, PC12 = SDA);
  * LED     : key 5's WS2812 strip is clocked out on PC9 (LED6) instead of
              PC8 (LED5).

Base   : PADUEX5_V3.2.bin (V3.4 is not on main yet, see the release notes)

Why not "read RAW[20..23] instead of RAW[16..19]" in the key scan: the stock
sampler (0x08006E48) only produces 20 channels - 16 internal ADC inputs for
ports 1..4 plus four I2C readings for port 5. RAW[20..35] are never written,
so the scan has nothing to pick up there. The only place port 6 can enter the
image is the I2C bus the port-5 readings come from.

Hardware mapping (read out of the image, not off the silkscreen):

  sampler 0x08006E48
    ADC ch 0..15                  -> ADC_NEW[0..15]  (ports 1..4, 0xFFF - adc)
    I2C read(0x80, reg 2)         -> ADC_NEW[16]     \\  bus 1 = PC11/PC12
    I2C read(0x88, reg 2)         -> ADC_NEW[17]      | value = 3915 - min(raw*0.625, 3915)
    I2C read(0x86, reg 2)         -> ADC_NEW[18]      |
    I2C read(0x84, reg 2)         -> ADC_NEW[19]     /
  bus 1 bit-bang chain 0x08008584..0x0800888E (read byte, ack, nack, start,
    stop, write byte) - only caller is the 16-bit reader 0x0800852A, whose only
    callers are the four sites above.
  bus 2 (PB8/PB9, chain 0x08008898..0x08008B..) reads two monitors on the
    100 ms tick (0x08006FFC) - not touched.
  GPIO init 0x08007E4C..0x0800846A configures six open-drain-style pin pairs
    in this order: PB8/PB9, PC11/PC12, PB3/PB4, PF6/PF7, PB10/PB11, PB2/PA10,
    and, in a later block, the strip outputs in this order: PD2, PC14, PC10
    (210-LED ambient strip, 0x08007684), PA15, PB14 (K1), PB15 (K2), PC6 (K3),
    PC7 (K4), PC8 (K5), PC9, PA8, PB13, PB12. JTAG is disabled (REMAP7 SWJTAG = 010) so PB3/PB4/PA15 are
    plain GPIO. Port 6 is therefore the next bus pair (PB3/PB4) and the next
    strip output (PC9).
  LED refresh 0x08005018: key 4 -> 0x080075E2 -> byte 0x080074EC -> bit
    routines 0x080071DC (0) / 0x0800720C (1), both GPIOC pin 0x100 (PC8).

Patches:
  1. bus-1 chain: GPIOC -> GPIOB literals (6), GPIOC clock -> GPIOB clock
     literals (6), SCL mask 0x800 -> 0x008 (14 mov.w), SDA mask 0x1000 ->
     0x010 (11 mov.w, 1 lsls, 5 "lsls r0,r0,#8" that derive 0x1000 from the
     0x10 output-mode byte and become nops).
  2. LED: four `mov.w r1, #0x100` -> `mov.w r1, #0x200` in 0x080071DC/0x0800720C.
  3. GET_MODE FW minor 2 -> 5 (0x0800616A). HVer stays 0x13.

Vector table, boot, USB, flash driver, key scan, Report_Normal, SendPacket,
and the other four keys are byte-identical to V3.2. The build asserts the
diff set, 0x14C == 0x080084A5 and size 37072.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from capstone import CS_ARCH_ARM, CS_MODE_MCLASS, CS_MODE_THUMB, Cs
from keystone import KS_ARCH_ARM, KS_MODE_THUMB, Ks

BASE = 0x08004000
SIZE = 37072
USB_VECTOR_OFF = 0x14C
USB_VECTOR = 0x080084A5

GPIOB = 0x40010C00
GPIOC = 0x40011000
CLK_GPIOB = 0x00180003
CLK_GPIOC = 0x00180004

# port-5 bus (stock) -> port-6 bus
OLD_PORT, OLD_SCL, OLD_SDA = GPIOC, 11, 12
NEW_PORT, NEW_SCL, NEW_SDA = GPIOB, 3, 4
NEW_CLK = CLK_GPIOB

I2C_READ16 = 0x0800852A
I2C_READ_SITES = (0x08006F7C, 0x08006F88, 0x08006F94, 0x08006FA0)
I2C_CHAIN_START = 0x08008584      # read byte
I2C_CHAIN_END = 0x08008898        # start of the bus-2 chain
I2C_LITERALS_PORT = (0x080085FC, 0x08008660, 0x080086C4, 0x08008734, 0x080087A4, 0x08008890)
I2C_LITERALS_CLK = (0x080085F8, 0x0800865C, 0x080086C0, 0x08008730, 0x080087A0, 0x08008894)
I2C_SDA_FROM_SCL = 0x080085C8     # lsls r1, r0, #1  (SDA mask = SCL mask << 1)
I2C_SDA_SHIFT = 0x080087AE        # lsls r0, r6, #12 (r6 = 1)
I2C_SDA_FROM_MODE = (0x08008622, 0x08008686, 0x080086EA, 0x0800875A, 0x080087D4)  # lsls r0, r0, #8 (r0 = 0x10)
EXPECT_SCL_MOVW = 14
EXPECT_SDA_MOVW = 11

LED_KEY5_SITES = (0x080071DC, 0x080071F0, 0x0800720C, 0x08007220)   # mov.w r1, #0x100
LED_OLD_PIN, LED_NEW_PIN = 8, 9                                      # PC8 -> PC9
LED_PORT_LITERALS = (0x08007208, 0x08007238)

HVER_SITE = 0x0800615E            # movs r1, #0x13
FW_MAJOR_SITE = 0x08006164        # movs r1, #3
FW_MINOR_SITE = 0x0800616A        # movs r1, #2
FW_MINOR_NEW = 5


def off(va: int) -> int:
    return va - BASE


def asm(ks: Ks, text: str, size: int) -> bytes:
    enc, _ = ks.asm(text)
    b = bytes(enc)
    assert len(b) == size, (text, b.hex())
    return b


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "PADUEX5_V3.2.bin").read_bytes()
    buf = bytearray(src)
    assert len(buf) == SIZE
    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB | CS_MODE_MCLASS)
    ks = Ks(KS_ARCH_ARM, KS_MODE_THUMB)

    # ---- pre-checks: this is the V3.2 five-key image ---------------------------------
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(HVER_SITE):off(HVER_SITE) + 2] == bytes([0x13, 0x21]), "HVer movs r1,#0x13"
    assert buf[off(FW_MAJOR_SITE):off(FW_MAJOR_SITE) + 2] == bytes([3, 0x21]), "movs r1,#3"
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([2, 0x21]), "movs r1,#2"
    for site in I2C_READ_SITES:
        insn = next(md.disasm(bytes(buf[off(site):off(site) + 4]), site))
        assert insn.mnemonic == "bl" and int(insn.op_str.lstrip("#"), 0) == I2C_READ16, hex(site)
    for lit in I2C_LITERALS_PORT:
        assert struct.unpack_from("<I", buf, off(lit))[0] == OLD_PORT, hex(lit)
    for lit in I2C_LITERALS_CLK:
        assert struct.unpack_from("<I", buf, off(lit))[0] == CLK_GPIOC, hex(lit)
    for lit in LED_PORT_LITERALS:
        assert struct.unpack_from("<I", buf, off(lit))[0] == GPIOC, hex(lit)
    assert buf[off(I2C_SDA_FROM_SCL):off(I2C_SDA_FROM_SCL) + 2] == bytes.fromhex("4100"), "lsls r1,r0,#1"
    assert buf[off(I2C_SDA_SHIFT):off(I2C_SDA_SHIFT) + 2] == bytes.fromhex("3003"), "lsls r0,r6,#12"
    for site in I2C_SDA_FROM_MODE:
        assert buf[off(site):off(site) + 2] == bytes.fromhex("0002"), f"lsls r0,r0,#8 @ {site:#x}"
        assert buf[off(site) - 6:off(site) - 4] == bytes.fromhex("1020"), f"movs r0,#0x10 @ {site - 6:#x}"
    for site in LED_KEY5_SITES:
        assert buf[off(site):off(site) + 4] == bytes.fromhex("4ff48071"), f"mov.w r1,#0x100 @ {site:#x}"

    patched: set[int] = set()

    def put(va: int, data: bytes) -> None:
        buf[off(va):off(va) + len(data)] = data
        patched.update(range(va, va + len(data)))

    # ---- 1. port-5 I2C bus -> port-6 I2C bus -----------------------------------------
    old_scl_mask, old_sda_mask = 1 << OLD_SCL, 1 << OLD_SDA
    new_scl_mask, new_sda_mask = 1 << NEW_SCL, 1 << NEW_SDA
    n_scl = n_sda = 0
    code = bytes(buf[off(I2C_CHAIN_START):off(I2C_CHAIN_END)])
    for insn in md.disasm(code, I2C_CHAIN_START):
        if insn.mnemonic != "mov.w" or insn.size != 4:
            continue
        reg, imm = insn.op_str.split(", #")
        imm = int(imm, 0)
        if imm == old_scl_mask:
            put(insn.address, asm(ks, f"mov.w {reg}, #{new_scl_mask:#x}", 4)); n_scl += 1
        elif imm == old_sda_mask:
            put(insn.address, asm(ks, f"mov.w {reg}, #{new_sda_mask:#x}", 4)); n_sda += 1
    assert n_scl == EXPECT_SCL_MOVW, n_scl
    assert n_sda == EXPECT_SDA_MOVW, n_sda
    if NEW_SDA != NEW_SCL + 1:
        put(I2C_SDA_FROM_SCL, asm(ks, f"movs r1, #{new_sda_mask:#x}", 2))
    put(I2C_SDA_SHIFT, asm(ks, f"lsls r0, r6, #{NEW_SDA}", 2))
    for site in I2C_SDA_FROM_MODE:        # r0 = 0x10 (GPIO output mode) -> pin mask
        if NEW_SDA == 4:
            put(site, asm(ks, "nop", 2))
        elif NEW_SDA > 4:
            put(site, asm(ks, f"lsls r0, r0, #{NEW_SDA - 4}", 2))
        else:
            put(site, asm(ks, f"lsrs r0, r0, #{4 - NEW_SDA}", 2))
    for lit in I2C_LITERALS_PORT:
        put(lit, struct.pack("<I", NEW_PORT))
    for lit in I2C_LITERALS_CLK:
        put(lit, struct.pack("<I", NEW_CLK))

    # ---- 2. key-5 LED strip PC8 -> PC9 -----------------------------------------------
    for site in LED_KEY5_SITES:
        put(site, asm(ks, f"mov.w r1, #{1 << LED_NEW_PIN:#x}", 4))

    # ---- 3. version ------------------------------------------------------------------
    put(FW_MINOR_SITE, bytes([FW_MINOR_NEW, 0x21]))

    # ---- post-checks -----------------------------------------------------------------
    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200], "vector table / early code must not change"
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    diffs = [BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b]
    for va in diffs:
        assert va in patched, hex(va)
    assert patched - {FW_MINOR_SITE, FW_MINOR_SITE + 1} <= (
        set(range(I2C_CHAIN_START, I2C_CHAIN_END)) | {a for s in LED_KEY5_SITES for a in range(s, s + 4)}
    ), "patch outside the bus-1 chain / LED bit routines"
    # everything the other keys run through is untouched
    for lo, hi in ((0x08005018, 0x08005370), (0x08005D48, 0x08005EA8), (0x080069CC, 0x08006E48),
                   (0x08006E48, 0x08007058), (0x08007058, 0x080071DC), (0x08007238, 0x08007A00),
                   (0x08008898, 0x0800D0D0)):
        assert buf[off(lo):off(hi)] == src[off(lo):off(hi)], f"{lo:#x}..{hi:#x} changed"
    print(f"diff vs PADUEX5_V3.2: {len(diffs)} bytes "
          f"(I2C chain {sum(I2C_CHAIN_START <= d < I2C_CHAIN_END for d in diffs)}, "
          f"LED {sum(any(s <= d < s + 4 for s in LED_KEY5_SITES) for d in diffs)}, FW minor 1)")

    dst = root / "PADUEX5_P6_V3.5.bin"
    dst.write_bytes(buf)
    print(f"wrote {dst} ({len(buf)} bytes), 0x14C = {struct.unpack_from('<I', buf, USB_VECTOR_OFF)[0]:#010x}")

    for a in (0x080085C0, 0x080087AE, 0x08008622, 0x080071DC, FW_MINOR_SITE):
        insn = next(md.disasm(bytes(buf[off(a):off(a) + 4]), a))
        print(f"  {insn.address:#010x}: {insn.mnemonic}\t{insn.op_str}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
