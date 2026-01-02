#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Chappy.Wpf.Controls.ContextMenu;

public sealed class ShellContextMenuHost : IDisposable
{
    private readonly HwndSource _source;

    private IContextMenu? _cm;
    private IContextMenu2? _cm2;
    private IContextMenu3? _cm3;

    private const uint IdCmdFirst = 1;
    private const uint IdCmdLast = 0x7FFF;

    public ShellContextMenuHost(Window ownerWindow)
    {
        // ★重要：SourceInitialized以降で呼ぶこと（Hwnd=0対策）
        var hwnd = new WindowInteropHelper(ownerWindow).Handle;
        if (hwnd == IntPtr.Zero) throw new ArgumentException("ゼロのHwndは無効です。SourceInitialized後に生成してください。", nameof(ownerWindow));

        _source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("HwndSourceが取得できません。");
        _source.AddHook(WndProc); // ← これが “WndProc相当”
    }

    public void ShowForItem(string path, Point screenPoint)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        CleanupCom();

        IntPtr hwndOwner = _source.Handle; // ★背景と同じ hwnd に統一
        if (hwndOwner == IntPtr.Zero) return;

        // path -> PIDL
        int hr = Native.SHParseDisplayName(path, IntPtr.Zero, out var pidlFull, 0, out _);
        if (hr != Native.S_OK || pidlFull == IntPtr.Zero) return;

        try
        {
            // 親フォルダ(IShellFolder)に bind して、子PIDLを得る
            hr = Native.SHBindToParent(pidlFull, in Native.IID_IShellFolder, out var ppvParent, out var pidlChild);
            if (hr != Native.S_OK || ppvParent == IntPtr.Zero || pidlChild == IntPtr.Zero) return;

            IShellFolder parentFolder;
            try
            {
                parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(ppvParent);
            }
            finally
            {
                Marshal.Release(ppvParent);
            }

            // 親フォルダから「そのアイテムの IContextMenu」を取る（GetUIObjectOf）
            Guid iid = Native.IID_IContextMenu;

            // apidl は “親から見た子PIDL” の配列
            IntPtr[] apidl = { pidlChild };

            hr = parentFolder.GetUIObjectOf(hwndOwner, (uint)apidl.Length, apidl, ref iid, IntPtr.Zero, out var ppvMenu);
            if (hr != Native.S_OK || ppvMenu == IntPtr.Zero) return;

            try
            {
                _cm = (IContextMenu)Marshal.GetObjectForIUnknown(ppvMenu);
            }
            finally
            {
                Marshal.Release(ppvMenu);
            }

            _cm2 = _cm as IContextMenu2;
            _cm3 = _cm as IContextMenu3;

            IntPtr hMenu = Native.CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            try
            {
                _cm.QueryContextMenu(hMenu, 0, IdCmdFirst, IdCmdLast, Native.CMF_NORMAL);

                int cmd = Native.TrackPopupMenuEx(
                    hMenu,
                    Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON,
                    (int)screenPoint.X,
                    (int)screenPoint.Y,
                    hwndOwner,
                    IntPtr.Zero);

                if (cmd > 0)
                    InvokeCommand(hwndOwner, cmd);
            }
            finally
            {
                Native.DestroyMenu(hMenu);
            }
        }
        finally
        {
            Native.CoTaskMemFree(pidlFull);
        }
    }

    public void ShowForItems(IReadOnlyList<string> paths, Point screenPoint)
    {
        if (paths == null || paths.Count == 0) return;

        // 「存在するパス」だけに絞る（安全）
        var valid = new List<string>(paths.Count);
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            if (System.IO.File.Exists(p) || System.IO.Directory.Exists(p))
                valid.Add(p);
        }
        if (valid.Count == 0) return;

        // Explorer同様：同一フォルダ内の複数選択が基本
        // もし混在してたら、最初のフォルダ分だけに寄せる（現実解）
        var baseDir = System.IO.Path.GetDirectoryName(valid[0]) ?? "";
        valid = valid.Where(p => string.Equals(System.IO.Path.GetDirectoryName(p) ?? "", baseDir, StringComparison.OrdinalIgnoreCase)).ToList();
        if (valid.Count == 0) return;

        CleanupCom();

        IntPtr hwndOwner = _source.Handle;
        if (hwndOwner == IntPtr.Zero) return;

        // full PIDL を全部確保して最後に解放する
        var pidlFullList = new List<IntPtr>(valid.Count);

        try
        {
            // 1つ目から親フォルダ(IShellFolder)と childPIDL を取得
            int hr = Native.SHParseDisplayName(valid[0], IntPtr.Zero, out var pidl0, 0, out _);
            if (hr != Native.S_OK || pidl0 == IntPtr.Zero) return;
            pidlFullList.Add(pidl0);

            hr = Native.SHBindToParent(pidl0, in Native.IID_IShellFolder, out var ppvParent, out var pidlChild0);
            if (hr != Native.S_OK || ppvParent == IntPtr.Zero || pidlChild0 == IntPtr.Zero) return;

            IShellFolder parentFolder;
            try { parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(ppvParent); }
            finally { Marshal.Release(ppvParent); }

            // childPIDL配列を作る（pidlChild は full pidl の内部を指すので解放不要）
            var childPidls = new List<IntPtr>(valid.Count) { pidlChild0 };

            // 2つ目以降：同じ親フォルダか確認しつつ childPIDL を集める
            for (int i = 1; i < valid.Count; i++)
            {
                hr = Native.SHParseDisplayName(valid[i], IntPtr.Zero, out var pidl, 0, out _);
                if (hr != Native.S_OK || pidl == IntPtr.Zero) continue;

                pidlFullList.Add(pidl);

                hr = Native.SHBindToParent(pidl, in Native.IID_IShellFolder, out var ppvParentI, out var pidlChildI);
                if (hr != Native.S_OK || ppvParentI == IntPtr.Zero || pidlChildI == IntPtr.Zero)
                    continue;

                // 同一フォルダ前提なので、親のCOMは捨てる（最初の parentFolder を使う）
                Marshal.Release(ppvParentI);

                childPidls.Add(pidlChildI);
            }

            if (childPidls.Count == 0) return;

            // 親フォルダから IContextMenu を取得（複数 = childPIDL配列）
            Guid iid = Native.IID_IContextMenu;
            hr = parentFolder.GetUIObjectOf(hwndOwner, (uint)childPidls.Count, childPidls.ToArray(), ref iid, IntPtr.Zero, out var ppvMenu);
            if (hr != Native.S_OK || ppvMenu == IntPtr.Zero) return;

            try { _cm = (IContextMenu)Marshal.GetObjectForIUnknown(ppvMenu); }
            finally { Marshal.Release(ppvMenu); }

            _cm2 = _cm as IContextMenu2;
            _cm3 = _cm as IContextMenu3;

            IntPtr hMenu = Native.CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            try
            {
                _cm.QueryContextMenu(hMenu, 0, IdCmdFirst, IdCmdLast, Native.CMF_NORMAL);

                int cmd = Native.TrackPopupMenuEx(
                    hMenu,
                    Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON,
                    (int)screenPoint.X,
                    (int)screenPoint.Y,
                    hwndOwner,
                    IntPtr.Zero);

                if (cmd > 0)
                    InvokeCommand(hwndOwner, cmd);
            }
            finally
            {
                Native.DestroyMenu(hMenu);
            }
        }
        finally
        {
            // full PIDL は全部解放
            foreach (var pidl in pidlFullList)
            {
                if (pidl != IntPtr.Zero) Native.CoTaskMemFree(pidl);
            }
        }
    }

    /// <summary>フォルダ背景（Explorerの背景右クリック相当）のメニュー</summary>
    public void ShowForFolderBackground(string folderPath, Point screenPoint)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        CleanupCom();

        // ★CreateViewObject / TrackPopupMenuEx / Hookは同じhwndで揃える
        IntPtr hwndOwner = _source.Handle;
        if (hwndOwner == IntPtr.Zero) return;

        int hr = Native.SHParseDisplayName(folderPath, IntPtr.Zero, out var pidlFolder, 0, out _);
        if (hr != Native.S_OK || pidlFolder == IntPtr.Zero) return;

        try
        {
            var folder = BindToShellFolder(pidlFolder);
            if (folder == null) return;

            Guid iid = Native.IID_IContextMenu;
            hr = folder.CreateViewObject(hwndOwner, ref iid, out var ppvMenu);
            if (hr != Native.S_OK || ppvMenu == IntPtr.Zero) return;

            try { _cm = (IContextMenu)Marshal.GetObjectForIUnknown(ppvMenu); }
            finally { Marshal.Release(ppvMenu); }

            _cm2 = _cm as IContextMenu2;
            _cm3 = _cm as IContextMenu3;

            IntPtr hMenu = Native.CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            try
            {
                _cm.QueryContextMenu(hMenu, 0, IdCmdFirst, IdCmdLast, Native.CMF_NORMAL);

                int cmd = Native.TrackPopupMenuEx(
                    hMenu,
                    Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON,
                    (int)screenPoint.X,
                    (int)screenPoint.Y,
                    hwndOwner,
                    IntPtr.Zero);

                if (cmd > 0)
                    InvokeCommand(hwndOwner, cmd);
            }
            finally
            {
                Native.DestroyMenu(hMenu);
            }
        }
        finally
        {
            Native.CoTaskMemFree(pidlFolder);
        }
    }

    private void InvokeCommand(IntPtr hwndOwner, int cmd)
    {
        int verb = cmd - (int)IdCmdFirst;
        if (verb < 0) return;

        var invoke = new CMINVOKECOMMANDINFOEX
        {
            cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
            fMask = Native.CMIC_MASK_UNICODE,
            hwnd = hwndOwner,
            lpVerb = (IntPtr)verb,
            lpVerbW = (IntPtr)verb,
            nShow = Native.SW_SHOWNORMAL,
            ptInvoke = new POINT { x = 0, y = 0 }
        };

        _cm!.InvokeCommand(ref invoke);
    }

    private IShellFolder? BindToShellFolder(IntPtr pidlFolder)
    {
        int hr = Native.SHGetDesktopFolder(out var desktop);
        if (hr != Native.S_OK || desktop == null) return null;

        try
        {
            Guid iid = Native.IID_IShellFolder;
            hr = desktop.BindToObject(pidlFolder, IntPtr.Zero, ref iid, out var ppv);
            if (hr != Native.S_OK || ppv == IntPtr.Zero) return null;

            try { return (IShellFolder)Marshal.GetObjectForIUnknown(ppv); }
            finally { Marshal.Release(ppv); }
        }
        finally
        {
            Marshal.FinalReleaseComObject(desktop);
        }
    }

    // ====== ここが “WndProc” 相当（IContextMenu2/3 にメッセージ中継） ======
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // 背景メニューのサブメニュー（例：「アクセスを許可する」）対策で中継範囲を広めに
        if (_cm3 != null)
        {
            if (msg is Native.WM_INITMENUPOPUP or Native.WM_UNINITMENUPOPUP
                or Native.WM_DRAWITEM or Native.WM_MEASUREITEM
                or Native.WM_MENUCHAR or Native.WM_MENUSELECT
                or Native.WM_ENTERIDLE)
            {
                _cm3.HandleMenuMsg2(msg, wParam, lParam, out var result);
                handled = true;
                return result;
            }
        }
        else if (_cm2 != null)
        {
            if (msg is Native.WM_INITMENUPOPUP or Native.WM_UNINITMENUPOPUP
                or Native.WM_DRAWITEM or Native.WM_MEASUREITEM
                or Native.WM_MENUSELECT or Native.WM_ENTERIDLE)
            {
                _cm2.HandleMenuMsg(msg, wParam, lParam);
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private void CleanupCom()
    {
        _cm3 = null;
        _cm2 = null;
        if (_cm != null)
        {
            try { Marshal.FinalReleaseComObject(_cm); } catch { }
            _cm = null;
        }
    }

    public void Dispose()
    {
        CleanupCom();
        _source.RemoveHook(WndProc);
    }

    // ===== Native / COM =====
    private static class Native
    {
        public const int S_OK = 0;

        public const uint CMF_NORMAL = 0x00000000;

        public const uint TPM_RIGHTBUTTON = 0x0002;
        public const uint TPM_RETURNCMD = 0x0100;

        public const int SW_SHOWNORMAL = 1;
        public const int CMIC_MASK_UNICODE = 0x00004000;

        public const int WM_INITMENUPOPUP = 0x0117;
        public const int WM_MENUSELECT = 0x011F;
        public const int WM_DRAWITEM = 0x002B;
        public const int WM_MEASUREITEM = 0x002C;
        public const int WM_MENUCHAR = 0x0120;
        public const int WM_ENTERIDLE = 0x0121;
        public const int WM_UNINITMENUPOPUP = 0x0125;

        public static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
        public static readonly Guid IID_IContextMenu = new("000214E4-0000-0000-C000-000000000046");

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int TrackPopupMenuEx(
            IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHParseDisplayName(
            string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        internal static extern int SHGetDesktopFolder(
            [MarshalAs(UnmanagedType.Interface)] out IShellFolder ppshf);

        [DllImport("ole32.dll")]
        internal static extern void CoTaskMemFree(IntPtr pv);

        [DllImport("shell32.dll")]
        internal static extern int SHBindToParent(
            IntPtr pidl, in Guid riid, out IntPtr ppv, out IntPtr ppidlLast);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214e6-0000-0000-c000-000000000046")]
    internal interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

        [PreserveSig] int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        [PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        // ★背景メニュー取得に必要
        [PreserveSig] int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        [PreserveSig] int GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl,
            ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        [PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
        [PreserveSig]
        int SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214e4-0000-0000-c000-000000000046")]
    internal interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        void GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved,
            [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder commandstring, int cch);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214f4-0000-0000-c000-000000000046")]
    internal interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        void GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved,
            [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder commandstring, int cch);
        [PreserveSig] int HandleMenuMsg(int uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
    internal interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        void GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved,
            [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder commandstring, int cch);
        [PreserveSig] int HandleMenuMsg2(int uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public int fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;
    }
}
