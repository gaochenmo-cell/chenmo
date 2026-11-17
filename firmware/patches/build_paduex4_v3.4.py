#!/usr/bin/env python3
"""
Build PADUEX4_V3.4.bin from PADUEX4_V3.2.bin.

Same DTick patch as the five-key image (see build_paduex5_v3.4.py): default
table 0, power-up forces DTick[0..35] = 0, SET_SENS type 2 is ignored and
GET_SENS returns 0. HVer stays 0x14. FVer becomes 3.4. The fifth-key mask is
untouched.
"""
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path


def _load():
    path = Path(__file__).with_name("build_paduex5_v3.4.py")
    spec = importlib.util.spec_from_file_location("build_paduex5_v34", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    return _load().build("PADUEX4_V3.2.bin", "PADUEX4_V3.4.bin", 0x14)


if __name__ == "__main__":
    sys.exit(main())
