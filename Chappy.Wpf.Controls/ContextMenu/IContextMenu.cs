using System;
using System.Runtime.InteropServices;

namespace Chappy.Wpf.Controls.ContextMenu;

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
 Guid("000214e4-0000-0000-c000-000000000046")]
internal interface IContextMenu
{
    [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
    void GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, System.Text.StringBuilder commandstring, int cch);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
 Guid("000214f4-0000-0000-c000-000000000046")]
internal interface IContextMenu2
{
    [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
    void GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, System.Text.StringBuilder commandstring, int cch);
    [PreserveSig] int HandleMenuMsg(int uMsg, IntPtr wParam, IntPtr lParam);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
 Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
internal interface IContextMenu3
{
    [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
    void GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, System.Text.StringBuilder commandstring, int cch);
    [PreserveSig] int HandleMenuMsg2(int uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct CMINVOKECOMMANDINFOEX
{
    public int cbSize;
    public int fMask;
    public IntPtr hwnd;
    public IntPtr lpVerb;         // MAKEINTRESOURCEA(cmd)
    public IntPtr lpParameters;
    public IntPtr lpDirectory;
    public int nShow;
    public int dwHotKey;
    public IntPtr hIcon;
    public IntPtr lpTitle;
    public IntPtr lpVerbW;        // MAKEINTRESOURCEW(cmd)
    public IntPtr lpParametersW;
    public IntPtr lpDirectoryW;
    public IntPtr lpTitleW;
    public POINT ptInvoke;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int x, y; }
