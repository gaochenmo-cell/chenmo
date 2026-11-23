# 五键板 5 号口损坏：第 5 键整体搬到 6 号口（PADUEX5_P6）

适用：**只有 5 号踏板口硬件损坏的五键板**。把 5 号踏板的 8 针线插到 6 号口，刷 `PADUEX5_P6_V3.5.bin`，其它四个口不动。
正常板不要刷：刷了以后 5 号口会变成死口，第 5 键要接在 6 号口才有反应。

对上位机、游戏和配置表来说它仍然是「第 5 键」：键码 bit4、`Sensi[16..19]`、`Color[16..19]`（含改键槽位 `Color[17]`）、`DTick` 槽位、GET/SET_SENS 的 20 项数据、RAW 调试流的第 17～20 项，全部不变。只有两条硬件通路换到 6 号口：四路传感器采样和灯带输出。

## 固件里 36 路通道与 9 个口的真实对应

扫描函数 `0x080069CC` 按 `key*4+n` 取 `RAW[36]`（`0x200019F6`），`Sensi/Reg/Tck/DTick` 同样按 36 路编址，看上去像 9 个口都有数据。但往 `RAW` 送数据的采样函数 `0x08006E48` 只产生 **20 路**：

| `RAW` 索引 | 来源 | 口 | 说明 |
|---|---|---|---|
| 0～15 | 片内 ADC 通道 0～15（PA0～7、PB0～1、PC0～5），`0xFFF - adc` | 口 1～4（丝印 AD_1～AD_16） | `0x08006E54..0x08006F76` |
| 16 | 软件 I²C 读器件 `0x80` 寄存器 2 | 口 5 第 1 路 | 总线 PC11（SCL）/ PC12（SDA） |
| 17 | 读器件 `0x88` 寄存器 2 | 口 5 第 2 路 | 同上 |
| 18 | 读器件 `0x86` 寄存器 2 | 口 5 第 3 路 | 同上 |
| 19 | 读器件 `0x84` 寄存器 2 | 口 5 第 4 路 | 同上 |
| 20～35 | **没有任何代码写入**，永远是 0 | 口 6～9 | — |

口 5 的四路不是 MCU 的模拟输入，而是四颗 I²C 电压采集芯片（8 位写地址 `0x80/0x88/0x86/0x84`，7 位 `0x40/0x44/0x43/0x42`，寄存器 2 为 16 位大端电压值），固件读回后做 `3915 - min(raw × 0.625, 3915)`（`0x08006FAC..0x08006FEA`，软件浮点）写入 `RAW[16..19]`。
所以「把扫描里第 5 键的索引 16～19 改成 20～23」行不通——`RAW[20..23]` 没有数据源。要让第 5 键读 6 号口，只能让这四次 I²C 读走 6 号口的总线。

### 6 号口的总线和灯脚是怎么确定的

固件只实现了两条软件 I²C 总线：

- 总线 1：PC11/PC12，原语 `0x08008584`（读字节）、`0x08008600`（ACK）、`0x08008664`（NACK）、`0x080086C8`（START）、`0x08008738`（STOP）、`0x080087A8`（写字节），只被 16 位读函数 `0x0800852A` 调用，而 `0x0800852A` 只被采样函数的四处 `bl` 调用（`0x08006F7C / 0x08006F88 / 0x08006F94 / 0x08006FA0`）。
- 总线 2：PB8/PB9，原语 `0x08008898..0x08008B..`，由 `0x080084C4` 使用；`0x08006FFC` 在 100 ms 节拍上读器件 `0x84`、`0x86` 的寄存器 2（`>>3 <<2`，毫伏）后打印一次 `CC Value`，是电源监测，与踏板无关。

板级 GPIO 初始化 `0x08007E4C..0x0800846A` 把下面这些脚配成输出，顺序如下（每个脚一次 `gpio_init`）：

```
PB8  PB9   | PC11 PC12 | PB3 PB4 | PF6 PF7 | PB10 PB11 | PB2 PA10      ← 6 对 I²C 脚（背面 I1～I6 测试点）
PD2  PC14  PC10  PA15                                                 ← 其它输出（PC10 = 210 颗的氛围灯带，0x08007684）
PB14 PB15  PC6  PC7  PC8  PC9  PA8  PB13  PB12                        ← 9 条踏板灯带
```

其中已被代码使用、能和口号对上的：PC11/PC12 = 口 5 总线；PB14 / PB15 / PC6 / PC7 / PC8 = K1～K5 灯带（刷灯 `0x08005018` 按键号分发到 `0x08007572 / 0x0800758E / 0x080075AA / 0x080075C6 / 0x080075E2`，位输出函数里的引脚常量分别是 GPIOB 0x4000、GPIOB 0x8000、GPIOC 0x40、GPIOC 0x80、GPIOC 0x100）。`0x0800847C` 把 REMAP7 的 SWJTAG 设为 010（关 JTAG、留 SWD），PB3 / PB4 / PA15 才能当普通 IO 用——这也说明 PB3/PB4 确实是设计上要用的脚。

按这个顺序，口 6 的总线是 PC11/PC12 之后的下一对 **PB3（SCL）/ PB4（SDA）**，口 6 的灯带是 PC8 之后的下一个 **PC9**。SCL/SDA 的先后沿用两条已知总线的规律（PB8 SCL / PB9 SDA，PC11 SCL / PC12 SDA：低位脚是 SCL，初始化先 SCL 后 SDA）。
固件里没有更多证据能把 PB3/PB4、PC9 和丝印「口 6」钉死，所以这是从初始化顺序推出的结论；刷之前可以用万用表量一下：背面 I2 测试点的 SCL/SDA 应分别通到 U3 的 PB3（LQFP64 第 55 脚）/ PB4（第 56 脚），口 6 的 LED6 针应通到 PC9（第 40 脚）。若量出来 SCL/SDA 反了，把 `build_paduex5_p6_v3.5.py` 里 `NEW_SCL / NEW_SDA` 对调后重新构建即可。

## 补丁（相对 `PADUEX5_V3.2.bin`，98 字节）

| 位置 | 原 | 新 | 作用 |
|---|---|---|---|
| 总线 1 原语链 `0x08008584..0x0800888E`，6 个字面量 | `0x40011000`（GPIOC） | `0x40010C00`（GPIOB） | 换端口 |
| 同链 6 个字面量 | `0x00180004`（GPIOC 时钟） | `0x00180003`（GPIOB 时钟） | 换时钟位 |
| 同链 14 处 `mov.w rX,#0x800` | PC11 | `#0x8`（PB3） | SCL |
| 同链 11 处 `mov.w rX,#0x1000` | PC12 | `#0x10`（PB4） | SDA |
| `0x080087AE` `lsls r0,r6,#12` | `1<<12` | `lsls r0,r6,#4` | SDA（写字节入口） |
| `0x08008622 / 0x08008686 / 0x080086EA / 0x0800875A / 0x080087D4` `lsls r0,r0,#8` | 由输出模式常量 `0x10` 算出 `0x1000` | `nop`（r0 保持 `0x10`） | SDA（gpio_init 的 pins 字段） |
| `0x080085C8` `lsls r1,r0,#1` | SDA = SCL<<1 | 不变 | PB4 = PB3<<1 仍成立 |
| `0x080071DC / 0x080071F0 / 0x0800720C / 0x08007220` `mov.w r1,#0x100` | PC8 | `#0x200`（PC9） | 第 5 键灯带位输出 |
| `0x0800616A` `movs r1,#2` | V3.2 | `movs r1,#5` | GET_MODE 上报 V3.5，HVer 仍 `0x13` |

采样函数本身、四处 `bl 0x0800852A`、浮点换算、扫描、`Report_Normal`、`SendPacket`、刷灯分发、USB 命令、Flash 读写、向量表（`0x14C = 0x080084A5`）全部不动，体积 37072。
口 5 的 PC11/PC12 从此没有代码再碰；口 6 的芯片用的是和口 5 同一组地址，所以读到的顺序、换算、阈值语义都和原来第 5 键一样。

构建脚本 `build_paduex5_p6_v3.5.py` 用 Capstone 在原语链里按指令模式替换并断言每类替换的个数（14 / 11 / 1 / 5 / 6 / 6），再断言所有差异都落在原语链、四处 LED 位常量和版本字节内，其余函数区逐字节与 V3.2 相同。

## 验证（Unicorn，跑镜像里的真实代码）

`firmware/tests/emu_port6_remap.py` 在 Unicorn 里挂了两条位级 I²C 从机模型（PC11/PC12 与 PB3/PB4 各四颗器件，地址 `0x40/0x44/0x43/0x42`，寄存器 2），拦截 GPIO 的 SCR/CLR 写和 IDT 读，然后执行真实的采样函数 → 扫描 → 刷灯：

```bash
python3 firmware/patches/build_paduex5_p6_v3.5.py
python3 firmware/tests/emu_port6_remap.py firmware/PADUEX5_P6_V3.5.bin   # 36 项：踩口 6 → K5 键码 + 灯在 PC9；踩口 5 无反应；K1～K4 不变
python3 firmware/tests/emu_port6_remap.py firmware/PADUEX5_V3.2.bin      # 对照：V3.2 上第 5 键仍读口 5、灯在 PC8
python3 firmware/tests/emu_set_sens.py        firmware/PADUEX5_P6_V3.5.bin
python3 firmware/tests/emu_report_normal.py   firmware/PADUEX5_P6_V3.5.bin
python3 firmware/tests/emu_keymap_e2e.py      firmware/PADUEX5_P6_V3.5.bin
python3 firmware/tests/emu_keyscan_paduex4.py firmware/PADUEX5_P6_V3.5.bin   # 五键对照组：K5 正常上报
python3 firmware/tests/emu_get_mode.py        firmware/PADUEX5_P6_V3.5.bin   # 47 mode A2 13 03 05
```

`emu_port6_remap.py` 验证的内容：PB3/PB4 总线上出现 4 笔完整事务（`S 80w reg2 S 81r 2 字节 P`，地址顺序 `0x80/0x88/0x86/0x84`），PC11/PC12 上没有 START；`RAW[16..19]` 的顺序与换算（`0/0x800/0x1000/0x1F00 → 3915/2635/1355/0`）和 V3.2 相同；踩口 6 四路 → `KeyCode = 0x10`、键盘 / 手柄报告收到 `0x10`、刷灯只在 PC9 上产生 1200 次电平写（75 字节 × 8 位 × 2）而 PC8 为 0；踩口 5 四路 → 键码、报告、灯全无；口 1～4 与 K1～K5 和弦逐项同 V3.2。

## 回滚

刷回普通五键固件（`firmware/PADUEX5_V3.2.bin`，或以后的普通 V3.x），第 5 键回到 5 号口。配置页（灵敏度 / 颜色 / 消抖）在两种固件之间通用，不需要重新设置。
