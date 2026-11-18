#include "key_engine.h"
#include "config.h"
#ifdef HOST_TEST
#include "host_stubs.h"
#else
#include "board.h"
#include "py32f002b_ll_bus.h"
#endif

static const uint16_t k_gear_mv[GEAR_COUNT] = GEAR_VOLTAGE_MV;

static uint8_t   s_gear = GEAR_DEFAULT;
static uint16_t  s_depth;
static bool      s_pressed;
static bool      s_calibrated;
static key_cal_t s_cal;
static uint16_t  s_thr[GEAR_COUNT];

static bool     s_capturing;
static bool     s_stroke;
static bool     s_seen;
static uint8_t  s_strokes;
static uint16_t s_cap_min;
static uint16_t s_cap_max;
static uint16_t s_baseline;

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
    s_calibrated = false;
    s_capturing = false;
    s_cal.rest_adc = MV_TO_ADC(HALL_V_LOW_MV);
    s_cal.bottom_adc = MV_TO_ADC(HALL_V_HIGH_MV);
    key_out_write(false);
    LL_GPIO_SetPinMode(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_PULL_NO);
}

/* 实测跨度短于 0.40 V 时，两端内缩改为跨度的 1/4，避免 0.20+0.20 把区间吃光。 */
static uint16_t inset_counts(uint16_t span)
{
    uint32_t span_mv = ((uint32_t)span * ADC_VREF_MV) / ADC_FULL_SCALE;
    uint32_t margin_mv = HALL_MARGIN_MV;
    if (span_mv < (2U * HALL_MARGIN_MV)) {
        margin_mv = span_mv / 4U;
    }
    uint32_t margin = (margin_mv * ADC_FULL_SCALE) / ADC_VREF_MV;
    if (margin == 0U && span >= 2U) {
        margin = 1U;
    }
    if ((margin * 2U) >= span) {
        margin = span / 4U;
        if (margin == 0U && span >= 2U) {
            margin = 1U;
        }
    }
    return (uint16_t)margin;
}

static void scale_thresholds(uint16_t vmin, uint16_t vmax)
{
    uint16_t span = (uint16_t)(vmax - vmin);
    uint16_t margin = inset_counts(span);
    uint16_t lo = (uint16_t)(vmin + margin);
    uint16_t hi = (uint16_t)(vmax - margin);
    if (hi < lo) {
        hi = lo;
    }
    uint16_t inner = (uint16_t)(hi - lo);
    for (uint8_t i = 0; i < GEAR_COUNT; i++) {
        s_thr[i] = (uint16_t)(lo + ((uint32_t)inner * i) / (GEAR_COUNT - 1U));
    }
}

void key_engine_set_cal(const key_cal_t *cal)
{
    if (cal == 0 || cal->bottom_adc <= cal->rest_adc) {
        s_calibrated = false;
        s_cal.rest_adc = MV_TO_ADC(HALL_V_LOW_MV);
        s_cal.bottom_adc = MV_TO_ADC(HALL_V_HIGH_MV);
        return;
    }
    s_cal = *cal;
    s_calibrated = true;
    scale_thresholds(cal->rest_adc, cal->bottom_adc);
}

const key_cal_t *key_engine_get_cal(void)
{
    return &s_cal;
}

bool key_engine_is_calibrated(void)
{
    return s_calibrated;
}

uint16_t key_engine_effective_rest(void)
{
    return s_calibrated ? s_cal.rest_adc : MV_TO_ADC(HALL_V_LOW_MV);
}

void key_engine_cal_begin(void)
{
    s_capturing = true;
    s_stroke = false;
    s_seen = false;
    s_strokes = 0;
    s_cap_min = 0;
    s_cap_max = 0;
    s_baseline = 0;
}

bool key_engine_cal_feed(uint16_t adc)
{
    if (!s_capturing) {
        return false;
    }
    if (!s_seen) {
        s_cap_min = adc;
        s_cap_max = adc;
        s_baseline = adc;
        s_seen = true;
        return false;
    }
    if (adc < s_cap_min) s_cap_min = adc;
    if (adc > s_cap_max) s_cap_max = adc;

    uint16_t delta = MV_TO_ADC(CAL_PRESS_DELTA_MV);
    if (!s_stroke) {
        if (adc < s_baseline) s_baseline = adc;
        uint16_t trip = (s_baseline > (uint16_t)(4095U - delta))
                            ? 4095U
                            : (uint16_t)(s_baseline + delta);
        if (trip > s_baseline && adc >= trip) {
            s_stroke = true;
            if (s_strokes < 255U) s_strokes++;
        }
    } else {
        uint16_t back = (uint16_t)(s_baseline + (delta / 4U));
        if (adc <= back) {
            s_stroke = false;
            if (s_strokes >= CAL_PRESS_COUNT) {
                s_capturing = false;
                return true;
            }
        }
    }
    return false;
}

uint16_t key_engine_cal_min(void)
{
    return s_cap_min;
}

uint16_t key_engine_cal_max(void)
{
    return s_cap_max;
}

uint8_t key_engine_cal_presses(void)
{
    return s_strokes;
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
    if (!s_calibrated) return k_gear_mv[gear - 1U];
    return (uint16_t)(((uint32_t)s_thr[gear - 1U] * ADC_VREF_MV) / ADC_FULL_SCALE);
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
    if (gear < 1U || gear > GEAR_COUNT) return 0;
    if (!s_calibrated) return MV_TO_ADC(k_gear_mv[gear - 1U]);
    return s_thr[gear - 1U];
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
