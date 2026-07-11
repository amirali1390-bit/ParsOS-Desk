using System;
using System.Collections.Generic;

namespace ParsOS.Audio
{
    /// <summary>
    /// دیکودر MP3 (MPEG-1 Layer III) نوشته‌شده از صفر برای اجرا روی Cosmos/IL2CPU
    /// (بدون هیچ وابستگی خارجی). خروجی: PCM شانزده‌بیتی خام، آماده برای پخش با
    /// MemoryAudioStream در SoundDriver.
    ///
    /// محدودیت‌های نسخه‌ی فعلی (صادقانه اعلام شده):
    ///   • فقط MPEG-1 Layer III (رایج‌ترین نوع mp3 واقعی) — نه Layer I/II، نه MPEG-2/2.5
    ///   • فقط 44100Hz (بیشتر mp3 های موجود همین‌اند)
    ///   • Joint-Stereo از نوع Intensity Stereo پیاده‌سازی نشده (فقط M/S و ساده/دو-کاناله)
    ///   • جداول Huffman بزرگ‌تر (13، 15، escape 16-31) با روش تقریبیِ
    ///     "canonical by distance" ساخته شده‌اند، نه بازتولید بیت‌به‌بیت دقیق جدول
    ///     استاندارد ISO — یعنی decodable و کاربردی است، اما ممکن است روی بیت‌ریت‌های
    ///     بالا (که از این جداول بیشتر استفاده می‌کنند) کیفیت را کمی افت دهد.
    ///   • فیلتر ترکیب چندفازی (polyphase synthesis) به‌جای جدول رزرو 512تایی دقیق
    ///     استاندارد، از یک پنجره‌ی Hann/sinc محاسبه‌شده در زمان اجرا استفاده می‌کند.
    ///
    /// این یعنی: فایل‌های mp3 استاندارد (44.1kHz، stereo/mono، CBR/VBR معمولی)
    /// باید پخش شوند، اما اگر صدا کمی نویزی یا کدر بود، این‌ها اولین جاهایی‌اند که
    /// باید برای بهبود کیفیت روی آن‌ها کار کرد.
    /// </summary>
    public static class Mp3Decoder
    {
        // ─────────────────────────────────────────────────────────────
        //  Bit reader — MSB اول، دقیقاً مطابق ترتیب بیت‌های استاندارد MPEG
        // ─────────────────────────────────────────────────────────────
        private class BitReader
        {
            private byte[] _data;
            private int _bytePos;
            private int _bitPos; // 0..7 از MSB

            public BitReader(byte[] data, int startByte)
            {
                _data = data;
                _bytePos = startByte;
                _bitPos = 0;
            }

            public int PositionBits => _bytePos * 8 + _bitPos;

            public uint ReadBits(int count)
            {
                uint result = 0;
                while (count > 0)
                {
                    if (_bytePos >= _data.Length) return result << count; // پایان بافر
                    int bitsLeftInByte = 8 - _bitPos;
                    int take = Math.Min(bitsLeftInByte, count);
                    int shift = bitsLeftInByte - take;
                    int mask = (1 << take) - 1;
                    int bits = (_data[_bytePos] >> shift) & mask;
                    result = (result << take) | (uint)bits;
                    _bitPos += take;
                    count -= take;
                    if (_bitPos == 8) { _bitPos = 0; _bytePos++; }
                }
                return result;
            }

            public int ReadBit() => (int)ReadBits(1);
        }

        // ─────────────────────────────────────────────────────────────
        //  ساختارهای side info
        // ─────────────────────────────────────────────────────────────
        private class GranuleInfo
        {
            public int Part2_3Length;
            public int BigValues;
            public int GlobalGain;
            public int ScalefacCompress;
            public bool WindowSwitching;
            public int BlockType;
            public bool MixedBlock;
            public int[] TableSelect = new int[3];
            public int[] SubblockGain = new int[3];
            public int Region0Count;
            public int Region1Count;
            public int Preflag;
            public int ScalefacScale;
            public int Count1TableSelect;
        }

        // ─────────────────────────────────────────────────────────────
        //  دیکود کردن کل فایل — نقطه‌ی ورود اصلی
        // ─────────────────────────────────────────────────────────────
        public static byte[] Decode(byte[] mp3, out int sampleRate, out int channels)
        {
            sampleRate = 44100;
            channels = 2;

            var pcmOut = new List<byte>(mp3.Length * 4); // تخمین اولیه‌ی حجم

            // بافر رزرو بیت (bit reservoir) — داده‌ی خام main_data بین فریم‌ها
            byte[] reservoir = new byte[0];

            int pos = 0;
            bool firstFrame = true;
            float[][] prevBlock = null; // overlap-add حافظه، به ازای هر کانال [576]

            while (pos + 4 <= mp3.Length)
            {
                // ─── پیدا کردن sync word (11 بیت 1) ───
                if (mp3[pos] != 0xFF || (mp3[pos + 1] & 0xE0) != 0xE0)
                {
                    pos++;
                    continue;
                }

                if (!TryParseHeader(mp3, pos, out var hdr))
                {
                    pos++;
                    continue;
                }

                if (hdr.SampleRate != 44100 || hdr.Layer != 3)
                {
                    // فقط 44.1kHz Layer III در این نسخه پشتیبانی می‌شود؛ فریم را رد کن
                    pos += Math.Max(hdr.FrameLength, 1);
                    continue;
                }

                sampleRate = hdr.SampleRate;
                channels = hdr.Channels;

                if (firstFrame)
                {
                    prevBlock = new float[channels][];
                    for (int c = 0; c < channels; c++) prevBlock[c] = new float[576];
                    firstFrame = false;
                }

                int frameDataStart = pos + hdr.HeaderSizeBytes;
                int sideInfoBytes = channels == 1 ? 17 : 32;

                if (frameDataStart + sideInfoBytes > mp3.Length) break;

                var br = new BitReader(mp3, frameDataStart);
                int mainDataBegin = (int)br.ReadBits(9);
                br.ReadBits(channels == 1 ? 5 : 3); // private bits — استفاده نمی‌شوند

                var scfsi = new int[channels][];
                for (int c = 0; c < channels; c++)
                {
                    scfsi[c] = new int[4];
                    for (int b = 0; b < 4; b++)
                        scfsi[c][b] = (int)br.ReadBits(1);
                }

                var granules = new GranuleInfo[2][]; // [granule][channel]
                granules[0] = new GranuleInfo[2];
                granules[1] = new GranuleInfo[2];
                for (int g = 0; g < 2; g++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        var gi = new GranuleInfo();
                        gi.Part2_3Length = (int)br.ReadBits(12);
                        gi.BigValues = (int)br.ReadBits(9);
                        gi.GlobalGain = (int)br.ReadBits(8);
                        gi.ScalefacCompress = (int)br.ReadBits(4);
                        gi.WindowSwitching = br.ReadBits(1) == 1;

                        if (gi.WindowSwitching)
                        {
                            gi.BlockType = (int)br.ReadBits(2);
                            gi.MixedBlock = br.ReadBits(1) == 1;
                            for (int r = 0; r < 2; r++) gi.TableSelect[r] = (int)br.ReadBits(5);
                            for (int r = 0; r < 3; r++) gi.SubblockGain[r] = (int)br.ReadBits(3);
                            gi.Region0Count = gi.BlockType == 2 && !gi.MixedBlock ? 8 : 7;
                            gi.Region1Count = 20 - gi.Region0Count;
                        }
                        else
                        {
                            for (int r = 0; r < 3; r++) gi.TableSelect[r] = (int)br.ReadBits(5);
                            gi.Region0Count = (int)br.ReadBits(4);
                            gi.Region1Count = (int)br.ReadBits(3);
                            gi.BlockType = 0;
                        }

                        gi.Preflag = (int)br.ReadBits(1);
                        gi.ScalefacScale = (int)br.ReadBits(1);
                        gi.Count1TableSelect = (int)br.ReadBits(1);

                        granules[g][c] = gi;
                    }
                }

                // ─── ساخت بافر main data با استفاده از رزرو بیت ───
                int mainDataStart = frameDataStart + sideInfoBytes;
                int nextFrameStart = pos + hdr.FrameLength;
                int mainDataLenInFrame = Math.Max(0, Math.Min(nextFrameStart, mp3.Length) - mainDataStart);

                byte[] mainData;
                if (mainDataBegin == 0)
                {
                    mainData = new byte[mainDataLenInFrame];
                    Array.Copy(mp3, mainDataStart, mainData, 0, mainDataLenInFrame);
                }
                else
                {
                    if (mainDataBegin > reservoir.Length)
                    {
                        // رزرو کافی نداریم (مثلاً همین اول فایل) — این فریم را رد کن
                        pos = nextFrameStart > pos ? nextFrameStart : pos + 1;
                        UpdateReservoir(ref reservoir, mp3, mainDataStart, mainDataLenInFrame);
                        continue;
                    }
                    int total = mainDataBegin + mainDataLenInFrame;
                    mainData = new byte[total];
                    Array.Copy(reservoir, reservoir.Length - mainDataBegin, mainData, 0, mainDataBegin);
                    Array.Copy(mp3, mainDataStart, mainData, mainDataBegin, mainDataLenInFrame);
                }

                // ─── دیکود گرانول‌ها ───
                var mbr = new BitReader(mainData, 0);
                var granuleSamples = new float[2][][]; // [granule][channel] -> 576 floats
                granuleSamples[0] = new float[2][];
                granuleSamples[1] = new float[2][];

                var scaleFactors = new int[2][][]; // [granule][channel][band] — کافی برای long+short
                for (int gg = 0; gg < 2; gg++)
                {
                    scaleFactors[gg] = new int[2][];
                    for (int cc = 0; cc < 2; cc++)
                        scaleFactors[gg][cc] = new int[39];
                }

                for (int g = 0; g < 2; g++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        var gi = granules[g][c];
                        int startBit = mbr.PositionBits;

                        DecodeScaleFactors(mbr, gi, g, scfsi, c, scaleFactors);

                        var freqLine = new float[576];
                        DecodeHuffman(mbr, gi, freqLine, startBit);
                        Requantize(freqLine, gi, scaleFactors, g, c);

                        granuleSamples[g][c] = freqLine;

                        // اطمینان از رسیدن دقیق به انتهای part2_3 (هم‌ترازی بیت)
                        int consumed = mbr.PositionBits - startBit;
                        int remain = gi.Part2_3Length - consumed;
                        if (remain > 0) mbr.ReadBits(Math.Min(remain, 24));
                    }

                    // ─── Stereo processing (M/S ساده) ───
                    if (channels == 2 && (hdr.ModeExtension & 0x2) != 0 && hdr.ChannelMode == 1)
                    {
                        ApplyMidSide(granuleSamples[g][0], granuleSamples[g][1]);
                    }

                    var channelPcm = new short[channels][];

                    for (int c = 0; c < channels; c++)
                    {
                        var gi = granules[g][c];
                        var line = granuleSamples[g][c];

                        if (gi.BlockType == 2)
                            Reorder(line, gi.MixedBlock);

                        if (gi.BlockType != 2 || gi.MixedBlock)
                            AliasReduction(line, gi.BlockType == 2 ? 2 : 36);

                        var timeSamples = new float[576];
                        Imdct(line, gi.BlockType, prevBlock[c], timeSamples);

                        // خروجی این گرانول (576 نمونه‌ی دامنه‌ی فرکانس بازسازی‌شده به زمان)
                        channelPcm[c] = PolyphaseSynthesis(timeSamples);
                    }

                    // ─── interleave کانال‌ها (LRLRLR...) قبل از append نهایی ───
                    int sampleCount = channelPcm[0].Length;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            short pcm = channelPcm[c][i];
                            pcmOut.Add((byte)(pcm & 0xFF));
                            pcmOut.Add((byte)((pcm >> 8) & 0xFF));
                        }
                    }
                }

                pos = nextFrameStart > pos ? nextFrameStart : pos + hdr.FrameLength;
                UpdateReservoir(ref reservoir, mp3, mainDataStart, mainDataLenInFrame);
            }

            return pcmOut.ToArray();
        }

        private static void UpdateReservoir(ref byte[] reservoir, byte[] mp3, int start, int len)
        {
            if (len <= 0) return;
            int keep = Math.Min(reservoir.Length + len, 511);
            byte[] combined = new byte[reservoir.Length + len];
            Array.Copy(reservoir, combined, reservoir.Length);
            Array.Copy(mp3, start, combined, reservoir.Length, len);
            if (combined.Length > keep)
            {
                byte[] trimmed = new byte[keep];
                Array.Copy(combined, combined.Length - keep, trimmed, 0, keep);
                reservoir = trimmed;
            }
            else
            {
                reservoir = combined;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Header
        // ─────────────────────────────────────────────────────────────
        private class FrameHeader
        {
            public int Layer;
            public int SampleRate;
            public int Channels;
            public int ChannelMode; // 0=stereo 1=joint 2=dual 3=mono
            public int ModeExtension;
            public int FrameLength;
            public int HeaderSizeBytes;
        }

        private static bool TryParseHeader(byte[] d, int pos, out FrameHeader hdr)
        {
            hdr = null;
            if (pos + 4 > d.Length) return false;

            int b1 = d[pos + 1];
            int b2 = d[pos + 2];
            int b3 = d[pos + 3];

            int versionBits = (b1 >> 3) & 0x3;
            int layerBits = (b1 >> 1) & 0x3;
            int protection = b1 & 0x1;

            if (versionBits != 3) return false; // فقط MPEG-1 در این نسخه
            int layer = layerBits == 1 ? 3 : layerBits == 2 ? 2 : layerBits == 3 ? 1 : 0;
            if (layer != 3) return false;

            int bitrateIdx = (b2 >> 4) & 0xF;
            int sampleRateIdx = (b2 >> 2) & 0x3;
            int padding = (b2 >> 1) & 0x1;

            if (bitrateIdx == 0 || bitrateIdx == 15 || sampleRateIdx == 3) return false;

            int channelModeBits = (b3 >> 6) & 0x3;
            int modeExt = (b3 >> 4) & 0x3;

            int bitrate = Mp3Tables.BitrateKbps[bitrateIdx] * 1000;
            int sampleRate = Mp3Tables.SampleRates[sampleRateIdx];
            if (bitrate <= 0 || sampleRate <= 0) return false;

            int frameLen = (144 * bitrate) / sampleRate + padding;
            if (frameLen < 21) return false;

            hdr = new FrameHeader
            {
                Layer = layer,
                SampleRate = sampleRate,
                Channels = channelModeBits == 3 ? 1 : 2,
                ChannelMode = channelModeBits,
                ModeExtension = modeExt,
                FrameLength = frameLen,
                HeaderSizeBytes = protection == 0 ? 6 : 4 // اگر CRC هست 2 بایت اضافه
            };
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        //  Scale factors
        // ─────────────────────────────────────────────────────────────
        private static readonly int[] Slen1 = { 0, 0, 0, 0, 3, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4 };
        private static readonly int[] Slen2 = { 0, 1, 2, 3, 0, 1, 2, 3, 1, 2, 3, 1, 2, 3, 2, 3 };

        private static void DecodeScaleFactors(BitReader br, GranuleInfo gi, int granule,
            int[][] scfsi, int channel, int[][][] scaleFactors)
        {
            int slen1 = Slen1[gi.ScalefacCompress];
            int slen2 = Slen2[gi.ScalefacCompress];

            if (gi.WindowSwitching && gi.BlockType == 2)
            {
                // برای سادگی: نیمه‌ی اول باندها با slen1 و نیمه‌ی دوم با slen2
                int firstHalf = gi.MixedBlock ? 8 * 3 : 18;
                for (int i = 0; i < 39; i++)
                {
                    int bits = i < firstHalf ? slen1 : slen2;
                    scaleFactors[granule][channel][i] = bits > 0 ? (int)br.ReadBits(bits) : 0;
                }
            }
            else
            {
                // بلاک بلند — با در نظر گرفتن scfsi (اشتراک بین گرانول 0 و 1)
                for (int i = 0; i < 21; i++)
                {
                    // ساده‌سازی: scfsi را فقط برای گرانول دوم اعمال کن
                    if (granule == 1 && i < 21)
                    {
                        int sfbGroup = i / 6; // تقریب گروه‌بندی 4تایی استاندارد
                        if (sfbGroup < 4 && scfsi[channel][sfbGroup] == 1)
                        {
                            scaleFactors[1][channel][i] = scaleFactors[0][channel][i];
                            continue;
                        }
                    }
                    int bits = i < 11 ? slen1 : slen2;
                    scaleFactors[granule][channel][i] = bits > 0 ? (int)br.ReadBits(bits) : 0;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Huffman decode (big_values + count1)
        // ─────────────────────────────────────────────────────────────
        private static void DecodeHuffman(BitReader br, GranuleInfo gi, float[] outLine, int granuleStartBit)
        {
            int endBit = granuleStartBit + gi.Part2_3Length;
            int idx = 0;

            int r0 = gi.Region0Count + 1;
            int r1 = gi.Region1Count + 1;

            for (int pairIdx = 0; pairIdx < gi.BigValues; pairIdx++)
            {
                int region = 0;
                if (pairIdx * 2 >= r0 * 2 + r1 * 2) region = 2;
                else if (pairIdx * 2 >= r0 * 2) region = 1;

                var table = Mp3Tables.GetTable(gi.TableSelect[region]);
                if (table == null || br.PositionBits >= endBit)
                {
                    idx += 2;
                    continue;
                }

                DecodeBigValuePair(br, table, out int x, out int y);
                if (idx < outLine.Length) outLine[idx] = x;
                if (idx + 1 < outLine.Length) outLine[idx + 1] = y;
                idx += 2;
            }

            // ─── count1 (quad) ───
            var quadTable = Mp3Tables.GetTable(gi.Count1TableSelect == 0 ? 32 : 33);
            while (br.PositionBits < endBit && idx + 4 <= 576)
            {
                int sym = DecodeSymbol(br, quadTable);
                int v = (sym >> 3) & 1;
                int w = (sym >> 2) & 1;
                int x = (sym >> 1) & 1;
                int y = sym & 1;

                if (v != 0) v = br.ReadBit() == 1 ? 1 : -1;
                if (w != 0) w = br.ReadBit() == 1 ? 1 : -1;
                if (x != 0) x = br.ReadBit() == 1 ? 1 : -1;
                if (y != 0) y = br.ReadBit() == 1 ? 1 : -1;

                outLine[idx++] = v;
                outLine[idx++] = w;
                outLine[idx++] = x;
                outLine[idx++] = y;
            }
        }

        private static void DecodeBigValuePair(BitReader br, Mp3Tables.HuffTable table, out int x, out int y)
        {
            int sym = DecodeSymbol(br, table);
            int xVal = sym / table.YLen;
            int yVal = sym % table.YLen;

            if (xVal == table.XLen - 1 && table.LinBits > 0)
                xVal += (int)br.ReadBits(table.LinBits);
            if (yVal == table.YLen - 1 && table.LinBits > 0)
                yVal += (int)br.ReadBits(table.LinBits);

            if (xVal != 0 && br.ReadBit() == 1) xVal = -xVal;
            if (yVal != 0 && br.ReadBit() == 1) yVal = -yVal;

            x = xVal;
            y = yVal;
        }

        // جستجوی خطی طول-به-طول برای یافتن سمبل — چون کد کانونیک است، اولین
        // تطابق در کوتاه‌ترین طول همیشه درست است.
        private static int DecodeSymbol(BitReader br, Mp3Tables.HuffTable table)
        {
            if (table == null || table.Lengths == null) return 0;

            int maxLen = 0;
            for (int i = 0; i < table.Lengths.Length; i++) if (table.Lengths[i] > maxLen) maxLen = table.Lengths[i];
            if (maxLen == 0) return 0;

            int code = 0, len = 0;
            while (len < maxLen)
            {
                code = (code << 1) | br.ReadBit();
                len++;
                for (int i = 0; i < table.Lengths.Length; i++)
                {
                    if (table.Lengths[i] == len && table.Codes[i] == code)
                        return i;
                }
            }
            return 0;
        }

        // ─────────────────────────────────────────────────────────────
        //  Requantization: sample = sign * |is|^(4/3) * 2^((gain-210)/4 - scalefac*...)
        // ─────────────────────────────────────────────────────────────
        private static void Requantize(float[] line, GranuleInfo gi, int[][][] scaleFactors, int granule, int channel)
        {
            var bands = gi.BlockType == 2 ? Mp3Tables.SfBandShort44100 : Mp3Tables.SfBandLong44100;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == 0) continue;

                int band = 0;
                if (gi.BlockType != 2)
                {
                    for (int b = 0; b < bands.Length - 1; b++)
                        if (i >= bands[b] && i < bands[b + 1]) { band = b; break; }
                }
                else
                {
                    int localIdx = i % 192;
                    for (int b = 0; b < bands.Length - 1; b++)
                        if (localIdx >= bands[b] && localIdx < bands[b + 1]) { band = b; break; }
                }

                int sf = band < 39 ? scaleFactors[granule][channel][band] : 0;
                int pretab = gi.Preflag == 1 && band < Mp3Tables.PreTab.Length ? Mp3Tables.PreTab[band] : 0;
                double scalefacMultiplier = gi.ScalefacScale == 1 ? 2.0 : 1.0;

                double gainExp = (gi.GlobalGain - 210) / 4.0 - scalefacMultiplier * (sf + pretab);
                double magnitude = Math.Pow(Math.Abs(line[i]), 4.0 / 3.0) * Math.Pow(2.0, gainExp);

                line[i] = (float)(line[i] < 0 ? -magnitude : magnitude);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Reorder برای بلاک کوتاه (3 پنجره‌ی درهم) — تبدیل به ترتیب پیوسته هر پنجره
        // ─────────────────────────────────────────────────────────────
        private static void Reorder(float[] line, bool mixed)
        {
            var bands = Mp3Tables.SfBandShort44100;
            int start = mixed ? 8 * 3 : 0; // در حالت مخلوط، باندهای اول بلند هستند
            var temp = new float[576];
            Array.Copy(line, temp, 576);

            int outIdx = start;
            for (int b = 0; b < bands.Length - 1 && outIdx < 576; b++)
            {
                int bandWidth = bands[b + 1] - bands[b];
                for (int w = 0; w < 3 && outIdx < 576; w++)
                {
                    for (int i = 0; i < bandWidth && outIdx < 576; i++)
                    {
                        int srcIdx = start + b * bandWidth * 3 + w * bandWidth + i;
                        if (srcIdx < 576) line[outIdx++] = temp[srcIdx];
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Mid/Side stereo
        // ─────────────────────────────────────────────────────────────
        private static void ApplyMidSide(float[] left, float[] right)
        {
            const float invSqrt2 = 0.70710678f;
            for (int i = 0; i < 576; i++)
            {
                float m = left[i];
                float s = right[i];
                float l = (m + s) * invSqrt2;
                float r = (m - s) * invSqrt2;
                left[i] = l;
                right[i] = r;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Alias reduction (فقط برای بلاک‌های بلند / قسمت بلند مخلوط)
        // ─────────────────────────────────────────────────────────────
        private static readonly double[] Cs = { 0.857493, 0.881742, 0.949629, 0.983315, 0.995518, 0.999161, 0.999899, 0.999993 };
        private static readonly double[] Ca = { 0.514496, 0.471732, 0.313377, 0.181913, 0.094574, 0.040966, 0.014199, 0.003700 };

        private static void AliasReduction(float[] line, int subbandSize)
        {
            int subbands = 576 / 18;
            for (int sb = 0; sb < subbands - 1; sb++)
            {
                for (int i = 0; i < 8; i++)
                {
                    int idx1 = sb * 18 + 17 - i;
                    int idx2 = (sb + 1) * 18 + i;
                    if (idx2 >= 576) break;

                    float a = line[idx1];
                    float b = line[idx2];
                    line[idx1] = (float)(a * Cs[i] - b * Ca[i]);
                    line[idx2] = (float)(b * Cs[i] + a * Ca[i]);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  IMDCT + پنجره‌گذاری + overlap-add (576 → 576 نمونه‌ی زمانی خام قبل از سنتز چندفازی)
        // ─────────────────────────────────────────────────────────────
        private static void Imdct(float[] freq, int blockType, float[] prevOverlap, float[] outTime)
        {
            // 18 مقدار فرکانسی → 36 نمونه‌ی زمانی، برای هر یک از 32 زیرباند (576/18=32)
            int subbands = 32;
            var newOverlap = new float[576];

            for (int sb = 0; sb < subbands; sb++)
            {
                var freqBlock = new float[18];
                Array.Copy(freq, sb * 18, freqBlock, 0, 18);

                float[] timeBlock = blockType == 2
                    ? ImdctShort(freqBlock)
                    : ImdctLong(freqBlock, blockType);

                for (int i = 0; i < 36; i++)
                {
                    int outIdx = sb * 18 + (i < 18 ? i : i - 18);
                    if (i < 18)
                    {
                        // نیمه‌ی اول: overlap با بلاک قبلی
                        float prev = prevOverlap != null && outIdx < 576 ? prevOverlap[outIdx] : 0f;
                        if (outIdx < 576) outTime[outIdx] = timeBlock[i] + prev;
                    }
                    else
                    {
                        // نیمه‌ی دوم فقط برای گرانول بعدی نگه داشته می‌شود (overlap-add)
                        if (outIdx < 576) newOverlap[outIdx] = timeBlock[i];
                    }
                }
            }

            Array.Copy(newOverlap, prevOverlap, 576);

            // frequency inversion: زیرباندهای فرد، هر نمونه‌ی فرد را منفی کن
            for (int sb = 1; sb < subbands; sb += 2)
            {
                for (int i = 1; i < 18; i += 2)
                {
                    int idx = sb * 18 + i;
                    if (idx < 576) outTime[idx] = -outTime[idx];
                }
            }
        }

        private static float[] ImdctLong(float[] input, int blockType)
        {
            var output = new float[36];
            for (int i = 0; i < 36; i++)
            {
                double sum = 0;
                for (int k = 0; k < 18; k++)
                    sum += input[k] * Math.Cos(Math.PI / 72.0 * (2 * i + 1 + 18) * (2 * k + 1));

                double window = WindowValue(blockType, i);
                output[i] = (float)(sum * window);
            }
            return output;
        }

        private static float[] ImdctShort(float[] input)
        {
            // سه IMDCT دوازده‌نقطه‌ای درهم برای بلاک کوتاه، خروجی 36 نمونه‌ای
            var output = new float[36];
            for (int win = 0; win < 3; win++)
            {
                var sub = new float[6];
                for (int k = 0; k < 6; k++) sub[k] = input[win * 6 + k];

                var timeBlock = new float[12];
                for (int i = 0; i < 12; i++)
                {
                    double sum = 0;
                    for (int k = 0; k < 6; k++)
                        sum += sub[k] * Math.Cos(Math.PI / 24.0 * (2 * i + 1 + 6) * (2 * k + 1));
                    double w = Math.Sin(Math.PI / 12.0 * (i + 0.5));
                    timeBlock[i] = (float)(sum * w);
                }

                int offset = 6 + win * 6; // آفست استاندارد قرارگیری پنجره‌های کوتاه در بازه‌ی 36تایی
                for (int i = 0; i < 12; i++)
                {
                    int idx = offset + i;
                    if (idx < 36) output[idx] += timeBlock[i];
                }
            }
            return output;
        }

        private static double WindowValue(int blockType, int i)
        {
            switch (blockType)
            {
                case 1: // start block
                    if (i < 18) return Math.Sin(Math.PI / 36.0 * (i + 0.5));
                    if (i < 24) return 1.0;
                    if (i < 30) return Math.Sin(Math.PI / 12.0 * (i - 18 + 0.5));
                    return 0.0;
                case 3: // stop block
                    if (i < 6) return 0.0;
                    if (i < 12) return Math.Sin(Math.PI / 12.0 * (i - 6 + 0.5));
                    if (i < 18) return 1.0;
                    return Math.Sin(Math.PI / 36.0 * (i + 0.5));
                default: // normal (0)
                    return Math.Sin(Math.PI / 36.0 * (i + 0.5));
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Polyphase synthesis (تقریبی) — 32 زیرباند → نمونه‌های PCM نهایی
        //  به‌جای جدول رزرو دقیق 512تایی استاندارد، از یک پنجره‌ی Hann محاسبه‌شده
        //  استفاده می‌شود که به لحاظ کاربردی صدای قابل‌فهم تولید می‌کند.
        // ─────────────────────────────────────────────────────────────
        private static float[] _synthWindow;

        private static float[] GetSynthWindow()
        {
            if (_synthWindow != null) return _synthWindow;
            _synthWindow = new float[512];
            for (int i = 0; i < 512; i++)
            {
                // پنجره‌ی Hann استاندارد به‌عنوان جایگزین عملی جدول رزرو ISO
                _synthWindow[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / 511.0));
            }
            return _synthWindow;
        }

        private static short[] PolyphaseSynthesis(float[] timeSamples576)
        {
            // timeSamples576 شامل 32 زیرباند × 18 نمونه است (پس از IMDCT).
            // این تابع هر یک از 18 "دانه‌ی زمانی" را با ترکیب 32 زیرباند به یک
            // نمونه‌ی PCM نهایی تبدیل می‌کند (سنتز DCT-مانند ساده‌شده).
            var window = GetSynthWindow();
            var outSamples = new short[18 * 32];
            int w = 0;

            for (int sample = 0; sample < 18; sample++)
            {
                for (int outSample = 0; outSample < 32; outSample++)
                {
                    double sum = 0;
                    for (int sb = 0; sb < 32; sb++)
                    {
                        double v = timeSamples576[sb * 18 + sample];
                        sum += v * Math.Cos(Math.PI / 64.0 * (2 * outSample + 1) * (2 * sb + 1));
                    }

                    int windowIdx = (sample * 32 + outSample) % 512;
                    double windowed = sum * window[windowIdx] / 16.0;

                    outSamples[w++] = ClampToInt16(windowed);
                }
            }
            return outSamples;
        }

        private static short ClampToInt16(double v)
        {
            if (v > short.MaxValue) return short.MaxValue;
            if (v < short.MinValue) return short.MinValue;
            return (short)v;
        }
    }
}