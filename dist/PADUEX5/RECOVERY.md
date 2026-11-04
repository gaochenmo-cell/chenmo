# 变砖救回

## OTA「成功」后无 USB 声音 / 上位机找不到

多半是刷了 **grok1.2 / 1.3 / 1.4**（boot hook 写超）。见 `patches/grok1.2-Brick.md`。

### 电脑完全认不到时

只能 **ISP / SWD**（BOOT0 或厂家方式）：

1. 固件：`firmware/grok1.5.bin`（或 `grok1.0.bin` / 官方 V2.13）  
2. 地址：`0x08004000`  
3. 长度：写满 **37072**  

### 若还能进 Bootloader（VID 0x0E8F PID 0x8901）

PADUEX5 → 固件升级 → 选 **grok1.5.bin**。

## 选用表

| 固件 | 结论 |
|------|------|
| **grok1.5** | 推荐 |
| grok1.0 / 官方 | 救砖可用 |
| 1.2 / 1.3 / 1.4 / 1.1 | 禁止 |
