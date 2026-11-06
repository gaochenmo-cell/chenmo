# PADUEX5 / PADUEX4 firmware

命名约定：**paduex3_x.y = 五键（PADUEX5）系列，paduex4_x.y = 四键（PADUEX4）系列**。

## 唯一推荐：`paduex3_1.7.bin`

= 官方 `5KEY_2P_V2.13.bin` + 两处 `SendPacket` 有界等待（防粘键，60 字节）+ 键盘模式改键（`Report_Normal` 86 字节）。  
向量表、启动序列、USB 命令、Flash 代码全部原厂。体积 37072，OTA/ISP 地址 `0x08004000`。

```bash
python3 firmware/patches/build_paduex3_1.6.py   # 原厂 + 防粘键
python3 firmware/patches/build_paduex3_1.7.py   # + 改键
python3 firmware/tests/emu_report_normal.py firmware/paduex3_1.7.bin   # Unicorn 验证 25/25
python3 firmware/tests/emu_keymap_e2e.py firmware/paduex3_1.7.bin      # 全链路：改 Q E S Z C → 存 Flash → 重启 → 按键 21/21
```

改键原理见 [`patches/paduex3_1.7-KeyMap.md`](./patches/paduex3_1.7-KeyMap.md)。

## 四键跳舞机：`paduex4_1.0.bin`

= paduex3_1.7 + 2 字节：键扫描循环边界 `0x08006BE2` 由 36 个传感器改为 16 个（只判定 K1～K4，K5 永不上报），版本字节改 18。
仅用于只插了 K1～K4 的板子；五键板请继续用 paduex3_1.7。上位机显示 **`FVer=2.18 (paduex4_1.0)`**。（1.0.0.62 时期此文件叫 `paduex3_1.8_4key.bin`，内容相同。）

```bash
python3 firmware/patches/build_paduex4_1.0.py                               # 以 paduex3_1.7.bin 为底
python3 firmware/tests/emu_keyscan_paduex4.py firmware/paduex4_1.0.bin     # Unicorn 跑真实键扫描 52/52（对照 1.7 也 52/52，但 K5 会触发）
```

详见 [`patches/paduex4-1.0-NoFifthKey.md`](./patches/paduex4-1.0-NoFifthKey.md)。刷入后上位机左上角应显示 **`FVer=2.17 (paduex3_1.7)`**；显示 2.12 = 原厂或 paduex3_1.6。`paduex3_1.6.bin` 保留作为无改键的保守版本。

## 禁止：paduex3_1.0 / 1.1 / 1.2 / 1.3 / 1.4 / 1.5

它们都带 paduex3_1.0 的补丁，该补丁把代码写进了 **向量表 0x14C（USB 中断）**，上电后第一个 USB 中断即崩溃 → 无提示音、上位机找不到。  
详见 [`patches/paduex3_1.0-Vector-Brick.md`](./patches/paduex3_1.0-Vector-Brick.md)。

上位机 1.0.0.52 起会在升级前校验 0x14C，直接拒绝这些镜像。

## 功能对照

| 需求 | 原厂 V2.13 | paduex3_1.6 | paduex3_1.7 | paduex4_1.0（四键） |
|------|-----------|---------|---------|---------|
| 灯板改色（红/蓝/白） | 有 | 有 | 有 | 有 |
| 消抖 DTick | 有 | 有 | 有 | 有 |
| ~1000 Hz 回报 | 有 | 有 | 有 | 有 |
| 防按键粘滞 | 无 | **有** | **有** | **有** |
| 键盘模式改键 | 无 | 无 | **有**（上位机「按键映射」页） | **有** |
| 第五键屏蔽（四键机） | 无 | 无 | 无 | **有** |

## 救砖

见 [`RECOVERY.md`](./RECOVERY.md)。
