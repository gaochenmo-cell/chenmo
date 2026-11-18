/*
 * 宿主机回归：绝对电压阈值。静止（≤0.15 V）四档都松开；
 * 采样越过该档 ADC 才按下。校准写入不得改阈值。
 */
#include <assert.h>
#include <stdio.h>
#include "key_engine.h"
#include "config.h"

int g_key_out;

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

    /* 旧的校准重映射不得把阈值拉回静止点附近 */
    key_cal_t bogus = { 400, 900 };
    key_engine_set_cal(&bogus);
    key_engine_set_gear(1);
    assert(key_engine_gear_threshold_adc(1) == k_adc[0]);
    assert(!key_engine_update(rest, now++));
    assert(!key_engine_update(400, now++));
    assert(key_engine_update(k_adc[0], now++));

    puts("key_engine host test OK");
    return 0;
}
