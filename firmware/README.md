# PADUEX5 firmware

## `grok1.5.bin`（推荐 — 改键 + 启动修复）

体积 **37072**（= 原厂）。基于 grok1.2，但 **修正 boot hook 多写 4 字节导致的启动 HardFault**。

```bash
python3 firmware/patches/build_grok1.5.py
```

OTA / ISP 地址 **`0x08004000`**。

## 禁止使用

| 固件 | 原因 |
|------|------|
| grok1.2 | boot hook 16B 盖坏后续 memcpy → 启动变砖 |
| grok1.3 | 上述 + SendPacket 栈对齐错误 |
| grok1.4 | 修了栈，**未修 boot hook** → 仍变砖 |
| grok1.1 | 体积 +228，ISP 截断变砖 |

详见 `patches/grok1.2-Brick.md`、`patches/grok1.3-Brick.md`。

## `grok1.0.bin`

灵敏度 + 消抖；无改键；boot 未改，可作救砖保底。

## 救砖

电脑完全无 USB 时用 **ISP** 刷 `grok1.5.bin` 或 `grok1.0.bin` / 官方 V2.13 @ `0x08004000`。见 [`RECOVERY.md`](./RECOVERY.md)。
