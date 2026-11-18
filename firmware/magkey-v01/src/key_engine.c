#include "key_engine.h"
#include "config.h"
#ifdef HOST_TEST
#include "host_stubs.h"
#else
#include "board.h"
#include "py32f002b_ll_bus.h"
#endif

static const uint16_t k_gear_mv[GEAR_COUNT] = GEAR_VOLTAGE_MV;

static uint8_t  s_gear = GEAR_DEFAULT;
static uint16_t s_depth;
static bool     s_pressed;

static inline void key_out_write(bool pressed)
{
#if KEY_OUT_ACTIVE_HIGH
    /* PB2 高 → Q1 导通 → J3.KEY1 低 */
    if (pressed) LL_GPIO_SetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
    else         LL_GPIO_ResetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
#else
    if (pressed) LL_GPIO_ResetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
    else         LL_GPIO_SetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
#endif
}

void key_engine_init(void)
{
    LL_IOP_GRP1_EnableClock(LL_IOP_GRP1_PERIPH_GPIOB);
    s_pressed = false;
    key_out_write(false);
    LL_GPIO_SetPinMode(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_PULL_NO);
}

void key_engine_set_cal(const key_cal_t *cal)
{
    (void)cal;
}

const key_cal_t *key_engine_get_cal(void)
{
    static const key_cal_t k_span = {
        MV_TO_ADC(HALL_V_LOW_MV),
        MV_TO_ADC(HALL_V_HIGH_MV)
    };
    return &k_span;
}

uint16_t key_engine_effective_rest(void)
{
    return MV_TO_ADC(HALL_V_LOW_MV);
}

void key_engine_set_gear(uint8_t gear)
{
    if (gear < 1U) gear = 1U;
    if (gear > GEAR_COUNT) gear = GEAR_COUNT;
    s_gear = gear;
}

uint8_t key_engine_get_gear(void)
{
    return s_gear;
}

uint16_t key_engine_gear_voltage_mv(uint8_t gear)
{
    if (gear < 1U || gear > GEAR_COUNT) return 0;
    return k_gear_mv[gear - 1U];
}

uint16_t key_engine_gear_travel(uint8_t gear)
{
    uint32_t span_mv = HALL_V_HIGH_MV - HALL_V_LOW_MV;
    uint16_t mv = key_engine_gear_voltage_mv(gear);
    uint32_t dm = (mv > HALL_V_LOW_MV) ? ((uint32_t)mv - HALL_V_LOW_MV) : 0U;
    return (uint16_t)((dm * SWITCH_TOTAL_TRAVEL + span_mv / 2U) / span_mv);
}

uint16_t key_engine_gear_threshold_adc(uint8_t gear)
{
    return MV_TO_ADC(key_engine_gear_voltage_mv(gear));
}

static uint16_t adc_to_depth(uint16_t adc)
{
    uint16_t rest = MV_TO_ADC(HALL_V_LOW_MV);
    uint16_t bottom = MV_TO_ADC(HALL_V_HIGH_MV);
    uint32_t span = (uint32_t)(bottom - rest);
    if (adc <= rest || span == 0U) return 0;
    uint32_t depth = ((uint32_t)(adc - rest) * SWITCH_TOTAL_TRAVEL + span / 2U) / span;
    if (depth > SWITCH_TOTAL_TRAVEL) depth = SWITCH_TOTAL_TRAVEL;
    return (uint16_t)depth;
}

bool key_engine_update(uint16_t adc, uint32_t now_ms)
{
    (void)now_ms;
    uint16_t thr = key_engine_gear_threshold_adc(s_gear);
    bool pressed = (thr != 0U) && (adc >= thr);
    if (pressed != s_pressed) {
        s_pressed = pressed;
        key_out_write(pressed);
    }
    s_depth = adc_to_depth(adc);
    return s_pressed;
}

void key_engine_force_release(void)
{
    s_pressed = false;
    key_out_write(false);
}

uint16_t key_engine_depth(void)
{
    return s_depth;
}

bool key_engine_pressed(void)
{
    return s_pressed;
}
