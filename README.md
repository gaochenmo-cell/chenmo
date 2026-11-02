# PADUEX5

Yuanpad **5Key** Windows 配置与固件升级工具（原 ConfigApp DDR 分支）。

## 功能

- **5Key 调参**：按真实垫子布局（四角 + 中心）调节灵敏度 / 灯色 / 消抖
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

主界面进入 **Yuanpad 5Key**，点 **固件升级** 选择 `.bin` 即可。

## 相关

- 板端固件：`uandancedance`（`5KEY_*.bin`，APP @ `0x08004000`）
