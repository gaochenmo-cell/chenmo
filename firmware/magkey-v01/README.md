# ADX-Magkey-V01 静音可调磁轴按键固件

麦麦DS「静音可调按键」单键板（硬件 `ADX-Magkey-V01`，原理图 REV 01，2026/11/8）的 MCU 固件。

- MCU：Puya **PY32F002BF15U6TR**（Cortex-M0+，24 MHz HSI，24 KB Flash，3 KB SRAM，QFN20）
- 轴体：智风 V1 磁轴 **DS-01GM-40**（内置线性霍尔，总行程 3.5 mm，输出 0.4 ~ 3.0 V）
- 工具链：`arm-none-eabi-gcc`（已在 13.2.1 验证），不依赖 Keil/IAR
- 产物：`build/magkey_v01.hex` / `.bin` / `.elf`（约 8 KB，占用 Flash 34%）

## 1. 功能

| 功能 | 说明 |
|---|---|
| 四档灵敏度 | 短按板上 S1（或 J4 外接按钮）按 红→绿→蓝→白→红 循环，档位写入片内 Flash，掉电保持 |
| 档位指示 | 单颗 RGB 指示灯 **LED6** 显示 红 / 绿 / 蓝 / 白 |
| 按键输出 | 每次采样的 ADC 与当前档的绝对阈值比较 → `KEY_OUT`(PB2) → Q1 → **J3.KEY1 低 = 按下，高 = 松开** |
| 触发 | 采样 ≥ 该档 ADC 即按下，低于该档即松开。不使用快速触发，也不用 Flash 里的校准去改阈值 |
| 恢复出厂 | 上电时按住 S1 3 s（白灯慢闪→绿灯闪 3 下），清除档位与校准 |
| 调试串口 | DEBUG2（PB4 TX / PB5 RX）115200 8N1 命令行：`status` `gears` `gear n` `cal` `raw` `led r g b` `reset` |
| 游戏灯链 | **不实现**。LED1~LED5 WS2813B 由游戏经 J1→J2 直接驱动，与 MCU 无连线；仅保留 `light_chain.h` 接口与串口 `light` 预留命令 |

## 2. 引脚映射（来自原理图，已对照 PY32F002B 数据手册复用表）

| QFN20 脚 | MCU | 网络 | 方向 | 说明 |
|---|---|---|---|---|
| 1 | PC0/NRST | RST | — | R4 10K 上拉，C12 0.1 µF |
| 3 | PB7 | SW1 | 输入 | S1 轻触开关 / J4 外接按钮并联到地；R8 10K 上拉；**低 = 按下**（固件另开内部上拉） |
| 5 | PB6 | SWDIO | — | DEBUG1 pin3 |
| 15 | PA2 | SWCLK | — | DEBUG1 pin2 |
| 7 | PB5 | RXD | 输入 | USART1_RX（AF1）→ DEBUG2 pin1 |
| 8 | PB4 | TXD | 输出 | USART1_TX（AF1）→ DEBUG2 pin2 |
| 10 | PB2 | KEY_OUT | 输出 | R7 510R → Q1 2N7002K 栅极（R6 100K 下拉）。出厂默认 **高 = 按下**（Q1 导通，J3.KEY1 被拉低），**低 = 松开**（Q1 关断，J3.KEY1 被 R5 拉高） |
| 11 | PB1 | KEY_ADC | 模拟 | ADC_IN0；U3 霍尔输出经 C17 1nF / R9 1K / C18 1nF 低通，TP1 测试点 |
| 18 | PA5 | LED6_R | 输出 | R1 1K → LED6 R 阴极 |
| 19 | PA6 | LED6_G | 输出 | R2 510R → LED6 G 阴极 |
| 20 | PA7 | LED6_B | 输出 | R3 330R → LED6 B 阴极 |
| 13/14 | PA0/PA1 | — | — | 引出测试点，未用 |
| 2/9/12/16/17 | PC1/PB3/PB0/PA3/PA4 | — | — | 悬空 |

LED6（`LED-SZYY1615RGB-B_4PIN`）第 4 脚 COM 接 +3.3V，为**共阳**：MCU 引脚输出低电平点亮。PA6 没有定时器通道，
因此三路统一使用 TIM14 中断驱动的软件 PWM（64 级，250 Hz 刷新）。

> 注意：LED6 三个通道与 PA5/PA6/PA7 的对应关系是按原理图走线几何追踪得到的（R1 1K/R 通道的走线落在 pin 18 = PA5，
> R3 330R/B 通道落在 pin 20 = PA7）。另一份资料整理（`docs/magkey-v01-design-reference.md`）给出的是相反顺序。
> 如果上电后颜色 R/B 互换，只需在 `src/board.h` 对调 `LED_R_PIN` 与 `LED_B_PIN`，或用串口命令 `led 255 0 0` 现场验证。

### 连接器（丝印）

| 连接器 | 脚 | 信号 | 说明 |
|---|---|---|---|
| **J3 接主板**（ZH1.5 3P） | 1 / 2 / 3 | +5V / KEY_OUT(=网络 KEY1) / GND | 板卡供电输入 + 按键输出。KEY1 经 Q1 开漏到地，R5 10K 上拉到 **3.3V**。出厂固件：**低电平 = 按下，高电平 = 松开** |
| **J1 RGB 背光输入**（ZH1.5 4P） | 1 / 2 / 3 / 4 | +5V_L / DIN / BIN / GND | 灯链输入（游戏侧） |
| **J2 RGB 背光输出**（ZH1.5 4P） | 4 / 3 / 2 / 1 | +5V_L / DOUT / BOUT / GND | 灯链输出到下一块板。**脚序与 J1 相反** |
| **J4**（ZH1.5 2P） | 1 / 2 | SW1 / GND | 外接调节按钮，与 S1 并联 |
| **DEBUG1**（2.54 mm 4P） | 1 / 2 / 3 / 4 | VCC(3.3V) / SWCLK / SWDIO / GND | SWD 烧录口 |
| **DEBUG2**（2.54 mm 3P） | 1 / 2 / 3 | RXD / TXD / GND | 调试串口（RXD 为 MCU 接收端） |

### 游戏灯链拓扑（仅文档，固件不介入）

```
J1.DIN ─► LED1.DI ─► LED1.DO ─► LED2.DI/BI ─► ... ─► LED5.DI ─► LED5.DO ─► J2.DOUT（同时接 J2.BOUT）
J1.BIN ─► LED1.BI                                                         （WS2813 备份数据线）
电源：J1.+5V_L ─► LED1~LED5 ─► J2.+5V_L（与 J3 的 +5V_IN 不相连，仅共地；未经 F1 保险丝）
```

每板 5 颗 WS2813B（LED1 右下、LED5 左下、LED4 中下、LED2/LED3 轴体两侧上方），顺序 LED1→LED2→LED3→LED4→LED5。
MCU 的任何引脚都没有接到 DIN/DOUT，因此固件**无法**也不应产生灯效；`light_chain_hook()` 恒返回 `false`，
串口 `light` 命令回复 `reserved`。LED6 指示灯是独立的分立 RGB，不在这条链上，不存在"只写链上第 N 颗"的取舍问题。

## 3. 四档灵敏度表

霍尔可用区间是 **0.15 V～2.80 V**（ADC 186～3474）。这两端不是触发点：静止电压就在低端，用它当红档会一上电就按下。

两端各留 **200 mV（248 个 ADC 计数）**。12 位、参考 3.3 V，1 LSB = 3300/4095 ≈ 0.806 mV；前端是 R9 1K 串联加 C17/C18 1nF，没有分压电阻，霍尔电压原样进 PB1。200 mV 比 1 个计数宽得多，红档因此离开静止点，白档也在 2.80 V 之前就能到。

内缩后的区间是 0.350 V～2.600 V，四档等分，步长 750 mV。比较函数是 `key_engine_update()`：`按下 = 本次 ADC ≥ key_engine_gear_threshold_adc(当前档)`。Flash 里保存的静止/触底值、行程换算和快速触发都不改这个数。

| 档位 | 颜色 | 电压 | ADC（3.3 V） | 适用 |
|---|---|---|---|---|
| 1 | 红 | 0.350 V | 434 | 最灵敏。0.15 V（ADC 186）及以下仍为松开 |
| 2 | 绿 | 1.100 V | 1365 | **出厂默认** |
| 3 | 蓝 | 1.850 V | 2295 | |
| 4 | 白 | 2.600 V | 3226 | 最迟钝，不必压到 2.80 V |

短按 S1 的顺序是 红 → 绿 → 蓝 → 白 → 红。已保存的旧七档记录里，5/6/7 档会在读取时归到第 4 档。档位号会从 Flash 恢复；里面的校准 ADC 不会拿来改阈值。

- 修改档位：编辑 `src/config.h` 的 `GEAR_VOLTAGE_MV`（毫伏，单调递增，落在 0.15 V 和 2.80 V 之间，不要贴两端）。
- 串口 `gears` 打印每档电压和 ADC 阈值。

档位表在 `firmware/magkey-v01/src/config.h` 的 `GEAR_VOLTAGE_MV`。比较在 `firmware/magkey-v01/src/key_engine.c` 的 `key_engine_update()`。颜色在 `firmware/magkey-v01/src/led_indicator.c` 的 `k_gear_colors`。

## 4. 编译

```bash
sudo apt-get install gcc-arm-none-eabi binutils-arm-none-eabi libnewlib-arm-none-eabi   # Debian/Ubuntu
cd firmware/magkey-v01
make            # 生成 build/magkey_v01.{elf,hex,bin,map,lst}
make test-host  # 在 PC 上跑按键判定回归（四档绝对阈值、静止不触发、校准不改阈值）
make clean
```

编译期开关（`make CFLAGS_EXTRA="..."` 或直接改 `src/config.h`）：

| 宏 | 默认 | 含义 |
|---|---|---|
| `KEY_OUT_ACTIVE_HIGH` | **1** | 出厂默认。按下时 PB2 高、Q1 导通，**J3.KEY1 低 = 按下、高 = 松开**。改为 0 则相反 |
| `HALL_MARGIN_MV` | 200 | 触发点离开 0.15 V 和 2.80 V 的距离（248 个 ADC 计数） |
| `GEAR_DEFAULT` | 2 | 出厂默认档位（绿，1.100 V） |
| `CFG_UART_CONSOLE` | 1 | 启用 DEBUG2 串口命令行 |

`build/` 目录是编译产物，不入库（见仓库根目录 `.gitignore`）。

## 5. 烧录

接口：**SWD**，DEBUG1 排针 `VCC SWCLK SWDIO GND`（VCC 为 3.3 V，可由调试器供电，也可板子经 J3 供 5V 后只接 SWCLK/SWDIO/GND）。
对应 MCU 引脚 PA2 = SWCLK、PB6 = SWDIO。芯片出厂 SWD 开启，无读保护。

| 工具 | 命令 |
|---|---|
| pyOCD（DAPLink / CMSIS-DAP / J-Link 均可） | `pip install pyocd` → `pyocd pack install PY32F002B` → `pyocd list --targets \| grep -i py32f002b` 确认目标名 → `make flash-pyocd`（默认 `--target py32f002bx5`，可用 `make flash-pyocd PYOCD_TARGET=<名称>` 覆盖） |
| J-Link | 安装 Puya J-Link 器件包后 `make flash-jlink`（`-device PY32F002BX5 -if SWD`） |
| OpenOCD | 需带 PY32F002B 支持的构建（如 py32 社区分支）：`make flash-openocd` |
| Puya ISP / PY32 Flash 工具（Windows） | 载入 `build/magkey_v01.hex`，接口 SWD，整片擦除后下载 |

起始地址 `0x08000000`（.bin 直接烧到此地址）。参数区在 `0x08005C00~0x08005FFF`（最后 1 KB，8 页轮换），
整片擦除会清空档位/校准，上电后回到出厂默认（第 2 档绿色，未校准）。

烧录后自检：LED6 应亮绿色（第 2 档）；短按 S1 颜色依次 蓝 → 白 → 红 → 绿；按键按下时 J3.KEY1 为低，松开时为高；轻触（霍尔仍在 0.15 V 附近）不应触发；DEBUG2 串口 115200 发送 `status` 应有回显。

## 6. 使用说明

触发只看当前档的绝对 ADC。长按校准和 Flash 里旧的静止/触底值不会改这四个数。串口 `raw 1` 仍以 20 Hz 输出 `adc,depth,pressed`，深度只是显示，不参与判定。

## 7. 假设与待确认事项

| # | 假设 | 依据 / 风险 |
|---|---|---|
| 1 | **输出接口 = 开漏按键信号**。出厂镜像 **J3.KEY1 低 = 按下，高 = 松开**（`KEY_OUT_ACTIVE_HIGH=1`：按下时 PB2 为高、Q1 导通，松开时 PB2 为低、R5 把 KEY1 拉高） | 原理图中无 USB。Q1 是 2N7002K，漏极开到 J3，R5 10K 上拉 |
| 2 | **按下电压升高**，静止在 0.15 V 一端 | 四档是绝对电压，不再按校准记录翻转极性 |
| 3 | 触发点在 0.35 V～2.60 V，不贴 0.15 V / 2.80 V | 贴低端时静止采样就会过阈值。要改边界就改 `GEAR_VOLTAGE_MV` |
| 4 | 总行程取 3.40 mm | 规格 3.5 +0/−0.4，实测均值 3.37 |
| 5 | LED6 R/G/B = PA5/PA6/PA7 | 原理图几何追踪；若与实物相反见第 2 节说明 |
| 6 | DEBUG2 丝印 RXD/TXD 以 MCU 为参照（PB5 = MCU RX） | 原理图网络 RXD→PB5、TXD→PB4，与 PY32F002B AF1 复用表一致 |
| 7 | 系统时钟 24 MHz HSI，无外部晶振 | 板上无晶振；`SystemInit` 加载出厂 HSI 校准值 |
| 8 | 第 4 档白色为 R/G/B 全亮 (255,255,255) | 1615 灯珠经 1K/510R/330R 限流，三通道电流不完全相等，白色会略偏；要改配色改 `led_indicator.c` 的 `k_gear_colors` |

## 8. 目录结构

```
firmware/magkey-v01/
├── Makefile                  arm-none-eabi-gcc 构建 + 烧录目标
├── py32f002bx5_magkey.ld     链接脚本（23 KB 程序 + 1 KB 参数区）
├── src/
│   ├── board.h               引脚定义（唯一需要按硬件改动的文件）
│   ├── config.h              档位表、触发算法、极性、串口等编译期参数
│   ├── main.c                主循环、S1 交互、校准状态机、恢复出厂
│   ├── key_engine.c/.h       ADC 与四档绝对阈值比较 → KEY_OUT
│   ├── hall_adc.c/.h         ADC 初始化、过采样、IIR
│   ├── led_indicator.c/.h    LED6 软件 PWM、四色表、闪烁
│   ├── button.c/.h           S1 消抖、短按/长按
│   ├── storage.c/.h          Flash 参数区（8 页轮换、CRC16）
│   ├── console.c/.h          DEBUG2 串口命令行
│   ├── light_chain.c/.h      游戏灯链预留接口（空实现）
│   ├── systick.c/.h          1 ms 时基
│   └── libc_stubs.c          _init/_fini 空实现
├── tests/host/               宿主机回归（make test-host），桩替换 GPIO
└── vendor/                   Puya 官方 CMSIS/LL 头文件、启动文件（BSD-3-Clause）
```
