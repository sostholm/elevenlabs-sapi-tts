using System;
using System.Runtime.InteropServices;

namespace ElevenLabsTtsEngine.Interop
{
    public static class SapiConstants
    {
        public const int S_OK = 0;
        public const int S_FALSE = 1;
        public const int E_FAIL = unchecked((int)0x80004005);
        public const int E_POINTER = unchecked((int)0x80004003);
        public const int E_INVALIDARG = unchecked((int)0x80070057);
        public const ushort WaveFormatPcm = 1;

        public static readonly Guid SPDFID_WaveFormatEx = new Guid("C31ADBAE-527F-4FF5-A230-F62BB61FF70C");
        public static readonly Guid EngineClassId = new Guid("961AE368-9B90-4277-B66B-D1593B74A888");
    }

    public enum SPVACTIONS
    {
        SPVA_Speak = 0,
        SPVA_Silence = 1,
        SPVA_Pronounce = 2,
        SPVA_Bookmark = 3,
        SPVA_SpellOut = 4,
        SPVA_Section = 5,
        SPVA_ParseUnknownTag = 6
    }

    [Flags]
    public enum SPVESACTIONS
    {
        SPVES_CONTINUE = 0,
        SPVES_ABORT = 1 << 0,
        SPVES_SKIP = 1 << 1,
        SPVES_RATE = 1 << 2,
        SPVES_VOLUME = 1 << 3
    }

    public enum SPVSKIPTYPE
    {
        SPVST_SENTENCE = 1 << 0
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;

        public static WAVEFORMATEX Pcm24Khz16BitMono()
        {
            const ushort channels = 1;
            const ushort bitsPerSample = 16;
            const uint samplesPerSecond = 24000;
            var blockAlign = (ushort)(channels * bitsPerSample / 8);

            return new WAVEFORMATEX
            {
                wFormatTag = SapiConstants.WaveFormatPcm,
                nChannels = channels,
                nSamplesPerSec = samplesPerSecond,
                nAvgBytesPerSec = samplesPerSecond * blockAlign,
                nBlockAlign = blockAlign,
                wBitsPerSample = bitsPerSample,
                cbSize = 0
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SPVPITCH
    {
        public int MiddleAdj;
        public int RangeAdj;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SPVCONTEXT
    {
        public IntPtr pCategory;
        public IntPtr pBefore;
        public IntPtr pAfter;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SPVSTATE
    {
        public SPVACTIONS eAction;
        public ushort LangID;
        public ushort wReserved;
        public int EmphAdj;
        public int RateAdj;
        public uint Volume;
        public SPVPITCH PitchAdj;
        public uint SilenceMSecs;
        public IntPtr pPhoneIds;
        public int ePartOfSpeech;
        public SPVCONTEXT Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SPVTEXTFRAG
    {
        public IntPtr pNext;
        public SPVSTATE State;
        public IntPtr pTextStart;
        public uint ulTextLen;
        public uint ulTextSrcOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SPEVENT
    {
        public ushort eEventId;
        public ushort elParamType;
        public uint ulStreamNum;
        public ulong ullAudioStreamOffset;
        public UIntPtr wParam;
        public IntPtr lParam;
    }
}
