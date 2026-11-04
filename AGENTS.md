# PADUEX5 / Yuanpad 5Key — 项目记忆（给 Cursor Agent 和新接手的人）

这份文件是整个项目的"对话记忆"。任何人 clone 本仓库后，Cursor 的 Agent 会自动读到它，不需要原来的聊天记录。

## 项目是什么

- 硬件：Yuanpad 5Key（DDR 五键跳舞毯控制板），MCU **AT32F415RBT7**（Cortex-M4），厂家代号 `uandancedance` / 5KEY_2P。
- 固件：厂家原始源码不在本仓库（曾在 `/tmp/uandancedance/src`，有 `main.c`、`AppUser/App_User.c`、`App_BSP.h`、`App_RGB.c`）。本仓库只有**二进制补丁脚本**和产出的 `.bin`。
- 上位机：`ConfigApp/`，.NET 4.0 WinForms，输出 `PADUEX5.exe`，用 `mono xbuild /p:Configuration=Release ConfigApp/HIDDeviceProject.sln` 编译；`ConfigApp/HIDDevice/Properties/AssemblyInfo.cs` 每次发布递增版本号；成品复制到 `dist/PADUEX5/` 并重新打 `dist/PADUEX5-win-release.zip`。
- 推送：`git push origin main` 和 `git push github main`（GitHub: likunyuan3276956-commits/uandepiudeshangweiji）。

## 固件内存布局（必须记住）

| 项 | 地址 |
|---|---|
| Bootloader | `0x08000000` |
| APP（OTA/ISP 写入地址，37072 字节） | `0x08004000` |
| APP 向量表 | `0x08004000`，IRQn 入口 = `0x40 + 4n` |
| **IRQ67 = OTGFS1（USB）向量** | 偏移 `0x14C`，正确值 `0x080084A5` |
| 工作模式页 WMODE | `0x0800F800` |
| SENSI / COLOR / DTICK 配置页 | `0x08010000` / `0x08010800` / `0x08011000` |
| `AppUser` 结构体 RAM | `0x200019D0`；`Color[36]` 在 `+0x146` = `0x20001B16` |
| `ConfigMode` | `0x20000008`（bit7 = 2P，低 4 位 = 键盘0/手柄1/IO4 2） |

## 血泪史：为什么 grok1.0～1.5 全部禁用

grok1.0 把补丁代码写进 `0x08004130..0x0800415F`，那是**中断向量表**。`0x14C`（USB 中断）被改成 `0x0000BD5B`，APP 一收到第一个 USB 中断就跳进乱码 → OTA 显示成功、插上无提示音、上位机找不到、只能 ISP/SWD 救。两块板因此变砖。grok1.1～1.5 全部继承了这个错误（1.2 另有 boot hook 多写 4 字节、1.3 另有 5 寄存器 push 栈不对齐，都是真 bug 但不是主因）。
详见 `firmware/patches/grok1.0-Vector-Brick.md`、`grok1.2-Brick.md`、`grok1.3-Brick.md`。

**铁律**：
1. 任何新固件必须以**官方 `5KEY_2P_V2.13.bin`** 为底，构建脚本必须断言"所有差异都在预期范围内"且 `0x14C == 0x080084A5`、体积 == 37072。
2. 不新增 USB 命令、不新增 Flash 写入代码、不新增 RAM 变量，除非找到真正的空闲区并经 Unicorn 验证。
3. 上位机 OTA 前会校验 `0x14C`（`App_OTA.cs`），拒绝 grok1.0～1.5。

## 当前可用固件

| 文件 | 内容 | 上报版本 |
|---|---|---|
| `firmware/grok1.6.bin` | 官方 V2.13 + 两处 `SendPacket` 有界等待（防粘键，60 字节） | 2.12（与原厂同） |
| **`firmware/grok1.7.bin`（推荐）** | grok1.6 + 键盘模式改键（`Report_Normal` 86 字节）+ 版本字节 | **2.17**，上位机标注 `(grok1.7)` |

原厂 V2.13 的 `FW_VERSION_MINOR` 常量仍是 12，所以原厂 2.12/2.13 和 grok1.6 都显示 2.12，无法区分；看到 2.17 才是 grok1.7。

构建：`python3 firmware/patches/build_grok1.6.py && python3 firmware/patches/build_grok1.7.py`（需要 `/tmp/uandancedance/V2.13/5KEY_2P_V2.13.bin`、capstone、keystone）。

## 关键事实（省得再反汇编一遍）

- 粘键根因：`App_USB_KBS/HID_SendPacket` 里 `while(g_*_tx_completed)` 死等。grok1.6 改为 ~0x80000 次有界循环，保留 6 寄存器 `push.w {r4-r8,lr}`（8 字节栈对齐）。
- 灯色改色、消抖 DTick、~1000Hz（bInterval=1）原厂 V2.13 本来就有，不需要新固件。
- 1P/2P：固件自 V2.12 起 `ConfigMode = (flash & 0x0F) | DEFAULT_1P2P`，上位机设的 1P/2P 位无效；游戏按插入顺序分配玩家。上位机已去掉该选项。
- 12V 供电检测：固件读 PF4/PF5 但从不通过 USB 上报，上位机无法知道。
- 改键原理（grok1.7）：`Color[key*4]` 决定灯色，`Color[key*4+1]` 原厂从不使用但随 GET/SET_SENS(type 1) 传输并存 Flash。放 `0x4B00 | usage`（'K' 标签）即为该键键值，否则走原厂 1P/2P 表（1P: W X A D S，2P: H K J N M）。仅键盘模式；usage ≤ 0x7F。详见 `firmware/patches/grok1.7-KeyMap.md`。
- 上位机改键页：读 = GET_SENS(1) 后解码；存 = 先 GET 刷新灯色再写 slot 再 SET_SENS(1)；"默认设置" = slot 清 0。调色页涂色不碰 `+1` 槽位。

## USB HID 协议速查

- VID `0x4D47`；OTA 模式 VID `0x0E8F` PID `0x8901`。
- 命令字：GET_MODE `0x47` / SET_MODE `0x4D`（写后 MCU 重启）；GET_SENS `0x52 'E' 'A' 'D' type`；SET_SENS `0x57 'R' 'I' 'T' type + 20×u16 大端 + CRC8`；SET_MRAW 调试流。type 低 4 位：0 灵敏度 / 1 颜色 / 2 消抖。
- 回包：GET → `52 52 53 50 type data… crc8`；SET → `57 4F 4B crc type`。
- CRC8：多项式 `0x8C` 反射（`firmware/tests/sim_paduex5.py::update_crc`）。
- GET_MODE 回包：`47 mode A2(UID) 13(HVer) FWmajor FWminor`。
- 灯色码：R=1 G=2 B=4 Y=3 P=5 C=6 W=7；WS2812 GRB，每键 25 颗。

## 测试（提交前要跑）

```bash
python3 firmware/tests/sim_paduex5.py                            # 协议/灯色/模式模拟 53 项
python3 firmware/tests/emu_report_normal.py firmware/grok1.7.bin # Unicorn 跑真实 Report_Normal 25 项
python3 firmware/tests/emu_keymap_e2e.py firmware/grok1.7.bin    # Unicorn 全链路：改键→存Flash→重启→按键 21 项
```
`emu_*` 只给 Flash 外设和 printf 打桩，其余全是镜像里的原字节；在 grok1.6 上跑 e2e 应有 5 项失败（对照组）。

## 上位机现状（1.0.0.58）

- 多语言：所有文案在 `ConfigApp/HIDDevice/Strings.resx`（en，中性）和 `Strings.zh-CN.resx`，代码里用 `L.Get("Key")` / `L.F("Key", args)`（`Localization.cs`）。新增文案必须两个 resx 都加。`Config.ini` `LANGUAGE=AUTO|CN|EN`（`AppConfig.cs` 读写），`AUTO` 跟随系统；切换走 `App_DDR.SetLanguage()` → `L.LanguageChanged` → `ApplyUiLanguage()`。
- 固件版本：`ReadDeviceMode()` 是唯一的 GET_MODE 入口（暂停 RAW、重试、校验 UID=0xA2）；`mFwValid` 为 false 时任何地方都不能使用 `mFwMajor/mFwMinor`。GET_MODE 回包固件只写进共享回复缓冲区（`AppUser+0x11E`），不走中断 IN 端点，靠 `HidD_GetInputReport` 取。

- 白天 Laverita 配色（`#F3F4F6/#FFFFFF/#2F6FED`，选中键琥珀 `#F59E0B`，默认按钮红 `#DC2626`）；黑夜自 1.0.0.59 起为霓虹紫（背景 `#0D0A1F`、卡片 `#171233/#241D4A`、主色 `#7C3AED`、标题 `#C084FC`、选中键 `#7C3AED→#EC4899` 渐变 + `#A855F7` 外发光、默认按钮粉 `#EC4899`）。`Palette` 类 + 递归 `ApplyTheme`，GroupBox/Tab/进度条自绘；`Palette.Glow/SelectedGradientEnd/AccentGradientEnd` 为 `Color.Empty` 时（白天）`PadKeyPainter` 不画渐变/发光，白天模式像素级不变。
- 窗口标题显示上位机版本；左上角显示 `UID/HVer/FVer (grokX.Y)`。
- 无设备时在 Linux 下可用 mono + Xvfb 跑起来截图（需用 dllmap 把 hid.dll/setupapi.dll 等映射到空桩）。

## 未完成 / 可继续的方向

- 手柄(HID)模式改键（目前只做了键盘模式）。
- grok1.7 还没有真机回报（截至 2026-11-04 用户刷的是旧 grok1.7，上报 2.12；新版应上报 2.17）。
- 已变砖的两块板需 ISP/SWD 在 `0x08004000` 刷 grok1.7 或官方 V2.13。
