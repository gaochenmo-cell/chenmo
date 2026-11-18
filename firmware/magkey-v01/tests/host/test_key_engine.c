/*
 * 宿主机回归：绝对电压阈值。静止（≤0.15 V）四档都松开；
 * 采样越过该档 ADC 才按下。校准写入不得改阈值。
 */
#include <assert.h>
#include <stdio.h>
#include "key_engine.h"
#include "button.h"
#include "config.h"

int g_key_out;
int g_sw1_down;

int main(void)
{
    uint32_t now = 0;
    static const uint16_t k_mv[4] = { 350, 1100, 1850, 2600 };
    static const uint16_t k_adc[4] = { 434, 1365, 2295, 3226 };
    uint16_t rest = MV_TO_ADC(HALL_V_LOW_MV);

    key_engine_init();
    /* KEY_OUT_ACTIVE_HIGH=1：松开时 PB2 为低（Q1 关，J3 被 R5 拉高） */
    assert(g_key_out == 0);
    assert(GEAR_COUNT == 4);
    assert(key_engine_get_gear() == 2);
    assert(rest == 186);
    assert(HALL_MARGIN_MV == 200);

    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        assert(key_engine_gear_voltage_mv(g) == k_mv[g - 1]);
        assert(key_engine_gear_threshold_adc(g) == k_adc[g - 1]);
        assert(k_adc[g - 1] == MV_TO_ADC(k_mv[g - 1]));
        assert(k_mv[g - 1] > HALL_V_LOW_MV);
        assert(k_mv[g - 1] < HALL_V_HIGH_MV);
    }
    assert(k_adc[1] - k_adc[0] > 0);
    assert((k_mv[1] - k_mv[0]) == (k_mv[2] - k_mv[1]));
    assert((k_mv[2] - k_mv[1]) == (k_mv[3] - k_mv[2]));

    /* 静止及以下：四档都松开，PB2 保持低 */
    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        key_engine_set_gear(g);
        assert(!key_engine_update(0, now++));
        assert(!key_engine_update(rest, now++));
        assert(!key_engine_update((uint16_t)(rest - 1U), now++));
        assert(!key_engine_pressed());
        assert(g_key_out == 0);
    }

    /* 越过本档阈值才按下；低一计数仍松开。按下时 PB2 为高。 */
    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        key_engine_set_gear(g);
        assert(!key_engine_update((uint16_t)(k_adc[g - 1] - 1U), now++));
        assert(g_key_out == 0);
        assert(key_engine_update(k_adc[g - 1], now++));
        assert(key_engine_pressed());
        assert(g_key_out == 1);
        assert(!key_engine_update((uint16_t)(k_adc[g - 1] - 1U), now++));
        assert(g_key_out == 0);
    }

    /* 未写入校准前，阈值保持出厂表 */
    assert(!key_engine_is_calibrated());

    /* 三击：最低 180，最高 3000。第三次松开才确认。 */
    key_engine_cal_begin();
    assert(!key_engine_cal_feed(200));
    assert(!key_engine_cal_feed(2000));
    assert(!key_engine_cal_feed(200));
    assert(key_engine_cal_presses() == 1);
    assert(!key_engine_cal_feed(180));
    assert(!key_engine_cal_feed(3000));
    assert(!key_engine_cal_feed(180));
    assert(key_engine_cal_presses() == 2);
    assert(!key_engine_cal_feed(2500));
    assert(key_engine_cal_feed(190));
    assert(key_engine_cal_presses() == 3);
    assert(key_engine_cal_min() == 180);
    assert(key_engine_cal_max() == 3000);

    key_cal_t span = { key_engine_cal_min(), key_engine_cal_max() };
    key_engine_set_cal(&span);
    assert(key_engine_is_calibrated());
    uint16_t margin = (uint16_t)((200U * 4095U) / 3300U);
    assert(key_engine_gear_threshold_adc(1) == (uint16_t)(180U + margin));
    assert(key_engine_gear_threshold_adc(4) == (uint16_t)(3000U - margin));
    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        key_engine_set_gear(g);
        assert(!key_engine_update(180, now++));
        assert(g_key_out == 0);
        assert(key_engine_update(key_engine_gear_threshold_adc(g), now++));
        assert(g_key_out == 1);
        key_engine_force_release();
    }

    /* 跨度短于 0.40 V：内缩改为跨度的 1/4，静止点仍松开 */
    key_cal_t short_span = { 1000, 1200 };
    key_engine_set_cal(&short_span);
    assert(key_engine_gear_threshold_adc(1) > 1000);
    assert(key_engine_gear_threshold_adc(4) < 1200);
    key_engine_set_gear(1);
    assert(!key_engine_update(1000, now++));

    /* 清除校准后回到出厂表 */
    key_engine_set_cal(0);
    assert(!key_engine_is_calibrated());
    assert(key_engine_gear_threshold_adc(2) == k_adc[1]);

    /* S1 按住 5 s 才是长按；4.999 s 不是，松开仍是短按 */
    g_sw1_down = 0;
    button_init();
    button_poll(0);
    g_sw1_down = 1;
    button_poll(1000);
    assert(button_poll(1000 + BTN_DEBOUNCE_MS) == BTN_EVT_NONE);
    uint32_t held = 1000U + BTN_DEBOUNCE_MS;
    assert(BTN_LONG_PRESS_MS == 5000U);
    assert(button_poll(held + 4999U) == BTN_EVT_NONE);
    assert(button_poll(held + 5000U) == BTN_EVT_LONG);
    g_sw1_down = 0;
    button_poll(held + 5000U);
    assert(button_poll(held + 5000U + BTN_DEBOUNCE_MS) == BTN_EVT_NONE);

    g_sw1_down = 1;
    button_poll(20000);
    button_poll(20000 + BTN_DEBOUNCE_MS);
    g_sw1_down = 0;
    button_poll(20100);
    assert(button_poll(20100 + BTN_DEBOUNCE_MS) == BTN_EVT_SHORT);

    puts("key_engine host test OK");
    return 0;
}
