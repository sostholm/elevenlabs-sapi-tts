using System;
using System.Runtime.InteropServices;

namespace ElevenLabsTtsEngine.Interop
{
    [ComImport]
    [Guid("14056581-E16C-11D2-BB90-00C04F8EE6C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpDataKey
    {
        [PreserveSig]
        int SetData([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, uint cbData, IntPtr pData);

        [PreserveSig]
        int GetData([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, ref uint pcbData, IntPtr pData);

        [PreserveSig]
        int SetStringValue([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, [MarshalAs(UnmanagedType.LPWStr)] string pszValue);

        [PreserveSig]
        int GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, out IntPtr ppszValue);

        [PreserveSig]
        int SetDWORD([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, uint dwValue);

        [PreserveSig]
        int GetDWORD([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, out uint pdwValue);

        [PreserveSig]
        int OpenKey([MarshalAs(UnmanagedType.LPWStr)] string pszSubKeyName, out ISpDataKey ppSubKey);

        [PreserveSig]
        int CreateKey([MarshalAs(UnmanagedType.LPWStr)] string pszSubKey, out ISpDataKey ppSubKey);

        [PreserveSig]
        int DeleteKey([MarshalAs(UnmanagedType.LPWStr)] string pszSubKey);

        [PreserveSig]
        int DeleteValue([MarshalAs(UnmanagedType.LPWStr)] string pszValueName);

        [PreserveSig]
        int EnumKeys(uint index, out IntPtr ppszSubKeyName);

        [PreserveSig]
        int EnumValues(uint index, out IntPtr ppszValueName);
    }

    [ComImport]
    [Guid("2D3D3845-39AF-4850-BBF9-40B49780011D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpObjectTokenCategory : ISpDataKey
    {
        [PreserveSig]
        int SetId([MarshalAs(UnmanagedType.LPWStr)] string pszCategoryId, [MarshalAs(UnmanagedType.Bool)] bool fCreateIfNotExist);

        [PreserveSig]
        int GetId(out IntPtr ppszCoMemCategoryId);

        [PreserveSig]
        int GetDataKey(uint spdkl, out ISpDataKey ppDataKey);

        [PreserveSig]
        int EnumTokens([MarshalAs(UnmanagedType.LPWStr)] string pzsReqAttribs, [MarshalAs(UnmanagedType.LPWStr)] string pszOptAttribs, out IntPtr ppEnum);

        [PreserveSig]
        int SetDefaultTokenId([MarshalAs(UnmanagedType.LPWStr)] string pszTokenId);

        [PreserveSig]
        int GetDefaultTokenId(out IntPtr ppszCoMemTokenId);
    }

    [ComImport]
    [Guid("14056589-E16C-11D2-BB90-00C04F8EE6C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpObjectToken : ISpDataKey
    {
        [PreserveSig]
        int SetId([MarshalAs(UnmanagedType.LPWStr)] string pszCategoryId, [MarshalAs(UnmanagedType.LPWStr)] string pszTokenId, [MarshalAs(UnmanagedType.Bool)] bool fCreateIfNotExist);

        [PreserveSig]
        int GetId(out IntPtr ppszCoMemTokenId);

        [PreserveSig]
        int GetCategory(out ISpObjectTokenCategory ppTokenCategory);

        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppvObject);

        [PreserveSig]
        int GetStorageFileName(ref Guid clsidCaller, [MarshalAs(UnmanagedType.LPWStr)] string pszValueName, [MarshalAs(UnmanagedType.LPWStr)] string pszFileNameSpecifier, uint nFolder, out IntPtr ppszFilePath);

        [PreserveSig]
        int RemoveStorageFileName(ref Guid clsidCaller, [MarshalAs(UnmanagedType.LPWStr)] string pszKeyName, [MarshalAs(UnmanagedType.Bool)] bool fDeleteFile);

        [PreserveSig]
        int Remove(IntPtr pclsidCaller);

        [PreserveSig]
        int IsUISupported([MarshalAs(UnmanagedType.LPWStr)] string pszTypeOfUI, IntPtr pvExtraData, uint cbExtraData, IntPtr punkObject, [MarshalAs(UnmanagedType.Bool)] out bool pfSupported);

        [PreserveSig]
        int DisplayUI(IntPtr hwndParent, [MarshalAs(UnmanagedType.LPWStr)] string pszTitle, [MarshalAs(UnmanagedType.LPWStr)] string pszTypeOfUI, IntPtr pvExtraData, uint cbExtraData, IntPtr punkObject);

        [PreserveSig]
        int MatchesAttributes([MarshalAs(UnmanagedType.LPWStr)] string pszAttributes, [MarshalAs(UnmanagedType.Bool)] out bool pfMatches);
    }

    [ComImport]
    [Guid("5B559F40-E952-11D2-BB91-00C04F8EE6C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpObjectWithToken
    {
        [PreserveSig]
        int SetObjectToken(ISpObjectToken pToken);

        [PreserveSig]
        int GetObjectToken(out ISpObjectToken ppToken);
    }

    [ComImport]
    [Guid("BE7A9CC9-5F9E-11D2-960F-00C04F8EE628")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpEventSink
    {
        [PreserveSig]
        int AddEvents(IntPtr pEventArray, uint ulCount);

        [PreserveSig]
        int GetEventInterest(out ulong pullEventInterest);
    }

    [ComImport]
    [Guid("9880499B-CCE9-11D2-B503-00C04F797396")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpTTSEngineSite
    {
        [PreserveSig]
        int AddEvents(IntPtr pEventArray, uint ulCount);

        [PreserveSig]
        int GetEventInterest(out ulong pullEventInterest);

        [PreserveSig]
        SPVESACTIONS GetActions();

        [PreserveSig]
        int Write(IntPtr pBuff, uint cb, out uint pcbWritten);

        [PreserveSig]
        int GetRate(out int pRateAdjust);

        [PreserveSig]
        int GetVolume(out ushort pusVolume);

        [PreserveSig]
        int GetSkipInfo(out SPVSKIPTYPE peType, out int plNumItems);

        [PreserveSig]
        int CompleteSkip(int ulNumSkipped);
    }

    [ComImport]
    [Guid("A74D7C8E-4CC5-4F2F-A6EB-804DEE18500E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISpTTSEngine
    {
        [PreserveSig]
        int Speak(uint dwSpeakFlags, ref Guid rguidFormatId, IntPtr pWaveFormatEx, IntPtr pTextFragList, ISpTTSEngineSite pOutputSite);

        [PreserveSig]
        int GetOutputFormat(IntPtr pTargetFmtId, IntPtr pTargetWaveFormatEx, out Guid pOutputFormatId, out IntPtr ppCoMemOutputWaveFormatEx);
    }
}
