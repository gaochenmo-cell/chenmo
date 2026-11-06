# PADUEX5 / PADUEX4 V3.2.1 发布说明（2026-11-06）—— 灵敏度拔插后恢复 200（第二轮）+ 固件升级核对

分支 `fix/sensitivity-persist`，PR [#8](https://github.com/gaochenmo-cell/chenmo/pull/8)（base：`release/v3.0`）。取代 `2026-11-06-sens-persist/` 的 V3.1.0。

## 本目录文件

| 文件 | 说明 | SHA-256 |
|---|---|---|
| `ConfigApp-PADUEX5-V3.2.1-20261106.zip` | 五键上位机包（`Config.ini` `KEYCOUNT=5`、`NAME=PADUEX5 V3.2.1`），内含两个 V3.2 固件与说明 | `4a737d73…e3095c` |
| `ConfigApp-PADUEX4-V3.2.1-20261106.zip` | 四键上位机包（`KEYCOUNT=4`、`NAME=PADUEX4 V3.2.1`），exe 与五键包相同 | `6b935d50…421264` |
| `PADUEX5_V3.2.bin` | 五键固件，37072 字节，`0x14C = 0x080084A5`，上报 `V3.2` HVer `13` | `83cb64af…4ff2c8` |
| `PADUEX4_V3.2.bin` | 四键固件，37072 字节，`0x14C = 0x080084A5`，上报 `V3.2` HVer `14` | `3948c15a…4bdbe5` |

两个包里都有 `DEBUGLOG=1`，日志在 exe 同目录 `PADUEX5.log`。固件 bin 与 3.2.0 发布时完全相同（SHA-256 未变）；上位机由 3.2.0 升到 3.2.1，见「三之二」。

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

## 三之二、上位机 3.2.1：固件升级为什么要再核对一次

3.2.0 的真机日志（`docs/issues/sens-persist-v32-PADUEX5.log`）：20:15:28 板子进 Bootloader（`0E8F:8901`），20:15:39 回到 `4D47` 后 GET_MODE 仍是 `03 01`（V3.1），随后四次保存的 `REBOOT-VERIFY` 全部 LOST。REBOOT-VERIFY 工作正常，是 V3.2 没有刷进去（交付的 `PADUEX4_V3.2.bin` 在 `0x216A` 确为 `02 21`，文件没问题；最可能是选到了旧目录里的 V3.1 bin）。3.2.0 的升级窗口不显示文件版本、不比对板子版本、升级后也不核对，所以这种情况完全无感。3.2.1：

- **选文件**：显示「文件固件: V3.2（PADUEX4 四键）」；日志 `OTA: file "<完整路径>" size=37072 sha256=… HVer=14 FVer=V3.2 (03 02)`。HVer / FVer 从 bin 的文件偏移 `0x215E` / `0x2164` / `0x216A`（APP 地址 `0x0800615E` / `0x08006164` / `0x0800616A`，`movs r1,#imm`）解析，三个 `21` 操作码对不上就按「无版本常量」处理。
- **开始前核对**（都可选择继续，选择写入日志）：文件 HVer 与板子不符（五键 / 四键互刷）；文件版本低于板子；文件版本等于板子（提醒升级后无法通过版本判断是否写入）；文件没有版本常量。确认框同时列出「文件固件」与「板子当前」。
- **升级过程日志**：进入 Bootloader 的方式（已在 OTA 模式 / 发 `SET_OTA` 后多少毫秒枚举）、HID 口 In/Out 报文长度、`START(5AA1)` ACK 与 Report-ID 前缀判定、`GETADDR` 回包、每个 1 KB 块 `block n/37 @0x0800xxxx 1024 bytes acknowledged (RX=…)`、`FINISH(5AA4)` ACK 与累计字节数、`JUMP(5AA6)` 是否 ACK、失败时已写字节数与原因。
- **升级后核对**：关闭升级窗口后主窗口等板子以 APP 重新枚举（≤ 15 s），用 GET_MODE 读版本与文件比对：`OTA-VERIFY: OK board reports HVer=14 FVer=V3.2 as in the file` / `OTA-VERIFY: MISMATCH file HVer=14 FVer=V3.2 (03 02); board now HVer=14 FVer=V3.1` / `OTA-VERIFY: NO DEVICE`。不一致弹「固件版本未变：文件是 V3.2，板子仍上报 V3.1，升级可能未写入」。
- **OTA 写入路径核对结果**：AT32 IAP Bootloader 没有读回命令，上位机只能依赖每 1 KB 块的 ACK（Bootloader 收满 1024 字节、编程完成后才回）与 `FINISH` ACK，这部分原代码没有静默吞错；发现并修复的一处是块内（非块尾）数据包连写两次失败时被 `if (lastOfBlock) throw` 跳过，留到块尾超时报一个含糊的「数据块 ACK 失败」，现改为立即报「数据包发送失败（第 n 块，块内偏移 m）」。`JUMP` 的 ACK 之前被忽略，现写入日志（Bootloader 通常先复位再无暇应答，以重新枚举后的版本核对为准）。文件选错（HVer 不符 / 旧版本）之前没有任何提示，现按上面的核对处理。
- **保存灵敏度前**：固件低于 V3.2 时先弹「当前固件 V3.1 不会保存灵敏度，每次上电都恢复 200。请先点「固件升级」升级到 V3.2。仍要写入吗？」（默认取消）。
- `REBOOT-VERIFY` 的「board back after」改为真实耗时。

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
- `ota-verify-mismatch.png`：V3.1 模型 + V3.2 文件，升级后核对弹「固件版本未变…」；`ota-verify-ok-en.png`：一致时的提示；`save-oldfw-warning.png`：V3.1 上保存灵敏度前的确认；`log-sample-ota-verify.txt`：`OTA-VERIFY` 与升级前核对（五键文件刷四键板 / 降级 / 同版本 / 无版本常量）的日志。

## 五、升级步骤

1. 解压对应包，运行 `PADUEX5.exe`，点「固件升级」，选**本目录**里的 `PADUEX4_V3.2.bin`（四键）或 `PADUEX5_V3.2.bin`（五键）——文件信息行应显示「文件固件: V3.2」。升级窗口关闭后会自动核对，应弹「升级已核对：板子现在上报固件 V3.2」，左上角 `FVer=V3.2`。若弹「固件版本未变」，把 `PADUEX5.log` 发回（里面有所选文件的完整路径、SHA-256 与每一步 ACK）。
2. 灵敏度页改值 →「保存设置」，等板子重启（约 2～3 s）后应弹「已确认写入 Flash」。
3. 拔插 USB →「读取设置」，数值应保持。若仍为 200，请把 `PADUEX5.log` 发回。
