# PADUEX5 firmware — grok1.0

## `grok1.0.bin`

Binary patch of official **5KEY_2P_V2.13.bin** with:

1. **Sensitivity Flash write restored** — stock V2.13 accepted `WRIT` for sensi in RAM but **skipped** `App_Set_Flash_Cofnig(FLASH_DATA_ADDR_SENSI, …)` (commented since V1.15). grok1.0 routes ConfigType=0x00 through the same flash path as Color/DTick.
2. **Debounce index fix** — `Tck[key] = DTick[key]` → `Tck[key] = DTick[key*4]` so per-key 延迟释放 matches the host buffer layout.

Flash with the existing PADUEX5 **固件升级** tool (OTA), same as V2.13.

## Keyboard keymap (host + source)

HIDLibrary already defines `GET_MAPP` / `SET_MAPP`. Stock Yuanpad FW has **hardcoded** `BUTTON_NKRO_MAP_P1/P2` and no Mapping flash page.

- Host UI (PADUEX5 ≥ 1.0.0.44) can edit 5-key HID usages in keyboard mode.
- Full on-device remap needs the source patch in `patches/` rebuilt with AT32 IDE (not included in the binary patch — too large for safe in-place edit).
- With remappable keys, **1P/2P is unnecessary in keyboard mode** (HID/手柄 mode still uses 1P/2P for gamepad identity). The app hides 1P/2P while USB=键盘.

## Rebuild from source

Apply `patches/grok1.0-App_User.patch` on top of [uandancedance](https://github.com/likunyuan3276956-commits/uandancedance) `src/`, then build 5KEY with AT32/Keil as usual.
