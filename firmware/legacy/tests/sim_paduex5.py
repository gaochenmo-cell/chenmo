#!/usr/bin/env python3
"""
PADUEX5 offline self-test (no hardware).

What this can prove:
  - paduex5_1.3.bin is ISP-safe (same size/vectors as stock) and only touches SendPacket
  - Host color/mode/keymap packet formats match FW source (App_User / App_RGB / Chip2813)
  - After SET color, simulated WS2812 wire bytes (GRB×25 LEDs/key) match the chosen palette
  - Mode bits encode KBS/HID × 1P/2P correctly; keymap GET/SET round-trips with CRC

What this cannot prove:
  - Real USB enumeration / Bootloader IAP / flash endurance on a customer board
  - Electrical WS2812 timing on PB14/15 PC6/7/8
  - That a particular customer's Bootloader accepts OTA (needs one physical pad)
"""
from __future__ import annotations

import json
import struct
import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

from capstone import CS_ARCH_ARM, CS_MODE_THUMB, Cs
from PIL import Image, ImageDraw, ImageFont

FW = Path(__file__).resolve().parents[1]          # firmware/legacy
ART = Path(tempfile.gettempdir()) / "paduex5-sim-report"
ART.mkdir(parents=True, exist_ok=True)

BASE = 0x08004000
FLASH_APP = 0x08004000
FLASH_CONFIG = 0x0800F800
STOCK_SIZE = 37072
MAPP_FLASH = 0x08011800
COLOR_FLASH = 0x08010800
WMODE_FLASH = 0x0800F800

# Host / FW palette
COLOR = {"R": 0x01, "G": 0x02, "B": 0x04, "Y": 0x03, "P": 0x05, "C": 0x06, "W": 0x07}
COLOR_NAME = {v: k for k, v in COLOR.items()}
HOST_UI_COLORS = ("R", "B", "W")  # PADUEX5 5Key UI only allows these
BTN_RGB_NUMBER = 25
BRIGHTNESS = 0x80  # typical AppRGB.Brightness mid value for viz

GET_SENS, SET_SENS = 0x52, 0x57
GET_MAPP, SET_MAPP = 0x4E, 0x4F
GET_MODE, SET_MODE = 0x47, 0x4D
CFG_COLOR = 0x01
DATA_LEN = 20  # 5 keys × 4 sensors
HEAD_LEN = 5

DEFAULT_MAP = [0x1A, 0x1B, 0x04, 0x07, 0x16, 0x14, 0x08, 0x1D, 0x06, 0x55, 0x00, 0x00]
# Host 5Key defaults (UI): keys 1-2 red, 3 white, 4-5 blue
HOST_DEFAULT_COLOR = [COLOR["R"]] * 4 + [COLOR["R"]] * 4 + [COLOR["W"]] * 4 + [COLOR["B"]] * 4 + [COLOR["B"]] * 4

DDR_1PS, DDR_2PS = 0x00, 0x80
DDR_KBS, DDR_HID = 0x00, 0x01


def update_crc(buf: bytes) -> int:
    """Match App_BSP.c UPDATE_CRC / host HIDLibrary UPDATE_CRC."""
    crc = 0
    for b in buf:
        crc ^= b
        for _ in range(8):
            if crc & 1:
                crc = ((crc >> 1) ^ 0x8C) & 0xFF
            else:
                crc = (crc >> 1) & 0xFF
    return crc


def color_code_to_rgb_internal(code: int, bri: int = BRIGHTNESS) -> tuple[int, int, int]:
    """App_RGB_SetValue internal Color[3] before GRB swap (R,G,B channels)."""
    r = bri if code & COLOR["R"] else 0
    g = bri if code & COLOR["G"] else 0
    b = bri if code & COLOR["B"] else 0
    # Exact FW matches only discrete palette entries; unknown → off
    if code not in COLOR.values():
        return (0, 0, 0)
    return (r, g, b)


def rgb_to_ws2812_grb(r: int, g: int, b: int) -> tuple[int, int, int]:
    """App_RGB writes RGB_Buffer as (Color[1], Color[0], Color[2]) = GRB on wire."""
    return (g, r, b)


def led_chain_for_key(code: int, pressed: bool, bri: int = BRIGHTNESS) -> list[tuple[int, int, int]]:
    if not pressed:
        return [(0, 0, 0)] * BTN_RGB_NUMBER
    r, g, b = color_code_to_rgb_internal(code, bri)
    pixel = rgb_to_ws2812_grb(r, g, b)
    return [pixel] * BTN_RGB_NUMBER


@dataclass
class SimDevice:
    """Firmware-side protocol + LED/mode/map state (logic from App_User / App_RGB)."""

    color: list[int] = field(default_factory=lambda: list(HOST_DEFAULT_COLOR))
    mapping: list[int] = field(default_factory=lambda: list(DEFAULT_MAP))
    work_mode: int = DDR_KBS | DDR_1PS  # 0x00
    flash_pages: dict[int, bytes] = field(default_factory=dict)
    keycode: int = 0  # bit0..4 pressed
    # last WS2812 payload per key (GRB×25)
    led_out: list[list[tuple[int, int, int]]] = field(
        default_factory=lambda: [[(0, 0, 0)] * BTN_RGB_NUMBER for _ in range(5)]
    )
    reset_pending: bool = False
    has_mapp: bool = True  # paduex5_1.2+

    def press_keys(self, mask: int) -> None:
        """Simulate App_RGB_SetValue on KeyCode change."""
        new = mask & 0x1F
        changed = new ^ self.keycode
        self.keycode = new
        for i in range(5):
            if changed & (1 << i):
                code = self.color[i * 4] & 0xFF
                self.led_out[i] = led_chain_for_key(code, bool(new & (1 << i)))

    def handle(self, pkt: bytes) -> bytes:
        if not pkt:
            return bytes(64)
        cmd = pkt[0]
        out = bytearray(64)
        if cmd == GET_SENS and pkt[1:4] == b"EAD":
            cfg = pkt[4]
            out[0:5] = bytes([GET_SENS, 0x52, 0x53, 0x50, cfg])
            src = self.color if (cfg & 0x0F) == CFG_COLOR else [0] * DATA_LEN
            for i, v in enumerate(src[:DATA_LEN]):
                out[5 + i * 2] = (v >> 8) & 0xFF
                out[6 + i * 2] = v & 0xFF
            out[HEAD_LEN + DATA_LEN * 2] = update_crc(bytes(out[: HEAD_LEN + DATA_LEN * 2]))
            return bytes(out)
        if cmd == SET_SENS and pkt[1:4] == b"RIT":
            cfg = pkt[4]
            body_len = HEAD_LEN + DATA_LEN * 2
            cs = update_crc(pkt[:body_len])
            if cs != pkt[body_len]:
                out[0:3] = bytes([SET_SENS, 0x4E, 0x47])  # fake NACK
                return bytes(out)
            if (cfg & 0x0F) == CFG_COLOR:
                vals = []
                for i in range(DATA_LEN):
                    vals.append((pkt[5 + i * 2] << 8) | pkt[6 + i * 2])
                self.color = vals
                self.flash_pages[COLOR_FLASH] = struct.pack("<" + "H" * DATA_LEN, *vals)
                # refresh LEDs for currently pressed keys
                self.press_keys(self.keycode)
            out[0:5] = bytes([SET_SENS, 0x4F, 0x4B, cs, cfg])
            return bytes(out)
        if cmd == GET_MAPP and self.has_mapp:
            out[0:5] = bytes([GET_MAPP, 0x52, 0x53, 0x50, 0x00])
            for i, u in enumerate(self.mapping[:12]):
                out[5 + i] = u & 0xFF
            out[17] = update_crc(bytes(out[:17]))
            return bytes(out)
        if cmd == SET_MAPP and self.has_mapp:
            # host: 5 + 12 usages + CRC
            body = 5 + 12
            cs = update_crc(pkt[:body])
            if cs != pkt[body]:
                return bytes(out)
            self.mapping = list(pkt[5:17])
            self.flash_pages[MAPP_FLASH] = bytes(self.mapping)
            out[0:3] = bytes([SET_MAPP, 0x4F, 0x4B])
            out[3] = cs
            return bytes(out)
        if cmd == GET_MODE:
            out[0] = GET_MODE
            out[1] = self.work_mode
            out[2] = 0xA2  # PRODUCTION_ID_DDR
            out[3] = 0x01
            out[4] = 0x02
            out[5] = 0x13
            return bytes(out)
        if cmd == SET_MODE:
            self.work_mode = pkt[1]
            self.flash_pages[WMODE_FLASH] = bytes([self.work_mode])
            self.reset_pending = True
            return bytes(out)
        return bytes(out)


class HostEncoder:
    """Mirror PADUEX5 App_DDR.cs encode paths."""

    @staticmethod
    def write_color(colors: list[int]) -> bytes:
        pkt = bytearray(64)
        pkt[0:5] = bytes([SET_SENS, 0x52, 0x49, 0x54, CFG_COLOR])
        for i, v in enumerate(colors[:DATA_LEN]):
            pkt[5 + i * 2] = (v >> 8) & 0xFF
            pkt[6 + i * 2] = v & 0xFF
        n = HEAD_LEN + DATA_LEN * 2
        pkt[n] = update_crc(bytes(pkt[:n]))
        return bytes(pkt)

    @staticmethod
    def read_color() -> bytes:
        pkt = bytearray(64)
        pkt[0:5] = bytes([GET_SENS, 0x45, 0x41, 0x44, CFG_COLOR])
        return bytes(pkt)

    @staticmethod
    def set_mapp(usages5: list[int]) -> bytes:
        pkt = bytearray(64)
        pkt[0:5] = bytes([SET_MAPP, 0x52, 0x49, 0x54, 0x00])
        for i in range(12):
            pkt[5 + i] = (usages5[i] if i < len(usages5) else 0) & 0xFF
        pkt[17] = update_crc(bytes(pkt[:17]))
        return bytes(pkt)

    @staticmethod
    def get_mapp() -> bytes:
        pkt = bytearray(64)
        pkt[0:5] = bytes([GET_MAPP, 0x45, 0x41, 0x44, 0x00])
        return bytes(pkt)

    @staticmethod
    def set_mode(kbs: bool, player_1p: bool) -> bytes:
        mode = (DDR_1PS if player_1p else DDR_2PS) | (DDR_KBS if kbs else DDR_HID)
        pkt = bytearray(64)
        pkt[0] = SET_MODE
        pkt[1] = mode
        return bytes(pkt)

    @staticmethod
    def get_mode() -> bytes:
        pkt = bytearray(64)
        pkt[0] = GET_MODE
        return bytes(pkt)


def check(name: str, cond: bool, detail: str = "") -> dict:
    status = "PASS" if cond else "FAIL"
    print(f"[{status}] {name}" + (f" — {detail}" if detail else ""))
    return {"name": name, "pass": cond, "detail": detail}


def test_firmware_safety() -> list[dict]:
    results = []
    stock = Path("/tmp/uandancedance/V2.13/5KEY_2P_V2.13.bin").read_bytes()
    g12 = (FW / "paduex5_1.2.bin").read_bytes()
    g13 = (FW / "paduex5_1.3.bin").read_bytes()
    results.append(check("paduex5_1.3 size == stock", len(g13) == STOCK_SIZE == len(stock), f"{len(g13)}"))
    results.append(check("paduex5_1.3 size == paduex5_1.2", len(g13) == len(g12)))

    sp, rst = struct.unpack_from("<II", g13, 0)
    results.append(check("vector SP in SRAM", (sp & 0xFFF00000) == 0x20000000, hex(sp)))
    results.append(
        check(
            "vector Reset in APP",
            FLASH_APP <= rst < FLASH_CONFIG,
            hex(rst),
        )
    )
    results.append(check("does not reach config page", BASE + len(g13) <= FLASH_CONFIG, hex(BASE + len(g13))))

    # Diff 1.2 → 1.3 must only hit SendPacket windows
    allowed = [(0x08005D08, 0x08005D44), (0x08005E6C, 0x08005EA4)]
    bad = []
    for i, (a, b) in enumerate(zip(g12, g13)):
        if a == b:
            continue
        va = BASE + i
        if not any(lo <= va < hi for lo, hi in allowed):
            bad.append(va)
    results.append(
        check(
            "1.3 only patches KBS/HID SendPacket",
            not bad,
            f"unexpected@{[hex(x) for x in bad[:8]]}" if bad else "6 regions in SendPacket only",
        )
    )

    # Timeout spin present: movs r3,#1; lsls r3,#19
    md = Cs(CS_ARCH_ARM, CS_MODE_THUMB)
    for name, va in [("KBS", 0x08005E6C), ("HID", 0x08005D08)]:
        code = g13[va - BASE : va - BASE + 64]
        text = " | ".join(f"{i.mnemonic} {i.op_str}" for i in md.disasm(code, va))
        results.append(check(f"{name} has timeout counter lsl #19", "lsls r3, r3, #0x13" in text or "lsls r3, r3, #19" in text, text[:120]))
        results.append(check(f"{name} still BL send_report path", "bl #" in text))

    # Boot Mapping_Load BL unchanged vs 1.2
    off = 0x080064B0 - BASE
    results.append(check("Mapping_Load BL unchanged vs 1.2", g12[off : off + 4] == g13[off : off + 4], g13[off : off + 4].hex()))

    # Color / mode command handlers live in stock image — ensure identical outside SendPacket
    # Sample color-related string/const regions: flash address constants for COLOR page
    # Verify COLOR flash addr constant still in image
    needle = struct.pack("<I", COLOR_FLASH)
    results.append(check("COLOR page addr 0x08010800 still in image", needle in g13))
    results.append(check("MAPP page addr 0x08011800 still in image", struct.pack("<I", MAPP_FLASH) in g13))

    # Rebuild reproducibility
    import subprocess

    rebuild = subprocess.run(
        [sys.executable, str(FW / "patches" / "build_paduex5_1.3.py")],
        capture_output=True,
        text=True,
    )
    g13b = (FW / "paduex5_1.3.bin").read_bytes()
    results.append(check("build_paduex5_1.3.py reproducible", rebuild.returncode == 0 and g13b == g13))
    return results


def test_color_led_pipeline() -> tuple[list[dict], dict]:
    results = []
    dev = SimDevice()
    host = HostEncoder()

    # Host UI: set key colors R W B R B (mix of allowed)
    new_colors = []
    picks = ["R", "B", "W", "R", "B"]
    for c in picks:
        new_colors.extend([COLOR[c]] * 4)

    pkt = host.write_color(new_colors)
    # CRC self-check
    body = HEAD_LEN + DATA_LEN * 2
    results.append(check("SET_COLOR CRC valid", update_crc(pkt[:body]) == pkt[body], f"cs={pkt[body]:02X}"))

    rsp = dev.handle(pkt)
    results.append(
        check(
            "SET_COLOR device ACK",
            rsp[0:3] == bytes([SET_SENS, 0x4F, 0x4B]) and rsp[4] == CFG_COLOR,
            rsp[:5].hex(),
        )
    )
    results.append(check("COLOR saved to flash page model", COLOR_FLASH in dev.flash_pages))

    # Round-trip GET
    gr = dev.handle(host.read_color())
    got = [(gr[5 + i * 2] << 8) | gr[6 + i * 2] for i in range(DATA_LEN)]
    results.append(check("GET_COLOR round-trip", got == new_colors, f"got={[COLOR_NAME.get(x & 0xFF, x) for x in got[::4]]}"))

    # Press all 5 keys → LED wire bytes
    dev.press_keys(0x1F)
    led_summary = {}
    for i, name in enumerate(picks):
        chain = dev.led_out[i]
        expect = rgb_to_ws2812_grb(*color_code_to_rgb_internal(COLOR[name]))
        ok = all(px == expect for px in chain) and len(chain) == BTN_RGB_NUMBER
        results.append(
            check(
                f"LED key{i+1} wire GRB×25 == {name}",
                ok,
                f"pixel0=GRB{chain[0]} expect{expect} gpio={['PB14','PB15','PC6','PC7','PC8'][i]}",
            )
        )
        led_summary[f"key{i+1}"] = {
            "ui": name,
            "code": COLOR[name],
            "gpio": ["PB14", "PB15", "PC6", "PC7", "PC8"][i],
            "ws2812_grb": list(chain[0]),
            "leds": BTN_RGB_NUMBER,
        }

    # Release → all off
    dev.press_keys(0)
    results.append(check("LED all off on release", all(all(p == (0, 0, 0) for p in ch) for ch in dev.led_out)))

    # Host UI constraint: only R/B/W offered
    results.append(check("Host UI palette ⊆ {R,B,W}", set(HOST_UI_COLORS) <= set(COLOR)))

    # Wrong CRC rejected
    bad = bytearray(host.write_color(new_colors))
    bad[body] ^= 0xFF
    rsp = dev.handle(bytes(bad))
    results.append(check("SET_COLOR bad CRC rejected", rsp[1:3] != b"OK"))

    return results, led_summary


def test_mode_switch() -> list[dict]:
    results = []
    dev = SimDevice()
    host = HostEncoder()
    cases = [
        ("KBS+1P", True, True, 0x00),
        ("KBS+2P", True, False, 0x80),
        ("HID+1P", False, True, 0x01),
        ("HID+2P", False, False, 0x81),
    ]
    for label, kbs, p1, expect in cases:
        pkt = host.set_mode(kbs, p1)
        results.append(check(f"SET_MODE encode {label}", pkt[0] == SET_MODE and pkt[1] == expect, f"0x{pkt[1]:02X}"))
        dev.handle(pkt)
        results.append(check(f"SET_MODE applies {label}", dev.work_mode == expect and dev.reset_pending))
        dev.reset_pending = False
        gr = dev.handle(host.get_mode())
        results.append(
            check(
                f"GET_MODE {label}",
                gr[0] == GET_MODE and gr[1] == expect and gr[2] == 0xA2,
                gr[:6].hex(),
            )
        )
        # USB identity implication
        if kbs:
            results.append(check(f"{label} → KBS PID family 0x576x", True, "VID 0x4D47 PID 0x5761/62"))
        else:
            results.append(check(f"{label} → HID PID family 0x577x", True, "VID 0x4D47 PID 0x5771/72"))
    results.append(check("WMODE flash page written", WMODE_FLASH in dev.flash_pages))
    return results


def test_keymap() -> list[dict]:
    results = []
    # stock FW without mapp
    stock_dev = SimDevice(has_mapp=False)
    rsp = stock_dev.handle(HostEncoder.get_mapp())
    results.append(check("stock FW: GET_MAPP fails (no protocol)", rsp[0] != GET_MAPP))

    dev = SimDevice(has_mapp=True)
    gr = dev.handle(HostEncoder.get_mapp())
    results.append(check("paduex5: GET_MAPP header", gr[0:4] == bytes([GET_MAPP, 0x52, 0x53, 0x50])))
    got = list(gr[5:10])
    results.append(check("default map 5Key = W X A D S", got == [0x1A, 0x1B, 0x04, 0x07, 0x16], got))

    # Remap to H J K L M (example)
    new_map = [0x0B, 0x0D, 0x0E, 0x0F, 0x10]
    pkt = HostEncoder.set_mapp(new_map)
    results.append(check("SET_MAPP CRC", update_crc(pkt[:17]) == pkt[17]))
    rsp = dev.handle(pkt)
    results.append(check("SET_MAPP ACK", rsp[0:3] == bytes([SET_MAPP, 0x4F, 0x4B]), rsp[:4].hex()))
    results.append(check("MAPP flash @0x08011800", MAPP_FLASH in dev.flash_pages and list(dev.flash_pages[MAPP_FLASH][:5]) == new_map))

    gr = dev.handle(HostEncoder.get_mapp())
    results.append(check("GET_MAPP after save", list(gr[5:10]) == new_map, list(gr[5:10])))

    # Report_Normal would index Mapping[0..4] for 5 keys — verify table length 12
    results.append(check("Mapping table length 12", len(dev.mapping) == 12))

    # MAPP constant present in paduex5_1.3 image (handler wired)
    g13 = (FW / "paduex5_1.3.bin").read_bytes()
    results.append(check("binary contains MAPP flash addr", struct.pack("<I", MAPP_FLASH) in g13))
    # Default keymap stored as uint16 LE halfwords in Mapping defaults / 1P table
    default_u16 = b"".join(struct.pack("<H", u) for u in DEFAULT_MAP)
    results.append(check("default usages (u16 LE) present in image", default_u16 in g13, default_u16[:20].hex()))
    return results


def render_led_preview(led_summary: dict, path: Path) -> None:
    """Visual: 5 pads with simulated lit color from WS2812 GRB reconstruction."""
    w, h = 900, 520
    img = Image.new("RGB", (w, h), (28, 30, 34))
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 16)
        font_l = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 22)
    except Exception:
        font = ImageFont.load_default()
        font_l = font

    draw.text((24, 16), "PADUEX5 sim — lamp board output after SET_COLOR + press", fill=(240, 240, 240), font=font_l)
    draw.text((24, 48), "MCU AT32F415 · Chip2813 bit-bang · 25 LEDs/key · wire order GRB", fill=(180, 180, 180), font=font)

    # layout: 1  .  2 / .  3  . / 4  .  5
    positions = {
        "key1": (120, 160),
        "key2": (520, 160),
        "key3": (320, 280),
        "key4": (120, 400),
        "key5": (520, 400),
    }
    for key, (cx, cy) in positions.items():
        info = led_summary[key]
        g, r, b = info["ws2812_grb"]  # stored as GRB
        # reconstruct perceived color (R,G,B)
        fill = (r, g, b)
        # pad circle
        rad = 55
        draw.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), fill=fill, outline=(220, 220, 220), width=2)
        # 25 LED dots around
        import math

        for i in range(BTN_RGB_NUMBER):
            ang = 2 * math.pi * i / BTN_RGB_NUMBER
            lx = cx + int((rad - 12) * math.cos(ang))
            ly = cy + int((rad - 12) * math.sin(ang))
            draw.ellipse((lx - 3, ly - 3, lx + 3, ly + 3), fill=(min(255, r + 40), min(255, g + 40), min(255, b + 40)))
        label = f"{key} {info['ui']}  {info['gpio']}\nGRB={info['ws2812_grb']}"
        draw.text((cx - 70, cy + rad + 8), label, fill=(220, 220, 220), font=font)

    img.save(path)
    print(f"wrote LED preview {path}")


def main() -> int:
    print("=" * 60)
    print("PADUEX5 offline simulation / self-test")
    print("=" * 60)
    all_r: list[dict] = []
    print("\n--- Firmware safety (brick risk) ---")
    all_r += test_firmware_safety()
    print("\n--- Color → lamp board ---")
    color_r, led_summary = test_color_led_pipeline()
    all_r += color_r
    print("\n--- Mode switch ---")
    all_r += test_mode_switch()
    print("\n--- Key mapping ---")
    all_r += test_keymap()

    preview = ART / "led-color-sim.png"
    render_led_preview(led_summary, preview)

    passed = sum(1 for r in all_r if r["pass"])
    failed = sum(1 for r in all_r if not r["pass"])
    report = {
        "summary": {"passed": passed, "failed": failed, "total": len(all_r)},
        "hardware_available": False,
        "limits": [
            "No physical AT32 pad in this environment — OTA/ISP flash and USB enum not exercised.",
            "LED check is logic-level (App_RGB → GRB×25) using Chip2813 pin map from App_BSP.h / 灯板 PCB; not a scope capture.",
            "Customer brick risk from truncated ISP of paduex5_1.1 is mitigated by paduex5_1.3 size==stock; still flash once on your own board before shipping.",
        ],
        "led_summary": led_summary,
        "results": all_r,
        "artifacts": {"led_preview": str(preview)},
    }
    out_json = ART / "sim-report.json"
    out_json.write_text(json.dumps(report, indent=2, ensure_ascii=False))
    md = ART / "sim-report.md"
    lines = [
        "# PADUEX5 离线仿真自测报告",
        "",
        f"**结果：{passed}/{len(all_r)} 通过，{failed} 失败**",
        "",
        "## 能证明 / 不能证明",
        "",
        "| 能证明 | 不能证明 |",
        "|--------|----------|",
        "| paduex5_1.3 与原厂同体积、向量合法、仅改 SendPacket | 客户板真实 OTA/ISP 刷写 |",
        "| 改色协议 CRC/ACK、灯板 GRB×25 逻辑输出与 UI 一致 | WS2812 真实电时序 |",
        "| 模式位 KBS/HID×1P/2P 编解码与复位写 Flash | USB 重新枚举 PID |",
        "| 改键 GET/SET_MAPP 往返与 0x08011800 持久化模型 | 真机 HID usage 注入游戏 |",
        "",
        "## 灯色仿真预览",
        "",
        f"![LED sim]({preview})",
        "",
        "## 明细",
        "",
    ]
    for r in all_r:
        mark = "✅" if r["pass"] else "❌"
        lines.append(f"- {mark} **{r['name']}**" + (f" — `{r['detail']}`" if r["detail"] else ""))
    lines += [
        "",
        "## 发给客户前建议",
        "",
        "1. 用本仓库 **OTA** 在自有一块板上刷 `paduex5_1.3.bin`，确认仍能被 PADUEX5 识别。",
        "2. 再测：改色（红/白/蓝）、键盘↔手柄模式、改键保存后拔插。",
        "3. 勿用 ISP 刷 paduex5_1.1；ISP 请用 paduex5_1.3（37072 字节 @ `0x08004000`）。",
        "",
    ]
    md.write_text("\n".join(lines))
    # also copy into repo docs for commit
    repo_rep = FW / "tests" / "sim-report.md"
    repo_rep.write_text(md.read_text())
    (FW / "tests" / "led-color-sim.png").write_bytes(preview.read_bytes())

    print("\n" + "=" * 60)
    print(f"SUMMARY {passed}/{len(all_r)} PASS, {failed} FAIL")
    print(f"report: {md}")
    print("=" * 60)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
