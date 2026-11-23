#!/usr/bin/env python3
"""
PADUEX5_DIAG.bin — one-shot I2C address scan, results on the RAW bars the
host actually draws (channels 0..15, keys 1..4). Reports no keys.

PADUEX5_BUS5EXT.bin — key 5 stays on PC11/PC12, but the probe skips the four
addresses the stock sampler uses (0x80/0x88/0x86/0x84) and keeps the first
four other even addresses in 0x82..0x9E that ACK.

Both are built from PADUEX5_V3.2.bin. See the release notes for the bar map.
"""
from __future__ import annotations

import hashlib
import struct
import sys
from pathlib import Path

import importlib.util

from keystone import KS_ARCH_ARM, KS_MODE_THUMB, Ks

_px_path = Path(__file__).resolve().parent / "build_paduex5_px_v3.5.py"
_spec = importlib.util.spec_from_file_location("paduex_px", _px_path)
_px = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_px)
BASE, SIZE = _px.BASE, _px.SIZE
USB_VECTOR, USB_VECTOR_OFF = _px.USB_VECTOR, _px.USB_VECTOR_OFF
build_bus5ext, off = _px.build_bus5ext, _px.off

FN_DELAY = 0x0800851C
FN_CLOCK = 0x08009748
READ16 = 0x0800852A
CHAIN_END = 0x08008898
EPILOGUE = 0x08006FEC          # sampler's pop.w; becomes b.w diag_entry
REPORT_KBD = 0x08006C8E        # bl Report_Normal
REPORT_HID = 0x08006CA8        # bl HID report
FW_MINOR_SITE = 0x0800616A
ADC_NEW = 0x20001BAE
CUR = 0x20007E00               # active pin pair
RES = 0x20007E40               # 12 x u16 bitmaps
MAGIC_ADDR = 0x20007F00
MAGIC = 0xA55A5AA5

# (scl port index, scl pin, sda port index, sda pin). Index: A0 B1 C2 F5.
PAIRS = [
    (2, 11, 2, 12),  # 0 PC11/PC12  port-5 bus, the suspected one
    (2, 12, 2, 11),  # 1 swapped
    (1, 8, 1, 9),    # 2 PB8/PB9    power monitor
    (1, 9, 1, 8),    # 3 swapped
    (1, 3, 1, 4),    # 4 PB3/PB4
    (1, 4, 1, 3),    # 5 swapped
    (5, 6, 5, 7),    # 6 PF6/PF7
    (5, 7, 5, 6),    # 7 swapped
    (1, 10, 1, 11),  # 8 PB10/PB11
    (1, 11, 1, 10),  # 9 swapped
    (1, 2, 0, 10),   # 10 PB2/PA10
    (0, 10, 1, 2),   # 11 swapped
]
# byte = (mode << 4) | pair. mode: 0 low12, 1 high4, 2 popcount, 3 first address, 4 high-address pair count
LAYOUT = [
    (0, 0), (0, 1), (0, 2), (0, 3),
    (2, 0), (4, 0), (6, 0), (8, 0),
    (10, 0), (1, 0), (5, 0), (7, 0),
    (9, 0), (11, 0), (3, 0), (0, 4),
]


def asm(ks: Ks, text: str, addr: int) -> bytes:
    enc, _ = ks.asm(text, addr)
    return bytes(enc)


DIAG = r"""
    .syntax unified
    .thumb
read16_stub:
    movw r0, #0xffff
    bx lr

diag_entry:
    push {lr}
    bl do_scan
    bl publish
    pop {lr}
    pop.w {r4, r5, r6, r7, r8, pc}

do_scan:
    push {r4, r5, r6, r7, lr}
    movw r4, #MAGIC_LO
    movt r4, #MAGIC_HI
    ldr r0, [r4]
    movw r1, #MAGICV_LO
    movt r1, #MAGICV_HI
    cmp r0, r1
    beq scan_done
    str r1, [r4]
    bl clocks_all
    movs r4, #0
pair_loop:
    mov r0, r4
    bl load_pair
    movs r5, #0
    movs r6, #0x80
addr_loop:
    bl i2c_start
    mov r0, r6
    bl i2c_write
    mov r7, r0
    bl i2c_stop
    cmp r7, #0
    bne addr_next
    subs r0, r6, #0x80
    lsrs r0, r0, #1
    movs r1, #1
    lsls r1, r0
    orrs r5, r1
addr_next:
    adds r6, #2
    cmp r6, #0xa0
    bne addr_loop
    movw r0, #RES_LO
    movt r0, #RES_HI
    strh r5, [r0, r4, lsl #1]
    adds r4, #1
    cmp r4, #12
    bne pair_loop
scan_done:
    pop {r4, r5, r6, r7, pc}

publish:
    push {r4, r5, r6, r7, lr}
    movw r4, #ADC_LO
    movt r4, #ADC_HI
    adr r5, layout
    movs r6, #0
pub_loop:
    ldrb r0, [r5, r6]
    lsrs r1, r0, #4
    ands r0, r0, #0xf
    cmp r1, #4
    beq pub_highs
    movw r2, #RES_LO
    movt r2, #RES_HI
    ldrh r0, [r2, r0, lsl #1]
    cmp r1, #1
    beq pub_high
    cmp r1, #2
    beq pub_count
    cmp r1, #3
    beq pub_first
    uxth r0, r0
    movw r1, #0xfff
    ands r0, r1
    b pub_store
pub_high:
    lsrs r0, r0, #12
    b pub_store
pub_count:
    movs r2, #0
pc_loop:
    cmp r0, #0
    beq pc_done
    lsrs r0, r0, #1
    movs r3, #0
    adcs r3, r3
    adds r2, r3
    b pc_loop
pc_done:
    mov r0, r2
    b pub_store
pub_first:
    movs r2, #0x80
fa_loop:
    cmp r0, #0
    beq fa_none
    lsrs r0, r0, #1
    bcs fa_hit
    adds r2, #2
    b fa_loop
fa_none:
    movs r2, #0
fa_hit:
    mov r0, r2
    b pub_store
pub_highs:
    movw r2, #RES_LO
    movt r2, #RES_HI
    movs r0, #0
    movs r3, #0
ph_loop:
    ldrh r1, [r2, r3, lsl #1]
    lsrs r1, r1, #12
    cbz r1, ph_next
    adds r0, #1
ph_next:
    adds r3, #1
    cmp r3, #12
    bne ph_loop
pub_store:
    strh r0, [r4, r6, lsl #1]
    adds r6, #1
    cmp r6, #16
    bne pub_loop
    movw r2, #RES_LO
    movt r2, #RES_HI
    ldrh r0, [r2]
    movw r1, #0xfff
    ands r0, r1
    strh r0, [r4, #32]
    ldrh r0, [r2]
    lsrs r0, r0, #12
    strh r0, [r4, #34]
    pop {r4, r5, r6, r7, pc}

clocks_all:
    push {r4, lr}
    movs r4, #2
ck_loop:
    movs r1, #1
    mov r0, r4
    movt r0, #0x18
    bl CLOCK
    adds r4, #1
    cmp r4, #5
    bne ck_loop
    movs r1, #1
    movs r0, #7
    movt r0, #0x18
    bl CLOCK
    pop {r4, pc}

load_pair:
    push {r4, r5, lr}
    adr r4, pairs
    lsls r0, r0, #2
    adds r4, r0
    movw r5, #CUR_LO
    movt r5, #CUR_HI
    movw r3, #0x0800
    movt r3, #0x4001
    ldrb r1, [r4]
    lsls r1, r1, #10
    adds r1, r3
    str r1, [r5]
    ldrb r1, [r4, #1]
    str r1, [r5, #4]
    movs r2, #1
    lsls r2, r1
    str r2, [r5, #8]
    ldrb r1, [r4, #2]
    lsls r1, r1, #10
    adds r1, r3
    str r1, [r5, #12]
    ldrb r1, [r4, #3]
    str r1, [r5, #16]
    movs r2, #1
    lsls r2, r1
    str r2, [r5, #20]
    pop {r4, r5, pc}

set_mode:
    push {r4, lr}
    cmp r1, #8
    blo sm_crl
    adds r0, #4
    subs r1, #8
sm_crl:
    lsls r1, r1, #2
    movs r3, #0xf
    lsls r3, r1
    ldr r4, [r0]
    bics r4, r3
    lsls r2, r1
    orrs r4, r2
    str r4, [r0]
    pop {r4, pc}

scl_hi:
    push {lr}
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3]
    ldr r1, [r3, #4]
    movs r2, #1
    bl set_mode
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3, #8]
    ldr r1, [r3]
    str r0, [r1, #0x10]
    pop {pc}
scl_lo:
    push {lr}
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3, #8]
    ldr r1, [r3]
    str r0, [r1, #0x14]
    pop {pc}
sda_drive:
    push {r4, lr}
    mov r4, r2
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3, #12]
    ldr r1, [r3, #16]
    movs r2, #1
    bl set_mode
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3, #20]
    ldr r1, [r3, #12]
    cmp r4, #0
    beq sda_clr
    str r0, [r1, #0x10]
    b sda_done
sda_clr:
    str r0, [r1, #0x14]
sda_done:
    pop {r4, pc}
sda_rel:
    push {lr}
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3, #12]
    ldr r1, [r3, #16]
    movs r2, #8
    bl set_mode
    movw r3, #CUR_LO
    movt r3, #CUR_HI
    ldr r0, [r3, #20]
    ldr r1, [r3, #12]
    str r0, [r1, #0x10]
    pop {pc}
sda_rd:
    movw r1, #CUR_LO
    movt r1, #CUR_HI
    ldr r0, [r1, #12]
    ldr r2, [r1, #20]
    ldr r0, [r0, #8]
    tst r0, r2
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
    beq wb_lo
    movs r2, #1
    bl sda_drive
    b wb_go
wb_lo:
    movs r2, #0
    bl sda_drive
wb_go:
    bl dly
    bl scl_hi
    bl dly
    bl scl_lo
    pop {pc}
i2c_write:
    push {r4, r5, lr}
    mov r4, r0
    movs r5, #8
iw_loop:
    lsrs r0, r4, #7
    bl write_bit
    lsls r4, r4, #1
    uxtb r4, r4
    subs r5, #1
    bne iw_loop
    bl sda_rel
    bl dly
    bl scl_hi
    bl dly
    bl sda_rd
    mov r4, r0
    bl scl_lo
    mov r0, r4
    pop {r4, r5, pc}
i2c_start:
    push {lr}
    movs r2, #1
    bl sda_drive
    bl scl_hi
    bl dly
    movs r2, #0
    bl sda_drive
    bl dly
    bl scl_lo
    pop {pc}
i2c_stop:
    push {lr}
    movs r2, #0
    bl sda_drive
    bl dly
    bl scl_hi
    bl dly
    movs r2, #1
    bl sda_drive
    bl dly
    pop {pc}

    .align 2
pairs:
PAIRBYTES
    .align 2
layout:
LAYOUTBYTES
"""


def lo(v: int) -> str:
    return f"{v & 0xFFFF:#x}"


def hi(v: int) -> str:
    return f"{(v >> 16) & 0xFFFF:#x}"


def build_diag(src: bytes) -> bytearray:
    ks = Ks(KS_ARCH_ARM, KS_MODE_THUMB)
    pairs = ", ".join(f"{b:#x}" for p in PAIRS for b in p)
    layout = ", ".join(f"{(mode << 4) | pair:#x}" for pair, mode in LAYOUT)
    text = (DIAG
            .replace("PAIRBYTES", f".byte {pairs}")
            .replace("LAYOUTBYTES", f".byte {layout}")
            .replace("MAGIC_LO", lo(MAGIC_ADDR)).replace("MAGIC_HI", hi(MAGIC_ADDR))
            .replace("MAGICV_LO", lo(MAGIC)).replace("MAGICV_HI", hi(MAGIC))
            .replace("RES_LO", lo(RES)).replace("RES_HI", hi(RES))
            .replace("ADC_LO", lo(ADC_NEW)).replace("ADC_HI", hi(ADC_NEW))
            .replace("CUR_LO", lo(CUR)).replace("CUR_HI", hi(CUR))
            .replace("CLOCK", f"{FN_CLOCK:#x}")
            .replace("DELAY", f"{FN_DELAY:#x}"))
    blob = asm(ks, text, READ16)
    assert READ16 + len(blob) <= CHAIN_END, hex(READ16 + len(blob))
    buf = bytearray(src)
    assert len(buf) == SIZE
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(FW_MINOR_SITE):off(FW_MINOR_SITE) + 2] == bytes([2, 0x21])
    # sampler epilogue is a 4-byte pop.w
    assert buf[off(EPILOGUE):off(EPILOGUE) + 2] == bytes.fromhex("bde8"), buf[off(EPILOGUE):off(EPILOGUE) + 4].hex()
    for site in (REPORT_KBD, REPORT_HID):
        assert buf[off(site) + 1] & 0xF8 == 0xF0, buf[off(site):off(site) + 4].hex()

    buf[off(READ16):off(READ16) + len(blob)] = blob
    buf[off(EPILOGUE):off(EPILOGUE) + 4] = asm(ks, f"b.w {READ16 + 6:#x}", EPILOGUE)
    for site in (REPORT_KBD, REPORT_HID):
        buf[off(site):off(site) + 4] = asm(ks, "nop\nnop", site)
    buf[off(FW_MINOR_SITE)] = 6

    assert len(buf) == SIZE
    assert buf[:0x200] == src[:0x200]
    assert struct.unpack_from("<I", buf, USB_VECTOR_OFF)[0] == USB_VECTOR
    assert buf[off(CHAIN_END):off(CHAIN_END) + 8] == src[off(CHAIN_END):off(CHAIN_END) + 8]
    allowed = set(range(READ16, READ16 + len(blob)))
    allowed |= set(range(EPILOGUE, EPILOGUE + 4))
    allowed |= set(range(REPORT_KBD, REPORT_KBD + 4))
    allowed |= set(range(REPORT_HID, REPORT_HID + 4))
    allowed.add(FW_MINOR_SITE)
    for va in (BASE + i for i, (a, b) in enumerate(zip(src, buf)) if a != b):
        assert va in allowed, hex(va)
    return buf


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    src = (root / "PADUEX5_V3.2.bin").read_bytes()
    images = {
        "PADUEX5_DIAG.bin": build_diag(src),
        "PADUEX5_BUS5EXT.bin": build_bus5ext(src),
    }
    for name, img in images.items():
        (root / name).write_bytes(img)
        print(f"{name}  {hashlib.sha256(img).hexdigest()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
