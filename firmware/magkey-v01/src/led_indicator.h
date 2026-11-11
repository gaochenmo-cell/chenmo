/*
 * led_indicator.h - 档位指示灯 LED6（共阳 RGB，PA5/PA6/PA7 低电平点亮，软件 PWM）
 */
#ifndef LED_INDICATOR_H
#define LED_INDICATOR_H

#include <stdint.h>

typedef struct {
    uint8_t r, g, b;
} rgb_t;

void led_init(void);

/* 直接设置颜色（0..255 各通道，内部按 LED_BRIGHTNESS 缩放并量化到 LED_PWM_STEPS） */
void led_set_rgb(rgb_t c);
void led_off(void);

/* 显示某档对应的颜色（1..GEAR_COUNT：赤 橙 黄 绿 蓝 靛 紫） */
void  led_show_gear(uint8_t gear);
rgb_t led_gear_color(uint8_t gear);

/* 闪烁：颜色 c 与熄灭交替，period_ms 为一个亮灭周期；count=0 表示一直闪到 led_stop_blink */
void led_blink(rgb_t c, uint16_t period_ms, uint8_t count);
void led_stop_blink(void);

/* 主循环定期调用，驱动闪烁时序 */
void led_task(uint32_t now_ms);

#endif /* LED_INDICATOR_H */
