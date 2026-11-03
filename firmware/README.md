# PADUEX5 firmware

## `grok1.1.bin`（含按键映射 — 注意刷写方式）

基于 **grok1.0** / 官方 `5KEY_2P_V2.13.bin`，增加**设备端按键映射**：

1. 继承 grok1.0：灵敏度 Flash 写回、消抖 `DTick[key*4]` 索引修正  
2. **`GET_MAPP` / `SET_MAPP`**：与上位机「按键映射」页互通  
3. **`App_USB_KBS_Report_Normal`** 从 RAM 表 `Mapping[12]` 取 HID usage（不再写死 1P/2P 立即数）  
4. 映射存 Flash **`0x08011800`**（配置区第 5 页，不占用 SENSI/COLOR/DTICK）  
5. 默认键位 = 原 1P：`W X A D S Q E Z C *`（前 5 键对应垫子 ↖↗●↙↘）

**重要：** `grok1.1` 比原厂镜像多约 228 字节（末尾 cave）。启动会跳进这段代码。  
用 **ISP/IPS 工具**若未整包写满或地址不是 `0x08004000`，会变成 USB「无法识别后消失」。详见 [`RECOVERY.md`](./RECOVERY.md)。

- 推荐：PADUEX5 **固件升级** v1.0.0.47+（中断 IAP，整包写入）  
- 救砖 / ISP：请刷 **`grok1.0.bin` 或官方 `5KEY_2P_V2.13.bin`**，地址 **`0x08004000`**

重建二进制补丁：

```bash
python3 firmware/patches/build_grok1.1.py   # 读 grok1.0.bin → 写 grok1.1.bin
```

## `grok1.0.bin`

仅灵敏度持久化 + 消抖索引；**无**改键协议。见 `patches/grok1.0-App_User.patch`。

## 源码对照

- grok1.0：`patches/grok1.0-App_User.patch`  
- grok1.1 逻辑说明：`patches/grok1.1-Mapping.md`（完整 AT32 工程需自行合入 `uandancedance` 后编译；本仓库以二进制补丁交付）
