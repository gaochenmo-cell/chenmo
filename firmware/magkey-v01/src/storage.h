/*
 * storage.h - 档位与校准值的掉电保存（片内 Flash 末尾 1 KB，8 页轮换写入）
 */
#ifndef STORAGE_H
#define STORAGE_H

#include <stdint.h>
#include <stdbool.h>

typedef struct {
    uint8_t  gear;          /* 1..GEAR_COUNT */
    uint8_t  calibrated;    /* 1 = rest/bottom 来自实际校准 */
    uint16_t rest_adc;
    uint16_t bottom_adc;
} settings_t;

/* 读取最新记录；没有有效记录时填入默认值并返回 false */
bool storage_load(settings_t *out);
/* 写入一条新记录（擦除下一页并编程）；返回 false 表示 Flash 操作失败 */
bool storage_save(const settings_t *in);
/* 擦除全部参数页（恢复出厂） */
bool storage_erase_all(void);

void storage_defaults(settings_t *out);

#endif /* STORAGE_H */
