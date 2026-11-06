# paduex5_1.1 Mapping (source-level intent)

Binary patcher: `build_paduex5_1.1.py`. Equivalent C changes on `uandancedance`:

## Flash

```c
#define FLASH_DATA_ADDR_MAPP  (FLASH_DATA_ADDR_START + FLASH_PAGE_SIZE * 4)  // 0x08011800
```

## AppUser

```c
unsigned short Mapping[12];
```

Default (1P Report_Normal):

```c
unsigned short Default_KeyMapping[12] = {
  0x1A, 0x1B, 0x04, 0x07, 0x16, 0x14, 0x08, 0x1D, 0x06, 0x55, 0x00, 0x00
};
```

Boot:

```c
App_Get_Flash_Cofnig(FLASH_DATA_ADDR_MAPP, AppUser.Mapping, Default_KeyMapping, 12);
```

## Report_Normal

Replace 1P/2P if/else with:

```c
AsciiChar = AppUser.Mapping[Index] & 0xFF;
```

## USB

In `App_User_CMD_Report` switch, handle `0x4E` / `0x4F` like Laverita `App_MAPPING_Command_Report` (12 usages, host pads to 12).

RAM in binary patch: `0x20001BB0` (gap after AppUser, before next BSS).
