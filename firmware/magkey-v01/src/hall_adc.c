#include "hall_adc.h"
#include "board.h"
#include "config.h"
#include "systick.h"
#include "py32f002b_ll_bus.h"
#include "py32f002b_ll_adc.h"

static uint16_t s_filtered;
static uint8_t  s_primed;

void hall_adc_init(void)
{
    LL_IOP_GRP1_EnableClock(LL_IOP_GRP1_PERIPH_GPIOB);
    LL_APB1_GRP2_EnableClock(LL_APB1_GRP2_PERIPH_ADC1);

    LL_GPIO_SetPinMode(HALL_ADC_GPIO_PORT, HALL_ADC_GPIO_PIN, LL_GPIO_MODE_ANALOG);
    LL_GPIO_SetPinPull(HALL_ADC_GPIO_PORT, HALL_ADC_GPIO_PIN, LL_GPIO_PULL_NO);

    /* ADC 时钟 = PCLK/4 = 6 MHz；239.5 周期采样 + 12.5 周期转换 ≈ 42 us/次 */
    LL_ADC_SetClock(ADC1, LL_ADC_CLOCK_SYNC_PCLK_DIV4);
    LL_ADC_SetResolution(ADC1, LL_ADC_RESOLUTION_12B);
    LL_ADC_SetDataAlignment(ADC1, LL_ADC_DATA_ALIGN_RIGHT);
    LL_ADC_SetLowPowerMode(ADC1, LL_ADC_LP_MODE_NONE);
    LL_ADC_SetSamplingTimeCommonChannels(ADC1, LL_ADC_SAMPLINGTIME_239CYCLES_5);
    LL_ADC_REG_SetTriggerSource(ADC1, LL_ADC_REG_TRIG_SOFTWARE);
    LL_ADC_REG_SetContinuousMode(ADC1, LL_ADC_REG_CONV_SINGLE);
    LL_ADC_REG_SetOverrun(ADC1, LL_ADC_REG_OVR_DATA_OVERWRITTEN);
    LL_ADC_REG_SetSequencerDiscont(ADC1, LL_ADC_REG_SEQ_DISCONT_DISABLE);
    LL_ADC_REG_SetSequencerChannels(ADC1, HALL_ADC_CHANNEL);
    LL_ADC_SetCommonPathInternalCh(__LL_ADC_COMMON_INSTANCE(ADC1), LL_ADC_PATH_INTERNAL_NONE);

    if (LL_ADC_IsEnabled(ADC1) == 0U) {
        LL_ADC_StartCalibration(ADC1);
        while (LL_ADC_IsCalibrationOnGoing(ADC1) != 0U) {
        }
        delay_ms(1);
    }
    LL_ADC_Enable(ADC1);
    delay_ms(1);
}

static uint16_t adc_convert_once(void)
{
    LL_ADC_REG_StartConversion(ADC1);
    while (LL_ADC_IsActiveFlag_EOC(ADC1) == 0U) {
    }
    LL_ADC_ClearFlag_EOC(ADC1);
    return LL_ADC_REG_ReadConversionData12(ADC1);
}

uint16_t hall_adc_read(void)
{
    uint32_t acc = 0;
    for (uint32_t i = 0; i < ADC_OVERSAMPLE; i++) {
        acc += adc_convert_once();
    }
    uint16_t sample = (uint16_t)(acc / ADC_OVERSAMPLE);

    if (!s_primed) {
        s_filtered = sample;
        s_primed = 1;
    } else {
#if ADC_IIR_SHIFT > 0
        int32_t diff = (int32_t)sample - (int32_t)s_filtered;
        s_filtered = (uint16_t)((int32_t)s_filtered + (diff >> ADC_IIR_SHIFT));
#else
        s_filtered = sample;
#endif
    }
    return s_filtered;
}

uint16_t hall_adc_last(void)
{
    return s_filtered;
}
