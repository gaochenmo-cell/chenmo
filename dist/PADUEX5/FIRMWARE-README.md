# PADUEX5 firmware

## `grok1.3.bin`（推荐 — 改键 + 粘键修复，与原厂同体积）

基于 **grok1.2**，**文件长度与原厂相同（37072 字节）**，额外修复「按键已松但主机仍粘住」：

1. 继承 grok1.2：改键 `GET_MAPP` / `SET_MAPP`、灵敏度 Flash、消抖 `DTick` 索引  
2. **`App_USB_KBS_SendPacket` / `App_USB_HID_SendPacket`**：原无限等待 `g_*_tx_completed` 改为有界自旋（约 `0x80000` 次后照常发送）  
3. **延后 `KeyCodeBK` 写回**：报告真正发出后再更新，避免发送阻塞时丢掉松开帧  

根因：USB IN 端点忙时主循环卡在发送等待，垫子 ADC 早已低于阈值，但主机仍看到上一帧按下报告。  
粘键与消抖 `DTick`（默认 0–3ms）无关；USB 全速 `bInterval≈1` ≈ 1000Hz 轮询正常。

改键 + 超时补丁均保持 **同体积**：

- PADUEX5 **固件升级**（OTA）可用  
- 厂家 **ISP / IPS** 按旧体积整包写也安全（地址必须 **`0x08004000`**）

重建：

```bash
python3 firmware/patches/build_grok1.3.py   # 读 grok1.2.bin → 写 grok1.3.bin
```

## `grok1.2.bin`（改键，同体积）

基于 **grok1.0** / 官方 `5KEY_2P_V2.13.bin`，含设备端按键映射，**无**粘键发送超时修复。新板请优先刷 **`grok1.3.bin`**。

1. 继承 grok1.0：灵敏度 Flash 写回、消抖 `DTick[key*4]` 索引修正  
2. **`GET_MAPP` / `SET_MAPP`**：与上位机「按键映射」页互通  
3. **`App_USB_KBS_Report_Normal`** 从 RAM 表 `Mapping[12]` 取 HID usage  
4. 映射存 Flash **`0x08011800`**（配置区第 5 页）  
5. 默认键位 = 原 1P：`W X A D S Q E Z C *`（前 5 键对应垫子 ↖↗●↙↘）

重建：

```bash
python3 firmware/patches/build_grok1.2.py   # 读 grok1.0.bin → 写 grok1.2.bin
```

## `grok1.1.bin`（已不推荐）

功能与 1.2 相同，但比原厂 **多约 228 字节**（末尾 cave）。  
用 ISP 若未写满会启动即 HardFault → USB「无法识别后消失」。详见 [`RECOVERY.md`](./RECOVERY.md)。  
新板请改刷 **`grok1.3.bin`**。

## `grok1.0.bin`

仅灵敏度持久化 + 消抖索引；**无**改键协议。见 `patches/grok1.0-App_User.patch`。

## 源码对照

- grok1.0：`patches/grok1.0-App_User.patch`  
- 映射逻辑：`patches/grok1.1-Mapping.md`（完整 AT32 工程需自行合入后编译；本仓库以二进制补丁交付）  
- 1.2 安全说明：`patches/grok1.2-Safety.md`  
- 1.3 粘键修复：`patches/build_grok1.3.py`
