# V3.1：灵敏度掉电保存（SET_SENS type 0 写入 Flash）

适用：`PADUEX5_V3.1.bin`、`PADUEX4_V3.1.bin`。构建脚本 `build_paduex5_v3.1.py`（输入 `legacy/PADUEX5_V3.0.bin`）、`build_paduex4_v3.1.py`（输入 `PADUEX5_V3.1.bin`）。

## 现象

上位机灵敏度页「保存」后设备立即按新阈值工作，但拔掉 USB 再插上，读回的灵敏度全部变成出厂值 800。键盘 / 手柄模式、五键 / 四键固件都复现；灯色和延迟释放不受影响。

## 根因：原厂固件只把颜色、消抖写进 Flash

`App_Config_Command_Report`（`0x080049E0`）处理 `SET_SENS`（`57 'R' 'I' 'T' type …`）的流程：

1. 校验 CRC8，不符回 `57 46 49`；
2. 按 `type & 0x0F` 选表：1 → `Color`（`0x20001B16`），2 → `DTick`（`Color + 0x48`），其他 → `Sensi`（`Color - 0x48`），指针放在 `r7`；
3. 把 20 个大端 u16 写进该表（RAM）；
4. **只对 type 1 / 2 调用 `App_Set_Flash_Cofnig`**，type 0 直接跳到 ACK：

```
08004bd8  ldrb r0,[r5,#4] ; and r0,#0xf ; cmp r0,#1 ; bne 4bee
08004be2  movs r2,#0x24 ; ldr r1,=Color ; ldr r0,=0x08010800 ; bl App_Set_Flash_Cofnig ; b 4c04
08004bee  ldrb r0,[r5,#4] ; and r0,#0xf ; cmp r0,#2 ; bne 4c04
08004bf8  movs r2,#0x24 ; ldr r1,=Color ; adds r1,#0x48 ; ldr r0,=0x08011000 ; bl App_Set_Flash_Cofnig
08004c04  movs r0,#0x57 … (ACK 57 4F 4B crc type)
```

整个镜像只有这两处调用 `App_Set_Flash_Cofnig`，也没有别的代码写 `0x08010000`。上电时 `0x08006492` 处 `App_Get_Flash_Cofnig(0x08010000, Sensi, 默认表 0x0800CE94, 36)` 读 SENSI 页：页头魔数 `0xA55A` + CRC16 校验失败（页从未写过，全 `FF`）就回退到内置默认表，即 20 × 800。

页校验只看魔数和 CRC16，与 HVer / FVer 常量无关；V3.0 改 HVer、FVer、键扫描边界都没有触及这里。原厂 V2.13（`legacy/paduex5_1.6`）、paduex5_1.7、V3.0 在 Unicorn 下行为一致：`emu_set_sens.py` 在这三个镜像上都是 6 项失败，全部是 Sensi 的 Flash 写入与重启后读回。

## 补丁（`0x08004BD8..0x08004C04`，44 字节原地替换）

`r7` 已经是本次 type 对应的表指针，页地址可以从 type 推出：`0x08010800 + (type - 1) * 0x800`（0 → `0x08010000`，1 → `0x08010800`，2 → `0x08011000`）。

```
08004bd8  ldrb r0,[r5,#4]
08004bda  and  r0,r0,#0xf
08004bde  cmp  r0,#2
08004be0  bhi  0x08004c04          ; 未知 type：维持原厂行为（只写 RAM）
08004be2  subs r0,#1
08004be4  lsls r0,r0,#11           ; (type-1)*0x800
08004be6  ldr  r3,[pc,#0x128]      ; =0x08010800（原厂 type 1 分支用的同一个字面量 0x08004D10）
08004be8  add  r0,r3
08004bea  mov  r1,r7
08004bec  movs r2,#0x24            ; 36 个半字（与原厂一致）
08004bee  bl   App_Set_Flash_Cofnig (0x080053F0)
08004bf2  nop × 9
08004c04  （原厂 ACK，未改）
```

另把 GET_MODE 回包的 FW minor 常量 `0x0800616A` 从 0 改为 1，上报 `V3.1`。

与 V3.0 的差异共 36 字节：`0x08004BD8..0x08004C04` 内 35 字节 + `0x0800616A`。向量表、`0x14C == 0x080084A5`、体积 37072、Flash 驱动、`App_Set_Flash_Cofnig` 本身都没有改动；没有新增 USB 命令、Flash 写入代码或 RAM 变量。四键版 `PADUEX4_V3.1.bin` 在此基础上仍只改 `0x0800615E`（HVer 0x14）和 `0x08006BE2`（扫描边界 0x10）两个字节。

## 验证

```bash
python3 firmware/patches/build_paduex5_v3.1.py
python3 firmware/patches/build_paduex4_v3.1.py
python3 firmware/tests/emu_set_sens.py firmware/PADUEX5_V3.1.bin           # 31/31：三张表 写 → 复位 → 读回一致
python3 firmware/tests/emu_set_sens.py firmware/PADUEX4_V3.1.bin           # 31/31
python3 firmware/tests/emu_set_sens.py firmware/legacy/PADUEX5_V3.0.bin    # 对照：6 项失败（Sensi 不落 Flash）
python3 firmware/tests/emu_set_sens.py firmware/legacy/paduex5_1.6.bin     # 对照：原厂同样 6 项失败
```

`emu_set_sens.py` 用镜像里的真实 `App_Config_Command_Report` / `App_Get_Flash_Cofnig` / `App_Set_Flash_Cofnig`，只给 Flash 擦写与 printf 打桩：空 Flash 上电 → 默认 800；SET_SENS type 0/1/2 → 断言 `App_Set_Flash_Cofnig` 的页地址、表指针、长度，断言擦除 + 写入 128 半字、页头 `A55A`；RAM 清零后重新上电 → GET_SENS 等于写入值；type 3 不碰 Flash；CRC 错误的包回 `57 46 49` 且不写 Flash。其他回归（`emu_report_normal`、`emu_keymap_e2e`、`emu_keyscan_paduex4`、`emu_get_mode`）在两个 V3.1 镜像上全部通过。

## 上位机配合（3.1.0.0）

- 保存后立即 GET_SENS 回读并逐项比较，不一致弹「保存未生效（Kx-y：写入 a，设备回读 b）」，日志记录完整 SET 报文、ACK、回读值。
- 固件版本 < V3.1 时保存灵敏度会提示「固件只把灵敏度写进 RAM，拔掉 USB 后恢复出厂值，请升级到 V3.1」，左上角版本标签也加注。
