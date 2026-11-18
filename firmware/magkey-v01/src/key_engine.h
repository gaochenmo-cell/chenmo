/*
 * key_engine.h - 霍尔 ADC 采样与当前档阈值比较，驱动 KEY_OUT
 *
 * 未校准时阈值是 GEAR_VOLTAGE_MV。校准后改成实测最低/最高电压之间的等分，
 * 两端仍留内缩。比较始终是「本次 ADC >= 当前档阈值」，没有行程重映射，也没有快速触发。
 */
#ifndef KEY_ENGINE_H
#define KEY_ENGINE_H

#include <stdint.h>
#include <stdbool.h>

typedef struct {
    uint16_t rest_adc;      /* 校准见到的最低 ADC，未校准时为名义 0.15 V */
    uint16_t bottom_adc;    /* 校准见到的最高 ADC，未校准时为名义 2.80 V */
} key_cal_t;

void key_engine_init(void);

/* bottom > rest 时启用校准阈值；否则回到出厂电压表。 */
void key_engine_set_cal(const key_cal_t *cal);
const key_cal_t *key_engine_get_cal(void);
bool key_engine_is_calibrated(void);
uint16_t key_engine_effective_rest(void);

/* 三击采样。cal_feed 在第三次按压松开时返回 true。 */
void key_engine_cal_begin(void);
bool key_engine_cal_feed(uint16_t adc);
uint16_t key_engine_cal_min(void);
uint16_t key_engine_cal_max(void);
uint8_t key_engine_cal_presses(void);

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
