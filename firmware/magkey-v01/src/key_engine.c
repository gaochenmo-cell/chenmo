#include "key_engine.h"
#include "config.h"
#ifdef HOST_TEST
/* 宿主机回归（make test-host）用桩替换 GPIO，见 tests/host/host_stubs.h */
#include "host_stubs.h"
#else
#include "board.h"
#include "py32f002b_ll_bus.h"
#endif

static const uint16_t k_gear_travel[GEAR_COUNT] = GEAR_ACTUATION_TRAVEL;

static key_cal_t s_cal = { CAL_DEFAULT_REST_ADC, CAL_DEFAULT_BOTTOM_ADC };
static uint16_t  s_rest_eff;          /* 自动跟踪后的 rest */
static bool      s_pressed_high;      /* true: 按下时 ADC 增大 */
static uint16_t  s_span;              /* |bottom - rest| */

static uint8_t   s_gear = GEAR_DEFAULT;
static uint16_t  s_act_travel;        /* 当前档触发行程 */

static uint16_t  s_depth;
static bool      s_pressed;
static uint16_t  s_peak;              /* 按下后到达的最深点 */
static uint16_t  s_rt_min;            /* 释放后的最浅点（快速触发再触发基准） */
static bool      s_rt_armed;          /* 处于触发点以下但尚未回到静止区 */

static uint32_t  s_idle_since_ms;
static uint32_t  s_last_track_ms;

static inline void key_out_write(bool pressed)
{
#if KEY_OUT_ACTIVE_HIGH
    if (pressed) LL_GPIO_SetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
    else         LL_GPIO_ResetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
#else
    if (pressed) LL_GPIO_ResetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
    else         LL_GPIO_SetOutputPin(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN);
#endif
}

static void recompute(void)
{
    s_pressed_high = (s_cal.bottom_adc >= s_cal.rest_adc);
    s_span = s_pressed_high ? (uint16_t)(s_cal.bottom_adc - s_cal.rest_adc)
                            : (uint16_t)(s_cal.rest_adc - s_cal.bottom_adc);
    if (s_span < 16U) {
        s_span = 16U;   /* 防止除零；正常校准值远大于此 */
    }
    s_act_travel = k_gear_travel[s_gear - 1U];
}

void key_engine_init(void)
{
    LL_IOP_GRP1_EnableClock(LL_IOP_GRP1_PERIPH_GPIOB);
    key_out_write(false);
    LL_GPIO_SetPinMode(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinPull(KEY_OUT_GPIO_PORT, KEY_OUT_GPIO_PIN, LL_GPIO_PULL_NO);

    s_rest_eff = s_cal.rest_adc;
    recompute();
}

void key_engine_set_cal(const key_cal_t *cal)
{
    s_cal = *cal;
    s_rest_eff = s_cal.rest_adc;
    recompute();
}

const key_cal_t *key_engine_get_cal(void)
{
    return &s_cal;
}

uint16_t key_engine_effective_rest(void)
{
    return s_rest_eff;
}

void key_engine_set_gear(uint8_t gear)
{
    if (gear < 1U) gear = 1U;
    if (gear > GEAR_COUNT) gear = GEAR_COUNT;
    s_gear = gear;
    recompute();
}

uint8_t key_engine_get_gear(void)
{
    return s_gear;
}

uint16_t key_engine_gear_travel(uint8_t gear)
{
    if (gear < 1U || gear > GEAR_COUNT) return 0;
    return k_gear_travel[gear - 1U];
}

uint16_t key_engine_gear_threshold_adc(uint8_t gear)
{
    uint32_t travel = key_engine_gear_travel(gear);
    uint32_t delta = (travel * s_span + (SWITCH_TOTAL_TRAVEL / 2U)) / SWITCH_TOTAL_TRAVEL;
    if (s_pressed_high) {
        return (uint16_t)(s_rest_eff + delta);
    }
    return (uint16_t)((delta > s_rest_eff) ? 0U : (s_rest_eff - delta));
}

/* ADC -> 行程深度（0.01 mm），超出范围时饱和 */
static uint16_t adc_to_depth(uint16_t adc)
{
    uint32_t delta;
    if (s_pressed_high) {
        delta = (adc > s_rest_eff) ? (uint32_t)(adc - s_rest_eff) : 0U;
    } else {
        delta = (adc < s_rest_eff) ? (uint32_t)(s_rest_eff - adc) : 0U;
    }
    uint32_t depth = (delta * SWITCH_TOTAL_TRAVEL + (s_span / 2U)) / s_span;
    if (depth > SWITCH_TOTAL_TRAVEL) depth = SWITCH_TOTAL_TRAVEL;
    return (uint16_t)depth;
}

#if CFG_AUTO_REST_TRACK
static void auto_rest_track(uint16_t adc, uint32_t now_ms)
{
    if (s_pressed || s_depth >= REST_ZONE_TRAVEL) {
        s_idle_since_ms = now_ms;
        return;
    }
    if ((now_ms - s_idle_since_ms) < AUTO_REST_IDLE_MS) return;
    if ((now_ms - s_last_track_ms) < AUTO_REST_STEP_MS) return;
    s_last_track_ms = now_ms;

    /* 只允许在 Flash 校准值附近有限范围内漂移 */
    int32_t lo = (int32_t)s_cal.rest_adc - (int32_t)AUTO_REST_MAX_DRIFT;
    int32_t hi = (int32_t)s_cal.rest_adc + (int32_t)AUTO_REST_MAX_DRIFT;
    int32_t target = adc;
    if (target < lo) target = lo;
    if (target > hi) target = hi;
    if (target < 0) target = 0;

    if (target > (int32_t)s_rest_eff) s_rest_eff++;
    else if (target < (int32_t)s_rest_eff) s_rest_eff--;
}
#endif

bool key_engine_update(uint16_t adc, uint32_t now_ms)
{
    uint16_t depth = adc_to_depth(adc);
    uint16_t act = s_act_travel;
    uint16_t rest_limit = (act > HYSTERESIS_TRAVEL) ? (uint16_t)(act - HYSTERESIS_TRAVEL) : 0U;
    s_depth = depth;

    if (!s_pressed) {
        bool press = false;
#if CFG_RAPID_TRIGGER
        if (s_rt_armed) {
            /* 已在触发点以下方向释放过，但仍停留在触发区内：只看相对再压入量 */
            if (depth < s_rt_min) s_rt_min = depth;
            if (depth >= (uint16_t)(s_rt_min + RT_PRESS_TRAVEL)) press = true;
        } else
#endif
        if (depth >= act) {
            press = true;
        }
#if CFG_RAPID_TRIGGER
        if (depth < REST_ZONE_TRAVEL || depth < rest_limit) {
            s_rt_armed = false;   /* 回到触发点以下，恢复普通阈值触发 */
        }
#endif
        if (press) {
            s_pressed = true;
            s_peak = depth;
            key_out_write(true);
        }
    } else {
        bool release = false;
        if (depth > s_peak) s_peak = depth;
#if CFG_RAPID_TRIGGER
        if (depth + RT_RELEASE_TRAVEL <= s_peak) release = true;
#endif
        if (depth < rest_limit) release = true;
        if (release) {
            s_pressed = false;
            key_out_write(false);
#if CFG_RAPID_TRIGGER
            s_rt_min = depth;
            s_rt_armed = (depth >= REST_ZONE_TRAVEL) && (depth >= rest_limit);
#endif
        }
    }

#if CFG_AUTO_REST_TRACK
    auto_rest_track(adc, now_ms);
#else
    (void)now_ms;
#endif
    return s_pressed;
}

void key_engine_force_release(void)
{
    s_pressed = false;
    s_rt_armed = false;
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
