#include "systick.h"
#include "py32f0xx.h"

static volatile uint32_t s_ms;

void systick_init(void)
{
    SystemCoreClockUpdate();
    SysTick_Config(SystemCoreClock / 1000U);
    NVIC_SetPriority(SysTick_IRQn, 3);
}

uint32_t millis(void)
{
    return s_ms;
}

void delay_ms(uint32_t ms)
{
    uint32_t start = s_ms;
    while ((s_ms - start) < ms) {
        __WFI();
    }
}

void SysTick_Handler(void)
{
    s_ms++;
}
