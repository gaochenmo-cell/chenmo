# grok1.3 变砖原因（已用 grok1.4 修复）

## 现象

刷入 `grok1.3.bin` 后：USB 插上无提示音、无设备、垫子无反应（与 HardFault / USB 栈崩溃一致）。

## 根因

`build_grok1.3.py` 改写 `App_USB_KBS/HID_SendPacket` 时，把原厂：

```text
push.w {r4, r5, r6, r7, r8, lr}   ; 6 个寄存器 = 24 字节（保持 8 字节栈对齐）
...
pop.w  {r4, r5, r6, r7, r8, pc}
```

改成了：

```text
push {r4, r5, r6, r7, lr}         ; 5 个寄存器 = 20 字节 → SP % 8 == 4
...
pop  {r4, r5, r6, r7, pc}
```

Cortex-M / AAPCS 要求在 BL 公共接口处 **8 字节栈对齐**。对齐被破坏后，随后 `BL usbd_*_send_report` / `usbd_connect_state_get` 易触发 **HardFault**，USB 枚举失败或立刻掉线。

粘键超时逻辑本身没有问题；**只有压栈寄存器个数错了**。

## 修复

`grok1.4.bin`：保留 1.3 的发送超时，恢复 `push.w/pop.w {r4–r8,lr/pc}`。

## 救砖

1. ISP / SWD 刷 **`grok1.4.bin`** 或 **`grok1.2.bin`** / 官方 V2.13，地址 **`0x08004000`**，长度 **37072**  
2. 或进 Bootloader 后用 PADUEX5 OTA 刷上述镜像  
3. **不要再刷 grok1.3**
