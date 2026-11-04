# PADUEX5 firmware

## `grok1.4.bin`（推荐 — 改键 + 粘键超时，栈对齐正确）

基于 **grok1.2**，体积 **37072**（= 原厂）。含：

1. 改键 `GET_MAPP` / `SET_MAPP`、灵敏度 / `DTick`  
2. KBS/HID `SendPacket` **有界等待**（修粘键）  
3. **`push.w/pop.w {r4–r8,lr/pc}`** — 保持 8 字节栈对齐（修 grok1.3 变砖）

```bash
python3 firmware/patches/build_grok1.4.py   # 读 grok1.2.bin → 写 grok1.4.bin
```

OTA / ISP 地址必须 **`0x08004000`**。

## `grok1.3.bin`（禁止 — 会变砖）

粘键超时逻辑正确，但压栈少保存了 `r8`，**破坏 8 字节栈对齐** → USB 路径 HardFault。  
详见 [`patches/grok1.3-Brick.md`](./patches/grok1.3-Brick.md)。已刷 1.3 请用 ISP/OTA 改刷 **1.4 或 1.2**。

## `grok1.2.bin`（改键，同体积，可用）

无粘键超时修复。见 `patches/build_grok1.2.py`。

## `grok1.1.bin`（禁止 ISP）

末尾 +228 字节 cave，ISP 截断会变砖。见 [`RECOVERY.md`](./RECOVERY.md)。

## `grok1.0.bin`

仅灵敏度 + 消抖索引；无改键。

## 救砖 / 离线自测

- 救砖：[`RECOVERY.md`](./RECOVERY.md)  
- 自测：`python3 firmware/tests/sim_paduex5.py`（逻辑仿真，不能替代真机）
