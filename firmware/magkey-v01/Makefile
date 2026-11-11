# ADX-Magkey-V01 固件 Makefile（arm-none-eabi-gcc）
#
#   make            -> build/magkey_v01.{elf,hex,bin,map,lst}
#   make clean
#   make flash-pyocd    (pyOCD + PY32 pack, SWD: DEBUG1)
#   make flash-jlink    (J-Link Commander)
#   make flash-openocd  (OpenOCD, 需支持 PY32F002B 的 target 脚本)
#
# 可覆盖的变量示例：
#   make CFLAGS_EXTRA="-DHALL_PRESSED_HIGH=0 -DCFG_RAPID_TRIGGER=0"

TARGET    := magkey_v01
BUILD_DIR := build

PREFIX ?= arm-none-eabi-
HOST_CC ?= gcc
CC      := $(PREFIX)gcc
OBJCOPY := $(PREFIX)objcopy
OBJDUMP := $(PREFIX)objdump
SIZE    := $(PREFIX)size

C_SOURCES := \
  src/main.c \
  src/systick.c \
  src/hall_adc.c \
  src/key_engine.c \
  src/led_indicator.c \
  src/button.c \
  src/storage.c \
  src/console.c \
  src/light_chain.c \
  src/libc_stubs.c \
  vendor/py32/system_py32f002b.c

ASM_SOURCES := vendor/py32/startup_py32f002bxx.s

INCLUDES := -Isrc -Ivendor/cmsis -Ivendor/py32 -Ivendor/py32_ll

LDSCRIPT := py32f002bx5_magkey.ld

CPU     := -mcpu=cortex-m0plus -mthumb
DEFS    := -DPY32F002Bx5 -DUSE_FULL_LL_DRIVER -DHSI_VALUE=24000000U
OPT     ?= -Os

CFLAGS  := $(CPU) $(DEFS) $(INCLUDES) $(OPT) -std=gnu11 \
           -Wall -Wextra -Wno-unused-parameter \
           -ffunction-sections -fdata-sections -fno-common \
           -g3 -MMD -MP $(CFLAGS_EXTRA)
ASFLAGS := $(CPU) -x assembler-with-cpp $(DEFS) -g3
LDFLAGS := $(CPU) -specs=nano.specs -nostartfiles -T$(LDSCRIPT) \
           -Wl,--gc-sections -Wl,-Map=$(BUILD_DIR)/$(TARGET).map,--cref \
           -Wl,--print-memory-usage

OBJECTS := $(addprefix $(BUILD_DIR)/,$(C_SOURCES:.c=.o)) \
           $(addprefix $(BUILD_DIR)/,$(ASM_SOURCES:.s=.o))

.PHONY: all clean test-host flash-pyocd flash-jlink flash-openocd

all: $(BUILD_DIR)/$(TARGET).elf $(BUILD_DIR)/$(TARGET).hex $(BUILD_DIR)/$(TARGET).bin $(BUILD_DIR)/$(TARGET).lst
	$(SIZE) $<

# 在 PC 上编译并运行按键判定回归（不需要交叉工具链，也不烧录）
test-host:
	@mkdir -p $(BUILD_DIR)
	$(HOST_CC) -std=gnu11 -Wall -Wextra -Werror -DHOST_TEST -Itests/host -Isrc \
	  tests/host/test_key_engine.c src/key_engine.c -o $(BUILD_DIR)/test_key_engine
	$(BUILD_DIR)/test_key_engine

$(BUILD_DIR)/%.o: %.c
	@mkdir -p $(dir $@)
	$(CC) -c $(CFLAGS) $< -o $@

$(BUILD_DIR)/%.o: %.s
	@mkdir -p $(dir $@)
	$(CC) -c $(ASFLAGS) $< -o $@

$(BUILD_DIR)/$(TARGET).elf: $(OBJECTS) $(LDSCRIPT)
	$(CC) $(OBJECTS) $(LDFLAGS) -o $@

$(BUILD_DIR)/%.hex: $(BUILD_DIR)/%.elf
	$(OBJCOPY) -O ihex $< $@

$(BUILD_DIR)/%.bin: $(BUILD_DIR)/%.elf
	$(OBJCOPY) -O binary -S $< $@

$(BUILD_DIR)/%.lst: $(BUILD_DIR)/%.elf
	$(OBJDUMP) -h -S $< > $@

clean:
	rm -rf $(BUILD_DIR)

# ---- 烧录（SWD：DEBUG1 排针 VCC/SWCLK/SWDIO/GND）----
PYOCD_TARGET ?= py32f002bx5
flash-pyocd: $(BUILD_DIR)/$(TARGET).hex
	pyocd load --target $(PYOCD_TARGET) --frequency 1000000 $<

flash-jlink: $(BUILD_DIR)/$(TARGET).hex
	printf 'r\nh\nloadfile %s\nr\ng\nexit\n' $< > $(BUILD_DIR)/flash.jlink
	JLinkExe -device PY32F002BX5 -if SWD -speed 1000 -autoconnect 1 -CommanderScript $(BUILD_DIR)/flash.jlink

flash-openocd: $(BUILD_DIR)/$(TARGET).elf
	openocd -f interface/cmsis-dap.cfg -f target/py32f002b.cfg -c "program $< verify reset exit"

-include $(OBJECTS:.o=.d)
