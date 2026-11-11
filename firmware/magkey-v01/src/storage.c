#include "storage.h"
#include "config.h"
#include "py32f0xx.h"

/*
 * PY32F002B Flash：页 128 B（擦除单位），编程必须一次写满一整页（32 个字）。
 * 参数区：STORAGE_PAGE_COUNT 页轮换。每页存一条记录：
 *   word0  magic
 *   word1  seq（单调递增）
 *   word2  gear | calibrated<<8 | rest_adc<<16
 *   word3  bottom_adc | crc16<<16
 *   其余字 0xFFFFFFFF
 */
#define REC_MAGIC       0x4D4B4731UL    /* "MKG1" */
#define WORDS_PER_PAGE  (STORAGE_PAGE_SIZE / 4UL)

/* 24 MHz HSI 对应的 Flash 时序参数在系统存储区的位置（与 Puya HAL 一致） */
static const uint32_t k_flash_timing_param[8] = {
    0x1FFF0158UL, 0x1FFF016CUL, 0x1FFF011CUL, 0x1FFF011CUL,
    0x1FFF011CUL, 0x1FFF0130UL, 0x1FFF011CUL, 0x1FFF011CUL
};

static uint16_t crc16_ccitt(const uint8_t *p, uint32_t n)
{
    uint16_t crc = 0xFFFFU;
    while (n--) {
        crc ^= (uint16_t)(*p++) << 8;
        for (int i = 0; i < 8; i++) {
            crc = (crc & 0x8000U) ? (uint16_t)((crc << 1) ^ 0x1021U) : (uint16_t)(crc << 1);
        }
    }
    return crc;
}

static void pack(const settings_t *s, uint32_t seq, uint32_t words[4])
{
    words[0] = REC_MAGIC;
    words[1] = seq;
    words[2] = (uint32_t)s->gear | ((uint32_t)s->calibrated << 8) | ((uint32_t)s->rest_adc << 16);
    words[3] = (uint32_t)s->bottom_adc;
    uint16_t crc = crc16_ccitt((const uint8_t *)words, 14U);   /* 前 14 字节（不含 crc 字段）*/
    words[3] |= (uint32_t)crc << 16;
}

static bool unpack(const uint32_t words[4], settings_t *s, uint32_t *seq)
{
    if (words[0] != REC_MAGIC) return false;
    uint32_t tmp[4] = { words[0], words[1], words[2], words[3] & 0xFFFFU };
    uint16_t crc = crc16_ccitt((const uint8_t *)tmp, 14U);
    if (crc != (uint16_t)(words[3] >> 16)) return false;
    s->gear       = (uint8_t)(words[2] & 0xFFU);
    s->calibrated = (uint8_t)((words[2] >> 8) & 0xFFU);
    s->rest_adc   = (uint16_t)(words[2] >> 16);
    s->bottom_adc = (uint16_t)(words[3] & 0xFFFFU);
    *seq = words[1];
    if (s->gear < 1U || s->gear > GEAR_COUNT) return false;
    return true;
}

static uint32_t page_addr(uint32_t idx)
{
    return STORAGE_BASE_ADDR + idx * STORAGE_PAGE_SIZE;
}

/* 找到 seq 最大的有效记录；返回页号，-1 表示没有 */
static int find_latest(settings_t *s, uint32_t *seq_out)
{
    int best = -1;
    uint32_t best_seq = 0;
    for (uint32_t i = 0; i < STORAGE_PAGE_COUNT; i++) {
        const uint32_t *w = (const uint32_t *)page_addr(i);
        settings_t tmp;
        uint32_t seq;
        if (unpack(w, &tmp, &seq) && (best < 0 || (int32_t)(seq - best_seq) > 0)) {
            best = (int)i;
            best_seq = seq;
            *s = tmp;
        }
    }
    if (best >= 0) *seq_out = best_seq;
    return best;
}

void storage_defaults(settings_t *out)
{
    out->gear = GEAR_DEFAULT;
    out->calibrated = 0;
    out->rest_adc = CAL_DEFAULT_REST_ADC;
    out->bottom_adc = CAL_DEFAULT_BOTTOM_ADC;
}

bool storage_load(settings_t *out)
{
    uint32_t seq;
    if (find_latest(out, &seq) < 0) {
        storage_defaults(out);
        return false;
    }
    return true;
}

/* ---- 底层 Flash 操作 ---- */

static void flash_timing_config(void)
{
    uint32_t idx = (RCC->ICSCR & RCC_ICSCR_HSI_FS) >> RCC_ICSCR_HSI_FS_Pos;
    uint32_t base = k_flash_timing_param[idx];
    uint32_t p0 = *(volatile uint32_t *)(base);
    uint32_t p1 = *(volatile uint32_t *)(base + 4U);
    uint32_t p2 = *(volatile uint32_t *)(base + 8U);
    uint32_t p3 = *(volatile uint32_t *)(base + 12U);
    uint32_t p4 = *(volatile uint32_t *)(base + 16U);
    FLASH->TS0     = p0 & 0x1FFU;
    FLASH->TS1     = (p0 >> 18) & 0x3FFU;
    FLASH->TS3     = (p0 >> 9) & 0x1FFU;
    FLASH->TS2P    = p1 & 0x1FFU;
    FLASH->TPS3    = (p1 >> 16) & 0xFFFU;
    FLASH->PERTPE  = p2 & 0x3FFFFU;
    FLASH->SMERTPE = p3 & 0x3FFFFU;
    FLASH->PRGTPE  = p4 & 0xFFFFU;
    FLASH->PRETPE  = (p4 >> 16) & 0x3FFFU;
}

static bool flash_wait(void)
{
    uint32_t guard = 2000000UL;
    while ((FLASH->SR & FLASH_SR_BSY) != 0U) {
        if (--guard == 0U) return false;
    }
    bool ok = (FLASH->SR & FLASH_SR_WRPERR) == 0U;
    FLASH->SR = FLASH_SR_EOP | FLASH_SR_WRPERR;   /* 清标志 */
    return ok;
}

static bool flash_unlock(void)
{
    if (FLASH->CR & FLASH_CR_LOCK) {
        FLASH->KEYR = FLASH_KEY1;
        FLASH->KEYR = FLASH_KEY2;
    }
    return (FLASH->CR & FLASH_CR_LOCK) == 0U;
}

static void flash_lock(void)
{
    FLASH->CR |= FLASH_CR_LOCK;
}

static bool flash_page_erase(uint32_t addr)
{
    if (!flash_wait()) return false;
    FLASH->CR |= FLASH_CR_PER;
    *(volatile uint32_t *)addr = 0xFFU;
    bool ok = flash_wait();
    FLASH->CR &= ~FLASH_CR_PER;
    return ok;
}

static bool flash_page_program(uint32_t addr, const uint32_t *words)
{
    if (!flash_wait()) return false;
    FLASH->CR |= FLASH_CR_PG;
    uint32_t primask = __get_PRIMASK();
    __disable_irq();
    for (uint32_t i = 0; i < WORDS_PER_PAGE; i++) {
        *(volatile uint32_t *)(addr + i * 4U) = words[i];
        if (i == WORDS_PER_PAGE - 2U) {
            FLASH->CR |= FLASH_CR_PGSTRT;
        }
    }
    __set_PRIMASK(primask);
    bool ok = flash_wait();
    FLASH->CR &= ~FLASH_CR_PG;
    return ok;
}

bool storage_save(const settings_t *in)
{
    settings_t cur;
    uint32_t seq = 0;
    int latest = find_latest(&cur, &seq);
    uint32_t next = (latest < 0) ? 0U : ((uint32_t)latest + 1U) % STORAGE_PAGE_COUNT;

    uint32_t page[WORDS_PER_PAGE];
    for (uint32_t i = 0; i < WORDS_PER_PAGE; i++) page[i] = 0xFFFFFFFFUL;
    pack(in, seq + 1U, page);

    flash_timing_config();
    if (!flash_unlock()) return false;
    bool ok = flash_page_erase(page_addr(next));
    if (ok) ok = flash_page_program(page_addr(next), page);
    flash_lock();

    if (ok) {
        const uint32_t *w = (const uint32_t *)page_addr(next);
        for (uint32_t i = 0; i < 4U; i++) {
            if (w[i] != page[i]) return false;
        }
    }
    return ok;
}

bool storage_erase_all(void)
{
    flash_timing_config();
    if (!flash_unlock()) return false;
    bool ok = true;
    for (uint32_t i = 0; i < STORAGE_PAGE_COUNT && ok; i++) {
        ok = flash_page_erase(page_addr(i));
    }
    flash_lock();
    return ok;
}
