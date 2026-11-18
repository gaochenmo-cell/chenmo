/*
 * key_engine.h - 霍尔 ADC 采样与当前档的绝对阈值比较，驱动 KEY_OUT
 *
 * 触发点是 GEAR_VOLTAGE_MV 换出的 ADC 计数。Flash 里的校准、行程换算、
 * 快速触发都不参与这次比较。
 */
#ifndef KEY_ENGINE_H
#define KEY_ENGINE_H

#include <stdint.h>
#include <stdbool.h>

typedef struct {
    uint16_t rest_adc;
    uint16_t bottom_adc;
} key_cal_t;

void key_engine_init(void);

/* 保留接口。校准值不改变触发阈值。 */
void key_engine_set_cal(const key_cal_t *cal);
const key_cal_t *key_engine_get_cal(void);
uint16_t key_engine_effective_rest(void);

void    key_engine_set_gear(uint8_t gear);
uint8_t key_engine_get_gear(void);

/* 档位的绝对触发电压（mV）和对应的 12 位 ADC 计数（3.3 V） */
uint16_t key_engine_gear_voltage_mv(uint8_t gear);
uint16_t key_engine_gear_travel(uint8_t gear);
uint16_t key_engine_gear_threshold_adc(uint8_t gear);

/* 用这一次的 ADC 采样和当前档阈值决定按下/松开 */
bool key_engine_update(uint16_t adc, uint32_t now_ms);

void key_engine_force_release(void);

/* 仅供串口显示，不参与判定 */
uint16_t key_engine_depth(void);
bool     key_engine_pressed(void);

#endif /* KEY_ENGINE_H */
