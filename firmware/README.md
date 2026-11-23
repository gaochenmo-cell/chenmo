# PADUEX5 / PADUEX4 固件

| 文件 | 适用 | 上报版本 | 内容 |
|---|---|---|---|
| `PADUEX5_V3.2.bin` | 五键机 | `V3.2`，HVer `13` | 原厂 `5KEY_2P_V2.13.bin` + SendPacket 有界等待（防粘键）+ 键盘模式改键 + 灵敏度掉电保存 |
| `PADUEX4_V3.2.bin` | 四键机（只插 K1～K4） | `V3.2`，HVer `14` | 同上 + 第五键永不上报 |
| `PADUEX5_P6_V3.5.bin` | **仅限 5 号口损坏的五键板**，第 5 键踏板插 6 号口 | `V3.5`，HVer `13` | 五键 V3.2 + 第 5 键的四路采样改读 6 号口 I²C 总线（PB3/PB4）、灯带改走 LED6（PC9）；正常板不要刷 |

三个镜像体积都是 37072 字节，OTA / ISP 地址 `0x08004000`，向量表、启动序列、USB 命令、Flash 代码全部原厂。
版本号格式沿用原厂 `V2.13` 的写法，从 V3.0 起步；上位机对 V3.x 固件按 HVer 标注 `(PADUEX5)` / `(PADUEX4)`。
V3.0 及更早的固件（含原厂）在 `SET_SENS` 时只把灵敏度写进 RAM；V3.1 改为三张表都写 Flash，但上电初始化（`0x08006470`）在加载完 Flash 之后仍把灵敏度表填成 200，所以拔插后还是 200；V3.2 去掉该填充循环并把回退默认表改为 200，见 [`patches/paduex5-sens-persist.md`](./patches/paduex5-sens-persist.md)。

## 构建与回归

```bash
pip install capstone keystone-engine unicorn
python3 firmware/patches/build_paduex5_v3.2.py        # legacy/PADUEX5_V3.1.bin -> PADUEX5_V3.2.bin
python3 firmware/patches/build_paduex4_v3.2.py        # PADUEX5_V3.2.bin        -> PADUEX4_V3.2.bin
python3 firmware/patches/build_paduex5_p6_v3.5.py     # PADUEX5_V3.2.bin        -> PADUEX5_P6_V3.5.bin（第 5 键在 6 号口）

python3 firmware/tests/emu_set_sens.py       firmware/PADUEX5_V3.2.bin   # 严格 Flash 模型：SET_SENS -> 复位 -> GET_SENS 36 项（legacy/PADUEX5_V3.1 应有 5 项失败：上电后 Sensi == 200）
python3 firmware/tests/emu_set_sens.py       firmware/legacy/PADUEX5_V3.0.bin  # 对照：Sensi 6 项失败
python3 firmware/tests/emu_report_normal.py  firmware/PADUEX5_V3.2.bin   # Report_Normal 改键 25 项
python3 firmware/tests/emu_keymap_e2e.py     firmware/PADUEX5_V3.2.bin   # 改键 -> 存 Flash -> 重启 -> 按键 21 项
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX4_V3.2.bin  # 键扫描 52 项，K5 永不上报
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX5_V3.2.bin  # 对照：五键镜像 K5 正常上报
python3 firmware/tests/emu_get_mode.py       firmware/PADUEX5_V3.2.bin   # GET_MODE 回包 47 mode A2 13 03 02
python3 firmware/tests/emu_get_mode.py       firmware/PADUEX4_V3.2.bin   # GET_MODE 回包 47 mode A2 14 03 02
python3 firmware/tests/emu_port6_remap.py    firmware/PADUEX5_P6_V3.5.bin # 36 项：位级 I²C 从机模型，踩 6 号口 -> K5 键码、灯在 PC9；踩 5 号口无反应
python3 firmware/tests/emu_port6_remap.py    firmware/PADUEX5_V3.2.bin   # 对照：第 5 键仍在 5 号口、灯在 PC8
python3 firmware/tests/emu_get_mode.py       firmware/PADUEX5_P6_V3.5.bin # GET_MODE 回包 47 mode A2 13 03 05
```

构建脚本会断言：所有差异都在预期地址内、`0x14C == 0x080084A5`（USB 中断向量）、体积 37072。
`emu_*` 脚本用 Unicorn 执行镜像里的真实代码，只给 Flash 外设、printf、USB 发包打桩。

## 原理文档

- [`patches/paduex5-keymap.md`](./patches/paduex5-keymap.md)：键盘模式改键与版本字节
- [`patches/paduex4-no-fifth-key.md`](./patches/paduex4-no-fifth-key.md)：四键机屏蔽第五键
- [`patches/paduex5-sens-persist.md`](./patches/paduex5-sens-persist.md)：灵敏度掉电保存（V3.1 写页 + V3.2 上电不重置）
- [`patches/paduex5-port6.md`](./patches/paduex5-port6.md)：5 号口损坏时把第 5 键整体搬到 6 号口（含 36 路通道、I²C 总线、灯带引脚与口号的对应关系）

## 旧版本

`legacy/` 保留 V3.2 之前的镜像（含 V3.0、V3.1）、构建脚本与分析文档，见 [`legacy/README.md`](./legacy/README.md)。
其中 `paduex5_1.0～1.5` 覆盖了 USB 中断向量，**禁止刷入**；上位机升级前会校验 `0x14C` 并拒绝这些镜像。

## 功能对照

| 需求 | 原厂 V2.13 | PADUEX5_V3.2 | PADUEX4_V3.2 |
|------|-----------|---------|---------|
| 灯板改色（红/蓝/白） | 有 | 有 | 有 |
| 消抖 DTick | 有 | 有 | 有 |
| ~1000 Hz 回报 | 有 | 有 | 有 |
| 灵敏度掉电保存 | 无（只写 RAM） | **有** | **有** |
| 防按键粘滞 | 无 | **有** | **有** |
| 键盘模式改键 | 无 | **有**（上位机「按键映射」页） | **有** |
| 第五键屏蔽（四键机） | 无 | 无 | **有** |

`PADUEX5_P6_V3.5.bin` 的功能与 `PADUEX5_V3.2` 相同，只是第 5 键的传感器和灯带接在 6 号口；5 号口在这一版上没有功能。

## 救砖

见 [`RECOVERY.md`](./RECOVERY.md)。
