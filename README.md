# PADUEX5


## 下载已编译程序（推荐）

解压即用（Windows）：

- 仓库内：[`dist/PADUEX5/`](./dist/PADUEX5/) → 直接运行 **`PADUEX5.exe`**
- 压缩包：[`dist/PADUEX5-win-release.zip`](./dist/PADUEX5-win-release.zip)

同目录需保留：`PADUEX5.exe`、`HIDLibrary.dll`、`LibUsbDotNet.dll`、`Config.INI`、`AppLogo.ico`

需要 **.NET Framework 4.0+**（Win7/10/11 一般已有）。

Yuanpad **5Key** Windows 配置与固件升级工具（原 ConfigApp DDR 分支）。

## 功能

- **启动即 5Key**：已移除 ADX / Laverita / Yuangeki 入口
- **5Key 调参**：按真实垫子布局（四角 + 中心空心黄圈）调节灵敏度 / 灯色 / 消抖
- **自适应 DPI**：System DPI + 运行时按 `DpiX/96` 整体缩放，避免大窗空白
- **Debounce**：固件消抖（松开保持，单位 ms，默认 3）
- **固件升级**：AT32 USB HID IAP（AN0007），带进度条与日志
- **防砖**：
  - 升级前检查向量表与体积（禁止覆盖 `0x0800F800` 配置区）
  - 校验 Bootloader APP 地址必须为 `0x08004000`
  - 整包写完并 **CRC 通过后才跳转**；失败则留在 Bootloader，可重试

## 布局

```
[1 ↖] [    ] [2 ↗]
[    ] [3 ●] [    ]
[4 ↙] [    ] [5 ↘]
```

## 本地编译

1. Visual Studio 打开 `ConfigApp/HIDDeviceProject.sln`
2. .NET Framework 4.0 WinForms
3. 输出：`PADUEX5.exe`
4. 运行目录可放 `Config.INI`

直接进入 **Yuanpad 5Key** 主界面，点 **固件升级** 选择 `.bin` 即可。

## 相关

- 板端固件：`uandancedance`（`5KEY_*.bin`，APP @ `0x08004000`）
