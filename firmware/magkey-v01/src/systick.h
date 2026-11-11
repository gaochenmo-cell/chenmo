#ifndef SYSTICK_H
#define SYSTICK_H

#include <stdint.h>

void     systick_init(void);
uint32_t millis(void);
void     delay_ms(uint32_t ms);

/* a - b，考虑 32 位回绕 */
static inline uint32_t elapsed_ms(uint32_t since)
{
    return millis() - since;
}

#endif /* SYSTICK_H */
