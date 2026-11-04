# PADUEX5

## 下载

- [`dist/PADUEX5/`](./dist/PADUEX5/) → `PADUEX5.exe`（1.0.0.53）
- [`dist/PADUEX5-win-release.zip`](./dist/PADUEX5-win-release.zip)

## 上位机 1.0.0.53

- 去掉「Num 1P/2P」选择：1P/2P 由游戏按插入顺序自动分配，固件自 V2.12 起也不再接受上位机设定的 1P/2P 位。
- 升级前校验固件 USB 中断向量（0x14C），拒绝 grok1.0～1.5。

## 固件

只刷 **`firmware/grok1.6.bin`**（官方 V2.13 + 防粘键）。

**grok1.0～1.5 全部禁止**：它们覆盖了 USB 中断向量，OTA 后无 USB。详见 [`firmware/patches/grok1.0-Vector-Brick.md`](./firmware/patches/grok1.0-Vector-Brick.md)。

## 本地编译

Visual Studio 打开 `ConfigApp/HIDDeviceProject.sln`（.NET 4.0）。
