# paduex3_1.8_4key：四键跳舞机专用——第五键永不上报（只改 2 字节）

适用硬件：与五键相同的 5KEY_2P 控制板，但只插了 K1～K4 四个键的孔。
上位机 1.0.0.62 起 `Config.ini` 设 `KEYCOUNT=4` 即为四键界面（PADUEX4），FVer 2.18 标注 `(paduex3_1.8-4key)`。

## 固件里第五键在哪

主循环每次调用按键扫描函数 `0x080069CC`（`App_User.c` 的键扫描）：

1. 第一段循环（36 个传感器 = 9 键 × 4 路 ADC）：把新采样 `0x20001BAE[]` 与上次 `AppUser+0x26[]` 比较，有变化才继续；
2. 第二段循环 `0x08006A8A..0x08006BE8`：对每个传感器 `i` 做「RAW > Sensi[i] 则按下 / 全部 4 路 ≤ 阈值且保持计数归零则释放」，
   调用 `SetKeyBit(key = i/4, pressed)`（`0x08006810`）置 / 清 `AppUser.KeyCode`（`+0x1D6`）的第 `key` 位；
3. `KeyCode` 变化时按 `ConfigMode` 分发：键盘 `Report_Normal(KeyCode)`、手柄 `0x08005C20(KeyCode)`，再 `0x08005018(KeyCode)` 刷灯。

第二段循环的边界是 `0x08006BE2: cmp r4, #0x24`（36）。五键板只用 0～19，20～35 本来就不存在。

## 补丁

| 地址 | 原 | 新 | 作用 |
|---|---|---|---|
| `0x08006BE2` | `24 2C`（`cmp r4,#0x24`） | `10 2C`（`cmp r4,#0x10`） | 按下/释放判定只跑传感器 0～15（K1～K4），K5 的 bit4 永远不会被置位 |
| `0x0800616A` | `11 21`（`movs r1,#17`） | `12 21`（`movs r1,#18`） | GET_MODE 上报 2.18 |

效果：K5 在键盘 / 手柄 / RAW 调试流里都不会出现，踩踏也不会触发 K5 的灯效；K1～K4 走的代码一个字节都没变。
没有新增任何循环、等待、RAM 变量或 Flash 写入；K5 的四路 ADC 仍照常采样（DMA），CPU 负担只少不多。
向量表、启动、USB 命令、Flash 读写、`Report_Normal` 改键、`SendPacket` 有界等待全部与 paduex3_1.7 相同。

`build_paduex3_1.8_4key.py` 以 `paduex3_1.7.bin` 为输入，断言：输入是 paduex3_1.7（扫描函数入口、循环边界、两处 `bl SetKeyBit`、版本字节）、
输出体积 37072、`0x14C == 0x080084A5`、前 0x200 字节不变、**差异恰好就是上面两个地址**。

## 验证（Unicorn，跑镜像里的真实代码）

`firmware/tests/emu_keyscan_4key.py` 直接执行 `0x080069CC`：写新采样缓冲 → 真实的变化检测 / 阈值比较 / `SetKeyBit` / 分发；
只对 printf、USB 发包、刷灯三处外设函数打桩，并记录它们收到的 `KeyCode`。

```bash
python3 firmware/patches/build_paduex3_1.8_4key.py
python3 firmware/tests/emu_keyscan_4key.py firmware/paduex3_1.8_4key.bin   # 52/52：K1～K4 每路按下/释放正常，K5 四路都为 0，键盘/手柄/灯都收不到 bit4
python3 firmware/tests/emu_keyscan_4key.py firmware/paduex3_1.7.bin        # 对照组 52/52：同一脚本在 1.7 上 K5 能触发（0x10）
python3 firmware/tests/emu_report_normal.py firmware/paduex3_1.8_4key.bin  # 25/25 改键逻辑未受影响
python3 firmware/tests/emu_keymap_e2e.py firmware/paduex3_1.8_4key.bin     # 21/21 改键存 Flash / 重启链路未受影响
```

## 不刷这个固件会怎样

第五个孔没插板时，K5 的 ADC 输入悬空 / 被上下拉固定，通常读数低于默认阈值 200，不会触发；对性能也没有影响。
若真机上发现 K5 误触发（悬空噪声），有两种处理：刷 `paduex3_1.8_4key.bin`（根治），或暂时用 `KEYCOUNT=5` 的界面把 K5 四路灵敏度设到 4095。

## 已知限制

- 四键模式下上/下/左/右与 K1～K4 的对应关系（K1=←、K2=↑、K3=↓、K4=→）是按接线顺序推断的，需真机确认；只影响上位机显示，不影响固件。
- 刷回五键板请用 `paduex3_1.7.bin`，`paduex3_1.8_4key.bin` 上 K5 永远无效。
