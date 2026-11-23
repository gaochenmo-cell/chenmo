#!/usr/bin/env python3
"""
Build PADUEX5_P6/P7/P8/P9_V3.5.bin.

Five-key images for a board whose port-5 connector is dead. Key 5 (key code,
Sensi/Color/DTick slots 16..19, host protocol) is unchanged; its four sensor
readings and its LED strip are taken from one of ports 6..9. Which port is
selected by the file. The four sensor addresses are not fixed: on the first
sample the firmware probes 8-bit write addresses 0x80..0x9E (INA219-style
0x40..0x4F), tries the port-5 set 0x80/0x88/0x86/0x84 first, and keeps the
first four that ACK, in that order. With nothing on the bus, key 5 reads as
released instead of stuck down.

Base: PADUEX5_V3.2.bin. HVer stays 0x13, FW minor 0x0800616A is 5, so every
file reports V3.5; tell them apart by the file name and its SHA-256.

The I2C engine replaces the port-5 bit-bang chain (0x0800852A..0x08008897),
which nothing but the four sampler calls uses. The four `movs r0, #addr`
sites become `bl` into the dead 2P chain at 0x08005DE8. See
patches/paduex5-port6.md for the pin assignment and why it is an inference.
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

GPIOA, GPIOB, GPIOC, GPIOF = 0x40010800, 0x40010C00, 0x40011000, 0x40011C00
CLK_BIT = {GPIOA: 2, GPIOB: 3, GPIOC: 4, GPIOF: 7}

FN_DELAY = 0x0800851C
FN_CLOCK = 0x08009748
READ16 = 0x0800852A          # sampler's four bl targets; veneer lives here
SLOT = 0x0800852E            # second veneer: getters branch here
CHAIN_END = 0x08008898       # bus-2 reader; must survive byte for byte
GETTER = 0x08005DE8          # dead 2P chain, skipped by the branch at 0x08005DE6
GETTER_END = 0x08005E00      # 4 x 6 bytes; live code resumes at 0x08005E50

# movs r1,#2; movs r0,#imm   immediately before each bl read16
ADDR_SITES = (0x08006F78, 0x08006F84, 0x08006F90, 0x08006F9C)
ADDR_IMMS = (0x80, 0x88, 0x86, 0x84)

LED_OLD_PORT = GPIOC
LED_OLD_MASK = 0x100
LED_SITES = (0x080071DC, 0x080071F0, 0x0800720C, 0x08007220)
LED_PORT_LITS = (0x08007208, 0x08007238)

HVER_SITE = 0x0800615E
FW_MINOR_SITE = 0x0800616A
FW_MINOR = 5

# magic / count / four addresses, above the image's stack (SP init 0x20002A20)
RAM_MAGIC = 0x20007F00
RAM_COUNT = 0x20007F04
RAM_ADDRS = 0x20007F08
MAGIC = 0xA55A5AA5

# Init order of the six open-drain pairs after the power-monitor pair (PB8/PB9),
# then the nine strip outputs after PD2/PC14/PC10/PA15. Port 5 is the first
# pair and is confirmed by the sampler; ports 6..9 are the next four pairs and
# the next four strips. SCL is the lower pin, matching PC11/PC12.
PORTS = {
    6: dict(scl_port=GPIOB, scl=3, sda_port=GPIOB, sda=4, led_port=GPIOC, led=9),
    7: dict(scl_port=GPIOF, scl=6, sda_port=GPIOF, sda=7, led_port=GPIOA, led=8),
    8: dict(scl_port=GPIOB, scl=10, sda_port=GPIOB, sda=11, led_port=GPIOB, led=13),
    9: dict(scl_port=GPIOB, scl=2, sda_port=GPIOA, sda=10, led_port=GPIOB, led=12),
}


def off(va: int) -> int:
    return va - BASE


def asm(ks: Ks, text: str, addr: int) -> bytes:
    enc, _ = ks.asm(text, addr)
    return bytes(enc)


ENGINE = r"""
    .syntax unified
    .thumb
veneer_read:
    b.w read16
veneer_slot:
    b.w slot_setup

read16:
    push {r4, r5, r6, lr}
    movw r2, #COUNT_LO
    movt r2, #COUNT_HI
    ldrb r2, [r2]
    cbnz r2, rd_go
    movw r0, #0xffff
    pop {r4, r5, r6, pc}
rd_go:
    mov r4, r0
    mov r6, r1
    bl i2c_start
    mov r0, r4
    bl i2c_write
    cmp r0, #0
    bne rd_fail
    mov r0, r6
    bl i2c_write
    cmp r0, #0
    bne rd_fail
    bl i2c_start
    mov r0, r4
    orr r0, r0, #1
    bl i2c_write
    cmp r0, #0
    bne rd_fail
    movs r0, #1
    bl read_byte
    mov r5, r0
    lsls r5, r5, #8
    movs r0, #0
    bl read_byte
    orrs r0, r5
    mov r6, r0
    bl i2c_stop
    mov r0, r6
    pop {r4, r5, r6, pc}
rd_fail:
    bl i2c_stop
    movs r0, #0
    pop {r4, r5, r6, pc}

slot_setup:
    push {r4, r5, r6, r7, r8, lr}
    mov r8, r2
    bl probe
    mov r2, r8
    movw r0, #ADDR_LO
    movt r0, #ADDR_HI
    ldrb r0, [r0, r2]
    movs r1, #2
    pop {r4, r5, r6, r7, r8, pc}

probe:
    push {r4, r5, r6, r7, lr}
    movw r4, #MAGIC_LO
    movt r4, #MAGIC_HI
    ldr r0, [r4]
    movw r1, #MAGICV_LO
    movt r1, #MAGICV_HI
    cmp r0, r1
    beq probe_done
    str r1, [r4]
    bl clocks
    movs r5, #0
    adr r6, cands
    movs r7, #16
probe_loop:
    ldrb r4, [r6]
    bl i2c_start
    mov r0, r4
    bl i2c_write
    push {r0}
    bl i2c_stop
    pop {r3}
    cmp r3, #0
    bne probe_next
    movw r0, #ADDR_LO
    movt r0, #ADDR_HI
    strb r4, [r0, r5]
    adds r5, #1
    cmp r5, #4
    beq probe_keep
probe_next:
    adds r6, #1
    subs r7, #1
    bne probe_loop
    mov r3, r5
    movw r0, #ADDR_LO
    movt r0, #ADDR_HI
    adr r1, stock
probe_fill:
    cmp r5, #4
    beq probe_store
    ldrb r2, [r1, r5]
    strb r2, [r0, r5]
    adds r5, #1
    b probe_fill
probe_keep:
    mov r3, r5
probe_store:
    movw r0, #COUNT_LO
    movt r0, #COUNT_HI
    strb r3, [r0]
probe_done:
    pop {r4, r5, r6, r7, pc}

clocks:
    push {lr}
    movs r1, #1
    movw r0, #SCL_CLK
    movt r0, #0x0018
    bl CLOCK
SDA_CLK_CALL
    pop {pc}

set_mode:
    push {r4, lr}
    cmp r1, #8
    blo set_mode_crl
    adds r0, #4
    subs r1, #8
set_mode_crl:
    lsls r1, r1, #2
    movs r3, #0xf
    lsls r3, r1
    ldr r4, [r0]
    bics r4, r4, r3
    lsls r2, r1
    orrs r4, r2
    str r4, [r0]
    pop {r4, pc}

scl_hi:
    push {lr}
    movw r0, #SCL_LO
    movt r0, #SCL_HI
    movs r1, #SCL_PIN
    movs r2, #1
    bl set_mode
    movw r0, #SCL_MASK
    movw r1, #SCL_LO
    movt r1, #SCL_HI
    str r0, [r1, #0x10]
    pop {pc}
scl_lo:
    push {lr}
    movw r0, #SCL_MASK
    movw r1, #SCL_LO
    movt r1, #SCL_HI
    str r0, [r1, #0x14]
    pop {pc}
sda_hi:
    push {lr}
    movw r0, #SDA_LO
    movt r0, #SDA_HI
    movs r1, #SDA_PIN
    movs r2, #1
    bl set_mode
    movw r0, #SDA_MASK
    movw r1, #SDA_LO
    movt r1, #SDA_HI
    str r0, [r1, #0x10]
    pop {pc}
sda_lo:
    push {lr}
    movw r0, #SDA_LO
    movt r0, #SDA_HI
    movs r1, #SDA_PIN
    movs r2, #1
    bl set_mode
    movw r0, #SDA_MASK
    movw r1, #SDA_LO
    movt r1, #SDA_HI
    str r0, [r1, #0x14]
    pop {pc}
sda_rel:
    push {lr}
    movw r0, #SDA_LO
    movt r0, #SDA_HI
    movs r1, #SDA_PIN
    movs r2, #8
    bl set_mode
    movw r0, #SDA_MASK
    movw r1, #SDA_LO
    movt r1, #SDA_HI
    str r0, [r1, #0x10]
    pop {pc}
sda_rd:
    movw r1, #SDA_LO
    movt r1, #SDA_HI
    ldr r0, [r1, #8]
    movw r1, #SDA_MASK
    tst r0, r1
    beq sda_rd0
    movs r0, #1
    bx lr
sda_rd0:
    movs r0, #0
    bx lr

dly:
    push {lr}
    movs r0, #8
    bl DELAY
    pop {pc}

write_bit:
    push {lr}
    cmp r0, #0
    beq write_bit_lo
    bl sda_hi
    b write_bit_go
write_bit_lo:
    bl sda_lo
write_bit_go:
    bl dly
    bl scl_hi
    bl dly
    bl scl_lo
    pop {pc}

i2c_write:
    push {r4, r5, lr}
    mov r4, r0
    movs r5, #8
i2c_write_loop:
    lsrs r0, r4, #7
    bl write_bit
    lsls r4, r4, #1
    uxtb r4, r4
    subs r5, #1
    bne i2c_write_loop
    bl sda_rel
    bl dly
    bl scl_hi
    bl dly
    bl sda_rd
    mov r4, r0
    bl scl_lo
    mov r0, r4
    pop {r4, r5, pc}

read_bit:
    push {r4, lr}
    bl sda_rel
    bl dly
    bl scl_hi
    bl dly
    bl sda_rd
    mov r4, r0
    bl scl_lo
    mov r0, r4
    pop {r4, pc}

read_byte:
    push {r4, r5, r6, lr}
    mov r5, r0
    movs r4, #0
    movs r6, #8
read_byte_loop:
    bl read_bit
    lsls r4, r4, #1
    orrs r4, r0
    subs r6, #1
    bne read_byte_loop
    cmp r5, #0
    beq read_byte_nack
    bl sda_lo
    b read_byte_clk
read_byte_nack:
    bl sda_hi
read_byte_clk:
    bl dly
    bl scl_hi
    bl dly
    bl scl_lo
    bl sda_rel
    mov r0, r4
    pop {r4, r5, r6, pc}

i2c_start:
    push {lr}
    bl sda_hi
    bl scl_hi
    bl dly
    bl sda_lo
    bl dly
    bl scl_lo
    pop {pc}
i2c_stop:
    push {lr}
    bl sda_lo
    bl dly
    bl scl_hi
    bl dly
    bl sda_hi
    bl dly
    pop {pc}

    .align 2
cands:
    .byte 0x80, 0x88, 0x86, 0x84
    .byte 0x82, 0x8a, 0x8c, 0x8e
    .byte 0x90, 0x92, 0x94, 0x96
    .byte 0x98, 0x9a, 0x9c, 0x9e
    .align 2
stock:
    .byte 0x80, 0x88, 0x86, 0x84
    .align 2
"""


def lo(v: int) -> str:
    return f"{v & 0xFFFF:#x}"


def hi(v: int) -> str:
    return f"{(v >> 16) & 0xFFFF:#x}"


def build_engine(ks: Ks, spec: dict) -> bytes:
    scl_clk = CLK_BIT[spec["scl_port"]]
    sda_clk = CLK_BIT[spec["sda_port"]]
    sda_clk_call = ""
    if sda_clk != scl_clk:
        sda_clk_call = (
            f"    movs r1, #1\n"
            f"    movw r0, #{sda_clk:#x}\n"
            f"    movt r0, #0x0018\n"
            f"    bl {FN_CLOCK:#x}\n"
        )
    text = ENGINE
    cands = spec.get("cands")
    if cands:
        text = text.replace("movs r7, #16", f"movs r7, #{len(cands)}")
        old_bytes = (
            ".byte 0x80, 0x88, 0x86, 0x84\n"
            "    .byte 0x82, 0x8a, 0x8c, 0x8e\n"
            "    .byte 0x90, 0x92, 0x94, 0x96\n"
            "    .byte 0x98, 0x9a, 0x9c, 0x9e"
        )
        text = text.replace(old_bytes, ".byte " + ", ".join(f"{b:#x}" for b in cands))
    if spec.get("empty_fill") == 0xFF:
        text = text.replace(
            "    adr r1, stock\n"
            "probe_fill:\n"
            "    cmp r5, #4\n"
            "    beq probe_store\n"
            "    ldrb r2, [r1, r5]\n"
            "    strb r2, [r0, r5]\n",
            "probe_fill:\n"
            "    cmp r5, #4\n"
            "    beq probe_store\n"
            "    movs r2, #0xff\n"
            "    strb r2, [r0, r5]\n",
        )
        text = text.replace(
            "rd_go:\n    mov r4, r0\n",
            "rd_go:\n"
            "    cmp r0, #0xff\n"
            "    bne rd_real\n"
            "    movw r0, #0xffff\n"
            "    pop {r4, r5, r6, pc}\n"
            "rd_real:\n"
            "    mov r4, r0\n",
        )
    text = (text
            .replace("COUNT_LO", lo(RAM_COUNT)).replace("COUNT_HI", hi(RAM_COUNT))
            .replace("ADDR_LO", lo(RAM_ADDRS)).replace("ADDR_HI", hi(RAM_ADDRS))
            .replace("MAGIC_LO", lo(RAM_MAGIC)).replace("MAGIC_HI", hi(RAM_MAGIC))
            .replace("MAGICV_LO", lo(MAGIC)).replace("MAGICV_HI", hi(MAGIC))
            .replace("SCL_CLK", f"{scl_clk:#x}")
            .replace("SDA_CLK_CALL", sda_clk_call)
            .replace("CLOCK", f"{FN_CLOCK:#x}")
            .replace("DELAY", f"{FN_DELAY:#x}")
            .replace("SCL_LO", lo(spec["scl_port"])).replace("SCL_HI", hi(spec["scl_port"]))
            .replace("SDA_LO", lo(spec["sda_port"])).replace("SDA_HI", hi(spec["sda_port"]))
            .replace("SCL_PIN", str(spec["scl"])).replace("SDA_PIN", str(spec["sda"]))
            .replace("SCL_MASK", f"{1 << spec['scl']:#x}")
            .replace("SDA_MASK", f"{1 << spec['sda']:#x}"))
    blob = asm(ks, text, READ16)
    assert READ16 + len(blob) <= CHAIN_END, f"engine spills into bus 2 ({READ16 + len(blob):#x})"
    # the two veneers are 4 bytes each, so getters can target a fixed address
    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB | CS_MODE_MCLASS)
    head = list(md.disasm(blob[:8], READ16))
    assert [i.mnemonic for i in head] == ["b.w", "b.w"], [(i.mnemonic, i.op_str) for i in head]
    assert head[1].address == SLOT
    return blob


def build_getters(ks: Ks) -> bytes:
    parts = []
    for slot in range(4):
        parts.append(asm(ks, f"movs r2, #{slot}\n b.w {SLOT:#x}", GETTER + 6 * slot))
        assert len(parts[-1]) == 6, len(parts[-1])
    blob = b"".join(parts)
    assert len(blob) == GETTER_END - GETTER
    return blob


def patch_led(ks: Ks, buf: bytearray, spec: dict) -> None:
    mask = 1 << spec["led"]
    for site in LED_SITES:
        assert buf[off(site):off(site) + 4] == bytes.fromhex("4ff48071"), hex(site)
        buf[off(site):off(site) + 4] = asm(ks, f"mov.w r1, #{mask:#x}", site)
    for lit in LED_PORT_LITS:
        assert struct.unpack_from("<I", buf, off(lit))[0] == LED_OLD_PORT
        struct.pack_into("<I", buf, off(lit), spec["led_port"])


def build_image(src: bytes, spec: dict) -> bytearray:
    ks = Ks(KS_ARCH_ARM, KS_MODE_THUMB)
    buf = bytearray(src)
    assert len(buf) == SIZE
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(HVER_SITE):off(HVER_SITE) + 2] == bytes([0x13, 0x21])
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([2, 0x21])
    for site, imm in zip(ADDR_SITES, ADDR_IMMS):
        assert buf[off(site):off(site) + 4] == bytes([0x02, 0x21, imm, 0x20]), hex(site)

    engine = build_engine(ks, spec)
    getters = build_getters(ks)
    buf[off(READ16):off(READ16) + len(engine)] = engine
    buf[off(GETTER):off(GETTER_END)] = getters
    for slot, site in enumerate(ADDR_SITES):
        buf[off(site):off(site) + 4] = asm(ks, f"bl {GETTER + 6 * slot:#x}", site)
    patch_led(ks, buf, spec)
    buf[off(FW_MINOR_SITE)] = spec.get("minor", FW_MINOR)

    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200]
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(0x08005DE6):off(0x08005DE8)] == src[off(0x08005DE6):off(0x08005DE8)]
    assert buf[off(CHAIN_END):off(CHAIN_END) + 8] == src[off(CHAIN_END):off(CHAIN_END) + 8]
    allowed = set(range(GETTER, GETTER_END)) | set(range(READ16, READ16 + len(engine)))
    allowed |= {a for s in ADDR_SITES for a in range(s, s + 4)}
    allowed |= {a for s in LED_SITES for a in range(s, s + 4)}
    allowed |= {a for lit in LED_PORT_LITS for a in range(lit, lit + 4)}
    allowed |= {FW_MINOR_SITE}
    for va in (BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b):
        assert va in allowed, hex(va)
    return buf


BUS5EXT = dict(
    scl_port=GPIOC, scl=11, sda_port=GPIOC, sda=12, led_port=GPIOC, led=8,
    # even addresses 0x80..0x9E except the four chips the stock sampler reads
    cands=[0x82, 0x8A, 0x8C, 0x8E, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9A, 0x9C, 0x9E],
    empty_fill=0xFF,
    minor=7,
)


def build_bus5ext(src: bytes) -> bytearray:
    return build_image(src, BUS5EXT)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "PADUEX5_V3.2.bin").read_bytes()
    want = [int(a) for a in sys.argv[1:]] or sorted(PORTS)
    for bad in want:
        if bad not in PORTS:
            raise SystemExit(f"port must be one of {sorted(PORTS)}")
    for port in want:
        img = build_image(src, PORTS[port])
        dst = root / f"PADUEX5_P{port}_V3.5.bin"
        dst.write_bytes(img)
        import hashlib
        digest = hashlib.sha256(img).hexdigest()
        spec = PORTS[port]
        print(f"PADUEX5_P{port}_V3.5.bin  {digest}  "
              f"SCL P{ {GPIOA:'A',GPIOB:'B',GPIOC:'C',GPIOF:'F'}[spec['scl_port']]}{spec['scl']} "
              f"SDA P{ {GPIOA:'A',GPIOB:'B',GPIOC:'C',GPIOF:'F'}[spec['sda_port']]}{spec['sda']} "
              f"LED P{ {GPIOA:'A',GPIOB:'B',GPIOC:'C',GPIOF:'F'}[spec['led_port']]}{spec['led']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
