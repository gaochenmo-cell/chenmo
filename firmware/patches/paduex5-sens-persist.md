# 灵敏度掉电保存：V3.1（SET_SENS 写 Flash）+ V3.2（上电不再重置为 200）

适用：`PADUEX5_V3.2.bin`、`PADUEX4_V3.2.bin`。构建脚本 `build_paduex5_v3.2.py`（输入 `legacy/PADUEX5_V3.1.bin`）、`build_paduex4_v3.2.py`（输入 `PADUEX5_V3.2.bin`）。V3.1 的脚本与镜像在 `legacy/`。

## 现象

上位机灵敏度页「保存」后设备立即按新阈值工作，拔掉 USB 再插上，读回的灵敏度全部变回 200。键盘 / 手柄模式、五键 / 四键固件都复现；颜色（含按键映射）、延迟释放和工作模式都能保住。真机日志（V3.1 四键）：保存 `2304 3034` → ACK `57 4F 4B` → 即时回读一致 → 拔插 → `GET_SENS` 回 20 × `00 C8`。

## 根因有两处，原厂固件都有

### 1. `SET_SENS` 只对颜色、消抖写 Flash（V3.1 修复）

`App_Config_Command_Report`（`0x080049E0`）校验 CRC8、把 20 个 u16 写进 RAM 表后：

```
08004bd8  cmp type,#1 → App_Set_Flash_Cofnig(0x08010800, Color, 36)
08004bee  cmp type,#2 → App_Set_Flash_Cofnig(0x08011000, DTick, 36)
其他（含 type 0 灵敏度）→ 直接回 ACK
```

V3.1 把 `0x08004BD8..0x08004C04` 改为按 type 算页地址 `0x08010800 + (type-1)*0x800` 后统一 `bl App_Set_Flash_Cofnig(addr, r7, 36)`（r7 = 固件已选好的表指针），type 0 → `0x08010000`。页格式与原厂 type 1 / 2 完全同构：`[0]=0xA55A`、`[1..36]` 数据、`[37]=CRC16(前 74 字节)`，由同一个 `App_Set_Flash_Cofnig` 生成、同一个 `App_Get_Flash_Cofnig` 校验。

### 2. 上电加载后又把灵敏度表填成 200（V3.2 修复）

上电配置加载函数 `0x08006470`（`main` 调一次）：

```
08006492  App_Get_Flash_Cofnig(0x08010000, Sensi, 默认表 0x0800CE94 (36×800), 36)
0800649e  App_Get_Flash_Cofnig(0x08010800, Color, 默认色表, 36)
080064ac  App_Get_Flash_Cofnig(0x08011000, DTick, 默认表 3, 36)
080064b8  打印三张表
080064d2  AppUser+0x1DA/0x1DB = 0
080064de  movs r4,#0 ; b 64f0
080064e2  movs r1,#0xC8          ; 200
080064e4  ldr  r0,=Color ; subs r0,#0x48   ; → Sensi
080064e8  strh.w r1,[r0,r4,lsl #1]
080064ec  adds r4,#1 ; uxtb ; cmp r4,#0x24 ; blt 64e2
080064f4  pop {r4,pc}
```

`0x080064DE..0x080064F4` 这个循环在加载完成**之后**无条件把 `Sensi[0..35]` 写成 200。原厂从不写 SENSI 页，所以加载永远得到默认表 800，这个循环是原厂把阈值定成 200 的方式——它也解释了为什么用户看到的"出厂值"是 200 而不是默认表里的 800，以及上位机 `DEFAULT_SENSI` 从 800 改成 200 的历史。V3.1 的页写对了（真机 WMODE、COLOR 页都保得住，同一套 `App_Set_Flash_Cofnig`），但每次上电这个循环都把刚加载的值冲掉。

Unicorn 之前只模拟了三次 `App_Get_Flash_Cofnig`，没跑完整的 `0x08006470`，所以没看到这一步。

## V3.2 补丁

| 地址 | 原来 | 现在 | 说明 |
|---|---|---|---|
| `0x080064DE..0x080064F4`（22 字节） | `Sensi[i] = 200` 循环 | 11 × `nop` | 保留加载结果；函数仍从 `0x080064F4 pop {r4,pc}` 返回 |
| `0x0800CE94..0x0800CEDC`（72 字节数据） | 36 × 800 | 36 × 200 | 唯一引用是 `0x08006500` 字面量（加载 SENSI 页的默认表）。页空白或校验失败时回退 200，与原来的上电行为一致 |
| `0x0800616A` | `movs r1,#1` | `movs r1,#2` | GET_MODE 上报 V3.2 |

相对 V3.1 共 94 字节差异（22 + 72 + 1，其中 `nop` 与原字节重合的不计）。向量表、`0x14C == 0x080084A5`、体积 37072、USB 命令、Flash 驱动不变；四键版仍只多改 `0x0800615E`（HVer 0x14）与 `0x08006BE2`（扫描边界）。

## 验证

`firmware/tests/emu_set_sens.py` 现在跑的是镜像的真实代码：USB 分发器 `0x08005FAC` → `App_Config_Command_Report` → `App_Set_Flash_Cofnig` → Flash 驱动（解锁 / 擦页 / 半字编程 / 上锁 / 2 KB 读改写）；上电跑完整的 `0x08006470`。Flash 控制器按真实语义建模：上锁或 PG 未置位的写被拒绝并置 PRGMERR，编程只能 1→0（结果 = 旧值 AND 新值，想置 1 记错误），PER+START 按 ADDR 擦 2 KB 页为 FF。

```bash
python3 firmware/tests/emu_set_sens.py firmware/PADUEX5_V3.2.bin          # 36/36
python3 firmware/tests/emu_set_sens.py firmware/PADUEX4_V3.2.bin          # 36/36
python3 firmware/tests/emu_set_sens.py firmware/legacy/PADUEX5_V3.1.bin   # 5 项失败：页写对了（A55A+CRC16 有效、擦→写→锁无错误），上电后 Sensi == 200
python3 firmware/tests/emu_set_sens.py firmware/legacy/PADUEX5_V3.0.bin   # 9 项失败：Sensi 从不写 Flash
python3 firmware/tests/emu_set_sens.py firmware/legacy/paduex5_1.6.bin    # 9 项失败：原厂同上
```

V3.1 的 5 项失败与真机日志逐条对应：ACK 正常、即时回读一致、页内容正确，拔插后 200。

## 上位机配合（3.2.0.0）

- 「保存设置」：SET_SENS → ACK → 即时回读 → **SET_MODE（当前模式）让板子复位 → 等待重新枚举（≤ 12 s）→ 再 GET_SENS 比对**。只有重启后的回读才来自 Flash。一致弹「设备已重启，重启后回读与保存值一致，已确认写入 Flash」；不一致弹「重启后数值没有保住（Kx-y：保存 a，重启后 b）」并按固件版本给出升级或发日志的提示。
- GET_SENS 回包无效（无应答 / RAW 帧 / CRC 错）时弹错误并保持界面原值，不填默认值；每笔 TX / RX 原始 64 字节 hex 都在 `PADUEX5.log`。
- 固件 < V3.2 时版本标签加注「建议升级 V3.2（灵敏度不掉电保存）」。
