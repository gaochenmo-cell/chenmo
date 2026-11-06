# 救砖

## 刷了 legacy/paduex5_1.0～1.5 → 插上无声音

USB 中断向量被覆盖（见 `legacy/patches/paduex5_1.0-Vector-Brick.md`）。APP 起不来，**OTA 无法再进**。

只能 ISP / SWD：

1. 固件：五键 `PADUEX5_V3.2.bin`，四键 `PADUEX4_V3.2.bin`（或官方 V2.13）
2. 地址：`0x08004000`，写满 37072 字节
3. 复位

## 新板升级建议（两步）

1. 先用上位机 OTA 刷 **官方 V2.13**，确认升级工具与 Bootloader 路径本身无问题
2. 再 OTA 刷 **`PADUEX5_V3.2.bin`**（四键机 `PADUEX4_V3.2.bin`）

| 固件 | 结论 |
|------|------|
| **PADUEX5_V3.2 / PADUEX4_V3.2** | 当前版本（防粘键 + 改键 + 灵敏度掉电保存；四键版另屏蔽 K5） |
| PADUEX5_V3.1 / PADUEX4_V3.1 | 可用，灵敏度页已写 Flash 但上电被重置为 200，建议升级 |
| PADUEX5_V3.0 / PADUEX4_V3.0 | 可用，但灵敏度只写 RAM，拔插后恢复 200，建议升级 |
| legacy/paduex5_1.7、paduex4_1.0 | 上一版，功能相同，仅版本字节不同 |
| legacy/paduex5_1.6 | 仅防粘键 |
| 官方 V2.13 | 安全基线 |
| legacy/paduex5_1.0～1.5 | **禁止** |
