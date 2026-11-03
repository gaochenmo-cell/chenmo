# PADUEX5 firmware

## `grok1.2.bin`（推荐 — 含改键，与原厂同体积）

基于 **grok1.0** / 官方 `5KEY_2P_V2.13.bin`，**文件长度与原厂相同（37072 字节）**，增加设备端按键映射：

1. 继承 grok1.0：灵敏度 Flash 写回、消抖 `DTick[key*4]` 索引修正  
2. **`GET_MAPP` / `SET_MAPP`**：与上位机「按键映射」页互通  
3. **`App_USB_KBS_Report_Normal`** 从 RAM 表 `Mapping[12]` 取 HID usage  
4. 映射存 Flash **`0x08011800`**（配置区第 5 页）  
5. 默认键位 = 原 1P：`W X A D S Q E Z C *`（前 5 键对应垫子 ↖↗●↙↘）

改键逻辑全部塞进原 `Report_Normal` 区域内，**不追加末尾 cave**，因此：

- PADUEX5 **固件升级**（OTA）可用  
- 厂家 **ISP / IPS** 按旧体积整包写也安全（地址必须 **`0x08004000`**）

重建：

```bash
python3 firmware/patches/build_grok1.2.py   # 读 grok1.0.bin → 写 grok1.2.bin
```

## `grok1.1.bin`（已不推荐）

功能与 1.2 相同，但比原厂 **多约 228 字节**（末尾 cave）。  
用 ISP 若未写满会启动即 HardFault → USB「无法识别后消失」。详见 [`RECOVERY.md`](./RECOVERY.md)。  
新板请改刷 **`grok1.2.bin`**。

## `grok1.0.bin`

仅灵敏度持久化 + 消抖索引；**无**改键协议。见 `patches/grok1.0-App_User.patch`。

## 源码对照

- grok1.0：`patches/grok1.0-App_User.patch`  
- 映射逻辑：`patches/grok1.1-Mapping.md`（完整 AT32 工程需自行合入后编译；本仓库以二进制补丁交付）
