# 四键固件 V3.3：键盘报告发送前按 UID 错开 0～15 µs

只针对 `PADUEX4`。五键镜像仍是 `PADUEX5_V3.2.bin`。

## 做了什么

`App_USB_KBS_SendPacket`（`0x08005E6C`）原来的第一条指令 `push.w {r4-r8,lr}` 换成 `b.w 0x08005DE8`。helper 放在 `Report_Normal` 跳过的原厂 2P 链里（`0x08005DE6` 的 `b 0x08005E50` 不变），做完延时后执行原来的 `push.w`，再 `b.w 0x08005E70` 回到函数主体。主体从 `0x08005E70` 起一个字节没改。

延时档 = `(UID0 ^ UID1 ^ UID2) & 0xF`，UID 在 `0x1FFFF7E8 / EC / F0`。0 档直接跳过循环；其余每档内层 48 次 `subs / bne`。固件自己的时钟初始化把系统时钟记成 144 MHz（`0x20000000`），Cortex-M4 上这 48 次是 144 个周期，约 1 µs。Flash 等待会把每一档拉长一些，档与档之间仍是固定倍数。

`0x0800616A` 的 `movs r1,#2` 改为 `#3`，GET_MODE 上报 `V3.3`。HVer `0x14` 不变。bInterval、扫描、释放判断、DTICK、灯效都不在差异里。`0x14C` 仍是 `0x080084A5`，体积仍是 37072。

## 预期

两块板读到的 UID 不同，写端点的时刻就差若干微秒。报告只在 `KeyCode` 变化时发一次，而且 helper 返回之后 `SendPacket` 还要等上一份报告被主机取走（`tx_completed`，正常时不超过 1 ms）。所以这几微秒经常被 1 ms 的轮询对齐吃掉，不能指望它消除「松开保持 2 ms、松脚后明显滞留」。

## 构建

```bash
python3 firmware/patches/build_paduex4_v3.3.py
python3 firmware/tests/emu_report_stagger.py
```

回滚：刷 `PADUEX4_V3.2.bin`。
