﻿namespace x360Utils.NAND {
    using System;
    using System.IO;

    public class NANDSpare {
        #region MetaType enum

        public enum MetaType {
            MetaTypeUnInitialized = int.MinValue, // Really old JTAG XeLL images
            MetaType0 = 0, // Pre Jasper (0x01198010)
            MetaType1 = 1, // Jasper, Trinity & Corona (0x00023010 [Jasper & Trinity] and 0x00043000 [Corona])
            MetaType2 = 2, // BigBlock Jasper (0x008A3020 and 0x00AA3020)
            MetaTypeNone = int.MaxValue // No spare type or unknown
        }

        #endregion

        internal static MetaType DetectSpareType(NANDReader reader, bool firsttry = true) {
            if(!reader.HasSpare)
                return MetaType.MetaTypeNone;
            if(firsttry)
                reader.RawSeek(0x4400, SeekOrigin.Begin);
            else
                reader.RawSeek(reader.RawLength - 0x4000, SeekOrigin.Begin);
            var tmp = reader.RawReadBytes(0x10);
            if (!CheckIsBadBlockSpare(ref tmp, MetaType.MetaTypeUnInitialized))
                return MetaType.MetaTypeUnInitialized;
            if(!CheckIsBadBlockSpare(ref tmp, MetaType.MetaType0)) {
                if(BlockIDFromSpare(ref tmp, MetaType.MetaType0) == 1)
                    return MetaType.MetaType0;
                if(BlockIDFromSpare(ref tmp, MetaType.MetaType1) == 1)
                    return MetaType.MetaType1;
            }
            if (!CheckIsBadBlockSpare(ref tmp, MetaType.MetaType2)) {
                if(firsttry)
                    reader.RawSeek(0x21200, SeekOrigin.Begin);
                else if(reader.RawLength <= 0x4200000)
                    reader.RawSeek(reader.RawLength - 0x4000, SeekOrigin.Begin);
                else
                    reader.RawSeek(0x4200000 - 0x4000, SeekOrigin.Begin);
                tmp = reader.RawReadBytes(0x10);
                if(!CheckIsBadBlockSpare(ref tmp, MetaType.MetaType2))
                    if(BlockIDFromSpare(ref tmp, MetaType.MetaType2) == 1)
                        return MetaType.MetaType2;
            }
            else if (Main.VerifyVerbosityLevel(1))
                Main.SendInfo(firsttry ? "Block 1 is bad!" : "The last system block is bad!");
            if(firsttry)
                return DetectSpareType(reader, false);
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownMetaType);
        }

        public static bool CheckIsBadBlockSpare(ref byte[] spareData, MetaType metaType) {
            switch(metaType) {
                case MetaType.MetaType0:
                case MetaType.MetaType1:
                    return spareData[5] != 0xFF;
                case MetaType.MetaType2:
                    return spareData[0] != 0xFF;
                case MetaType.MetaTypeUnInitialized:
                    return (Common.BitOperations.CountByteInstances(ref spareData, 0xFF) != spareData.Length);
                default:
                    throw new ArgumentOutOfRangeException("metaType");
            }
        }

        public static bool CheckIsBadBlock(ref byte[] blockData, MetaType metaType) {
            switch(metaType) {
                case MetaType.MetaType0:
                case MetaType.MetaType1:
                    return blockData[0x205] != 0xFF;
                case MetaType.MetaType2:
                    return blockData[0x200] != 0xFF;
                case MetaType.MetaTypeUnInitialized:
                    return (Common.BitOperations.CountByteInstances(ref blockData, 0xFF, 0x200, 0x10) != 0x10);
                default:
                    throw new ArgumentOutOfRangeException("metaType");
            }
        }

        public static int BlockIDFromSpare(ref byte[] spareData, MetaType metaType) {
            if (CheckIsBadBlockSpare(ref spareData, metaType))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.BadBlockDetected);
            switch(metaType) {
                case MetaType.MetaType0:
                    return BitConverter.ToUInt16(spareData, 0);
                case MetaType.MetaType1:
                case MetaType.MetaType2:
                    return BitConverter.ToUInt16(spareData, 1);
                case MetaType.MetaTypeUnInitialized:
                    throw new NotSupportedException();
                default:
                    throw new ArgumentOutOfRangeException("metaType");
            }
        }

        public static int BlockIDFromBlock(ref byte[] blockData, MetaType metaType) {
            if (CheckIsBadBlock(ref blockData, metaType))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.BadBlockDetected);
            switch(metaType) {
                case MetaType.MetaType0:
                    return BitConverter.ToUInt16(blockData, 0x200);
                case MetaType.MetaType2:
                case MetaType.MetaType1:
                    return BitConverter.ToUInt16(blockData, 0x201);
                case MetaType.MetaTypeUnInitialized:
                    throw new NotSupportedException();
                default:
                    throw new ArgumentOutOfRangeException("metaType");
            }
        }

        public static byte[] CalculateECD(ref byte[] data, int offset) {
            UInt32 i, val = 0, v = 0;
            var count = 0;
            for(i = 0; i < 0x1066; i++) {
                if((i & 31) == 0) {
                    v = ~BitConverter.ToUInt32(data, (count + offset));
                    count += 4;
                }
                val ^= v & 1;
                v >>= 1;
                if((val & 1) != 0)
                    val ^= 0x6954559;
                val >>= 1;
            }
            val = ~val;
            return new[] { (byte) (val << 6), (byte) ((val >> 2) & 0xFF), (byte) ((val >> 10) & 0xFF), (byte) ((val >> 18) & 0xFF) };
        }

        internal static bool CheckPageECD(ref byte[] data, int offset) {
            var actual = new byte[4];
            var calculated = CalculateECD(ref data, offset);
            Buffer.BlockCopy(data, offset + 524, actual, 0, 4);
            return (calculated[0] == actual[0] && calculated[1] == actual[1] && calculated[2] == actual[2] && calculated[3] == actual[3]);
        }

        internal static bool PageIsFS(ref byte[] data) {
            return false;
        }
    }
}