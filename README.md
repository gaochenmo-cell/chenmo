# PADUEX5

## 下载

- [`dist/PADUEX5/`](./dist/PADUEX5/) → `PADUEX5.exe`（1.0.0.57）
- [`dist/PADUEX5-win-release.zip`](./dist/PADUEX5-win-release.zip)

## 上位机 1.0.0.58

- 界面国际化：全部文案抽到 `ConfigApp/HIDDevice/Strings.resx`（英文）/ `Strings.zh-CN.resx`（简体中文），编译后生成 `zh-CN\PADUEX5.resources.dll` 语言文件。`Config.ini` 的 `LANGUAGE=AUTO|CN|EN`，默认 `AUTO` 跟随 Windows 显示语言；右上角「语言」菜单可切换（跟随系统 / 简体中文 / English），立即生效并写回 `Config.ini`。白天/黑夜主题下文案一致。
- 固件版本显示修正：不再残留上一块板子 / 读取失败前的旧版本；读 GET_MODE 时先暂停 RAW 调试流、多次重试并校验 UID；只对已知版本加标注（2.17 = grok1.7；2.12 = 原厂 V2.12/V2.13 或 grok1.6），不再把 2.16 误标成 grok1.6；窗口标题不再拼接 `Config.ini` 里过期的 `NAME=PADUEX5 V1.0.0.44`。

## 上位机 1.0.0.57

- grok1.7 固件上报 `FVer=2.17`，上位机标注 `(grok1.7)`；固件不支持改键时保存映射前会提醒。

## 上位机 1.0.0.56

- 窗口标题显示上位机版本号；设备 UID/HVer/FVer 标签移到左上角。

## 上位机 1.0.0.55

- 「按键映射」页改为通过颜色表槽位 `Color[key*4+1]` 读写键位（grok1.7 固件），新增「默认设置」。

## 上位机 1.0.0.54

- 白天/黑夜配色改为 Laverita 同款（灰阶底 + 蓝色主按钮 + 红色「默认」+ 琥珀色选中键），分组卡片、标签页、进度条全部扁平化。

- 去掉「Num 1P/2P」选择：1P/2P 由游戏按插入顺序自动分配，固件自 V2.12 起也不再接受上位机设定的 1P/2P 位。
- 升级前校验固件 USB 中断向量（0x14C），拒绝 grok1.0～1.5。

## 固件

只刷 **`firmware/grok1.7.bin`**（官方 V2.13 + 防粘键 + 键盘改键）。保守版 `grok1.6.bin` 仅防粘键。

**grok1.0～1.5 全部禁止**：它们覆盖了 USB 中断向量，OTA 后无 USB。详见 [`firmware/patches/grok1.0-Vector-Brick.md`](./firmware/patches/grok1.0-Vector-Brick.md)。

## 本地编译

Visual Studio 打开 `ConfigApp/HIDDeviceProject.sln`（.NET 4.0）。
