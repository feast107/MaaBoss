using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MaaBoss.Core.Infrastructure;

/// <summary>
/// BOSS直聘 启动器：通过 PEB 注入在启动时注入 --remote-debugging-port 参数。
/// </summary>
public static class BossLauncher
{
    public static Process LaunchWithDebuggingPort(string exePath, int port = 9222)
    {
        // 先杀掉守护进程，断掉反调试监控
        foreach (var proc in Process.GetProcessesByName("boss-zhipin-daemon"))
        {
            try { proc.Kill(); proc.WaitForExit(1000); } catch { /* ignored */ }
        }
        var si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);

        if (!CreateProcess(
                exePath,
                null, // 不通过 lpCommandLine 传参，直接改 PEB
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                ProcessCreationFlags.CREATE_SUSPENDED | ProcessCreationFlags.CREATE_NEW_CONSOLE,
                IntPtr.Zero,
                null,
                ref si,
                out var pi))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");
        }

        try
        {
            InjectCommandLine(pi.hProcess, $"\"{exePath}\" --remote-debugging-port={port}");
            ResumeThread(pi.hThread);
            return Process.GetProcessById((int)pi.dwProcessId);
        }
        catch
        {
            TerminateProcess(pi.hProcess, 1);
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            throw;
        }
        finally
        {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
        }
    }

    private static unsafe void InjectCommandLine(IntPtr hProcess, string newCmdLine)
    {
        // 1. 读取 PEB 基址
        var pbi = new PROCESS_BASIC_INFORMATION();
        int returnLength = 0;
        NtQueryInformationProcess(
            hProcess,
            ProcessInformationClass.ProcessBasicInformation,
            &pbi,
            Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(),
            &returnLength);

        // 2. 读取 PEB 中的 ProcessParameters 指针 (PEB+0x20 on x64)
        nint pebAddr = pbi.PebBaseAddress;
        nint processParamsAddr = ReadIntPtr(hProcess, pebAddr + 0x20);

        // 3. 读取 CommandLine UNICODE_STRING (RTL_USER_PROCESS_PARAMETERS+0x70 on x64)
        nint commandLineAddr = processParamsAddr + 0x70;
        var uniStr = ReadUnicodeString(hProcess, commandLineAddr);

        Console.WriteLine($"[PEB] Original cmdline at 0x{uniStr.Buffer:X}, len={uniStr.Length}");

        // 4. 分配新内存写入新命令行
        var newCmdBytes = Encoding.Unicode.GetBytes(newCmdLine + '\0');
        var newMem = VirtualAllocEx(
            hProcess,
            IntPtr.Zero,
            (uint)newCmdBytes.Length,
            AllocationType.MEM_COMMIT | AllocationType.MEM_RESERVE,
            MemoryProtection.PAGE_READWRITE);

        if (newMem == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed");

        WriteProcessMemory(hProcess, newMem, newCmdBytes, newCmdBytes.Length, out _);

        // 5. 修改 UNICODE_STRING 结构体指向新内存
        var newUni = new UNICODE_STRING
        {
            Length = (ushort)((newCmdLine.Length) * 2),
            MaximumLength = (ushort)(newCmdBytes.Length),
            Buffer = newMem
        };

        WriteStruct(hProcess, commandLineAddr, newUni);
        Console.WriteLine($"[PEB] Patched cmdline to: {newCmdLine}");
    }

    // ============ P/Invoke ============

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string lpApplicationName,
        string? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        ProcessCreationFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll")]
    private static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll")]
    private static extern unsafe int NtQueryInformationProcess(
        IntPtr processHandle,
        ProcessInformationClass processInformationClass,
        void* processInformation,
        int processInformationLength,
        int* returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        uint dwSize,
        AllocationType flAllocationType,
        MemoryProtection flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int nSize,
        out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int nSize,
        out int lpNumberOfBytesRead);

    private static nint ReadIntPtr(IntPtr hProcess, nint addr)
    {
        var buf = new byte[IntPtr.Size];
        ReadProcessMemory(hProcess, (IntPtr)addr, buf, buf.Length, out _);
        return IntPtr.Size == 8 ? (nint)BitConverter.ToInt64(buf) : BitConverter.ToInt32(buf);
    }

    private static unsafe UNICODE_STRING ReadUnicodeString(IntPtr hProcess, nint addr)
    {
        var buf = new byte[Marshal.SizeOf<UNICODE_STRING>()];
        ReadProcessMemory(hProcess, (IntPtr)addr, buf, buf.Length, out _);
        fixed (byte* p = buf)
            return *(UNICODE_STRING*)p;
    }

    private static unsafe void WriteStruct(IntPtr hProcess, nint addr, UNICODE_STRING value)
    {
        var size = Marshal.SizeOf<UNICODE_STRING>();
        var buf = new byte[size];
        fixed (byte* p = buf)
            *(UNICODE_STRING*)p = value;
        WriteProcessMemory(hProcess, (IntPtr)addr, buf, size, out _);
    }

    // ============ Structs / Enums ============

    [Flags]
    private enum ProcessCreationFlags : uint
    {
        CREATE_SUSPENDED = 0x00000004,
        CREATE_NEW_CONSOLE = 0x00000010,
    }

    private enum ProcessInformationClass
    {
        ProcessBasicInformation = 0,
    }

    [Flags]
    private enum AllocationType : uint
    {
        MEM_COMMIT = 0x1000,
        MEM_RESERVE = 0x2000,
    }

    private enum MemoryProtection : uint
    {
        PAGE_READWRITE = 0x04,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }
}
