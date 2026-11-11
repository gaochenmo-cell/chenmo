/*
 * board.h - ADX-Magkey-V01 硬件引脚定义
 *
 * 来源：ADX-Magkey-V01.sch (2026/11/8, REV 01) + PCB 丝印。
 * MCU：Puya PY32F002BF15U6TR（Cortex-M0+，24 MHz HSI，24 KB Flash，3 KB SRAM，QFN20）。
 *
 *  QFN20 引脚   MCU 端口        网络        说明
 *  ---------   ------------   ---------   ---------------------------------------------
 *  1           PC0/NRST       RST         R4 10K 上拉 + C12 0.1uF，未引出到 DEBUG1
 *  3           PB7            SW1         S1 轻触开关 / J4 外接按键，R8 10K 上拉到 3.3V，按下为低
 *  5           PB6/SWD        SWDIO       DEBUG1 pin3
 *  15          PA2/SWCLK      SWCLK       DEBUG1 pin2
 *  7           PB5            RXD         DEBUG2 pin1（USART1_RX, AF1）
 *  8           PB4            TXD         DEBUG2 pin2（USART1_TX, AF1）
 *  10          PB2            KEY_OUT     R7 510R -> Q1 2N7002K 栅极；高电平 = 按下 = J3.KEY1 拉低
 *  11          PB1/ADC_IN0    KEY_ADC     U3 智风V1 磁轴霍尔输出（0.4V 静止 ~ 3.0V 触底），R9 1K + C17/C18 1nF
 *  18          PA5            LED6_R      R1 1K   -> LED6 R 阴极（LED6 共阳接 3.3V，低电平点亮）
 *  19          PA6            LED6_G      R2 510R -> LED6 G 阴极
 *  20          PA7            LED6_B      R3 330R -> LED6 B 阴极
 *  13/14/16/17 PA0/PA1/PA3/PA4 -          未使用
 *  2/9/12      PC1/PB3/PB0    -           未使用
 *
 *  LED1~LED5 (WS2813B) 为游戏灯效链路：J1.DIN -> LED1 -> ... -> LED5 -> J2.DOUT，
 *  由 +5V_L 供电，与 MCU 没有任何电气连接，固件无法也不应驱动它们（见 light_chain.h）。
 */
#ifndef BOARD_H
#define BOARD_H

#include "py32f0xx.h"
#include "py32f002b_ll_gpio.h"

/* 霍尔输出 ADC 输入：PB1 / ADC_IN0 */
#define HALL_ADC_GPIO_PORT      GPIOB
#define HALL_ADC_GPIO_PIN       LL_GPIO_PIN_1
#define HALL_ADC_CHANNEL        LL_ADC_CHANNEL_0

/* 按键输出：PB2 -> Q1 栅极 */
#define KEY_OUT_GPIO_PORT       GPIOB
#define KEY_OUT_GPIO_PIN        LL_GPIO_PIN_2

/* 灵敏度调节 / 校准按钮：PB7，低有效 */
#define SW1_GPIO_PORT           GPIOB
#define SW1_GPIO_PIN            LL_GPIO_PIN_7

/* 档位指示灯 LED6（共阳，低电平点亮） */
#define LED_GPIO_PORT           GPIOA
#define LED_R_PIN               LL_GPIO_PIN_5
#define LED_G_PIN               LL_GPIO_PIN_6
#define LED_B_PIN               LL_GPIO_PIN_7

/* 调试串口 DEBUG2：USART1，PB4 = TX，PB5 = RX，AF1 */
#define DBG_UART                USART1
#define DBG_UART_GPIO_PORT      GPIOB
#define DBG_UART_TX_PIN         LL_GPIO_PIN_4
#define DBG_UART_RX_PIN         LL_GPIO_PIN_5
#define DBG_UART_AF             LL_GPIO_AF_1

#endif /* BOARD_H */
