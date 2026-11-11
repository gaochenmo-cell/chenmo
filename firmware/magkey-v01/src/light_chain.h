/*
 * light_chain.h - 游戏灯效链路（LED1~LED5 WS2813B）的预留接口 —— 仅占位，不含任何逻辑
 *
 * 硬件事实（ADX-Magkey-V01.sch）：
 *   J1.DIN -> LED1.DI -> LED1.DO -> LED2.DI/BI -> ... -> LED5.DO -> J2.DOUT
 *   J1.BIN -> LED1.BI（WS2813 备份信号线），LED5.DO 同时接 J2.BOUT
 *   灯链由 J1 的 +5V_L 供电（与 J3 的 +5V_IN 不相连，仅共地），每板 5 颗。
 *   灯链与 MCU（PY32F002B）之间 **没有任何连线**：数据由游戏/主板从 J1 送入，透传到 J2，
 *   供下一块按键板级联。因此固件既不能也不应驱动这些灯。
 *
 * 这里保留的函数 / 串口命令只是为了将来硬件改版（例如 MCU 引脚接入 DIN）时有统一的挂接点。
 */
#ifndef LIGHT_CHAIN_H
#define LIGHT_CHAIN_H

#include <stdint.h>
#include <stdbool.h>

/* 本板灯链拓扑常量，供文档 / 上位机查询 */
#define LIGHT_CHAIN_LED_COUNT       5U      /* LED1..LED5 */
#define LIGHT_CHAIN_LED_TYPE        "WS2813B"
#define LIGHT_CHAIN_MCU_CONNECTED   0       /* 0 = MCU 未接入灯链（当前硬件） */

/* 预留：灯效命令 ID（串口协议中 "light" 命令转发到这里） */
typedef enum {
    LIGHT_CMD_RESERVED = 0,
} light_cmd_t;

/* 预留钩子：当前恒返回 false（未实现 / 硬件不支持），不做任何事情。 */
bool light_chain_hook(light_cmd_t cmd, const uint8_t *payload, uint8_t len);

#endif /* LIGHT_CHAIN_H */
