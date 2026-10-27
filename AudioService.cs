using System.Runtime.InteropServices;

namespace I3wsb;

internal static class AudioService
{
    static readonly Guid IID_IAudioEndpointVolume = new("5BC64843-8047-412C-8DEC-5D0FA4EB4169");

    public static bool TryGetMasterVolume(out int volumePercent, out bool isMuted)
    {
        volumePercent = 0;
        isMuted = false;
        try
        {
            var volume = GetEndpointVolume();
            if (volume == null) return false;

            if (volume.GetMasterVolumeLevelScalar(out float level) == 0)
                volumePercent = (int)Math.Round(level * 100f);

            if (volume.GetMute(out bool mute) == 0)
                isMuted = mute;

            Marshal.ReleaseComObject(volume);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetMasterVolume(int volumePercent)
    {
        try
        {
            var volume = GetEndpointVolume();
            if (volume == null) return false;

            float level = Math.Clamp(volumePercent / 100f, 0f, 1f);
            Guid ctx = Guid.Empty;
            int hr = volume.SetMasterVolumeLevelScalar(level, ref ctx);
            Marshal.ReleaseComObject(volume);
            return hr == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool AdjustVolume(int deltaPercent)
    {
        if (!TryGetMasterVolume(out int current, out _)) return false;
        return SetMasterVolume(current + deltaPercent);
    }

    public static bool ToggleMute()
    {
        try
        {
            var volume = GetEndpointVolume();
            if (volume == null) return false;

            if (volume.GetMute(out bool muted) == 0)
            {
                Guid ctx = Guid.Empty;
                volume.SetMute(!muted, ref ctx);
            }
            Marshal.ReleaseComObject(volume);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static IAudioEndpointVolume? GetEndpointVolume()
    {
        var enumerator = new MMDeviceEnumerator() as IMMDeviceEnumerator;
        if (enumerator == null) return null;

        try
        {
            // eRender = 0, eMultimedia = 1
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device) != 0 || device == null)
            {
                Marshal.ReleaseComObject(enumerator);
                return null;
            }

            try
            {
                Guid iid = IID_IAudioEndpointVolume;
                // CLSCTX_INPROC_SERVER = 0x1, CLSCTX_ALL = 0x17
                if (device.Activate(ref iid, 0x17, IntPtr.Zero, out object epvObj) != 0 || epvObj == null)
                    return null;

                return epvObj as IAudioEndpointVolume;
            }
            finally
            {
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch
        {
            return null;
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice(string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        [PreserveSig] int GetId(out IntPtr ppstrId);
        [PreserveSig] int GetState(out int pdwState);
    }

    [Guid("5BC64843-8047-412C-8DEC-5D0FA4EB4169"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out int pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }
}
