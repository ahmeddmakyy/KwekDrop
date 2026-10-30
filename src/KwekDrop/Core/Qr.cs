using System;
using System.Collections.Generic;
using System.Text;

namespace KwekDrop;

public enum QrCorrection { L, M, Q, H }
public enum Correction { L, M, Q, H }

public sealed class QrMatrix
{
    private readonly bool[,] modules;
    private readonly bool[,] functionModules;

    public int Size { get; }
    public int Version { get; }
    public QrCorrection Correction { get; }
    public int Mask { get; }

    internal QrMatrix(int size, int version, QrCorrection level, int mask, bool[,] modules, bool[,] functionModules)
    {
        Size = size;
        Version = version;
        Correction = level;
        Mask = mask;
        this.modules = modules;
        this.functionModules = functionModules;
    }

    public bool IsDark(int x, int y)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return false;
        return modules[x, y];
    }

    public bool IsFunction(int x, int y)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return false;
        return functionModules[x, y];
    }

    public bool IsFinder(int x, int y) => IsFinder(x, y, out _);

    public bool IsFinder(int x, int y, out int eyeIndex)
    {
        eyeIndex = -1;
        if (x < 0 || x >= Size || y < 0 || y >= Size) return false;
        if (x < 7 && y < 7) { eyeIndex = 0; return true; } // Top-left
        if (x >= Size - 7 && y < 7) { eyeIndex = 1; return true; } // Top-right
        if (x < 7 && y >= Size - 7) { eyeIndex = 2; return true; } // Bottom-left
        return false;
    }

    public static QrMatrix? Encode(string text, QrCorrection level = QrCorrection.M)
    {
        if (text == null) return null;
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        return Encode(utf8, level);
    }

    public static QrMatrix? Encode(string text, Correction level) =>
        Encode(text, (QrCorrection)level);

    public static QrMatrix? Encode(byte[] data, QrCorrection level = QrCorrection.M)
    {
        if (data == null) return null;
        int targetVersion = -1;
        for (int v = 1; v <= 40; v++)
        {
            int capacity = GetMaxDataBytes(v, level);
            if (data.Length <= capacity)
            {
                targetVersion = v;
                break;
            }
        }
        if (targetVersion == -1) return null;

        return Generate(data, targetVersion, level);
    }

    private static int GetMaxDataBytes(int version, QrCorrection level)
    {
        int totalCodewords = GetNumRawDataModules(version) / 8;
        int eccPerBlock = EccCodewordsPerBlock[(int)level, version];
        int numBlocks = NumErrorCorrectionBlocks[(int)level, version];
        int totalDataCodewords = totalCodewords - (eccPerBlock * numBlocks);
        int countBits = version < 10 ? 8 : 16;
        int headerBits = 4 + countBits;
        int availableDataBits = (totalDataCodewords * 8) - headerBits;
        if (availableDataBits < 0) return 0;
        return availableDataBits / 8;
    }

    private static QrMatrix Generate(byte[] data, int version, QrCorrection level)
    {
        int size = 17 + 4 * version;
        var func = new bool[size, size];
        var grid = new bool[size, size];

        // 1. Mark function patterns
        MarkFinderPatterns(func, grid, size);
        MarkTimingPatterns(func, grid, size);
        MarkAlignmentPatterns(func, grid, version, size);
        MarkFormatAndVersionAreas(func, size, version);
        DrawVersionBits(grid, size, version);

        // 2. Encode bit stream (Byte Mode UTF-8)
        byte[] fullCodewords = BuildCodewords(data, version, level);

        // 3. Draw codewords into grid (zigzag scan)
        DrawCodewords(grid, func, size, fullCodewords);

        // 4. Evaluate masks and pick the best mask
        int bestMask = 0;
        long lowestPenalty = long.MaxValue;
        bool[,] bestGrid = new bool[size, size];

        for (int mask = 0; mask < 8; mask++)
        {
            var testGrid = (bool[,])grid.Clone();
            ApplyMask(testGrid, func, size, mask);
            DrawFormatBits(testGrid, size, level, mask);
            long penalty = CalculatePenalty(testGrid, size);
            if (penalty < lowestPenalty)
            {
                lowestPenalty = penalty;
                bestMask = mask;
                bestGrid = testGrid;
            }
        }

        return new QrMatrix(size, version, level, bestMask, bestGrid, func);
    }

    private static void MarkFinderPatterns(bool[,] func, bool[,] grid, int size)
    {
        int[] origins = [0, size - 7];
        foreach (int ox in origins)
        {
            foreach (int oy in origins)
            {
                if (ox == size - 7 && oy == size - 7) continue; // bottom-right has no finder

                // 7x7 finder + 1 module separator border
                for (int dy = -1; dy <= 7; dy++)
                {
                    for (int dx = -1; dx <= 7; dx++)
                    {
                        int x = ox + dx;
                        int y = oy + dy;
                        if (x >= 0 && x < size && y >= 0 && y < size)
                        {
                            func[x, y] = true;
                            if (dx >= 0 && dx < 7 && dy >= 0 && dy < 7)
                            {
                                bool isDark = dx == 0 || dx == 6 || dy == 0 || dy == 6 ||
                                              (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4);
                                grid[x, y] = isDark;
                            }
                            else
                            {
                                grid[x, y] = false; // separator
                            }
                        }
                    }
                }
            }
        }
    }

    private static void MarkTimingPatterns(bool[,] func, bool[,] grid, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            if (!func[i, 6])
            {
                func[i, 6] = true;
                grid[i, 6] = (i % 2 == 0);
            }
            if (!func[6, i])
            {
                func[6, i] = true;
                grid[6, i] = (i % 2 == 0);
            }
        }
    }

    private static void MarkAlignmentPatterns(bool[,] func, bool[,] grid, int version, int size)
    {
        if (version < 2) return;
        int[] positions = GetAlignmentPositions(version);
        for (int i = 0; i < positions.Length; i++)
        {
            for (int j = 0; j < positions.Length; j++)
            {
                int cx = positions[i];
                int cy = positions[j];
                // Skip if overlapping finder patterns
                if ((i == 0 && j == 0) || (i == 0 && j == positions.Length - 1) || (i == positions.Length - 1 && j == 0))
                    continue;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        func[x, y] = true;
                        bool isDark = Math.Abs(dx) == 2 || Math.Abs(dy) == 2 || (dx == 0 && dy == 0);
                        grid[x, y] = isDark;
                    }
                }
            }
        }
    }

    private static void MarkFormatAndVersionAreas(bool[,] func, int size, int version)
    {
        // Format bits area around finder patterns
        for (int i = 0; i <= 8; i++)
        {
            func[8, i] = true;
            func[i, 8] = true;
        }
        for (int i = 0; i <= 7; i++)
        {
            func[size - 1 - i, 8] = true;
            func[8, size - 1 - i] = true;
        }
        func[8, size - 8] = true; // Dark module

        // Version blocks for version >= 7
        if (version >= 7)
        {
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    func[x, size - 11 + y] = true;
                    func[size - 11 + y, x] = true;
                }
            }
        }
    }

    private static byte[] BuildCodewords(byte[] data, int version, QrCorrection level)
    {
        int totalRawCodewords = GetNumRawDataModules(version) / 8;
        int eccPerBlock = EccCodewordsPerBlock[(int)level, version];
        int numBlocks = NumErrorCorrectionBlocks[(int)level, version];
        int totalDataCodewords = totalRawCodewords - (eccPerBlock * numBlocks);

        // Bit stream
        var bitList = new List<bool>();
        void AppendBits(int val, int bitCount)
        {
            for (int i = bitCount - 1; i >= 0; i--)
                bitList.Add(((val >> i) & 1) != 0);
        }

        // Byte mode indicator: 0100
        AppendBits(0x04, 4);
        int countBits = version < 10 ? 8 : 16;
        AppendBits(data.Length, countBits);
        foreach (byte b in data)
            AppendBits(b, 8);

        // Terminator (up to 4 zeroes)
        int capacityBits = totalDataCodewords * 8;
        int term = Math.Min(4, capacityBits - bitList.Count);
        for (int i = 0; i < term; i++) bitList.Add(false);

        // Pad to byte boundary
        while (bitList.Count % 8 != 0) bitList.Add(false);

        // Pad bytes 0xEC and 0x11
        byte[] padBytes = [0xEC, 0x11];
        int padIdx = 0;
        while (bitList.Count < capacityBits)
        {
            AppendBits(padBytes[padIdx % 2], 8);
            padIdx++;
        }

        var dataBytes = new byte[totalDataCodewords];
        for (int i = 0; i < totalDataCodewords; i++)
        {
            byte b = 0;
            for (int bit = 0; bit < 8; bit++)
                if (bitList[i * 8 + bit]) b |= (byte)(1 << (7 - bit));
            dataBytes[i] = b;
        }

        // Divide data into blocks and compute Reed-Solomon EC for each block
        int shortBlockLen = totalDataCodewords / numBlocks;
        int numShortBlocks = numBlocks - (totalDataCodewords % numBlocks);
        byte[] divisor = ComputeDivisor(eccPerBlock);

        var dataBlocks = new byte[numBlocks][];
        var eccBlocks = new byte[numBlocks][];
        int offset = 0;

        for (int i = 0; i < numBlocks; i++)
        {
            int blockLen = shortBlockLen + (i < numShortBlocks ? 0 : 1);
            var block = new byte[blockLen];
            Array.Copy(dataBytes, offset, block, 0, blockLen);
            offset += blockLen;
            dataBlocks[i] = block;
            eccBlocks[i] = ComputeRemainder(block, divisor);
        }

        // Interleave data codewords, then error correction codewords
        var finalResult = new byte[totalRawCodewords];
        int dst = 0;
        int maxDataLen = shortBlockLen + (numShortBlocks < numBlocks ? 1 : 0);

        for (int j = 0; j < maxDataLen; j++)
        {
            for (int b = 0; b < numBlocks; b++)
            {
                if (j < dataBlocks[b].Length)
                    finalResult[dst++] = dataBlocks[b][j];
            }
        }

        for (int j = 0; j < eccPerBlock; j++)
        {
            for (int b = 0; b < numBlocks; b++)
            {
                finalResult[dst++] = eccBlocks[b][j];
            }
        }

        return finalResult;
    }

    private static void DrawCodewords(bool[,] grid, bool[,] func, int size, byte[] codewords)
    {
        int bitIdx = 0;
        int totalBits = codewords.Length * 8;

        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5; // skip vertical timing column
            for (int vert = 0; vert < size; vert++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? size - 1 - vert : vert;

                    if (!func[x, y] && bitIdx < totalBits)
                    {
                        byte b = codewords[bitIdx >> 3];
                        bool dark = ((b >> (7 - (bitIdx & 7))) & 1) != 0;
                        grid[x, y] = dark;
                        bitIdx++;
                    }
                }
            }
        }
    }

    private static void ApplyMask(bool[,] grid, bool[,] func, int size, int mask)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (func[x, y]) continue;
                bool invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => (x * y) % 2 + (x * y) % 3 == 0,
                    6 => ((x * y) % 2 + (x * y) % 3) % 2 == 0,
                    7 => (((x + y) % 2) + ((x * y) % 3)) % 2 == 0,
                    _ => false
                };
                grid[x, y] ^= invert;
            }
        }
    }

    public static int GetFormatBits(QrCorrection level, int mask)
    {
        int[] table = [1, 0, 3, 2]; // L->1, M->0, Q->3, H->2
        int data = (table[(int)level] << 3) | (mask & 7);
        int rem = data;
        for (int i = 0; i < 10; i++)
            rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        return ((data << 10) | rem) ^ 0x5412;
    }

    private static void DrawFormatBits(bool[,] grid, int size, QrCorrection level, int mask)
    {
        int bits = GetFormatBits(level, mask);

        // Copy 1 (around top-left)
        for (int i = 0; i <= 5; i++)
            grid[8, i] = ((bits >> i) & 1) != 0;
        grid[8, 7] = ((bits >> 6) & 1) != 0;
        grid[8, 8] = ((bits >> 7) & 1) != 0;
        grid[7, 8] = ((bits >> 8) & 1) != 0;
        for (int i = 9; i < 15; i++)
            grid[14 - i, 8] = ((bits >> i) & 1) != 0;

        // Copy 2 (around other finders)
        for (int i = 0; i < 8; i++)
            grid[size - 1 - i, 8] = ((bits >> i) & 1) != 0;
        for (int i = 8; i < 15; i++)
            grid[8, size - 15 + i] = ((bits >> i) & 1) != 0;
        grid[8, size - 8] = true; // Dark module: always dark
    }

    private static void DrawVersionBits(bool[,] grid, int size, int version)
    {
        if (version < 7) return;
        int rem = version;
        for (int i = 0; i < 12; i++)
            rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        long bits = ((long)version << 12) | (long)rem;

        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int k = size - 11 + j;
                bool dark = (bits & 1) != 0;
                grid[k, i] = dark;
                grid[i, k] = dark;
                bits >>= 1;
            }
        }
    }

    private static long CalculatePenalty(bool[,] grid, int size)
    {
        long penalty = 0;

        // N1 & N3: Rows
        for (int y = 0; y < size; y++)
        {
            bool runColor = false;
            int runCount = 0;
            int[] history = new int[7];
            for (int x = 0; x < size; x++)
            {
                if (grid[x, y] == runColor)
                {
                    runCount++;
                    if (runCount == 5) penalty += 3;
                    else if (runCount > 5) penalty++;
                }
                else
                {
                    AddHistory(runCount, history, size);
                    if (!runColor && CheckPattern(history, size))
                        penalty += 40;
                    runColor = grid[x, y];
                    runCount = 1;
                }
            }
            penalty += TerminatePattern(runColor, runCount, history, size) * 40;
        }

        // N1 & N3: Columns
        for (int x = 0; x < size; x++)
        {
            bool runColor = false;
            int runCount = 0;
            int[] history = new int[7];
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == runColor)
                {
                    runCount++;
                    if (runCount == 5) penalty += 3;
                    else if (runCount > 5) penalty++;
                }
                else
                {
                    AddHistory(runCount, history, size);
                    if (!runColor && CheckPattern(history, size))
                        penalty += 40;
                    runColor = grid[x, y];
                    runCount = 1;
                }
            }
            penalty += TerminatePattern(runColor, runCount, history, size) * 40;
        }

        // N2: 2x2 blocks of same color
        for (int y = 0; y < size - 1; y++)
        {
            for (int x = 0; x < size - 1; x++)
            {
                bool c = grid[x, y];
                if (c == grid[x + 1, y] && c == grid[x, y + 1] && c == grid[x + 1, y + 1])
                    penalty += 3;
            }
        }

        // N4: Balance of dark and light modules
        int darkCount = 0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (grid[x, y]) darkCount++;

        int total = size * size;
        int k = (int)((Math.Abs(darkCount * 20L - total * 10L) + total - 1) / total) - 1;
        if (k > 0) penalty += k * 10;

        return penalty;
    }

    private static bool CheckPattern(int[] h, int size)
    {
        int n = h[1];
        if (n <= 0) return false;
        bool core = h[2] == n && h[3] == n * 3 && h[4] == n && h[5] == n;
        if (!core) return false;
        return (h[0] >= n * 4 && h[6] >= n) || (h[6] >= n * 4 && h[0] >= n);
    }

    private static int TerminatePattern(bool color, int count, int[] h, int size)
    {
        if (color)
        {
            AddHistory(count, h, size);
            count = 0;
        }
        count += size;
        AddHistory(count, h, size);
        return CheckPattern(h, size) ? 1 : 0;
    }

    private static void AddHistory(int count, int[] h, int size)
    {
        if (h[0] == 0) count += size;
        Array.Copy(h, 0, h, 1, 6);
        h[0] = count;
    }

    // Reed-Solomon & GF(256) Math
    public static byte GfMultiply(byte x, byte y)
    {
        byte z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (byte)((z << 1) ^ ((z >> 7) * 0x11D));
            z ^= (byte)(((y >> i) & 1) * x);
        }
        return z;
    }

    public static byte[] ComputeDivisor(int degree)
    {
        var res = new byte[degree];
        res[degree - 1] = 1;
        byte root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < degree; j++)
            {
                res[j] = GfMultiply(res[j], root);
                if (j + 1 < degree) res[j] ^= res[j + 1];
            }
            root = GfMultiply(root, 0x02);
        }
        return res;
    }

    public static byte[] ComputeRemainder(byte[] data, byte[] divisor)
    {
        int degree = divisor.Length;
        var res = new byte[degree];
        for (int i = 0; i < data.Length; i++)
        {
            byte factor = (byte)(data[i] ^ res[0]);
            Array.Copy(res, 1, res, 0, degree - 1);
            res[degree - 1] = 0;
            for (int j = 0; j < degree; j++)
                res[j] ^= GfMultiply(divisor[j], factor);
        }
        return res;
    }

    public static byte EvaluatePolynomial(byte[] polynomial, byte x)
    {
        byte val = 0;
        foreach (byte coef in polynomial)
            val = (byte)(GfMultiply(val, x) ^ coef);
        return val;
    }

    public static int GetNumRawDataModules(int ver)
    {
        int result = (16 * ver + 128) * ver + 64;
        if (ver >= 2)
        {
            int numAlign = ver / 7 + 2;
            result -= (25 * numAlign - 10) * numAlign - 55;
            if (ver >= 7) result -= 36;
        }
        return result;
    }

    public static int[] GetAlignmentPositions(int version)
    {
        if (version == 1) return [];
        int numAlign = version / 7 + 2;
        int step = (version * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4) * 2;
        var result = new int[numAlign];
        for (int i = numAlign - 1, pos = version * 4 + 10; i >= 1; i--, pos -= step)
            result[i] = pos;
        result[0] = 6;
        return result;
    }

    // Lookup tables from ISO/IEC 18004
    public static readonly sbyte[,] EccCodewordsPerBlock = {
        // 0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18  19  20  21  22  23  24  25  26  27  28  29  30  31  32  33  34  35  36  37  38  39  40
        { -1,  7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }, // L
        { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 }, // M
        { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }, // Q
        { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }, // H
    };

    public static readonly sbyte[,] NumErrorCorrectionBlocks = {
        // 0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18  19  20  21  22  23  24  25  26  27  28  29  30  31  32  33  34  35  36  37  38  39  40
        { -1,  1,  1,  1,  1,  1,  2,  2,  2,  2,  4,  4,  4,  4,  4,  6,  6,  6,  6,  7,  8,  8,  9,  9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25 }, // L
        { -1,  1,  1,  1,  2,  2,  4,  4,  4,  5,  5,  5,  8,  9,  9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 }, // M
        { -1,  1,  1,  2,  2,  4,  4,  6,  6,  8,  8,  8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68 }, // Q
        { -1,  1,  1,  2,  4,  4,  4,  5,  6,  8,  8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81 }, // H
    };
}

public static class QrContent
{
    public static string Wifi(string ssid, string password, string security = "WPA", bool hidden = false)
    {
        string sec = security?.Trim().ToUpperInvariant() switch
        {
            "WEP" => "WEP",
            "NONE" or "NOPASS" => "nopass",
            _ => "WPA"
        };

        var sb = new StringBuilder();
        sb.Append("WIFI:T:").Append(sec).Append(';');
        sb.Append("S:").Append(Escape(ssid ?? "")).Append(';');
        if (sec != "nopass" && !string.IsNullOrEmpty(password))
        {
            sb.Append("P:").Append(Escape(password)).Append(';');
        }
        if (hidden)
        {
            sb.Append("H:true;");
        }
        sb.Append(';');
        return sb.ToString();
    }

    public static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 4);
        foreach (char c in s)
        {
            if (c is '\\' or ';' or ',' or ':' or '"')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
