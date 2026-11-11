/* 宿主机桩：替代 board.h / LL GPIO，供 make test-host 编译 key_engine.c。 */
#ifndef HOST_STUBS_H
#define HOST_STUBS_H

#include <stdint.h>

extern int g_key_out;

#define KEY_OUT_GPIO_PORT 0
#define KEY_OUT_GPIO_PIN  0

#define LL_GPIO_MODE_OUTPUT      0
#define LL_GPIO_OUTPUT_PUSHPULL  0
#define LL_GPIO_SPEED_FREQ_LOW   0
#define LL_GPIO_PULL_NO          0
#define LL_IOP_GRP1_PERIPH_GPIOB 0

static inline void LL_GPIO_SetOutputPin(int port, int pin)
{
    (void)port;
    (void)pin;
    g_key_out = 1;
}

static inline void LL_GPIO_ResetOutputPin(int port, int pin)
{
    (void)port;
    (void)pin;
    g_key_out = 0;
}

static inline void LL_GPIO_SetPinMode(int a, int b, int c) { (void)a; (void)b; (void)c; }
static inline void LL_GPIO_SetPinOutputType(int a, int b, int c) { (void)a; (void)b; (void)c; }
static inline void LL_GPIO_SetPinSpeed(int a, int b, int c) { (void)a; (void)b; (void)c; }
static inline void LL_GPIO_SetPinPull(int a, int b, int c) { (void)a; (void)b; (void)c; }
static inline void LL_IOP_GRP1_EnableClock(int a) { (void)a; }

#endif
