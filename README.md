# Yuanpad 5Key ConfigApp

Windows 上位机，用于配置 **Yuanpad / DDR 舞垫 5Key** 固件（灵敏度、灯色、消抖、USB 模式）。

## 5Key 真实布局

```
[1 ↖] [    ] [2 ↗]
[    ] [3 ●] [    ]
[4 ↙] [    ] [5 ↘]
```

对应固件通道 Key1–Key5（每键 4 个传感器），读写包长与固件 `DATA_LEN = 5*4*2` 对齐。

## 本地运行

1. 用 Visual Studio（建议 2019/2022）打开 `ConfigApp/HIDDeviceProject.sln`
2. 目标框架：.NET Framework 4.0（WinForms）
3. 依赖已在 `ConfigApp/HIDDevice/lib/`：`HIDLibrary.dll`、`LibUsbDotNet.dll`
4. 编译后运行；主界面点 **Yuanpad 5Key**

运行目录旁可放置 `Config.INI`（语言等）；仓库根目录有一份参考配置。

## 相关工程

- 板端固件 / 硬件：`uandancedance`（AT32F415，`5KEY_*.bin`）
- 本仓当前仅保留 Yuanpad 的 **5Key 模式** UI 与协议；其它产品入口仍在主界面，后续可再裁剪。
