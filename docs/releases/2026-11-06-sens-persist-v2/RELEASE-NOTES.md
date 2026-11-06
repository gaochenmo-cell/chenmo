# PADUEX5 / PADUEX4 V3.2.0 发布说明（2026-11-06）—— 灵敏度拔插后恢复 200（第二轮）

分支 `fix/sensitivity-persist`，PR [#8](https://github.com/gaochenmo-cell/chenmo/pull/8)（base：`release/v3.0`）。取代 `2026-11-06-sens-persist/` 的 V3.1.0。

## 本目录文件

| 文件 | 说明 | SHA-256 |
|---|---|---|
| `ConfigApp-PADUEX5-V3.2.0-20261106.zip` | 五键上位机包（`Config.ini` `KEYCOUNT=5`、`NAME=PADUEX5 V3.2.0`），内含两个 V3.2 固件与说明 | `dd5c9529…3ea2db` |
| `ConfigApp-PADUEX4-V3.2.0-20261106.zip` | 四键上位机包（`KEYCOUNT=4`、`NAME=PADUEX4 V3.2.0`），exe 与五键包相同 | `e593e498…dbcd4e` |
| `PADUEX5_V3.2.bin` | 五键固件，37072 字节，`0x14C = 0x080084A5`，上报 `V3.2` HVer `13` | `83cb64af…4ff2c8` |
| `PADUEX4_V3.2.bin` | 四键固件，37072 字节，`0x14C = 0x080084A5`，上报 `V3.2` HVer `14` | `3948c15a…4bdbe5` |

两个包里都有 `DEBUGLOG=1`，日志在 exe 同目录 `PADUEX5.log`。

## 一、V3.1 为什么在真机上还是不行

真机日志（PADUEX4 3.1.0，固件 `V3.1` HVer 14）：`SET_SENS` 回 `57 4F 4B`，即时 `GET_SENS` 与保存值一致；拔插后 `GET_SENS` 回 20 × `00 C8`（200）。同一块板的颜色页（type 1）和工作模式页都能保住。

V3.1 的页写入本身是对的——灵敏度页走的是和颜色页**同一个** `App_Set_Flash_Cofnig(0x08010000, Sensi, 36)`，格式 `[0]=0xA55A`、36 × u16、`[37]=CRC16`，`App_Get_Flash_Cofnig` 校验通过。问题在上电：

```
08006470  上电配置加载（main 调一次）
08006492    App_Get_Flash_Cofnig(0x08010000, Sensi, 默认表 0x0800CE94 = 36×800, 36)
0800649e    App_Get_Flash_Cofnig(0x08010800, Color, …)
080064ac    App_Get_Flash_Cofnig(0x08011000, DTick, …)
080064b8    打印三张表
080064de    for (i = 0; i < 36; i++) Sensi[i] = 200;     ← 无条件覆盖
080064f4    pop {r4,pc}
```

`0x080064DE..0x080064F4` 在加载完成**之后**把 `Sensi[0..35]` 全部填成 200。原厂从不写 SENSI 页，所以这个循环就是原厂把阈值定成 200 的方式——也解释了用户看到的"出厂值"是 200 而不是默认表里的 800（之前文档写"回退 800"不对，800 表存在但随即被盖掉）。V3.1 把页写进 Flash 后，每次上电照样被这一段冲掉。

之前的 Unicorn 用例只调了三次 `App_Get_Flash_Cofnig`，没有跑完整的 `0x08006470`，所以没看到。

## 二、固件 V3.2 补丁（相对 V3.1，共 94 字节）

| 地址 | 原来 | 现在 | 说明 |
|---|---|---|---|
| `0x080064DE..0x080064F4`（22 字节） | `Sensi[i] = 200` 循环 | 11 × `nop` | 保留从 Flash 加载的值 |
| `0x0800CE94..0x0800CEDC`（72 字节数据） | 36 × 800 | 36 × 200 | SENSI 页为空 / 校验失败时的回退表，唯一引用在 `0x08006500`；没保存过的板子上电仍是 200 |
| `0x0800616A` | `movs r1,#1` | `movs r1,#2` | GET_MODE 上报 V3.2 |

四键版另有 `0x0800615E`（HVer 0x14）、`0x08006BE2`（扫描边界 0x10），与 V3.0 / V3.1 相同。向量表、USB 命令、Flash 驱动不变，构建脚本断言差异集合、`0x14C`、体积。

## 三、上位机 3.2.0

- 「保存设置」（灵敏度 / 调色 / 延迟释放都一样）：SET_SENS → ACK → 即时回读 → **发 SET_MODE（当前模式）让板子复位 → 等重新枚举（最长 12 s）→ 再 GET_SENS 逐项比对**。重启后仍一致才弹「保存完成。设备已重启，重启后回读与保存值一致，已确认写入 Flash」。
- 不一致弹「设备已应答并通过即时回读，但重启后数值没有保住（Kx-y：保存 a，重启后 b）」；固件 < V3.2 时附「固件 V3.x 上电时会把灵敏度重置为出厂值，请点「固件升级」升级到 V3.2」，否则附「请把程序目录下的 PADUEX5.log 发给我们」。12 s 内设备没回来：「设备 12 秒内没有重新连接，请拔插 USB」。
- GET_SENS 回包无效（无应答 / RAW 调试帧 / CRC 错）时弹错误并保留界面原值，不回退默认值。
- 固件 < V3.2 时版本标签加注「建议升级 V3.2（灵敏度不掉电保存）」。
- `PADUEX5.log` 记录每笔 TX / RX 原始 64 字节、`REBOOT-VERIFY` 的每一步（发 SET_MODE、几毫秒后回来、回读结果）。

## 四、验证

### Unicorn（镜像真实代码 + 严格 Flash 模型）

`firmware/tests/emu_set_sens.py` 现在执行镜像里的 USB 分发器 `0x08005FAC` → `App_Config_Command_Report` → `App_Set_Flash_Cofnig` → Flash 驱动（解锁 / 擦页 / 半字编程 / 上锁 / 2 KB 读改写）；上电执行完整的 `0x08006470`。Flash 控制器按真实语义建模：上锁或 PG 未置位的写被拒绝并置 PRGMERR；编程只能 1→0；PER+START 擦 2 KB 页。

| 镜像 | 结果 |
|---|---|
| `PADUEX5_V3.2.bin` / `PADUEX4_V3.2.bin` | 36/36：ACK、RAM、`App_Set_Flash_Cofnig(0x08010000, Sensi, 36)`、解锁→擦→128 次编程→锁、页 `A55A`+数据+CRC16、两次掉电重启后仍是保存值、CRC8 错误被拒、重写已有页 |
| `legacy/PADUEX5_V3.1.bin` / `PADUEX4_V3.1.bin` | 5 项失败，全部是「上电后 Sensi == 200」，页内容正确——与真机日志逐条对应 |
| `legacy/PADUEX5_V3.0.bin`、`paduex5_1.6.bin`（原厂） | 9 项失败：灵敏度从不写 Flash |

其余用例在两款 V3.2 镜像上全部通过：`emu_report_normal` 25、`emu_keymap_e2e` 21、`emu_keyscan_paduex4` 52（四键 K5 永不上报 / 五键对照）、`emu_get_mode`（`47 mode A2 13/14 03 02`）。

### 上位机（mono + Xvfb + 模拟设备）

截图与日志样本见 `media/sens-persist-v2/`：

- `save-reboot-verified.png` / `-en.png`：V3.2 模型，保存 → 板子复位（模拟 1.5 s 离线）→ 回读一致。
- `save-reboot-lost-v31.png`：V3.1 四键模型（页写入正确、上电填 200），弹「重启后数值没有保住（K1-A：保存 300，重启后 200）… 升级到 V3.2」。
- `save-reboot-nodevice.png`：复位后设备 12 s 内没回来的提示。
- 丢弃写入的设备：即时回读即报「保存未生效（K1-A：写入 300，设备回读 200）」。
- `log-sample-v32.txt` / `log-sample-v31.txt`：完整的 TX / RX / REBOOT-VERIFY 日志。

## 五、升级步骤

1. 解压对应包，运行 `PADUEX5.exe`，点「固件升级」刷 `PADUEX4_V3.2.bin`（四键）或 `PADUEX5_V3.2.bin`（五键），左上角应显示 `FVer=V3.2`。
2. 灵敏度页改值 →「保存设置」，等板子重启（约 2～3 s）后应弹「已确认写入 Flash」。
3. 拔插 USB →「读取设置」，数值应保持。若仍为 200，请把 `PADUEX5.log` 发回。
