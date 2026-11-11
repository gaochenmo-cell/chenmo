/*
 * button.h - S1 / J4 调节按钮（PB7，低有效）消抖与短按/长按识别
 */
#ifndef BUTTON_H
#define BUTTON_H

#include <stdint.h>
#include <stdbool.h>

typedef enum {
    BTN_EVT_NONE = 0,
    BTN_EVT_SHORT,        /* 松开时判定：按住时间 < BTN_LONG_PRESS_MS */
    BTN_EVT_LONG,         /* 按住达到 BTN_LONG_PRESS_MS 时立即触发一次 */
} btn_event_t;

void        button_init(void);
/* 主循环定期调用 */
btn_event_t button_poll(uint32_t now_ms);
/* 当前消抖后的物理状态 */
bool        button_is_down(void);
/* 读原始电平（上电检测用） */
bool        button_raw_down(void);

#endif /* BUTTON_H */
