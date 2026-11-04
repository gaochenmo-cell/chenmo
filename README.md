# PADUEX5

## 下载

- [`dist/PADUEX5/`](./dist/PADUEX5/) → `PADUEX5.exe`（1.0.0.60）
- [`dist/PADUEX5-win-release.zip`](./dist/PADUEX5-win-release.zip)

## 上位机 1.0.0.60

- 修复真机「未读到设备版本 / 灵敏度全 0」：根因是板子停留在 RAW 调试模式（上次开过「调试模式」后直接关程序，固件不会自己退出），固件每次扫描都把回包缓冲区覆盖成 `AMRAW` 调试帧，GET_MODE/GET_SENS 的回包在主机读到之前就没了。现在读到 `AMRAW` 帧会自动发 `NOLMD` 退出调试流后重发；关闭窗口时若调试模式开着也会发 `NOLMD`。
- 读取放宽：首字节是命令字就接受，UID≠A2 / HVer≠13 只在版本标签后加注，不再拒收；读不到时显示具体原因（HID 读取失败 / 设备未回复 / 仍在 RAW 模式 / 回包格式不对）。
- 诊断日志：`Config.ini` 新增 `DEBUGLOG=1`（默认开），在 exe 同目录写 `PADUEX5.log`，记录 USB 连接、每笔命令的发送字节、收到的 64 字节原始 hex、判定结果与最终状态；反馈问题时附上这个文件。

## 上位机 1.0.0.59

- 黑夜主题改为霓虹紫配色。

## 上位机 1.0.0.58

- 界面国际化：全部文案抽到 `ConfigApp/HIDDevice/Strings.resx`（英文）/ `Strings.zh-CN.resx`（简体中文），编译后生成 `zh-CN\PADUEX5.resources.dll` 语言文件。`Config.ini` 的 `LANGUAGE=AUTO|CN|EN`，默认 `AUTO` 跟随 Windows 显示语言；右上角「语言」菜单可切换（跟随系统 / 简体中文 / English），立即生效并写回 `Config.ini`。白天/黑夜主题下文案一致。
- 固件版本显示修正：不再残留上一块板子 / 读取失败前的旧版本；读 GET_MODE 时先暂停 RAW 调试流、多次重试并校验 UID；只对已知版本加标注（2.17 = paduex3_1.7；2.12 = 原厂 V2.12/V2.13 或 paduex3_1.6），不再把 2.16 误标成 paduex3_1.6；窗口标题不再拼接 `Config.ini` 里过期的 `NAME=PADUEX5 V1.0.0.44`。

## 上位机 1.0.0.57

- paduex3_1.7 固件上报 `FVer=2.17`，上位机标注 `(paduex3_1.7)`；固件不支持改键时保存映射前会提醒。

## 上位机 1.0.0.56

- 窗口标题显示上位机版本号；设备 UID/HVer/FVer 标签移到左上角。

## 上位机 1.0.0.55

- 「按键映射」页改为通过颜色表槽位 `Color[key*4+1]` 读写键位（paduex3_1.7 固件），新增「默认设置」。

## 上位机 1.0.0.54

- 白天/黑夜配色改为 Laverita 同款（灰阶底 + 蓝色主按钮 + 红色「默认」+ 琥珀色选中键），分组卡片、标签页、进度条全部扁平化。

- 去掉「Num 1P/2P」选择：1P/2P 由游戏按插入顺序自动分配，固件自 V2.12 起也不再接受上位机设定的 1P/2P 位。
- 升级前校验固件 USB 中断向量（0x14C），拒绝 paduex3_1.0～1.5。

## 固件

只刷 **`firmware/paduex3_1.7.bin`**（官方 V2.13 + 防粘键 + 键盘改键）。保守版 `paduex3_1.6.bin` 仅防粘键。

**paduex3_1.0～1.5 全部禁止**：它们覆盖了 USB 中断向量，OTA 后无 USB。详见 [`firmware/patches/paduex3_1.0-Vector-Brick.md`](./firmware/patches/paduex3_1.0-Vector-Brick.md)。

## 本地编译

Visual Studio 打开 `ConfigApp/HIDDeviceProject.sln`（.NET 4.0）。
