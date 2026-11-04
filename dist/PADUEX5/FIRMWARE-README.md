# PADUEX5 firmware

## 唯一推荐：`grok1.7.bin`

= 官方 `5KEY_2P_V2.13.bin` + 两处 `SendPacket` 有界等待（防粘键，60 字节）+ 键盘模式改键（`Report_Normal` 86 字节）。  
向量表、启动序列、USB 命令、Flash 代码全部原厂。体积 37072，OTA/ISP 地址 `0x08004000`。

```bash
python3 firmware/patches/build_grok1.6.py   # 原厂 + 防粘键
python3 firmware/patches/build_grok1.7.py   # + 改键
python3 firmware/tests/emu_report_normal.py firmware/grok1.7.bin   # Unicorn 验证 25/25
python3 firmware/tests/emu_keymap_e2e.py firmware/grok1.7.bin      # 全链路：改 Q E S Z C → 存 Flash → 重启 → 按键 21/21
```

改键原理见 [`patches/grok1.7-KeyMap.md`](./patches/grok1.7-KeyMap.md)。`grok1.6.bin` 保留作为无改键的保守版本。

## 禁止：grok1.0 / 1.1 / 1.2 / 1.3 / 1.4 / 1.5

它们都带 grok1.0 的补丁，该补丁把代码写进了 **向量表 0x14C（USB 中断）**，上电后第一个 USB 中断即崩溃 → 无提示音、上位机找不到。  
详见 [`patches/grok1.0-Vector-Brick.md`](./patches/grok1.0-Vector-Brick.md)。

上位机 1.0.0.52 起会在升级前校验 0x14C，直接拒绝这些镜像。

## 功能对照

| 需求 | 原厂 V2.13 | grok1.6 | grok1.7 |
|------|-----------|---------|---------|
| 灯板改色（红/蓝/白） | 有 | 有 | 有 |
| 消抖 DTick | 有 | 有 | 有 |
| ~1000 Hz 回报 | 有 | 有 | 有 |
| 防按键粘滞 | 无 | **有** | **有** |
| 键盘模式改键 | 无 | 无 | **有**（上位机「按键映射」页） |

## 救砖

见 [`RECOVERY.md`](./RECOVERY.md)。
