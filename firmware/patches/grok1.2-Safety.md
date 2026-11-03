# grok1.2 vs grok1.1

| | grok1.1 | grok1.2 |
|--|---------|---------|
| Size | 37300 (+228 cave) | **37072 (= grok1.0)** |
| Remap | GET/SET_MAPP | same |
| Report_Normal | patch lookup → Mapping[] | rewrite compact + handlers in-region |
| Boot `0x080064B0` | BL into **appended** cave | BL into **in-image** Mapping_Load |
| ISP by stock length | **can brick** (cave truncated) | **safe** |

Builder: `python3 firmware/patches/build_grok1.2.py`
