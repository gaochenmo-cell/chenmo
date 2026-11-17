# 救砖

## 刷了 legacy/paduex5_1.0～1.5 → 插上无声音

USB 中断向量被覆盖（见 `legacy/patches/paduex5_1.0-Vector-Brick.md`）。APP 起不来，**OTA 无法再进**。

只能 ISP / SWD：

1. 固件：五键 `PADUEX5_V3.4.bin`，四键 `PADUEX4_V3.4.bin`（回滚用 V3.2，或官方 V2.13）
2. 地址：`0x08004000`，写满 37072 字节
3. 复位

## 新板升级建议（两步）

接线：插主板后置 USB 口；不要用机箱前面板 USB 口（供电常不足、走延长线，会导致设备异常）和无源 HUB；两块板同时使用时如需扩展口，用带独立供电的 HUB。

1. 先用上位机 OTA 刷 **官方 V2.13**，确认升级工具与 Bootloader 路径本身无问题
2. 再 OTA 刷 **`PADUEX5_V3.4.bin`**（四键机 `PADUEX4_V3.4.bin`）。两块板一起用时两块都刷。回滚用 V3.2

| 固件 | 结论 |
|------|------|
| **PADUEX5_V3.4 / PADUEX4_V3.4** | 当前版本（V3.2 + 松开保持固定 0，不能再改） |
| **PADUEX5_V3.2 / PADUEX4_V3.2** | 上一版，松开保持仍可改、默认 3；可刷回 |
| PADUEX5_V3.1 / PADUEX4_V3.1 | 可用，灵敏度页已写 Flash 但上电被重置为 200，建议升级 |
| PADUEX5_V3.0 / PADUEX4_V3.0 | 可用，但灵敏度只写 RAM，拔插后恢复 200，建议升级 |
| legacy/paduex5_1.7、paduex4_1.0 | 上一版，功能相同，仅版本字节不同 |
| legacy/paduex5_1.6 | 仅防粘键 |
| 官方 V2.13 | 安全基线 |
| legacy/paduex5_1.0～1.5 | **禁止** |
