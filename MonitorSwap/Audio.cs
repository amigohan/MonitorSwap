using System.Runtime.InteropServices;

namespace MonitorSwap;

/// <summary>Windows ses çıkış cihazlarını listeler ve varsayılan cihazı değiştirir.</summary>
static class Audio
{
    public sealed record Device(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    const int eRender = 0;
    const int DEVICE_STATE_ACTIVE = 1;
    public const int RoleConsole = 0, RoleMultimedia = 1, RoleCommunications = 2;

    static PROPERTYKEY PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14,
    };

    public static List<Device> ListOutputs()
    {
        var list = new List<Device>();
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        try
        {
            if (en.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out var col) != 0) return list;
            try
            {
                col.GetCount(out uint n);
                for (uint i = 0; i < n; i++)
                {
                    if (col.Item(i, out var d) != 0) continue;
                    try
                    {
                        d.GetId(out string id);
                        list.Add(new Device(id, FriendlyName(d) ?? id));
                    }
                    finally { Marshal.ReleaseComObject(d); }
                }
            }
            finally { Marshal.ReleaseComObject(col); }
        }
        finally { Marshal.ReleaseComObject(en); }
        return list;
    }

    public static string GetDefaultId(int role)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        try
        {
            if (en.GetDefaultAudioEndpoint(eRender, role, out var d) != 0) return null;
            try { d.GetId(out string id); return id; }
            finally { Marshal.ReleaseComObject(d); }
        }
        finally { Marshal.ReleaseComObject(en); }
    }

    public static bool SetDefault(string id, int role)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var pc = (IPolicyConfig)new PolicyConfigClient();
        try { return pc.SetDefaultEndpoint(id, role) == 0; }
        finally { Marshal.ReleaseComObject(pc); }
    }

    static string FriendlyName(IMMDevice d)
    {
        if (d.OpenPropertyStore(0 /*STGM_READ*/, out var ps) != 0) return null;
        try
        {
            if (ps.GetValue(ref PKEY_Device_FriendlyName, out var pv) != 0) return null;
            try { return pv.vt == 31 /*VT_LPWSTR*/ ? Marshal.PtrToStringUni(pv.ptr) : null; }
            finally { PropVariantClear(ref pv); }
        }
        finally { Marshal.ReleaseComObject(ps); }
    }

    // ---------------- COM tanımları ----------------

    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY { public Guid fmtid; public int pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr ptr;
    }

    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    class PolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        [PreserveSig] int Commit();
    }

    // Belgelenmemiş ama Windows 7–11'de kararlı arayüz (SoundSwitch, EarTrumpet vb. de bunu kullanır).
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, IntPtr defaultPeriod, IntPtr minPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }
}
