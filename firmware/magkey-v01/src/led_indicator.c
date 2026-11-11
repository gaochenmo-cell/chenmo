#include "led_indicator.h"
#include "board.h"
#include "config.h"
#include "py32f002b_ll_bus.h"
#include "py32f002b_ll_tim.h"

/* 七档颜色：赤 橙 黄 绿 蓝 靛 紫（按 LED6 的 1K/510R/330R 限流做了简单配比） */
static const rgb_t k_gear_colors[GEAR_COUNT] = {
    { 255,   0,   0 },   /* 1 赤 */
    { 255,  70,   0 },   /* 2 橙 */
    { 255, 200,   0 },   /* 3 黄 */
    {   0, 255,   0 },   /* 4 绿 */
    {   0,   0, 255 },   /* 5 蓝 */
    {  40,   0, 160 },   /* 6 靛 */
    { 180,   0, 255 },   /* 7 紫 */
};

/* 占空比（0..LED_PWM_STEPS），由 TIM14 中断读取 */
static volatile uint8_t s_duty_r, s_duty_g, s_duty_b;
static volatile uint8_t s_pwm_phase;

static rgb_t    s_base_color;     /* 闪烁结束后恢复的颜色 */
static rgb_t    s_blink_color;
static uint16_t s_blink_half_ms;
static uint8_t  s_blink_remaining;   /* 剩余半周期数；0xFF = 无限 */
static uint8_t  s_blink_on;
static uint8_t  s_blinking;
static uint32_t s_blink_t0;

static uint8_t to_duty(uint8_t v)
{
    uint32_t scaled = ((uint32_t)v * LED_BRIGHTNESS) / 255U;
    /* 感知亮度近似：平方曲线，让低值颜色（橙、靛）更有区分度 */
    scaled = (scaled * scaled) / 255U;
    return (uint8_t)((scaled * LED_PWM_STEPS + 127U) / 255U);
}

static void apply_rgb(rgb_t c)
{
    s_duty_r = to_duty(c.r);
    s_duty_g = to_duty(c.g);
    s_duty_b = to_duty(c.b);
}

void led_init(void)
{
    LL_IOP_GRP1_EnableClock(LL_IOP_GRP1_PERIPH_GPIOA);
    LL_APB1_GRP2_EnableClock(LL_APB1_GRP2_PERIPH_TIM14);

    uint32_t pins = LED_R_PIN | LED_G_PIN | LED_B_PIN;
    LL_GPIO_SetOutputPin(LED_GPIO_PORT, pins);   /* 高 = 熄灭 */
    LL_GPIO_SetPinMode(LED_GPIO_PORT, LED_R_PIN, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinMode(LED_GPIO_PORT, LED_G_PIN, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinMode(LED_GPIO_PORT, LED_B_PIN, LL_GPIO_MODE_OUTPUT);
    LL_GPIO_SetPinOutputType(LED_GPIO_PORT, pins, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinSpeed(LED_GPIO_PORT, LED_R_PIN, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinSpeed(LED_GPIO_PORT, LED_G_PIN, LL_GPIO_SPEED_FREQ_LOW);
    LL_GPIO_SetPinSpeed(LED_GPIO_PORT, LED_B_PIN, LL_GPIO_SPEED_FREQ_LOW);

    /* TIM14：PCLK 24 MHz -> LED_PWM_TICK_HZ 更新中断 */
    LL_TIM_SetPrescaler(TIM14, 0U);
    LL_TIM_SetAutoReload(TIM14, (SystemCoreClock / LED_PWM_TICK_HZ) - 1U);
    LL_TIM_ClearFlag_UPDATE(TIM14);
    LL_TIM_EnableIT_UPDATE(TIM14);
    NVIC_SetPriority(TIM14_IRQn, 1);
    NVIC_EnableIRQ(TIM14_IRQn);
    LL_TIM_EnableCounter(TIM14);
}

void TIM14_IRQHandler(void)
{
    if (LL_TIM_IsActiveFlag_UPDATE(TIM14)) {
        LL_TIM_ClearFlag_UPDATE(TIM14);
        uint8_t ph = s_pwm_phase;
        uint32_t set = 0, clr = 0;
        /* 低电平点亮：phase < duty 时拉低 */
        if (ph < s_duty_r) clr |= LED_R_PIN; else set |= LED_R_PIN;
        if (ph < s_duty_g) clr |= LED_G_PIN; else set |= LED_G_PIN;
        if (ph < s_duty_b) clr |= LED_B_PIN; else set |= LED_B_PIN;
        LED_GPIO_PORT->BSRR = set | (clr << 16);
        ph++;
        if (ph >= LED_PWM_STEPS) ph = 0;
        s_pwm_phase = ph;
    }
}

void led_set_rgb(rgb_t c)
{
    s_base_color = c;
    if (!s_blinking) {
        apply_rgb(c);
    }
}

void led_off(void)
{
    rgb_t off = { 0, 0, 0 };
    led_set_rgb(off);
}

rgb_t led_gear_color(uint8_t gear)
{
    if (gear < 1U || gear > GEAR_COUNT) {
        rgb_t off = { 0, 0, 0 };
        return off;
    }
    return k_gear_colors[gear - 1U];
}

void led_show_gear(uint8_t gear)
{
    led_set_rgb(led_gear_color(gear));
}

void led_blink(rgb_t c, uint16_t period_ms, uint8_t count)
{
    s_blink_color = c;
    s_blink_half_ms = period_ms / 2U;
    s_blink_remaining = (count == 0U) ? 0xFFU : (uint8_t)(count * 2U);
    s_blink_on = 1;
    s_blinking = 1;
    s_blink_t0 = 0;        /* 下一次 led_task 立即点亮 */
    apply_rgb(c);
}

void led_stop_blink(void)
{
    s_blinking = 0;
    apply_rgb(s_base_color);
}

void led_task(uint32_t now_ms)
{
    if (!s_blinking) return;
    if (s_blink_t0 == 0U) {
        s_blink_t0 = now_ms ? now_ms : 1U;
        return;
    }
    if ((now_ms - s_blink_t0) < s_blink_half_ms) return;
    s_blink_t0 = now_ms ? now_ms : 1U;

    if (s_blink_remaining != 0xFFU) {
        if (s_blink_remaining > 0U) s_blink_remaining--;
        if (s_blink_remaining == 0U) {
            led_stop_blink();
            return;
        }
    }
    s_blink_on = !s_blink_on;
    if (s_blink_on) {
        apply_rgb(s_blink_color);
    } else {
        rgb_t off = { 0, 0, 0 };
        apply_rgb(off);
    }
}
