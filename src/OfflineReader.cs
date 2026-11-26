using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

internal sealed class OfflineCharacter
{
    internal int Slot;
    internal string Name;
    internal readonly List<OfflineTalisman> Talismans = new List<OfflineTalisman>();
    internal readonly Dictionary<int, int> DecorationCounts = new Dictionary<int, int>();
    internal readonly Dictionary<int, int> MountedDecorationCounts = new Dictionary<int, int>();
    internal string DecorationError;
}

internal sealed class OfflineTalisman
{
    internal int FreeVal0, FreeVal1;
    internal int[] Packed;
    internal bool Favorite, Equipped;
}

// Read-only support for Capcom's Wilds PC save format.
// Format descriptions: github.com/kvasszn/ree-save-editor/tree/main/src/save
// Equipment layout: github.com/zamcoder/bulk-sell-equipment-mhwilds
internal static class OfflineReader
{
    const int MaxBytes = 128 * 1024 * 1024;
    static readonly string[] Keys = {
        "_Data", "_BasicData", "CharName", "PlayStartDateTime", "_Equip", "_EquipBox", "_EquipIndex", "Index",
        "Category_Gender", "FreeVal0", "FreeVal1", "FreeVal4", "BowgunCustomizeId",
        "_AccessoryBox", "ID", "Num", "EquipmentAccessoryIdArray"
    };
    static readonly Dictionary<uint, string> FieldNames = BuildFieldNames();

    static Dictionary<uint, string> BuildFieldNames()
    {
        var names = new Dictionary<uint, string>();
        foreach (var key in Keys) {
            var bytes = Encoding.UTF8.GetBytes(key);
            names.Add(Murmur(bytes, bytes.Length), key);
        }
        return names;
    }

    static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }
    static uint MixWord(uint value)
    {
        unchecked {
            value *= 0xcc9e2d51;
            return ((value << 15) | (value >> 17)) * 0x1b873593;
        }
    }
    static uint Murmur(byte[] data, int len)
    {
        unchecked {
            uint hash = uint.MaxValue;
            int i = 0;
            for (; i + 4 <= len; i += 4) {
                hash ^= MixWord(BitConverter.ToUInt32(data, i));
                hash = ((hash << 13) | (hash >> 19)) * 5 + 0xe6546b64;
            }
            uint tail = 0;
            for (int j = 0; i + j < len; j++) tail |= (uint)data[i + j] << (8 * j);
            hash ^= MixWord(tail) ^ (uint)len;
            hash = (hash ^ (hash >> 16)) * 0x85ebca6b;
            hash = (hash ^ (hash >> 13)) * 0xc2b2ae35;
            return hash ^ (hash >> 16);
        }
    }
    static ulong Next(ulong state)
    {
        unchecked {
            state += 0x9e3779b97f4a7c15UL;
            state = (state ^ (state >> 30)) * 0xbf58476d1ce4e5b9UL;
            state = (state ^ (state >> 27)) * 0x94d049bb133111ebUL;
            return state ^ (state >> 31);
        }
    }
    sealed class SaveChunk
    {
        internal int Header, Capacity, Start, Length;
        internal readonly byte[] Key = new byte[16], Iv = new byte[16];
    }

    static IEnumerable<SaveChunk> Chunks(int length, ulong steamId)
    {
        ulong state = 0x7a36955255266cedUL;
        var sizes = new List<int>();
        for (int used = 0; used < length; used += 0x4000) {
            sizes.Add((int)(1 + (state & 7)) * 0x4000);
            state = Next(state);
        }
        state = unchecked(state + ~steamId);
        int header = 16, start = 0;
        foreach (int size in sizes) {
            if (start == length) yield break;
            var chunk = new SaveChunk { Header = header, Capacity = size, Start = start,
                Length = Math.Min(size, length - start) };
            for (int i = 0; i < 16; i++) {
                state = Next(state);
                chunk.Key[i] = (byte)state;
                chunk.Iv[i] = (byte)(state >> 8);
            }
            yield return chunk;
            for (int i = 0; i < 0x210; i++) state = Next(state);
            header += 0x210 + size;
            start += chunk.Length;
        }
    }

    static void TransformChunk(Aes aes, SaveChunk chunk, byte[] input, int inputStart, byte[] output, int outputStart)
    {
        var feedback = (byte[])chunk.Iv.Clone();
        var nextFeedback = new byte[16];
        using (var transform = aes.CreateEncryptor(chunk.Key, new byte[16])) {
            for (int off = 0; off < chunk.Length; off += 16) {
                transform.TransformBlock(feedback, 0, 16, nextFeedback, 0);
                var previous = feedback;
                feedback = nextFeedback;
                nextFeedback = previous;
                for (int j = 0; j < 16 && off + j < chunk.Length; j++)
                    output[outputStart + off + j] = (byte)(feedback[j] ^ input[inputStart + off + j]);
            }
        }
    }

    static byte[] Unpack(byte[] save, ulong steamId)
    {
        return Inflate(ReadPayload(save, steamId));
    }

    static byte[] ReadPayload(byte[] save, ulong steamId)
    {
        Require(save != null && save.Length >= 160 && save.Length <= MaxBytes &&
                Encoding.ASCII.GetString(save, 0, 4) == "DSSS" &&
                BitConverter.ToUInt32(save, 4) == 2 && BitConverter.ToUInt32(save, 8) == 0x18,
                "不支持的存档格式，请选择 Steam 版荒野的角色存档 data001Slot.bin。");
        Require(Murmur(save, save.Length - 4) == BitConverter.ToUInt32(save, save.Length - 4),
                "存档校验失败，请等待游戏完成保存后重试。");
        ulong length = BitConverter.ToUInt64(save, save.Length - 12);
        Require(length >= 24 && length <= (ulong)save.Length, "存档数据长度无效。");
        byte[] payload = new byte[(int)length];
        using (var aes = Aes.Create()) {
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            foreach (var chunk in Chunks(payload.Length, steamId)) {
                int input = chunk.Header + 0x210;
                Require(input + chunk.Capacity <= save.Length - 12, "存档数据块不完整。");
                TransformChunk(aes, chunk, save, input, payload, chunk.Start);
            }
        }
        return payload;
    }

    static byte[] Inflate(byte[] payload)
    {
        ulong plainLen = BitConverter.ToUInt64(payload, 16);
        Require(BitConverter.ToUInt32(payload, 12) == payload.Length - 24 && plainLen >= 12 && plainLen < MaxBytes - 16,
                "此 Steam 账户与存档不匹配，或游戏存档格式已更改。");
        // Preserve the 16-byte alignment origin used by serialized fields.
        var plain = new byte[(int)plainLen + 16];
        using (var compressed = new MemoryStream(payload, 24, payload.Length - 24, false))
        using (var stream = new DeflateStream(compressed, CompressionMode.Decompress)) {
            int pos = 16;
            while (pos < plain.Length) {
                int read = stream.Read(plain, pos, plain.Length - pos);
                Require(read > 0, "存档解压后缺少数据。");
                pos += read;
            }
            Require(stream.ReadByte() == -1, "存档解压后的长度与文件头不符。");
        }
        return plain;
    }

    // Unknown game fields are consumed without materializing personal strings or blobs.
    sealed class Fields
    {
        readonly byte[] data;
        internal int Pos = 16;
        internal Fields(byte[] data) { this.data = data; }
        internal int Take(int count)
        {
            int old = Pos;
            Require(count >= 0 && count <= data.Length - Pos, "存档字段超出文件范围。");
            Pos += count;
            return old;
        }
        void Align(int size) { Take(((Pos + size - 1) & ~(size - 1)) - Pos); }
        int Int() { return BitConverter.ToInt32(data, Take(4)); }
        internal Dictionary<string, object> Object(int depth, bool keep)
        {
            Require(depth < 128, "存档字段嵌套过深。");
            int count = Int();
            Take(4); // class identifier
            Require(count >= 0 && count <= (data.Length - Pos) / 8, "无效的存档字段数量。");
            var obj = keep ? new Dictionary<string, object>() : null;
            for (int i = 0; i < count; i++) {
                uint id = unchecked((uint)Int());
                string name = null;
                bool want = keep && FieldNames.TryGetValue(id, out name);
                int type = Int();
                var result = Value(type, depth + 1, want, -1);
                if (want) obj[name] = result;
                Align(4);
            }
            return obj;
        }
        object Value(int type, int depth, bool keep, int size)
        {
            Require(depth < 128, "存档字段嵌套过深。");
            if (type == 17) return Object(depth, keep);
            if (type == -1) {
                Align(4);
                int member = Int(), width = Int(), count = Int(), kind = Int();
                Require((kind == 0 || kind == 1) && count >= 0 && count <= data.Length - Pos, "无效的存档数组。");
                if (kind == 1 && Pos + 4 <= data.Length && BitConverter.ToUInt32(data, Pos) == 0xffeeffee)
                    Take(checked(4 + count * 4));
                // Plain arrays of fixed-size primitives are contiguous after the first alignment.
                if (!keep && count > 0 && kind == 0 && member >= 1 && member <= 14 &&
                    (width == 1 || width == 2 || width == 4 || width == 8)) {
                    Align(width);
                    Take(checked(count * width));
                    Align(4);
                    return null;
                }
                Require(!keep || count <= 1000000, "存档数组数量超出支持范围。");
                var array = keep ? new object[count] : null;
                for (int i = 0; i < count; i++) {
                    var value = Value(kind == 1 ? 17 : member, depth + 1, keep, width);
                    if (keep) array[i] = value;
                }
                Align(4);
                return array;
            }
            if (type == 15) {
                Align(4);
                int bytes = checked(Int() * 2);
                int offset = Take(bytes);
                if (!keep) return null;
                string text = Encoding.Unicode.GetString(data, offset, bytes);
                int end = text.IndexOf('\0');
                return end < 0 ? text : text.Substring(0, end);
            }
            if (size == -1) {
                Align(4);
                size = Int();
            }
            Require(type >= 1 && type <= 16 && size > 0 && size <= data.Length, "不支持的存档字段。");
            Align(size);
            int start = Take(size);
            if (!keep || type == 16) return null;
            bool signed = type == 1 || type == 3 || type == 5 || type == 7 || type == 9;
            switch (size) {
                case 1: return signed ? (int)(sbyte)data[start] : (int)data[start];
                case 2: return signed ? (int)BitConverter.ToInt16(data, start) : (int)BitConverter.ToUInt16(data, start);
                case 4: return BitConverter.ToInt32(data, start);
                case 8: return BitConverter.ToInt64(data, start);
                // Keep the stream aligned. Each consumer validates its own expected fields.
                default: return null;
            }
        }
    }
    static T Get<T>(Dictionary<string, object> obj, string key)
    {
        object value;
        if (obj == null || !obj.TryGetValue(key, out value) || !(value is T))
            throw new InvalidDataException("未能识别存档字段 " + key + "，请检查游戏版本。");
        return (T)value;
    }
    static string ReadHunterName(Dictionary<string, object> slot)
    {
        object basic, name;
        if (!slot.TryGetValue("_BasicData", out basic)) return null;
        var fields = basic as Dictionary<string, object>;
        return fields != null && fields.TryGetValue("CharName", out name) ? name as string : null;
    }
    static void CountDecorations(Dictionary<int, int> counts, int id, int quantity)
    {
        int previous;
        counts.TryGetValue(id, out previous);
        counts[id] = checked(previous + quantity);
    }

    static void ReadDecorations(OfflineCharacter character, Dictionary<string, object> equipment, object[] box)
    {
        try
        {
            // cAccessoryWork.ID is ACCESSORY_ID; -1 is INVALID and 0 is a real jewel.
            foreach (var entry in Get<object[]>(equipment, "_AccessoryBox"))
            {
                var stack = entry as Dictionary<string, object>;
                int id = Get<int>(stack, "ID"), quantity = Get<int>(stack, "Num");
                Require(quantity >= 0 && quantity <= UInt16.MaxValue && id >= -1 && (id >= 0 || quantity == 0),
                    "未能识别装饰珠库存记录。");
                if (quantity > 0) CountDecorations(character.DecorationCounts, id, quantity);
            }

            // The equipment box contains one entry per physical piece, including worn gear.
            foreach (var entry in box)
            {
                var gear = entry as Dictionary<string, object>;
                if ((Get<int>(gear, "Category_Gender") & 0x80) != 0) continue;
                foreach (object socket in Get<object[]>(gear, "EquipmentAccessoryIdArray"))
                {
                    Require(socket is int && (int)socket >= -1, "未能识别装备上镶嵌的装饰珠。");
                    int id = (int)socket;
                    if (id >= 0) CountDecorations(character.MountedDecorationCounts, id, 1);
                }
            }
        }
        catch (InvalidDataException e) { character.DecorationError = e.Message; }
        catch (OverflowException) { character.DecorationError = "装饰珠数量超出支持范围。"; }
        if (character.DecorationError != null)
        {
            character.DecorationCounts.Clear();
            character.MountedDecorationCounts.Clear();
        }
    }
    internal static List<OfflineCharacter> Read(byte[] save, ulong steamId)
    {
        return ReadCharacters(Unpack(save, steamId));
    }

    static List<OfflineCharacter> ReadCharacters(byte[] plain)
    {
        var fields = new Fields(plain);
        object[] slots = null;
        while (fields.Pos + 12 <= plain.Length) {
            fields.Take(4);
            var root = fields.Object(0, true);
            if (root.ContainsKey("_Data")) {
                slots = Get<object[]>(root, "_Data");
                break;
            }
        }
        Require(slots != null && slots.Length > 0, "此文件没有角色存档栏。");
        var result = new List<OfflineCharacter>();
        for (int index = 0; index < slots.Length; index++) {
            var slot = slots[index] as Dictionary<string, object>;
            var equipment = Get<Dictionary<string, object>>(slot, "_Equip");
            var box = Get<object[]>(equipment, "_EquipBox");
            var equipped = Get<object[]>(Get<Dictionary<string, object>>(equipment, "_EquipIndex"), "Index");
            var character = new OfflineCharacter { Slot = index + 1, Name = ReadHunterName(slot) };
            bool started = Get<long>(slot, "PlayStartDateTime") != 0;
            for (int i = 0; i < box.Length; i++) {
                var item = box[i] as Dictionary<string, object>;
                int flags = Get<int>(item, "Category_Gender");
                if ((flags & 0x80) != 0) continue;
                started = true;
                // Category is stored above the three low gender/appearance bits. Amulet = 2.
                if (((flags >> 3) & 15) != 2) continue;
                var packed = Get<object[]>(item, "BowgunCustomizeId");
                Require(packed.Length >= 4, "护石的技能和插槽字段不完整。");
                var talisman = new OfflineTalisman {
                    FreeVal0 = Get<int>(item, "FreeVal0"), FreeVal1 = Get<int>(item, "FreeVal1"),
                    Packed = Array.ConvertAll(packed, v => (int)v),
                    Favorite = Get<int>(item, "FreeVal4") != 0,
                    Equipped = equipped.Length > 6 && (int)equipped[6] == i
                };
                character.Talismans.Add(talisman);
            }
            if (started)
            {
                ReadDecorations(character, equipment, box);
                result.Add(character);
            }
        }
        return result;
    }
}
