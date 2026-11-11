/*
 * console.h - DEBUG2 串口（115200 8N1）调试 / 配置命令行
 *
 * 命令（以回车结束）：
 *   help            命令列表
 *   ver             固件版本
 *   status          档位、原始 ADC、深度、按下状态、校准值
 *   gears           七档触发行程与当前 ADC 阈值
 *   gear <1..7>     设置档位并保存
 *   cal             启动校准流程（等同长按 S1）
 *   raw [0|1]       打开/关闭 20 Hz 原始数据流（adc,depth,pressed）
 *   led <r> <g> <b> 临时点亮 LED6 测试颜色（0..255），led 无参数恢复档位色
 *   reset           恢复出厂设置
 *   light ...       预留，返回 "light: reserved"（见 light_chain.h）
 */
#ifndef CONSOLE_H
#define CONSOLE_H

#include <stdint.h>
#include <stdbool.h>

void console_init(void);
void console_task(uint32_t now_ms);
void console_puts(const char *s);
void console_put_uint(uint32_t v);

/* 由 main 提供给控制台的回调 */
void app_request_gear(uint8_t gear);
void app_request_calibration(void);
void app_request_factory_reset(void);

#endif /* CONSOLE_H */
