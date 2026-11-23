# PADUEX5 / PADUEX4

Yuanpad 五键（PADUEX5）与四键（PADUEX4）跳舞毯控制板的 Windows 配置工具与固件。

## 下载

- [`dist/PADUEX5/`](./dist/PADUEX5/) → `PADUEX5.exe`（V3.2.1）
- [`dist/PADUEX5-win-release.zip`](./dist/PADUEX5-win-release.zip)

同一个程序通过 `Config.ini` 的 `KEYCOUNT=5|4` 切换五键 / 四键界面，窗口标题分别为 `PADUEX5 V3.2.1` / `PADUEX4 V3.2.1`。

## 功能

- 每个踏板 4 路传感器的灵敏度、灯色（红 / 白 / 蓝）、延迟释放（消抖）
- USB 工作模式：键盘 / 手柄
- 键盘模式改键（需固件 V3.0 及以上）
- 调试模式：实时显示传感器 RAW 值与踩踏
- 固件升级（OTA，升级前校验镜像）
- 中文 / English，白天 / 黑夜主题

## 固件

| 文件 | 适用 | 上报 |
|---|---|---|
| [`firmware/PADUEX5_V3.2.bin`](./firmware/PADUEX5_V3.2.bin) | 五键机 | `V3.2 (PADUEX5)` |
| [`firmware/PADUEX4_V3.2.bin`](./firmware/PADUEX4_V3.2.bin) | 四键机 | `V3.2 (PADUEX4)` |
| [`firmware/PADUEX5_P6_V3.5.bin`](./firmware/PADUEX5_P6_V3.5.bin) | 仅限 5 号口损坏的五键机，第 5 键踏板插 6 号口，正常板不要刷 | `V3.5 (PADUEX5)` |
| [`firmware/PADUEX5_P7_V3.5.bin`](./firmware/PADUEX5_P7_V3.5.bin) | 同上，第 5 键踏板插 7 号口 | `V3.5 (PADUEX5)` |
| [`firmware/PADUEX5_P8_V3.5.bin`](./firmware/PADUEX5_P8_V3.5.bin) | 同上，第 5 键踏板插 8 号口 | `V3.5 (PADUEX5)` |
| [`firmware/PADUEX5_P9_V3.5.bin`](./firmware/PADUEX5_P9_V3.5.bin) | 同上，第 5 键踏板插 9 号口 | `V3.5 (PADUEX5)` |
| [`firmware/PADUEX5_DIAG.bin`](./firmware/PADUEX5_DIAG.bin) | 同上，只看哪对脚有芯片应答，不发按键 | `V3.6 (PADUEX5)` |
| [`firmware/PADUEX5_BUS5EXT.bin`](./firmware/PADUEX5_BUS5EXT.bin) | 同上，第 5 键仍在口 5 总线，地址避开原来的四颗；踏板插 6～9 号口 | `V3.7 (PADUEX5)` |

基于原厂 `5KEY_2P_V2.13`，加入防粘键、键盘模式改键与灵敏度掉电保存（原厂及 V3.0 的灵敏度只写 RAM；V3.1 写了 Flash 但上电又被重置为 200；V3.2 起拔插后保持）；四键版另屏蔽第五键。
上位机对原厂固件（显示 2.12）、V3.0 和更早的镜像（2.17 / 2.18）仍可识别，并提示升级。
V3.2 之前的镜像归档在 [`firmware/legacy/`](./firmware/legacy/)，其中 `paduex5_1.0～1.5` 禁止刷入，详见 [`firmware/README.md`](./firmware/README.md)。

## 编译

- Windows：Visual Studio 打开 `ConfigApp/HIDDeviceProject.sln`（.NET Framework 4.0）
- Linux：`xbuild /p:Configuration=Release ConfigApp/HIDDeviceProject.sln`

开发说明见 [`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md)，变更记录见 [`CHANGELOG.md`](./CHANGELOG.md)。
