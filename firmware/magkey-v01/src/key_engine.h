/*
 * key_engine.h - 霍尔 ADC -> 行程深度 -> 七档触发判定 -> KEY_OUT
 */
#ifndef KEY_ENGINE_H
#define KEY_ENGINE_H

#include <stdint.h>
#include <stdbool.h>

typedef struct {
    uint16_t rest_adc;      /* 完全松开时的 ADC 值 */
    uint16_t bottom_adc;    /* 按到底时的 ADC 值 */
} key_cal_t;

void key_engine_init(void);

/* 设置校准值（来自 Flash 或刚完成的校准），同时重算档位阈值 */
void key_engine_set_cal(const key_cal_t *cal);
const key_cal_t *key_engine_get_cal(void);
/* 当前（含自动基线跟踪后）的 rest 值 */
uint16_t key_engine_effective_rest(void);

/* 设置当前档位 1..GEAR_COUNT */
void    key_engine_set_gear(uint8_t gear);
uint8_t key_engine_get_gear(void);

/* 返回某档的触发行程（0.01 mm）与对应的 ADC 阈值 */
uint16_t key_engine_gear_travel(uint8_t gear);
uint16_t key_engine_gear_threshold_adc(uint8_t gear);

/* 主循环中每次 ADC 采样后调用；返回当前按下状态 */
bool key_engine_update(uint16_t adc, uint32_t now_ms);

/* 把输出强制为松开（校准期间使用） */
void key_engine_force_release(void);

/* 最近一次计算出的行程深度（0.01 mm，0..SWITCH_TOTAL_TRAVEL） */
uint16_t key_engine_depth(void);
bool     key_engine_pressed(void);

#endif /* KEY_ENGINE_H */
