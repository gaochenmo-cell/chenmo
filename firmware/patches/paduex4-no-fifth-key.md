# 四键跳舞机固件：第五键永不上报

适用硬件：与五键相同的 5KEY_2P 控制板，但只插了 K1～K4 四个键的孔。
上位机 `Config.ini` 设 `KEYCOUNT=4` 即为四键界面（PADUEX4）。

## 固件里第五键在哪

主循环每次调用按键扫描函数 `0x080069CC`（`App_User.c` 的键扫描）：

1. 第一段循环（36 个传感器 = 9 键 × 4 路 ADC）：把新采样 `0x20001BAE[]` 与上次 `AppUser+0x26[]` 比较，有变化才继续；
2. 第二段循环 `0x08006A8A..0x08006BE8`：对每个传感器 `i` 做「RAW > Sensi[i] 则按下 / 全部 4 路 ≤ 阈值且保持计数归零则释放」，
   调用 `SetKeyBit(key = i/4, pressed)`（`0x08006810`）置 / 清 `AppUser.KeyCode`（`+0x1D6`）的第 `key` 位；
3. `KeyCode` 变化时按 `ConfigMode` 分发：键盘 `Report_Normal(KeyCode)`、手柄 `0x08005C20(KeyCode)`，再 `0x08005018(KeyCode)` 刷灯。

第二段循环的边界是 `0x08006BE2: cmp r4, #0x24`（36）。五键板只用 0～19，20～35 本来就不存在。

## 补丁（相对同版本的五键镜像，V3.0 起；V3.2 为 `build_paduex4_v3.2.py`）

| 地址 | 原 | 新 | 作用 |
|---|---|---|---|
| `0x08006BE2` | `24 2C`（`cmp r4,#0x24`） | `10 2C`（`cmp r4,#0x10`） | 按下/释放判定只跑传感器 0～15（K1～K4），K5 的 bit4 永远不会被置位 |
| `0x0800615E` | `13 21`（`movs r1,#0x13`） | `14 21`（`movs r1,#0x14`） | GET_MODE 回包 HVer = 0x14，上位机据此标注四键固件 |

效果：K5 在键盘 / 手柄 / RAW 调试流里都不会出现，踩踏也不会触发 K5 的灯效；K1～K4 走的代码一个字节都没变。
没有新增任何循环、等待、RAM 变量或 Flash 写入；K5 的四路 ADC 仍照常采样（DMA）。
向量表、启动、USB 命令、Flash 读写、`Report_Normal` 改键、`SendPacket` 有界等待、灵敏度掉电保存全部与同版本的 `PADUEX5_V3.x.bin` 相同。
HVer 常量只在组装 GET_MODE 回包时使用，固件其它逻辑不读它。

`build_paduex4_v3.2.py` 以 `PADUEX5_V3.2.bin` 为输入（V3.0 / V3.1 版脚本在 `legacy/patches/`），断言：输入是对应的五键镜像（扫描函数入口、循环边界、两处 `bl SetKeyBit`、HVer 与版本字节）、
输出体积 37072、`0x14C == 0x080084A5`、前 0x200 字节不变、差异恰好就是上面两个地址。

## 验证（Unicorn，跑镜像里的真实代码）

`firmware/tests/emu_keyscan_paduex4.py` 直接执行 `0x080069CC`：写新采样缓冲 → 真实的变化检测 / 阈值比较 / `SetKeyBit` / 分发；
只对 printf、USB 发包、刷灯三处外设函数打桩，并记录它们收到的 `KeyCode`。

```bash
python3 firmware/patches/build_paduex4_v3.0.py
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX4_V3.2.bin   # 52/52：K1～K4 正常，K5 永不上报
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX5_V3.2.bin   # 对照组 52/52：五键镜像上 K5 能触发
python3 firmware/tests/emu_report_normal.py firmware/PADUEX4_V3.2.bin     # 25/25 改键逻辑未受影响
python3 firmware/tests/emu_keymap_e2e.py firmware/PADUEX4_V3.2.bin        # 21/21 改键存 Flash / 重启链路未受影响
python3 firmware/tests/emu_get_mode.py firmware/PADUEX4_V3.2.bin          # 回包 47 mode A2 14 03 02
```

## 不刷这个固件会怎样

第五个孔没插板时，K5 的 ADC 输入悬空 / 被上下拉固定，通常读数低于默认阈值 200，不会触发。
若真机上发现 K5 误触发（悬空噪声），刷 `PADUEX4_V3.2.bin`，或暂时用 `KEYCOUNT=5` 的界面把 K5 四路灵敏度设到 4095。

## 已知限制

- 四键模式下方向与 K1～K4 的对应关系：K1=↑、K2=←、K3=→、K4=↓（真机实测）；只影响上位机显示，不影响固件。
- 刷回五键板请用 `PADUEX5_V3.2.bin`，`PADUEX4_V3.2.bin` 上 K5 永远无效。
