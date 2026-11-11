# PADUEX5 / PADUEX4 开发手册

本文面向维护上位机与固件补丁的开发者，汇总了内存布局、USB 协议、固件补丁约束、上位机结构与回归测试。

## 1. 项目组成

- 硬件：Yuanpad 5Key（五键跳舞毯控制板，产品名 PADUEX5）及只插 K1～K4 的四键机（PADUEX4），主控 **AT32F415RBT7**（Cortex-M4），厂家代号 `uandancedance` / 5KEY_2P。
- 固件：厂家原始 C 源码不在本仓库，仓库只包含对原厂 `5KEY_2P_V2.13.bin` 的**二进制补丁脚本**与产出的 `.bin`（`firmware/`）。
- 上位机：`ConfigApp/`，.NET Framework 4.0 WinForms，输出 `PADUEX5.exe`。
  - Windows：Visual Studio 打开 `ConfigApp/HIDDeviceProject.sln`，Release|Any CPU。
  - Linux：`xbuild /p:Configuration=Release ConfigApp/HIDDeviceProject.sln`（mono-complete）。
  - 发布：更新 `ConfigApp/HIDDevice/Properties/AssemblyInfo.cs` 版本号，把 exe、两个 DLL、`zh-CN/`、`Config.ini`、`AppLogo.ico`、固件和说明复制到 `dist/PADUEX5/` 并重新打包。

## 2. 版本约定（V3.0 起）

| 项 | 格式 | 示例 |
|---|---|---|
| 固件文件 | `PADUEX5_Vx.y.bin` / `PADUEX4_Vx.y.bin` | `PADUEX5_V3.2.bin` |
| 固件上报 | GET_MODE 回包 FW major / minor 为整数 | `03 02` → 上位机显示 `V3.2` |
| 上位机 | `AssemblyVersion` 三段式显示 | `PADUEX5 V3.2.2` |
| 交付包 | `ConfigApp-PADUEX5-Vx.y.z-YYYYMMDD.zip` | `ConfigApp-PADUEX5-V3.2.2-20261111.zip` |

五键 / 四键固件版本号相同，由 GET_MODE 回包的 HVer 字节区分：`0x13` 五键、`0x14` 四键。

上位机已知的旧版本：`V3.0`（灵敏度不掉电保存）、`2.12` 原厂 V2.12/V2.13，`2.17` 旧五键（paduex5_1.7），`2.18` 旧四键（paduex4_1.0）；读到这些值会提示升级，不会拒绝。

## 3. 固件内存布局

| 项 | 地址 |
|---|---|
| Bootloader | `0x08000000` |
| APP（OTA/ISP 写入地址，37072 字节） | `0x08004000` |
| APP 向量表 | `0x08004000`，IRQn 入口 = `0x40 + 4n` |
| **IRQ67 = OTGFS1（USB）向量** | 偏移 `0x14C`，正确值 `0x080084A5` |
| 工作模式页 WMODE | `0x0800F800` |
| SENSI / COLOR / DTICK 配置页 | `0x08010000` / `0x08010800` / `0x08011000` |
| `AppUser` 结构体 RAM | `0x200019D0`；`Color[36]` 在 `+0x146` = `0x20001B16`；共享回复缓冲区在 `+0x11E` |
| `ConfigMode` | `0x20000008`（bit7 = 2P，低 4 位 = 键盘 0 / 手柄 1 / IO4 2） |

### 固件关键地址

| 地址 | 内容 |
|---|---|
| `0x08005D48` | `Report_Normal`（键盘报文组装），改键补丁位于 `0x08005D72..0x08005DCA` |
| `0x08005FAC` | USB OUT 包分发函数（`0x47/0x4D/0x52/0x57/0x41/0x42`） |
| `0x08006148` | GET_MODE 回包组装：`0x0800615E` HVer、`0x08006164` FW major、`0x0800616A` FW minor（均为 `movs r1,#imm`） |
| `0x080069CC` | 键扫描；`0x08006BE2 cmp r4,#imm` 为按下/释放循环边界（五键 0x24，四键 0x10） |
| `0x080049E0` | `App_Config_Command_Report`（GET/SET_SENS 处理）；`0x08004BD8..0x08004C04` 为 SET_SENS 落 Flash 的分支（V3.1 改写） |
| `0x08006470` | 上电配置加载（三次 `App_Get_Flash_Cofnig` + 打印）；`0x080064DE..0x080064F4` 原为 `Sensi[i]=200` 填充循环（V3.2 置 NOP）；`0x0800CE94` 为 SENSI 页的回退默认表（V3.2 由 800 改 200） |
| `0x08004D38` / `0x080053F0` | `App_Get_Flash_Cofnig` / `App_Set_Flash_Cofnig` |

## 4. 固件补丁约束

早期补丁（`firmware/legacy/paduex5_1.0～1.5`）把代码写进 `0x08004130..0x0800415F`，覆盖了中断向量表；`0x14C`（USB 中断）被改掉后，APP 收到第一个 USB 中断就跳进无效地址，表现为 OTA 显示成功、插上无提示音、上位机找不到设备，只能 ISP/SWD 恢复。分析见 `firmware/legacy/patches/paduex5_1.0-Vector-Brick.md`、`paduex5_1.2-Brick.md`、`paduex5_1.3-Brick.md`。

因此固件补丁必须满足：

1. 以原厂 `5KEY_2P_V2.13.bin` 为底（现在通过 `legacy/paduex5_1.6.bin` → `legacy/PADUEX5_V3.0.bin` 间接继承），构建脚本断言所有差异都在预期范围内、`0x14C == 0x080084A5`、体积 == 37072。
2. 不新增 USB 命令、不新增 Flash 写入代码、不新增 RAM 变量，除非找到真正的空闲区并经 Unicorn 验证。
3. 上位机 OTA 前校验 `0x14C`（`App_OTA.ValidateFirmware`），拒绝向量被覆盖的镜像。

### 当前补丁内容

- 防粘键：`App_USB_KBS/HID_SendPacket` 原来 `while(g_*_tx_completed)` 死等；改为约 0x80000 次的有界循环，保留 6 寄存器 `push.w {r4-r8,lr}`（8 字节栈对齐）。
- 键盘模式改键：`Color[key*4]` 决定灯色，`Color[key*4+1]` 原厂从不使用但随 GET/SET_SENS(type 1) 传输并存 Flash。写入 `0x4B00 | usage` 即为该键键值，否则走原厂 1P/2P 表（1P: W X A D S，2P: H K J N M）。仅键盘模式；usage ≤ 0x7F。详见 `firmware/patches/paduex5-keymap.md`。
- 四键版：键扫描第二段循环边界 36 → 16 个传感器，K5 的 `KeyCode` bit4 永远不置位；HVer 字节 `0x13 → 0x14`。详见 `firmware/patches/paduex4-no-fifth-key.md`。
- 灵敏度掉电保存（V3.1 + V3.2）：原厂 SET_SENS 只对 type 1（颜色）/ 2（消抖）调用 `App_Set_Flash_Cofnig`，type 0（灵敏度）只写 RAM。V3.1 把 `0x08004BD8..0x08004C04` 改为按 type 计算页地址 `0x08010800 + (type-1)*0x800` 后统一调用 `App_Set_Flash_Cofnig(addr, r7, 36)`，页能正确写入。但上电加载函数 `0x08006470` 在三次 `App_Get_Flash_Cofnig` 之后无条件执行 `for(i<36) Sensi[i]=200`（`0x080064DE..0x080064F4`），这才是设备上"出厂值 200"的来源（默认表 `0x0800CE94` 虽是 800，但随即被盖掉），也是 V3.1 拔插后仍回 200 的原因。V3.2 把该循环置 NOP、默认表改为 36×200（页空白时行为不变）、FW minor → 2。详见 `firmware/patches/paduex5-sens-persist.md`。
- 版本字节：FW major 2 → 3，minor → 1（V3.0 为 0）。

### 已知固件事实

- 灯色、消抖 DTick、约 1000 Hz 回报（bInterval=1）原厂 V2.13 已有。
- 1P/2P：固件自 V2.12 起 `ConfigMode = (flash & 0x0F) | DEFAULT_1P2P`，上位机设置的 1P/2P 位无效，游戏按插入顺序分配玩家，上位机已去掉该选项。
- 消抖：固件按 `DTick[key]` 取值而不是 `DTick[key*4]`，上位机两个索引同时写。
- 12 V 供电检测：固件读 PF4/PF5，但不经 USB 上报。
- 原厂 V2.13 的 `FW_VERSION_MINOR` 常量仍是 12，所以原厂 2.12 / 2.13 都显示 2.12。
- 配置页格式：`[0]=0xA55A`、`[1..36]` 数据、`[37]` CRC16，`App_Get_Flash_Cofnig` 只校验魔数和 CRC16，与 HVer / FVer 无关。原厂及 V3.0 以前的镜像从不写 SENSI 页 `0x08010000`。

## 5. USB HID 协议

- VID `0x4D47`；OTA（Bootloader）模式 VID `0x0E8F` PID `0x8901`。64 字节 Report，ReportID 0。
- 命令字：GET_MODE `0x47` / SET_MODE `0x4D`（写后 MCU 重启）；GET_SENS `0x52 'E' 'A' 'D' type`；SET_SENS `0x57 'R' 'I' 'T' type + 20×u16 大端 + CRC8`；SET_MRAW `0x41` 调试流（`RAWMD` 进入、`NOLMD` 退出、`CLEAR` 清零）；SET_OTA `'O' 'T' 'A'` 进入 Bootloader。
- `type` 低 4 位：0 灵敏度 / 1 颜色 / 2 消抖。每张表 20 个 u16 = 5 键 × 4 槽位。
- 回包：GET_MODE → `47 mode A2(UID) HVer FWmajor FWminor`；GET_SENS → `52 52 53 50 type data[40] crc8`；SET_SENS → `57 4F 4B crc type`。
- CRC8：多项式 `0x8C` 反射（`DeviceProtocol.Crc8`、`firmware/tests/emu_keymap_e2e.py::crc8`）。
- 灯色码：R=1 G=2 B=4 Y=3 P=5 C=6 W=7；WS2812 GRB，每键 25 颗。
- GET_MODE 回包只写进固件的共享回复缓冲区（`AppUser+0x11E`），不走中断 IN 端点，上位机用 `HidD_GetInputReport` 读取。板子处于 RAW 调试模式时主循环每次扫描都把该缓冲区改写成 `41 'M' 'R' 'A' 'W' …`，GET_MODE / GET_SENS 的回包会在主机读取前被覆盖；RAW 标志一直保留到 `NOLMD` 或重新插拔。上位机读到 AMRAW 帧时会先发 `NOLMD` 再重发，退出时若调试模式开着也会发 `NOLMD`。

## 6. 上位机结构

| 文件 | 职责 |
|---|---|
| `App_DDR.cs` | 主窗体：布局、协议读写、主题、按键映射页、调试模式 |
| `App_OTA.cs` | 固件升级（AT32 IAP：`5AA1` START → `5AA7` GETADDR → 每 1 KB `5AA2` 地址 + 17×`5AA3` 数据（块尾等 ACK）→ `5AA4` FINISH → `5AA6` JUMP）。Bootloader 没有读回命令，所以升级前校验镜像（向量表、`0x14C`、`FirmwareImageInfo` 解析 HVer/FVer 并与板子比对），升级后由 `App_DDR.Verify_Firmware_AfterOta` 等板子重新枚举、用 GET_MODE 核对版本（`OTA-VERIFY`） |
| `DeviceProtocol.cs` | 回包分类 / 解析、CRC8；`FirmwareImageInfo` 从 bin 文件偏移 `0x215E/0x2164/0x216A` 读 HVer / FW major / FW minor（`movs r1,#imm`，`imm 21`），算 SHA-256 |
| `DeviceProtocol.cs` | 回包分类与解析（`ReplyKind`、`ModeReply`、`HVER_5KEY/HVER_4KEY`、CRC8） |
| `Localization.cs` | `L.Get/L.F`，资源在 `Strings.resx`（英文，中性）与 `Strings.zh-CN.resx`；新增文案两个文件都要加 |
| `AppConfig.cs` | `Config.ini` `[APP Config]` 读写（`LANGUAGE=AUTO|CN|EN`、`THEME=LIGHT|DARK`、`KEYCOUNT=4|5`、`DEBUGLOG=1|0`） |
| `DebugLog.cs` | exe 同目录 `PADUEX5.log`（2 MB 轮转），记录每笔 USB 收发与判定结果 |
| `UiFont.cs` | 字体族回退链（Microsoft YaHei UI → Microsoft YaHei → 系统默认）与字号常量，随 `ApplyTheme` 下发 |
| `PadKeyPainter.cs` | 踏板按钮矢量绘制（三层 chevron、中心圆环、踩踏高亮） |

要点：

- `ReadDeviceMode()` 是唯一的 GET_MODE 入口；`mFwValid` 为 false 时不得使用 `mFwMajor/mFwMinor`。`FwVersionText()` 对 major ≥ 3 显示 `V3.2`，对 2.x 保留 `2.17` 写法；`FwTag()` 按 HVer 标注 `(PADUEX5)` / `(PADUEX4)`，旧版本提示升级；`FwHasKeyMap()` = V3.0+ 或 2.17+；`FwPersistsSensitivity()` = V3.1+。
- 保存流程：`Write_Config_Value` 发 SET_SENS 并检查 ACK 后，调用 `Verify_Config_Written` 用 `Read_Config_Core(quiet)` 回读同一张表逐项比对，不一致弹「保存未生效」并在 `PADUEX5.log` 记下发送值与回读值；灵敏度在 `FwPersistsSensitivity()` 为 false 的固件上保存成功也会提示拔插后会恢复出厂值。
- 四键 / 五键一套代码：`KeyCount`（`ReadAppConfig` 读 `KEYCOUNT`）、`FourKeyMode`、`AppName`、`FourKeyKinds[]`（K1=↑ K2=←  K3=→ K4=↓）。报文仍为 20 个 u16，四键模式下 K5 槽位原样回写。
- 按键映射页：读取 = GET_SENS(1) 后解码 `Color[key*4+1]`；保存 = 先 GET 刷新灯色，再写 slot，再 SET_SENS(1)；默认 = slot 清 0。调色页涂色不碰 `+1` 槽位。
- 踩踏高亮颜色取自调色页灯色（`padLedKnown[]` 标记是否已从设备读到），未知时回退默认色。非调试模式下 `timer1` 把共享回复缓冲区字节 1/2 当键位掩码。
- 主题：白天灰阶 + 蓝主色（`#F3F4F6/#FFFFFF/#2F6FED`，选中键 `#F59E0B`）；黑夜霓虹紫（背景 `#0D0A1F`、主色 `#7C3AED`、选中键 `#7C3AED→#EC4899` 渐变）。`Palette` + 递归 `ApplyTheme`，GroupBox / Tab / 进度条自绘。
- OTA 对话框固定像素布局（`AutoScaleMode.None`），用 `UiFont.Refamily` 换字体族。

## 7. 回归测试

固件（需 `pip install capstone keystone-engine unicorn`）：

```bash
python3 firmware/patches/build_paduex5_v3.1.py
python3 firmware/patches/build_paduex4_v3.1.py
python3 firmware/tests/emu_set_sens.py       firmware/PADUEX5_V3.2.bin   # 36 项：严格 Flash 模型，SET_SENS → 复位（真实 0x08006470）→ GET_SENS（legacy/PADUEX5_V3.1 应有 5 项失败、V3.0 9 项，作对照）
python3 firmware/tests/emu_report_normal.py  firmware/PADUEX5_V3.2.bin   # 25 项
python3 firmware/tests/emu_keymap_e2e.py     firmware/PADUEX5_V3.2.bin   # 21 项（legacy/paduex5_1.6 上应有 5 项失败，作对照）
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX4_V3.2.bin  # 52 项
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX5_V3.2.bin  # 52 项（对照）
python3 firmware/tests/emu_get_mode.py       firmware/PADUEX5_V3.2.bin   # HVer 13 / FW 3.2
python3 firmware/tests/emu_get_mode.py       firmware/PADUEX4_V3.2.bin   # HVer 14 / FW 3.2
```

上位机：`xbuild /p:Configuration=Release ConfigApp/HIDDeviceProject.sln` 编译通过；无设备时可在 Linux 用 mono + Xvfb 运行界面截图——把桩 `.so` 复制成 `hid.dll` / `setupapi.dll` / `kernel32.dll` / `user32.dll` 放到 exe 目录即可（mono 6 不读 `.config` 里的 dllmap），桩的 `CreateFile` 必须返回 mono 自己打开过的 fd。真机问题先看 `PADUEX5.log`。

## 8. 待办

- 手柄(HID)模式改键（目前只做了键盘模式）。
- 已刷入 legacy/paduex5_1.0～1.5 的板子需 ISP/SWD 在 `0x08004000` 刷 `PADUEX5_V3.2.bin` 或官方 V2.13。
