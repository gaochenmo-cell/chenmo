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
    assert(key_engine_get_gear() == 2);

    /* 0.15–2.80 V 四等分，3.3 V 参考下的 ADC，以及换算行程 */
    static const uint16_t k_mv[4] = { 150, 1033, 1917, 2800 };
    static const uint16_t k_adc[4] = { 186, 1281, 2378, 3474 };
    static const uint16_t k_travel[4] = { 0, 113, 227, 340 };
    uint16_t prev = 0;
    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        uint16_t thr = key_engine_gear_threshold_adc(g);
        assert(key_engine_gear_voltage_mv(g) == k_mv[g - 1]);
        assert(key_engine_gear_travel(g) == k_travel[g - 1]);
        assert(thr == k_adc[g - 1]);
        assert(thr > prev);
        prev = thr;
    }
    assert(k_adc[0] == CAL_DEFAULT_REST_ADC);
    assert(k_adc[3] == CAL_DEFAULT_BOTTOM_ADC);

    /* 第 1 档边界就是静止电压：停在边界不按下，越过 1 个计数才按下，回到边界即松开 */
    key_engine_set_gear(1);
    assert(!key_engine_update(k_adc[0], now++));
    assert(g_key_out == 1);
    assert(key_engine_update((uint16_t)(k_adc[0] + 1), now++));
    assert(g_key_out == 0);
    assert(!key_engine_update(k_adc[0], now++));
    assert(g_key_out == 1);

    /* 第 2 档：ADC 达到 1281 按下。按下时 PB2 为低。 */
    key_engine_set_gear(2);
    assert(!key_engine_update((uint16_t)(k_adc[1] - 1), now++));
    assert(key_engine_update(k_adc[1], now++));
    assert(g_key_out == 0);

    /* 快速触发：3.00 mm 按下后回弹 0.30 mm 释放，再压 0.25 mm 重新触发，全程高于触发点 */
    sweep(0, 300, 5, &now);
    assert(key_engine_pressed());
    sweep(300, 270, -5, &now);
    assert(!key_engine_pressed());
    sweep(270, 295, 5, &now);
    assert(key_engine_pressed());
    sweep(295, 0, -5, &now);
    assert(!key_engine_pressed());

    /* 第 2 档迟滞：刚低于 1281 仍保持按下，明显回到触发点以上才松开 */
    key_engine_set_gear(2);
    assert(key_engine_update(1281, now++));
    assert(key_engine_update(1280, now++));
    assert(g_key_out == 0);
    assert(!key_engine_update(1100, now++));
    assert(g_key_out == 1);

    /* 反极性校准：按下电压降低时，行程仍然按深度判定 */
    key_cal_t inv = { 3700, 500 };
    key_engine_set_cal(&inv);
    key_engine_set_gear(2);
    assert(key_engine_gear_threshold_adc(2) < inv.rest_adc);
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
