# 旧版固件归档

V3.2 之前的镜像、构建脚本和分析文档。日常只需要上一级目录的 `PADUEX5_V3.2.bin` / `PADUEX4_V3.2.bin`。

| 文件 | 上报版本 | 说明 |
|---|---|---|
| `PADUEX5_V3.1.bin` | V3.1 | V3.2 五键版的前身，是 `build_paduex5_v3.2.py` 的输入；`SET_SENS` 已把灵敏度页写进 Flash，但上电初始化 `0x080064DE..0x080064F4` 又把表填成 200，拔插后仍是 200 |
| `PADUEX4_V3.1.bin` | V3.1 | V3.1 四键版，同样上电重置灵敏度 |
| `PADUEX5_V3.0.bin` | V3.0 | V3.1 五键版的前身，是 `patches/build_paduex5_v3.1.py` 的输入；灵敏度 `SET_SENS` 只写 RAM，拔插后恢复 200 |
| `PADUEX4_V3.0.bin` | V3.0 | V3.1 四键版的前身，同样不保存灵敏度 |
| `paduex5_1.7.bin` | 2.17 | V3.0 五键版的前身，与 `PADUEX5_V3.0.bin` 仅版本字节（`0x08006164`、`0x0800616A`）不同 |
| `paduex4_1.0.bin` | 2.18 | V3.0 四键版的前身，与 `PADUEX4_V3.0.bin` 仅版本字节与 HVer 字节（`0x0800615E`）不同 |
| `paduex5_1.6.bin` | 2.12 | 原厂 V2.13 + SendPacket 有界等待，无改键；是 `build_paduex5_v3.0.py` 的输入 |
| `paduex5_1.0～1.5.bin` | — | **禁止刷入**：补丁代码覆盖了向量表 `0x14C`（USB 中断），上电后第一个 USB 中断即崩溃 |

`patches/` 为各版本的构建脚本与事故分析（`paduex5_1.0-Vector-Brick.md`、`paduex5_1.2-Brick.md`、`paduex5_1.3-Brick.md` 等）。
`build_paduex5_1.6.py` 需要原厂 `5KEY_2P_V2.13.bin`（不在仓库中）；`build_paduex5_v3.0.py` / `build_paduex4_v3.0.py` 在本目录内重建 V3.0 镜像。
`tests/sim_paduex5.py` 是 1.3 时期的离线自测，同样依赖原厂镜像。
