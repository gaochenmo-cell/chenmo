#include "light_chain.h"

bool light_chain_hook(light_cmd_t cmd, const uint8_t *payload, uint8_t len)
{
    /* 故意留空：灯效由游戏通过 J1/J2 直接控制，详见 light_chain.h。 */
    (void)cmd;
    (void)payload;
    (void)len;
    return false;
}
