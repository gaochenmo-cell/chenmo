# paduex3_1.2 / 1.3 / 1.4 启动变砖根因（paduex3_1.5 已修）

## 现象

OTA 显示「升级成功 / 跳转应用」后：电脑无 USB 提示音、上位机检测不到。

## 根因（boot hook 写超）

`build_paduex3_1.2.py` 在 `0x080064B0` 写入：

```text
BL Mapping_Load + 6×NOP  = 16 字节
```

原厂此处第一个 `memcpy(0x24)` 只有 **12 字节**。多写的 4 字节盖掉了下一段的：

```text
movs r2, #0x24
ldr  r1, [pc, #…]
```

后面仍执行 `adr r0; bl 0x080048A4`，此时 **r1/r2 是垃圾** → 乱拷内存 → HardFault → USB 来不及枚举。

这与 paduex3_1.3 的栈对齐问题是两类 bug；**1.4 虽修了栈，但 boot hook 仍是 16 字节，所以照样砖。**

## 修复（paduex3_1.5）

```text
BL Mapping_Load + 4×NOP  = 12 字节
并恢复 0x080064BC..BF = 原厂 movs/ldr
```

SendPacket 保持 paduex3_1.2（暂不加粘键超时），先保证能启动。

## 救砖

电脑完全认不到时只能 **ISP/SWD** 刷 `paduex3_1.5.bin` / `paduex3_1.0.bin` / 官方 V2.13，地址 `0x08004000`。  
**不要**再刷 1.2 / 1.3 / 1.4。
