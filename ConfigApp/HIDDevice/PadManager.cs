using System;
using System.Collections.Generic;

namespace ConfigAppNS
{
    /// <summary>One of the two board slots shown by the host window.</summary>
    internal sealed class PadSlot
    {
        public readonly int Index;          // 0 = 1P, 1 = 2P
        public string Name { get { return Index == 0 ? "1P" : "2P"; } }
        public string ConfigKey { get { return Index == 0 ? "PAD1P" : "PAD2P"; } }
        /// <summary>Board key remembered in Config.ini (serial number); "" when nothing is bound yet.</summary>
        public string BoundKey = "";
        /// <summary>Board currently present in this slot (vendor HID interface), or null.</summary>
        public HidDeviceInfo Device;

        public PadSlot(int index) { Index = index; }
        public bool Present { get { return Device != null; } }
    }

    /// <summary>
    /// Assigns the boards found on USB to the 1P / 2P slots.
    ///
    /// Identity: a board is identified by its USB serial number (HidDeviceInfo.BoardKey). The
    /// firmware derives it from the MCU unique ID, so it is different for every board and does
    /// not change with the USB port. Both boards share VID / PID and report the same GET_MODE
    /// mode / HVer bytes, so nothing else in the protocol tells them apart.
    ///
    /// Rules, applied in this order on every rescan:
    ///  1. A board that is already attached to a slot stays there (plugging / unplugging the other
    ///     board never moves a working column).
    ///  2. A board whose serial equals the PAD1P / PAD2P value in Config.ini goes to that slot when
    ///     it is free (the binding from the last session, or from "Swap 1P / 2P").
    ///  3. Any other board takes a free slot: a second-player stock PID (5762 / 5772 / 5763) prefers
    ///     2P, everything else 1P first. Two unknown boards arriving together are ordered by device
    ///     path (hub port), so the result is reproducible for the same cabling.
    ///  4. Whatever ends up in a slot is written back to Config.ini (PAD1P= / PAD2P=), so the next
    ///     start puts each board where it was. "Swap 1P / 2P" exchanges the two bindings.
    /// </summary>
    internal sealed class PadManager
    {
        public readonly PadSlot[] Slots = new PadSlot[] { new PadSlot(0), new PadSlot(1) };
        /// <summary>Number of boards currently enumerating as the OTA bootloader (0E8F:8901).</summary>
        public int BootloaderCount { get; private set; }
        /// <summary>Boards found but not placed (a third board, or a board while both slots are taken).</summary>
        public int UnplacedCount { get; private set; }

        public PadManager()
        {
            foreach (PadSlot s in Slots)
                s.BoundKey = (AppConfig.Read(s.ConfigKey) ?? "").Trim();
        }

        /// <summary>Enumerate USB and update the slots. Returns true when any slot changed.</summary>
        public bool Rescan()
        {
            List<HidDeviceInfo> boards = EnumerateBoards();
            BootloaderCount = CountBootloaders();

            HidDeviceInfo[] before = new HidDeviceInfo[] { Slots[0].Device, Slots[1].Device };
            HidDeviceInfo[] next = new HidDeviceInfo[2];
            List<HidDeviceInfo> pending = new List<HidDeviceInfo>(boards);

            // 1. keep what is attached
            for (int i = 0; i < 2; i++)
            {
                if (before[i] == null) continue;
                HidDeviceInfo still = TakeByKey(pending, before[i].BoardKey);
                if (still != null) next[i] = still;
            }
            // 2. remembered binding
            for (int i = 0; i < 2; i++)
            {
                if (next[i] != null || Slots[i].BoundKey.Length == 0) continue;
                HidDeviceInfo bound = TakeByKey(pending, Slots[i].BoundKey);
                if (bound != null) next[i] = bound;
            }
            // 3. newcomers
            pending.Sort((a, b) => string.CompareOrdinal(a.PortKey, b.PortKey));
            foreach (HidDeviceInfo dev in pending.ToArray())
            {
                int prefer = DeviceProtocol.IsPlayer2Pid(dev.Pid) ? 1 : 0;
                int slot = next[prefer] == null ? prefer : (next[1 - prefer] == null ? 1 - prefer : -1);
                if (slot < 0) break;
                next[slot] = dev;
                pending.Remove(dev);
            }
            UnplacedCount = pending.Count;

            bool changed = false;
            for (int i = 0; i < 2; i++)
            {
                bool was = before[i] != null, now = next[i] != null;
                if (was != now || (now && (before[i].BoardKey != next[i].BoardKey || before[i].Path != next[i].Path)))
                    changed = true;
                Slots[i].Device = next[i];
                // 4. remember
                if (now && Slots[i].BoundKey != next[i].BoardKey)
                {
                    Slots[i].BoundKey = next[i].BoardKey;
                    AppConfig.Write(Slots[i].ConfigKey, Slots[i].BoundKey);
                    DebugLog.Log("SLOT {0}: bound to {1}", Slots[i].Name, Slots[i].BoundKey);
                }
            }
            if (changed || UnplacedCount > 0)
            {
                DebugLog.Log("USB: {0} board(s), {1} bootloader(s); 1P={2}; 2P={3}{4}",
                    boards.Count, BootloaderCount,
                    next[0] != null ? next[0].ToString() : "-", next[1] != null ? next[1].ToString() : "-",
                    UnplacedCount > 0 ? "; " + UnplacedCount + " not placed" : "");
            }
            return changed;
        }

        /// <summary>Exchange the two slots (boards and remembered bindings).</summary>
        public void Swap()
        {
            HidDeviceInfo d = Slots[0].Device; Slots[0].Device = Slots[1].Device; Slots[1].Device = d;
            string k = Slots[0].BoundKey; Slots[0].BoundKey = Slots[1].BoundKey; Slots[1].BoundKey = k;
            foreach (PadSlot s in Slots)
                AppConfig.Write(s.ConfigKey, s.BoundKey);
            DebugLog.Log("SLOT swap: 1P={0} ({1}); 2P={2} ({3})",
                Slots[0].Device != null ? Slots[0].Device.ToString() : "-", Slots[0].BoundKey,
                Slots[1].Device != null ? Slots[1].Device.ToString() : "-", Slots[1].BoundKey);
        }

        private static HidDeviceInfo TakeByKey(List<HidDeviceInfo> list, string key)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].BoardKey == key)
                {
                    HidDeviceInfo d = list[i];
                    list.RemoveAt(i);
                    return d;
                }
            }
            return null;
        }

        /// <summary>
        /// One entry per physical board: the vendor HID interface (usage page 0xFF00) that carries
        /// the configuration protocol. The boot-keyboard interface of the same board cannot be
        /// opened for read / write on Windows and is skipped by the enumerator anyway.
        /// </summary>
        public static List<HidDeviceInfo> EnumerateBoards()
        {
            List<HidDeviceInfo> all = HidEnumerator.Find(DeviceProtocol.PAD_VID, DeviceProtocol.PAD_PIDS);
            Dictionary<string, HidDeviceInfo> byBoard = new Dictionary<string, HidDeviceInfo>();
            List<string> order = new List<string>();
            foreach (HidDeviceInfo d in all)
            {
                HidDeviceInfo cur;
                if (!byBoard.TryGetValue(d.BoardKey, out cur))
                {
                    byBoard[d.BoardKey] = d;
                    order.Add(d.BoardKey);
                }
                else if (!cur.IsVendorPage && d.IsVendorPage)
                {
                    byBoard[d.BoardKey] = d;
                }
            }
            List<HidDeviceInfo> boards = new List<HidDeviceInfo>();
            foreach (string k in order) boards.Add(byBoard[k]);
            return boards;
        }

        public static int CountBootloaders()
        {
            List<HidDeviceInfo> list = HidEnumerator.Find(DeviceProtocol.OTA_VID, new int[] { DeviceProtocol.OTA_PID });
            Dictionary<string, bool> keys = new Dictionary<string, bool>();
            foreach (HidDeviceInfo d in list) keys[d.BoardKey] = true;
            return keys.Count;
        }
    }
}
