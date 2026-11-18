/*
 * config.h - 可在编译期调整的参数（档位表、触发算法、输出极性、串口等）
 *
 * 所有行程相关的量都以 0.01 mm 为单位（例如 340 = 3.40 mm）。
 * 所有 ADC 相关的量都是 12 位右对齐原始值（0..4095，参考电压 = VCC = 3.3 V）。
 */
#ifndef CONFIG_H
#define CONFIG_H

#include <stdint.h>

#define FW_VERSION_STR          "MAGKEY-V01 FW 1.0.0"

/* ------------------------------------------------------------------ */
/* 轴体参数（来自《智风V1磁轴 样品承认书》DS-01GM-40，2026-08-22）          */
/* ------------------------------------------------------------------ */
/* 总行程 3.5 +0/-0.4 mm，荷重测试实测平均 3.37 mm；按 3.40 mm 建模。
 * 0.15 V 对应 0 mm，2.80 V 对应 3.40 mm（见下面的可用区间）。 */
#define SWITCH_TOTAL_TRAVEL     340U
/* 霍尔可用区间：浅端 0.15 V，深端 2.80 V（参考电压 3.3 V）。
 * HALL_PRESSED_HIGH = 1：按下电压升高（默认）；0：按下电压降低。
 * 该宏只决定未校准时的默认静止/触底。做过校准后，极性由 rest/bottom 的大小关系决定，
 * 四档仍按同一比例落在校准后的区间上。 */
#define HALL_PRESSED_HIGH       1
#define HALL_V_LOW_MV           150U
#define HALL_V_HIGH_MV          2800U
#define ADC_VREF_MV             3300U
#define ADC_FULL_SCALE          4095U
#define MV_TO_ADC(mv)           ((uint16_t)(((uint32_t)(mv) * ADC_FULL_SCALE) / ADC_VREF_MV))

/* 未校准时的默认静止/触底 ADC（0.15 V -> 186，2.80 V -> 3474） */
#if HALL_PRESSED_HIGH
#define CAL_DEFAULT_REST_ADC    MV_TO_ADC(HALL_V_LOW_MV)
#define CAL_DEFAULT_BOTTOM_ADC  MV_TO_ADC(HALL_V_HIGH_MV)
#else
#define CAL_DEFAULT_REST_ADC    MV_TO_ADC(HALL_V_HIGH_MV)
#define CAL_DEFAULT_BOTTOM_ADC  MV_TO_ADC(HALL_V_LOW_MV)
#endif
/* 校准结果必须满足 bottom - rest >= 此值（约 1.0 V），否则视为失败 */
#define CAL_MIN_SPAN_ADC        MV_TO_ADC(1000U)

/* ------------------------------------------------------------------ */
/* 四档灵敏度。档位 1 最灵敏，档位 4 最迟钝。                              */
/* 颜色：红 绿 蓝 白。短按 S1 按此顺序循环后回到红。出厂默认第 2 档（绿）。 */
/* 0.15 V 和 2.80 V 只是霍尔区间两端，不能当触发点（静止就落在低端上）。   */
/* 两端各留 HALL_MARGIN_MV。12 位、3.3 V 时 1 LSB ≈ 0.806 mV；            */
/* 200 mV = 200*4095/3300 = 248 个计数。前端是 R9 1K + C17/C18 1nF，      */
/* 没有分压，霍尔电压直接进 PB1。四档在内缩区间上等分（含内缩后的两端）。   */
/* ------------------------------------------------------------------ */
#define HALL_MARGIN_MV          200U
#define GEAR_V_LOW_MV           (HALL_V_LOW_MV + HALL_MARGIN_MV)   /* 350 mV */
#define GEAR_V_HIGH_MV          (HALL_V_HIGH_MV - HALL_MARGIN_MV)  /* 2600 mV */
#define GEAR_STEP_MV            ((GEAR_V_HIGH_MV - GEAR_V_LOW_MV) / 3U) /* 750 mV */

#define GEAR_COUNT              4U
#define GEAR_DEFAULT            2U      /* 出厂默认：第 2 档（绿，1.100 V） */

/* 每档绝对触发电压（mV）：350 / 1100 / 1850 / 2600。比较器直接用这些值。 */
#define GEAR_VOLTAGE_MV         { \
    GEAR_V_LOW_MV, \
    (GEAR_V_LOW_MV + GEAR_STEP_MV), \
    (GEAR_V_LOW_MV + 2U * GEAR_STEP_MV), \
    GEAR_V_HIGH_MV \
}

/* ADC 过采样次数（每次按键扫描对 ADC 连续采样 N 次取平均） */
#define ADC_OVERSAMPLE          8U
/* 一阶 IIR 低通：y += (x - y) / 2^IIR_SHIFT；0 表示不滤波 */
#define ADC_IIR_SHIFT           1U

/* ------------------------------------------------------------------ */
/* 按键输出（J3.KEY1）                                                  */
/* ------------------------------------------------------------------ */
/* PB2 → R7 510R → Q1 2N7002K 栅极（R6 100K 下拉），漏极 = J3.KEY1，R5 10K 上拉到 3.3 V。
 * 1：按下时 PB2 高，Q1 导通，J3.KEY1 被拉低；松开时 PB2 低，Q1 关断，J3.KEY1 被 R5 拉高。
 * 0：相反（按下 = J3 高）。出厂镜像使用 1：按下 = J3 低，松开 = J3 高。 */
#define KEY_OUT_ACTIVE_HIGH     1

/* ------------------------------------------------------------------ */
/* S1 按钮交互                                                          */
/* ------------------------------------------------------------------ */
#define BTN_DEBOUNCE_MS         20U
#define BTN_LONG_PRESS_MS       5000U   /* 按住 S1 5 s 进入校准 */
#define BTN_BOOT_RESET_MS       3000U   /* 上电时按住 3 s 恢复出厂设置 */
/* 档位切换后延迟写入 Flash 的时间（合并连续多次短按，减少擦写） */
#define GEAR_SAVE_DELAY_MS      1500U

/* 校准：磁轴按下 3 次。一笔按压 = 霍尔比当前低点升高 CAL_PRESS_DELTA_MV，
 * 再落回低点附近算松开。第三次松开即确认。全程记录见到的最低/最高 ADC。 */
#define CAL_PRESS_COUNT         3U
#define CAL_PRESS_DELTA_MV      100U
/* 完成后黄灯闪三下的整周期（亮一半、灭一半） */
#define CAL_DONE_BLINK_MS       400U

/* ------------------------------------------------------------------ */
/* 档位指示灯 LED6                                                      */
/* ------------------------------------------------------------------ */
#define LED_PWM_STEPS           64U     /* 软件 PWM 分辨率 */
#define LED_PWM_TICK_HZ         16000U  /* TIM14 中断频率 -> 刷新率 = 16000/64 = 250 Hz */
/* 总亮度 0..255（LED6 由 1K/510R/330R 限流，电流已经很小，一般保持 255） */
#define LED_BRIGHTNESS          255U

/* ------------------------------------------------------------------ */
/* 调试 / 配置串口（DEBUG2：PB4 TX，PB5 RX）                             */
/* ------------------------------------------------------------------ */
#define CFG_UART_CONSOLE        1
#define UART_BAUDRATE           115200U

/* ------------------------------------------------------------------ */
/* 参数存储（片内 Flash，页大小 128 B）                                  */
/* ------------------------------------------------------------------ */
/* 链接脚本把 Flash 末尾 1 KB（8 页）留给参数区：0x08005C00 .. 0x08005FFF */
#define STORAGE_BASE_ADDR       0x08005C00UL
#define STORAGE_PAGE_SIZE       128UL
#define STORAGE_PAGE_COUNT      8UL

#endif /* CONFIG_H */
