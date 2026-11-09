using System.Runtime.InteropServices;

namespace DriverGuard.Infrastructure.Security;

/// <summary>
/// Declarações nativas P/Invoke para a Win32 Trust API (wintrust.dll / crypt32.dll).
/// </summary>
internal static class WinVerifyTrustNative
{
    // GUID de verificação genérica de autenticidade (Authenticode)
    public static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

    // GUID oficial da Microsoft para validação de drivers de dispositivo (DRIVER_ACTION_VERIFY)
    public static readonly Guid DRIVER_ACTION_VERIFY = new("{F750E6C3-38EE-11D1-85E5-00C04FC295EE}");

    // OIDs de assinatura WHQL (Windows Hardware Quality Labs)
    public const string OID_WHQL_CRYPTO = "1.3.6.1.4.1.311.10.3.5";
    public const string OID_WHQL_WINDOWS = "1.3.6.1.4.1.311.10.3.5.1";

    public const uint WTD_UI_NONE = 2;
    public const uint WTD_REVOKE_NONE = 0;
    public const uint WTD_CHOICE_FILE = 1;
    public const uint WTD_CHOICE_CATALOG = 2;
    public const uint WTD_STATEACTION_IGNORE = 0;

    // Flags de segurança: proíbe download externo silencioso e previne travamento de rede
    public const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00001000;
    public const uint WTD_REVOCATION_CHECK_NONE = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WINTRUST_CATALOG_INFO
    {
        public uint cbStruct;
        public uint dwCatalogVersion;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszCatalogFilePath;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pcwszMemberTag;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszMemberFilePath;
        public IntPtr hMemberFile;
        public IntPtr pbCalculatedMessageDigest;
        public uint cbCalculatedMessageDigest;
        public IntPtr pcCatalogContext;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFileOrCatalog;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    public static extern int WinVerifyTrust(
        IntPtr hWnd,
        [In] ref Guid pgActionID,
        [In] ref WINTRUST_DATA pWVTData);
}
