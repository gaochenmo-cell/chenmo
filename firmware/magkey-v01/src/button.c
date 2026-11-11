#include "button.h"
#include "board.h"
#include "config.h"
#include "py32f002b_ll_bus.h"

static bool     s_stable_down;
static bool     s_last_raw;
static uint32_t s_raw_change_ms;
static uint32_t s_down_since_ms;
static bool     s_long_fired;

void button_init(void)
{
    LL_IOP_GRP1_EnableClock(LL_IOP_GRP1_PERIPH_GPIOB);
    LL_GPIO_SetPinMode(SW1_GPIO_PORT, SW1_GPIO_PIN, LL_GPIO_MODE_INPUT);
    /* 板上已有 R8 10K 上拉，再开内部上拉以防 R8 未贴 */
    LL_GPIO_SetPinPull(SW1_GPIO_PORT, SW1_GPIO_PIN, LL_GPIO_PULL_UP);
    s_last_raw = button_raw_down();
    s_stable_down = s_last_raw;
}

bool button_raw_down(void)
{
    return LL_GPIO_IsInputPinSet(SW1_GPIO_PORT, SW1_GPIO_PIN) == 0U;
}

bool button_is_down(void)
{
    return s_stable_down;
}

btn_event_t button_poll(uint32_t now_ms)
{
    btn_event_t evt = BTN_EVT_NONE;
    bool raw = button_raw_down();

    if (raw != s_last_raw) {
        s_last_raw = raw;
        s_raw_change_ms = now_ms;
    }
    if (raw != s_stable_down && (now_ms - s_raw_change_ms) >= BTN_DEBOUNCE_MS) {
        s_stable_down = raw;
        if (raw) {
            s_down_since_ms = now_ms;
            s_long_fired = false;
        } else if (!s_long_fired) {
            evt = BTN_EVT_SHORT;
        }
    }
    if (s_stable_down && !s_long_fired && (now_ms - s_down_since_ms) >= BTN_LONG_PRESS_MS) {
        s_long_fired = true;
        evt = BTN_EVT_LONG;
    }
    return evt;
}
