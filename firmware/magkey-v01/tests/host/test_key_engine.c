/*
 * 宿主机回归：用线性行程模型驱动真实的 key_engine.c。
 *   gcc 编译，不需要交叉工具链：make test-host
 */
#include <assert.h>
#include <stdio.h>
#include "key_engine.h"
#include "config.h"

int g_key_out;

static uint16_t adc_of(uint16_t travel)
{
    return (uint16_t)(CAL_DEFAULT_REST_ADC
        + (uint32_t)travel * (CAL_DEFAULT_BOTTOM_ADC - CAL_DEFAULT_REST_ADC)
          / SWITCH_TOTAL_TRAVEL);
}

static void sweep(uint16_t from, uint16_t to, int step, uint32_t *now)
{
    if (step > 0) {
        for (int t = from; t <= (int)to; t += step) {
            key_engine_update(adc_of((uint16_t)t), (*now)++);
        }
    } else {
        for (int t = from; t >= (int)to; t += step) {
            key_engine_update(adc_of((uint16_t)t), (*now)++);
        }
    }
}

int main(void)
{
    uint32_t now = 0;
    key_engine_init();
    /* 出厂 KEY_OUT_ACTIVE_HIGH=0：松开时 PB2 为高 */
    assert(g_key_out == 1);
    assert(GEAR_COUNT == 4);

    /* 四档阈值随行程单调上升，且落在 0.4 V ~ 3.0 V 之间 */
    static const uint16_t k_travel[4] = { 30, 107, 183, 260 };
    static const uint16_t k_thr[4] = { 781, 1511, 2232, 2963 };
    uint16_t prev = 0;
    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        uint16_t thr = key_engine_gear_threshold_adc(g);
        assert(key_engine_gear_travel(g) == k_travel[g - 1]);
        assert(thr == k_thr[g - 1]);
        assert(thr > prev);
        assert(thr > CAL_DEFAULT_REST_ADC && thr < CAL_DEFAULT_BOTTOM_ADC);
        prev = thr;
    }

    /* 第 2 档：1.07 mm 触发，从 2.00 mm 回弹 0.20 mm 释放。按下时 PB2 为低。 */
    key_engine_set_gear(2);
    int pressed_at = -1, released_at = -1;
    for (int t = 0; t <= 200; t += 5) {
        bool p = key_engine_update(adc_of((uint16_t)t), now++);
        if (p && pressed_at < 0) pressed_at = t;
    }
    assert(g_key_out == 0);
    for (int t = 200; t >= 0; t -= 5) {
        bool p = key_engine_update(adc_of((uint16_t)t), now++);
        if (!p && released_at < 0) released_at = t;
    }
    assert(pressed_at == 110);
    assert(released_at == 200 - RT_RELEASE_TRAVEL);
    assert(g_key_out == 1);

    /* 快速触发：3.00 mm 按下后回弹 0.30 mm 释放，再压 0.25 mm 重新触发，全程高于触发点 */
    sweep(0, 300, 5, &now);
    assert(key_engine_pressed());
    sweep(300, 270, -5, &now);
    assert(!key_engine_pressed());
    sweep(270, 295, 5, &now);
    assert(key_engine_pressed());
    sweep(295, 0, -5, &now);
    assert(!key_engine_pressed());

    /* 第 1 档边界：0.25 mm 不触发，0.31 mm 触发，回到触发点 - 迟滞才释放 */
    key_engine_set_gear(1);
    for (int i = 0; i < 50; i++) key_engine_update(adc_of(25), now++);
    assert(!key_engine_pressed());
    key_engine_update(adc_of(31), now++);
    assert(key_engine_pressed());
    assert(g_key_out == 0);
    key_engine_update(adc_of(19), now++);
    assert(!key_engine_pressed());
    assert(g_key_out == 1);

    /* 反极性校准：按下电压降低时，行程仍然按深度判定 */
    key_cal_t inv = { 3700, 500 };
    key_engine_set_cal(&inv);
    key_engine_set_gear(2);
    assert(key_engine_gear_threshold_adc(4) < inv.rest_adc);
    for (int t = 0; t <= 200; t += 5) {
        uint16_t adc = (uint16_t)(3700 - (uint32_t)t * 3200 / 340);
        key_engine_update(adc, now++);
    }
    assert(key_engine_pressed());
    key_engine_force_release();
    assert(!key_engine_pressed() && g_key_out == 1);

    /* 静止基线跟踪：松开超过空闲时间后，rest 跟到当前 ADC（不超过漂移上限） */
    key_cal_t def = { CAL_DEFAULT_REST_ADC, CAL_DEFAULT_BOTTOM_ADC };
    key_engine_set_cal(&def);
    for (uint32_t i = 0; i < 6000; i++) {
        key_engine_update(CAL_DEFAULT_REST_ADC + 100, now++);
    }
    assert(key_engine_effective_rest() == CAL_DEFAULT_REST_ADC + 100);

    puts("key_engine host test OK");
    return 0;
}
