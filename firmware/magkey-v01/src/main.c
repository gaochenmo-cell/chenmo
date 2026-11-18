/*
 * main.c - ADX-Magkey-V01 静音可调磁轴按键固件
 *
 * 功能概要：
 *   - 霍尔 ADC 采样与当前档的绝对电压阈值比较 -> KEY_OUT (PB2 -> Q1 -> J3.KEY1 低 = 按下)
 *   - S1 短按：档位 1..4 循环，LED6 显示 红/绿/蓝/白，档位写入 Flash
 *   - S1 按住 5 s：校准。LED6 品红常亮，磁轴按 3 次，第三次松开确认。
 *     完成后黄灯闪三下，再回到当前档颜色。
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
    APP_CAL,
} app_state_t;

static settings_t  s_settings;
static app_state_t s_state = APP_RUN;
static bool        s_save_pending;
static uint32_t    s_save_due_ms;

static const rgb_t k_white   = { 255, 255, 255 };
static const rgb_t k_green   = { 0, 255, 0 };
static const rgb_t k_red     = { 255, 0, 0 };
static const rgb_t k_magenta = { 255, 0, 255 }; /* 校准中，不是四档颜色 */
static const rgb_t k_yellow  = { 255, 255, 0 }; /* 校准完成，闪三下 */

static void apply_settings(void)
{
    if (s_settings.calibrated && s_settings.bottom_adc > s_settings.rest_adc) {
        key_cal_t cal = { s_settings.rest_adc, s_settings.bottom_adc };
        key_engine_set_cal(&cal);
    } else {
        key_engine_set_cal(0);
    }
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
    (void)now_ms;
    s_state = APP_CAL;
    key_engine_cal_begin();
    led_stop_blink();
    led_set_rgb(k_magenta);
    console_puts("cal: press the magnetic key 3 times\n");
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

static void cal_finish(uint32_t now_ms)
{
    (void)now_ms;
    s_state = APP_RUN;
    uint16_t vmin = key_engine_cal_min();
    uint16_t vmax = key_engine_cal_max();
    if (vmax > vmin) {
        s_settings.calibrated = 1;
        s_settings.rest_adc = vmin;
        s_settings.bottom_adc = vmax;
        key_cal_t cal = { vmin, vmax };
        key_engine_set_cal(&cal);
        s_save_pending = false;
        if (!storage_save(&s_settings)) {
            console_puts("flash save failed\n");
        }
        console_puts("cal ok min=");
        console_put_uint(vmin);
        console_puts(" max=");
        console_put_uint(vmax);
        console_puts("\n");
    } else {
        console_puts("cal ignored (no span)\n");
    }
    /* 先记下当前档颜色，再黄灯闪三下；闪完 led_task 会回到这个颜色 */
    led_show_gear(s_settings.gear);
    led_blink(k_yellow, CAL_DONE_BLINK_MS, 3);
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

        key_engine_update(adc, now);
        if (s_state == APP_CAL && key_engine_cal_feed(adc)) {
            cal_finish(now);
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
