/*
 * main.c - ADX-Magkey-V01 静音可调磁轴按键固件
 *
 * 功能概要：
 *   - 霍尔 ADC 连续采样 -> 行程深度 -> 七档触发点 + 快速触发/迟滞 -> KEY_OUT (PB2 -> Q1 -> J3.KEY1)
 *   - S1 短按：档位 1..7 循环，LED6 显示 赤/橙/黄/绿/蓝/靛/紫，档位写入 Flash
 *   - S1 长按 3 s：校准（白灯快闪时松开键；青灯常亮时把键按到底）
 *   - 上电按住 S1 3 s：恢复出厂设置
 *   - DEBUG2 串口 115200：status / gears / gear n / cal / raw / led / reset / light(预留)
 *   - LED1~LED5 游戏灯链：不实现，仅保留 light_chain.h 接口
 */
#include "py32f0xx.h"
#include "config.h"
#include "board.h"
#include "systick.h"
#include "hall_adc.h"
#include "key_engine.h"
#include "led_indicator.h"
#include "button.h"
#include "storage.h"
#include "console.h"
#include "py32f002b_ll_bus.h"

typedef enum {
    APP_RUN = 0,
    APP_CAL_REST,
    APP_CAL_BOTTOM,
} app_state_t;

static settings_t  s_settings;
static app_state_t s_state = APP_RUN;
static uint32_t    s_state_t0;
static bool        s_save_pending;
static uint32_t    s_save_due_ms;

/* 校准过程变量 */
static uint32_t    s_cal_acc;
static uint32_t    s_cal_n;
static uint16_t    s_cal_rest;
static uint16_t    s_cal_extreme;
static bool        s_cal_bottom_seen;
static uint32_t    s_cal_bottom_t0;

static const rgb_t k_white = { 255, 255, 255 };
static const rgb_t k_cyan  = { 0, 255, 255 };
static const rgb_t k_green = { 0, 255, 0 };
static const rgb_t k_red   = { 255, 0, 0 };

static void apply_settings(void)
{
    key_cal_t cal = { s_settings.rest_adc, s_settings.bottom_adc };
    key_engine_set_cal(&cal);
    key_engine_set_gear(s_settings.gear);
    led_show_gear(s_settings.gear);
}

static void schedule_save(uint32_t now_ms)
{
    s_save_pending = true;
    s_save_due_ms = now_ms + GEAR_SAVE_DELAY_MS;
}

static void save_now(void)
{
    s_save_pending = false;
    if (!storage_save(&s_settings)) {
        console_puts("flash save failed\n");
        led_blink(k_red, 200, 3);
    }
}

void app_request_gear(uint8_t gear)
{
    if (gear < 1U || gear > GEAR_COUNT) return;
    s_settings.gear = gear;
    key_engine_set_gear(gear);
    led_show_gear(gear);
    schedule_save(millis());
}

static void cal_start(uint32_t now_ms)
{
    key_engine_force_release();
    s_state = APP_CAL_REST;
    s_state_t0 = now_ms;
    s_cal_acc = 0;
    s_cal_n = 0;
    s_cal_bottom_seen = false;
    led_blink(k_white, 200, 0);
    console_puts("cal: sampling rest, do not touch key\n");
}

void app_request_calibration(void)
{
    if (s_state == APP_RUN) {
        cal_start(millis());
    }
}

void app_request_factory_reset(void)
{
    storage_erase_all();
    storage_defaults(&s_settings);
    apply_settings();
    s_save_pending = false;
}

static void cal_finish(bool ok, uint32_t now_ms)
{
    s_state = APP_RUN;
    led_stop_blink();
    if (ok) {
        s_settings.calibrated = 1;
        s_settings.rest_adc = s_cal_rest;
        s_settings.bottom_adc = s_cal_extreme;
        apply_settings();
        save_now();
        led_blink(k_green, 250, 3);
        console_puts("cal ok rest=");
        console_put_uint(s_cal_rest);
        console_puts(" bottom=");
        console_put_uint(s_cal_extreme);
        console_puts("\n");
    } else {
        apply_settings();   /* 恢复原值和档位颜色 */
        led_blink(k_red, 250, 3);
        console_puts("cal failed (span too small / timeout)\n");
    }
    (void)now_ms;
}

static uint16_t abs_diff(uint16_t a, uint16_t b)
{
    return (a > b) ? (uint16_t)(a - b) : (uint16_t)(b - a);
}

static void cal_task(uint16_t adc, uint32_t now_ms)
{
    switch (s_state) {
    case APP_CAL_REST:
        /* 前 300 ms 不采样，给用户松手时间 */
        if ((now_ms - s_state_t0) >= 300U) {
            s_cal_acc += adc;
            s_cal_n++;
        }
        if ((now_ms - s_state_t0) >= CAL_REST_SAMPLE_MS && s_cal_n > 0U) {
            s_cal_rest = (uint16_t)(s_cal_acc / s_cal_n);
            s_cal_extreme = s_cal_rest;
            s_state = APP_CAL_BOTTOM;
            s_state_t0 = now_ms;
            led_stop_blink();
            led_set_rgb(k_cyan);
            console_puts("cal: rest=");
            console_put_uint(s_cal_rest);
            console_puts(" now press key to bottom\n");
        }
        break;

    case APP_CAL_BOTTOM:
        if (abs_diff(adc, s_cal_rest) > abs_diff(s_cal_extreme, s_cal_rest)) {
            s_cal_extreme = adc;
        }
        if (!s_cal_bottom_seen && abs_diff(s_cal_extreme, s_cal_rest) >= CAL_MIN_SPAN_ADC) {
            s_cal_bottom_seen = true;
            s_cal_bottom_t0 = now_ms;
        }
        if (s_cal_bottom_seen && (now_ms - s_cal_bottom_t0) >= CAL_BOTTOM_HOLD_MS) {
            cal_finish(true, now_ms);
        } else if (!s_cal_bottom_seen && (now_ms - s_state_t0) >= CAL_BOTTOM_TIMEOUT_MS) {
            cal_finish(false, now_ms);
        }
        break;

    default:
        break;
    }
}

/* 上电按住 S1 达 BTN_BOOT_RESET_MS -> 恢复出厂 */
static void boot_factory_reset_check(void)
{
    if (!button_raw_down()) return;
    uint32_t t0 = millis();
    led_blink(k_white, 300, 0);
    while (button_raw_down()) {
        led_task(millis());
        if (elapsed_ms(t0) >= BTN_BOOT_RESET_MS) {
            led_stop_blink();
            app_request_factory_reset();
            led_blink(k_green, 250, 3);
            while (button_raw_down()) {
                led_task(millis());
            }
            return;
        }
    }
    led_stop_blink();
}

int main(void)
{
    LL_APB1_GRP2_EnableClock(LL_APB1_GRP2_PERIPH_SYSCFG);
    LL_APB1_GRP1_EnableClock(LL_APB1_GRP1_PERIPH_PWR);

    systick_init();
    key_engine_init();      /* 先把 KEY_OUT 置为松开 */
    led_init();
    button_init();
    hall_adc_init();
    console_init();

    storage_load(&s_settings);
    apply_settings();
    boot_factory_reset_check();
    apply_settings();

    for (;;) {
        uint32_t now = millis();
        uint16_t adc = hall_adc_read();

        if (s_state == APP_RUN) {
            key_engine_update(adc, now);
        } else {
            cal_task(adc, now);
        }

        btn_event_t evt = button_poll(now);
        if (evt == BTN_EVT_SHORT && s_state == APP_RUN) {
            uint8_t g = key_engine_get_gear() + 1U;
            if (g > GEAR_COUNT) g = 1U;
            app_request_gear(g);
        } else if (evt == BTN_EVT_LONG && s_state == APP_RUN) {
            cal_start(now);
        }

        if (s_save_pending && (int32_t)(now - s_save_due_ms) >= 0) {
            save_now();
        }

        led_task(now);
        console_task(now);
    }
}
