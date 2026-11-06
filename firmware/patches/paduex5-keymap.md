# 键盘模式改键（不加命令、不加 Flash 代码、不动向量表）

适用：`PADUEX5_V3.0.bin`、`PADUEX4_V3.0.bin`（以及旧版 paduex5_1.7 / paduex4_1.0）。

## 思路

原厂 `AppUser.Color[36]`（u16）每个按键占 4 格，固件只读 `Color[key*4]` 决定灯色，
`Color[key*4+1..3]` 由 GET/SET_SENS(type=1) 原样传输、写入 Flash `0x08010800`、开机加载——但从未使用。

改键固件把键盘键值放进 `Color[key*4+1]`：

| 值 | 固件行为 |
|---|---|
| `0x4B00 \| usage`（高字节 'K'） | 键盘模式下发 `usage` |
| 其它（旧上位机写的 0～7 色码、0） | 走原厂 1P/2P 表 |

## 固件改动

只改 `Report_Normal`（main.c 的 1P/2P if/else 链）`0x08005D72..0x08005DCA`，共 88 字节：

```
ldr   r0, =AppUser
lsls  r1, r4, #3
adds  r0, r0, r1
ldrh.w r5, [r0, #0x148]      ; Color[Index*4+1]
lsrs  r0, r5, #8
cmp   r0, #0x4B
bne   fallback
uxtb  r5, r5
b     join                    ; 原厂 shift 位处理 + 写入 keyboard_buf
fallback:
ldr   r0, =ConfigMode
ldrb  r0, [r0]
adr   r1, tab_1p
lsrs  r0, r0, #4
beq   pick
adds  r1, #10                 ; tab_2p
pick:
ldrb  r5, [r1, r4]
b     join
tab_1p: 1A 1B 04 07 16 14 08 1D 06 55
tab_2p: 0B 0E 0D 11 10 14 08 1D 06 55
```

原 2P 链 `0x08005DE8..0x08005E50` 变成不可达代码，保持原样。

其它全部与基底（原厂 V2.13 + SendPacket 有界等待）相同：向量表、启动、USB 命令、Flash 读写。
`build_paduex5_v3.0.py` 断言所有差异都在上述范围内加两个版本字节，且 `0x14C` = `0x080084A5`。

## 验证

`firmware/tests/emu_report_normal.py` 用 Unicorn 执行真实的 `Report_Normal`：
原厂 1P/2P 表、全部重映射、部分重映射、组合键、错误标签回退 —— 25/25 通过；对无改键的基底跑同一测试，重映射用例按预期失败。

`firmware/tests/emu_keymap_e2e.py` 跑完整链路（真实固件代码：USB 命令解析 → CRC8 → `App_Set_Flash_Cofnig` → Flash 页 → 重启 `App_Get_Flash_Cofnig` → `Report_Normal`），
把五键改成 Q E S Z C：读取默认 → 保存 → 立即生效 → 掉电重启后仍为 Q E S Z C → 改灯色不影响键位 → 恢复默认回 H K J N M，21/21 通过；基底上同一脚本 5 项按预期失败。

## 上位机

「按键映射」页：读取 = GET_SENS(1) 后解码；保存 = 先 GET_SENS(1) 刷新灯色，再写入 slot，再 SET_SENS(1)；
「默认设置」= slot 清 0 → 固件回原厂表。调色页涂色与默认设置不碰 `+1` 槽位。

## 版本号

GET_MODE 回包 `47 mode A2 HVer FWmajor FWminor` 在 `0x08006148` 组装，四个常量都是独立的 `movs` 立即数：

| 地址 | 原厂 V2.13 | V3.0 | 含义 |
|---|---|---|---|
| `0x0800615E` | `movs r1,#0x13` | 五键 `0x13`，四键 `0x14` | HVer |
| `0x08006164` | `movs r1,#2` | `movs r1,#3` | FW major |
| `0x0800616A` | `movs r1,#12` | `movs r1,#0` | FW minor |

原厂 2.12 / 2.13 的 bin 里 minor 常量都是 12，所以原厂固件上位机一律显示 `2.12`。
旧版 paduex5_1.7 / paduex4_1.0 分别上报 2.17 / 2.18；V3.0 两款都上报 3.0，由 HVer 区分五键 / 四键。
`firmware/tests/emu_get_mode.py` 跑真实的 USB 命令分发函数验证回包。

## 已知限制

- 仅键盘模式；手柄(HID)模式不受影响。
- usage ≤ 0x7F（bit7 在原厂逻辑里是 Shift 标记）。
- 升级前若用旧上位机保存过颜色，slot 里是色码 → 自动回退原厂表，不会乱键。
