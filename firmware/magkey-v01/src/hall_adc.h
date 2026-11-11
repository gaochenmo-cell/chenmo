#ifndef HALL_ADC_H
#define HALL_ADC_H

#include <stdint.h>

/* 初始化 ADC（PB1 / ADC_IN0），包含自校准 */
void     hall_adc_init(void);
/* 阻塞采样一次（含 ADC_OVERSAMPLE 次过采样 + IIR），返回 0..4095 */
uint16_t hall_adc_read(void);
/* 最近一次 hall_adc_read 的结果 */
uint16_t hall_adc_last(void);

#endif /* HALL_ADC_H */
