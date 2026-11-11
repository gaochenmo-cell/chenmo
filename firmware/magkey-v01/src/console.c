#include "console.h"
#include "config.h"

#if CFG_UART_CONSOLE

#include "board.h"
#include "key_engine.h"
#include "led_indicator.h"
#include "hall_adc.h"
#include "light_chain.h"
#include "py32f002b_ll_bus.h"
#include "py32f002b_ll_usart.h"

#define LINE_MAX 32

static char     s_line[LINE_MAX];
static uint8_t  s_len;
static bool     s_raw_stream;
static uint32_t s_raw_last_ms;

static void uart_putc(char c)
{
    while (!LL_USART_IsActiveFlag_TXE(DBG_UART)) {
    }
    LL_USART_TransmitData8(DBG_UART, (uint8_t)c);
}

void console_puts(const char *s)
{
    while (*s) {
        if (*s == '\n') uart_putc('\r');
        uart_putc(*s++);
    }
}

void console_put_uint(uint32_t v)
{
    char buf[11];
    int i = 10;
    buf[i] = '\0';
    do {
        buf[--i] = (char)('0' + (v % 10U));
        v /= 10U;
    } while (v && i > 0);
    console_puts(&buf[i]);
}

/* 输出 0.01 mm 单位的数值为 "x.yy" */
static void put_travel(uint16_t t)
{
    console_put_uint(t / 100U);
    uart_putc('.');
    uart_putc((char)('0' + (t / 10U) % 10U));
    uart_putc((char)('0' + t % 10U));
}

void console_init(void)
{
    LL_IOP_GRP1_EnableClock(LL_IOP_GRP1_PERIPH_GPIOB);
    LL_APB1_GRP2_EnableClock(LL_APB1_GRP2_PERIPH_USART1);

    LL_GPIO_SetPinMode(DBG_UART_GPIO_PORT, DBG_UART_TX_PIN, LL_GPIO_MODE_ALTERNATE);
    LL_GPIO_SetPinMode(DBG_UART_GPIO_PORT, DBG_UART_RX_PIN, LL_GPIO_MODE_ALTERNATE);
    LL_GPIO_SetAFPin_0_7(DBG_UART_GPIO_PORT, DBG_UART_TX_PIN, DBG_UART_AF);
    LL_GPIO_SetAFPin_0_7(DBG_UART_GPIO_PORT, DBG_UART_RX_PIN, DBG_UART_AF);
    LL_GPIO_SetPinSpeed(DBG_UART_GPIO_PORT, DBG_UART_TX_PIN, LL_GPIO_SPEED_FREQ_HIGH);
    LL_GPIO_SetPinOutputType(DBG_UART_GPIO_PORT, DBG_UART_TX_PIN, LL_GPIO_OUTPUT_PUSHPULL);
    LL_GPIO_SetPinPull(DBG_UART_GPIO_PORT, DBG_UART_TX_PIN, LL_GPIO_PULL_UP);
    LL_GPIO_SetPinPull(DBG_UART_GPIO_PORT, DBG_UART_RX_PIN, LL_GPIO_PULL_UP);

    LL_USART_SetBaudRate(DBG_UART, SystemCoreClock, LL_USART_OVERSAMPLING_16, UART_BAUDRATE);
    LL_USART_SetDataWidth(DBG_UART, LL_USART_DATAWIDTH_8B);
    LL_USART_SetStopBitsLength(DBG_UART, LL_USART_STOPBITS_1);
    LL_USART_SetParity(DBG_UART, LL_USART_PARITY_NONE);
    LL_USART_SetTransferDirection(DBG_UART, LL_USART_DIRECTION_TX_RX);
    LL_USART_Enable(DBG_UART);

    console_puts("\n" FW_VERSION_STR " ready. type 'help'\n");
}

static bool str_eq(const char *a, const char *b)
{
    while (*a && *b) {
        if (*a != *b) return false;
        a++; b++;
    }
    return *a == *b;
}

static bool starts_with(const char *s, const char *prefix)
{
    while (*prefix) {
        if (*s++ != *prefix++) return false;
    }
    return true;
}

/* 解析一个十进制数，跳过前导空格；返回指向数字后的位置，无数字时 *ok=false */
static const char *parse_uint(const char *s, uint32_t *out, bool *ok)
{
    while (*s == ' ') s++;
    if (*s < '0' || *s > '9') {
        *ok = false;
        return s;
    }
    uint32_t v = 0;
    while (*s >= '0' && *s <= '9') {
        v = v * 10U + (uint32_t)(*s - '0');
        s++;
    }
    *out = v;
    *ok = true;
    return s;
}

static void cmd_status(void)
{
    const key_cal_t *cal = key_engine_get_cal();
    console_puts("gear=");      console_put_uint(key_engine_get_gear());
    console_puts(" adc=");      console_put_uint(hall_adc_last());
    console_puts(" depth=");    put_travel(key_engine_depth());
    console_puts("mm pressed="); console_put_uint(key_engine_pressed() ? 1U : 0U);
    console_puts("\ncal rest=");   console_put_uint(cal->rest_adc);
    console_puts(" bottom=");      console_put_uint(cal->bottom_adc);
    console_puts(" rest_eff=");    console_put_uint(key_engine_effective_rest());
    console_puts(" polarity=");
    console_puts(cal->bottom_adc >= cal->rest_adc ? "pressed_high" : "pressed_low");
    console_puts("\nlight_chain: ");
    console_put_uint(LIGHT_CHAIN_LED_COUNT);
    console_puts("x" LIGHT_CHAIN_LED_TYPE " J1.DIN->LED1..LED5->J2.DOUT, mcu_connected=");
    console_put_uint(LIGHT_CHAIN_MCU_CONNECTED);
    console_puts(" (game controlled, not implemented)\n");
}

static void cmd_gears(void)
{
    static const char *const names[GEAR_COUNT] = {
        "red", "orange", "yellow", "green", "blue", "indigo", "purple"
    };
    for (uint8_t g = 1; g <= GEAR_COUNT; g++) {
        console_puts(g == key_engine_get_gear() ? "*" : " ");
        console_put_uint(g);
        console_puts(" ");
        console_puts(names[g - 1U]);
        console_puts(" act=");
        put_travel(key_engine_gear_travel(g));
        console_puts("mm thr_adc=");
        console_put_uint(key_engine_gear_threshold_adc(g));
        console_puts("\n");
    }
}

static void handle_line(const char *line)
{
    bool ok;
    uint32_t a, b, c;

    if (*line == '\0') return;

    if (str_eq(line, "help")) {
        console_puts("help ver status gears gear<1-7> cal raw[0|1] led[r g b] reset light\n");
    } else if (str_eq(line, "ver")) {
        console_puts(FW_VERSION_STR "\n");
    } else if (str_eq(line, "status")) {
        cmd_status();
    } else if (str_eq(line, "gears")) {
        cmd_gears();
    } else if (starts_with(line, "gear")) {
        parse_uint(line + 4, &a, &ok);
        if (ok && a >= 1U && a <= GEAR_COUNT) {
            app_request_gear((uint8_t)a);
            console_puts("ok gear=");
            console_put_uint(a);
            console_puts("\n");
        } else {
            console_puts("err: gear 1..7\n");
        }
    } else if (str_eq(line, "cal")) {
        app_request_calibration();
        console_puts("cal: release key, wait...\n");
    } else if (starts_with(line, "raw")) {
        parse_uint(line + 3, &a, &ok);
        s_raw_stream = ok ? (a != 0U) : !s_raw_stream;
        console_puts(s_raw_stream ? "raw on\n" : "raw off\n");
    } else if (starts_with(line, "led")) {
        const char *p = parse_uint(line + 3, &a, &ok);
        if (!ok) {
            led_show_gear(key_engine_get_gear());
            console_puts("led: gear color\n");
        } else {
            p = parse_uint(p, &b, &ok);
            if (ok) parse_uint(p, &c, &ok);
            if (ok && a <= 255U && b <= 255U && c <= 255U) {
                rgb_t col = { (uint8_t)a, (uint8_t)b, (uint8_t)c };
                led_set_rgb(col);
                console_puts("ok\n");
            } else {
                console_puts("err: led r g b (0..255)\n");
            }
        }
    } else if (str_eq(line, "reset")) {
        app_request_factory_reset();
        console_puts("factory reset done\n");
    } else if (starts_with(line, "light")) {
        bool handled = light_chain_hook(LIGHT_CMD_RESERVED, (const uint8_t *)line, (uint8_t)s_len);
        console_puts(handled ? "light: ok\n"
                             : "light: reserved (game-controlled chain, not implemented)\n");
    } else {
        console_puts("err: unknown, try help\n");
    }
}

void console_task(uint32_t now_ms)
{
    if (LL_USART_IsActiveFlag_ORE(DBG_UART)) {
        LL_USART_ClearFlag_ORE(DBG_UART);
    }
    while (LL_USART_IsActiveFlag_RXNE(DBG_UART)) {
        char ch = (char)LL_USART_ReceiveData8(DBG_UART);
        if (ch == '\r' || ch == '\n') {
            if (s_len > 0U) {
                s_line[s_len] = '\0';
                handle_line(s_line);
                s_len = 0;
            }
        } else if (ch == 0x08 || ch == 0x7F) {
            if (s_len > 0U) s_len--;
        } else if (s_len < LINE_MAX - 1U && ch >= ' ') {
            s_line[s_len++] = ch;
        }
    }

    if (s_raw_stream && (now_ms - s_raw_last_ms) >= 50U) {
        s_raw_last_ms = now_ms;
        console_put_uint(hall_adc_last());
        uart_putc(',');
        console_put_uint(key_engine_depth());
        uart_putc(',');
        console_put_uint(key_engine_pressed() ? 1U : 0U);
        console_puts("\n");
    }
}

#else /* !CFG_UART_CONSOLE */

void console_init(void) {}
void console_task(uint32_t now_ms) { (void)now_ms; }
void console_puts(const char *s) { (void)s; }
void console_put_uint(uint32_t v) { (void)v; }

#endif
