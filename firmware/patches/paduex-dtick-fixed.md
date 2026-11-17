# 固件 V3.4：松开保持固定为 0，且不能再改

四键、五键同一处改动。输入分别是 `PADUEX4_V3.2.bin`、`PADUEX5_V3.2.bin`。没有带上 V3.3 的发送错开。

## 做了什么

松开保持仍走原来的路径：采样高于阈值时 `Tck[key] = DTick[key]`。V3.4 让 `DTick[]` 一直是 0，所以运行时和「V3.2 上手动把 DTICK 写成 0」是同一段代码、同一张表。

| 地址 | 原来（V3.2） | 现在 | 说明 |
|---|---|---|---|
| `0x0800CF24` 起 72 字节 | 36 × `3` | 36 × `0` | DTick 页空白或校验失败时的默认表。只被 `0x08006514` 引用 |
| `0x080064DE..0x080064F4` | 11 条 `nop` | 把 `DTick[0..35]` 写成 0 的循环 | 上电加载（含 Flash `0x08011000` 里以前保存的值）之后强制写成 0 |
| `0x08004BDE..0x08004C04` | type ≤ 2 都写 Flash | type 0 / 1 照旧写 Flash；type 2 不写 Flash，并把刚抄进 RAM 的 36 个半字清 0，然后回 ACK | GET 读的是这张 RAM，所以回 0 |
| `0x0800616A` | `movs r1,#2` | `movs r1,#4` | GET_MODE 上报 V3.4。HVer 不动 |

扫描、阈值比较、灯效、改键、灵敏度页和颜色页都不在差异里。`0x14C` 仍是 `0x080084A5`，体积仍是 37072。

## 构建

```bash
python3 firmware/patches/build_paduex5_v3.4.py
python3 firmware/patches/build_paduex4_v3.4.py
python3 firmware/tests/emu_dtick_fixed.py
```

回滚：刷回 `PADUEX5_V3.2.bin` / `PADUEX4_V3.2.bin`。
