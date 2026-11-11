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
/* 总行程 3.5 +0/-0.4 mm，荷重测试实测平均 3.37 mm；按 3.40 mm 建模。 */
#define SWITCH_TOTAL_TRAVEL     340U
/* 霍尔输出范围 0.4 ±0.2 V ~ 3.0 ±0.2 V（额定电压 3.3 V）。
 * 承认书中两份报告对极性描述相反（荷重报告：按压 3.0 V / 未按 0.4 V；
 * 寿命报告：未按 2.8~3.2 V / 按压 0.2~0.6 V）。
 * HALL_PRESSED_HIGH = 1：按下电压升高（默认，与原理图 "ADC 输出 0.2-3.0V" 及荷重报告一致）；
 * HALL_PRESSED_HIGH = 0：按下电压降低。
 * 该宏只决定"未校准时的默认值"；一旦做过校准，极性由校准得到的 rest/bottom 大小关系自动决定。 */
#define HALL_PRESSED_HIGH       1
#define HALL_V_LOW_MV           400U
#define HALL_V_HIGH_MV          3000U
#define ADC_VREF_MV             3300U
#define ADC_FULL_SCALE          4095U
#define MV_TO_ADC(mv)           ((uint16_t)(((uint32_t)(mv) * ADC_FULL_SCALE) / ADC_VREF_MV))

/* 未校准时使用的默认静止/触底 ADC 值（0.4 V -> 496，3.0 V -> 3723） */
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
/* 七档灵敏度（触发行程）。档位 1 最灵敏，档位 7 最迟钝。                  */
/* 颜色顺序：赤 橙 黄 绿 蓝 靛 紫                                        */
/* ------------------------------------------------------------------ */
#define GEAR_COUNT              7U
#define GEAR_DEFAULT            4U      /* 出厂默认：第 4 档（绿，1.40 mm） */

/* 每档触发点（0.01 mm），单调递增 */
#define GEAR_ACTUATION_TRAVEL   { 30U, 60U, 100U, 140U, 180U, 220U, 260U }

/* ------------------------------------------------------------------ */
/* 触发 / 释放算法                                                      */
/* ------------------------------------------------------------------ */
/* 1 = 快速触发（Rapid Trigger）：按下后只要回弹 RT_RELEASE_TRAVEL 即释放，
 *     释放后在触发点以下再压入 RT_PRESS_TRAVEL 即再次触发；
 * 0 = 传统迟滞：越过触发点按下，回到触发点 - HYSTERESIS_TRAVEL 以上才释放。 */
#define CFG_RAPID_TRIGGER       1
#define RT_RELEASE_TRAVEL       20U     /* 0.20 mm */
#define RT_PRESS_TRAVEL         20U     /* 0.20 mm */
/* 传统模式的迟滞，也是快速触发模式下"回到静止区"的判定余量 */
#define HYSTERESIS_TRAVEL       10U     /* 0.10 mm */
/* 低于此深度认为完全松开（用于复位快速触发状态） */
#define REST_ZONE_TRAVEL        15U     /* 0.15 mm */

/* 静止基线自动跟踪：按键连续松开 AUTO_REST_IDLE_MS 后，把 RAM 中的 rest 值
 * 缓慢拉向当前 ADC 值，补偿温漂和老化（承认书：1 亿次后未按电压漂移可达 -20%）。
 * 只改 RAM，不写 Flash；校准值仍以 Flash 中保存的为准。 */
#define CFG_AUTO_REST_TRACK     1
#define AUTO_REST_IDLE_MS       2000U
/* 每 AUTO_REST_STEP_MS 向当前值靠近 1 LSB */
#define AUTO_REST_STEP_MS       20U
/* 自动跟踪允许偏离 Flash 校准值的最大范围（ADC LSB，约 0.25 V） */
#define AUTO_REST_MAX_DRIFT     310U

/* ADC 过采样次数（每次按键扫描对 ADC 连续采样 N 次取平均） */
#define ADC_OVERSAMPLE          8U
/* 一阶 IIR 低通：y += (x - y) / 2^IIR_SHIFT；0 表示不滤波 */
#define ADC_IIR_SHIFT           1U

/* ------------------------------------------------------------------ */
/* 按键输出（J3.KEY1）                                                  */
/* ------------------------------------------------------------------ */
/* 1：PB2 输出高电平 = 按下（原理图：PB2 -> Q1 NMOS 栅极，Q1 漏极 = KEY1 拉低）。
 * 0：反相（PB2 低 = 按下），仅在不经 Q1 直接输出到主板时使用。 */
#define KEY_OUT_ACTIVE_HIGH     1

/* ------------------------------------------------------------------ */
/* S1 按钮交互                                                          */
/* ------------------------------------------------------------------ */
#define BTN_DEBOUNCE_MS         20U
#define BTN_LONG_PRESS_MS       3000U   /* 长按 3 s 进入校准 */
#define BTN_BOOT_RESET_MS       3000U   /* 上电时按住 3 s 恢复出厂设置 */
/* 档位切换后延迟写入 Flash 的时间（合并连续多次短按，减少擦写） */
#define GEAR_SAVE_DELAY_MS      1500U

/* 校准流程时间 */
#define CAL_REST_SAMPLE_MS      1500U   /* 阶段 1：采样静止值（白灯快闪，不要碰键） */
#define CAL_BOTTOM_TIMEOUT_MS   8000U   /* 阶段 2：等待用户把键按到底的最长时间（青灯常亮） */
#define CAL_BOTTOM_HOLD_MS      1500U   /* 阶段 2：检测到按下后继续取极值的时间 */

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
