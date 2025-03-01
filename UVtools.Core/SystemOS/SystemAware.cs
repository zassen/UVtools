﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace UVtools.Core.SystemOS;

public static class SystemAware
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer); //Used to use ref with comment below
    // but ref doesn't work.(Use of [In, Out] instead of ref
    //causes access violation exception on windows xp
    //comment: most probably caused by MEMORYSTATUSEX being declared as a class
    //(at least at pinvoke.net). On Win7, ref and struct work.

    // Alternate Version Using "ref," And Works With Alternate Code Below.
    // Also See Alternate Version Of [MEMORYSTATUSEX] Structure With
    // Fields Documented.
    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    static extern bool _GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static MEMORYSTATUSEX GetMemoryStatus()
    {
        var statEX = new MEMORYSTATUSEX();
        if (OperatingSystem.IsWindows())
        {
            GlobalMemoryStatusEx(statEX);
        }
        else if (OperatingSystem.IsLinux())
        {
            if (!File.Exists("/proc/meminfo")) return statEX;
            try
            {
                const ushort factor = 1024;
                var result = File.ReadAllText("/proc/meminfo");
                //var result = "MemTotal:        8288440 kB\nMemFree:         5616380 kB\nMemAvailable:    6885408 kB\nBuffers:           63240 kB\nCached:          1390996 kB\nSwapCached:            0 kB\nActive:           272516 kB\nInactive:        1773312 kB\nActive(anon):       1888 kB\nInactive(anon):   596168 kB\nActive(file):     270628 kB\nInactive(file):  1177144 kB\nUnevictable:           0 kB\nMlocked:               0 kB\nSwapTotal:       2097148 kB\nSwapFree:        2097148 kB\nDirty:               172 kB\nWriteback:             0 kB\nAnonPages:        591608 kB\nMapped:           292288 kB\nShmem:              6464 kB\nKReclaimable:      86228 kB\nSlab:             170544 kB\nSReclaimable:      86228 kB\nSUnreclaim:        84316 kB\nKernelStack:       12160 kB\nPageTables:        13992 kB\nNFS_Unstable:          0 kB\nBounce:                0 kB\nWritebackTmp:          0 kB\nCommitLimit:     6241368 kB\nCommitted_AS:    3588500 kB\nVmallocTotal:   34359738367 kB\nVmallocUsed:       61060 kB\nVmallocChunk:          0 kB\nPercpu:            91136 kB\nHardwareCorrupted:     0 kB\nAnonHugePages:         0 kB\nShmemHugePages:        0 kB\nShmemPmdMapped:        0 kB\nFileHugePages:         0 kB\nFilePmdMapped:         0 kB\nHugePages_Total:       0\nHugePages_Free:        0\nHugePages_Rsvd:        0\nHugePages_Surp:        0\nHugepagesize:       2048 kB\nHugetlb:               0 kB\nDirectMap4k:      216464 kB\nDirectMap2M:     4169728 kB\nDirectMap1G:     5242880 kB";
                var matches = Regex.Matches(result, @"(\S+):\s*(\d+).(\S+)");
                foreach (Match match in matches)
                {
                    if (!match.Success || match.Groups.Count < 2) continue;
                    ulong value = ulong.Parse(match.Groups[2].Value) * factor;
                    switch (match.Groups[1].Value)
                    {
                        case "MemTotal":
                            statEX.ullTotalPhys = value;
                            statEX.ullTotalVirtual = value;
                            continue;
                        case "MemFree":
                            statEX.ullAvailPhys = value;
                            continue;
                        case "MemAvailable":
                            statEX.ullAvailVirtual = value;
                            continue;
                        case "SwapTotal":
                            statEX.ullTotalPageFile = value;
                            continue;
                        case "SwapFree":
                            statEX.ullAvailPageFile = value;
                            continue;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
        else if(OperatingSystem.IsMacOS())
        {
            try
            {
                var result = GetProcessOutput("vm_stat");

                if (string.IsNullOrWhiteSpace(result)) return statEX;

                //var result = "Mach Virtual Memory Statistics: (page size of 4096 bytes)\nPages free:                             3044485.\nPages active:                            400375.\nPages inactive:                          235679.\nPages speculative:                       189311.\nPages throttled:                              0.\nPages wired down:                        324269.\nPages purgeable:                          27417.\n\"Translation faults\":                   5500903.\nPages copy-on-write:                     388354.\nPages zero filled:                      2724856.\nPages reactivated:                          410.\nPages purged:                               972.\nFile-backed pages:                       400847.\nAnonymous pages:                         424518.\nPages stored in compressor:                   0.\nPages occupied by compressor:                 0.\nDecompressions:                               0.\nCompressions:                                 0.\nPageins:                                 354428.\nPageouts:                                     0.\nSwapins:                                      0.\nSwapouts:                                     0.";
                var matchPageSize = Regex.Match(result, @"page size of (\d+) bytes");

                if (!matchPageSize.Success || matchPageSize.Groups.Count < 2) return statEX;
                ushort pageSize = ushort.Parse(matchPageSize.Groups[1].Value);

                var matches = Regex.Matches(result, @"(.*):\s*(\d+)");
                foreach (Match match in matches)
                {
                    if (!match.Success || match.Groups.Count < 2) continue;

                    ulong value = ulong.Parse(match.Groups[2].Value) * pageSize;

                    if (match.Groups[1].Value.StartsWith("\"Translation faults\"")) break;
                    if (match.Groups[1].Value.StartsWith("Pages "))
                    {
                        statEX.ullTotalPhys += value;
                    }

                    switch (match.Groups[1].Value)
                    {
                        case "Pages free":
                        case "Pages inactive":
                        case "Pages purgeable":
                        case "Pages throttled":
                        case "Pages speculative":
                            statEX.ullAvailPhys += value;
                            continue;
                    }
                }

                statEX.ullTotalVirtual = statEX.ullTotalPhys;
                statEX.ullAvailVirtual = statEX.ullAvailPhys;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        return statEX;
    }

    public static string? GetProcessorName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                /*ManagementObjectSearcher mos =
                    new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                foreach (ManagementObject mo in mos.Get())
                {
                    Console.WriteLine(mo["Name"]);
                }*/
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString();
            }

            if (OperatingSystem.IsLinux())
            {
                if (!File.Exists("/proc/cpuinfo")) return null;
                //var result = "processor\t: 0\nvendor_id\t: GenuineIntel\ncpu family\t: 6\nmodel\t\t: 158\nmodel name\t: Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz\nstepping\t: 12\nmicrocode\t: 0xffffffff\ncpu MHz\t\t: 3600.011\ncache size\t: 16384 KB\nphysical id\t: 0\nsiblings\t: 6\ncore id\t\t: 0\ncpu cores\t: 6\napicid\t\t: 0\ninitial apicid\t: 0\nfpu\t\t: yes\nfpu_exception\t: yes\ncpuid level\t: 22\nwp\t\t: yes\nflags\t\t: fpu vme de pse tsc msr pae mce cx8 apic sep mtrr pge mca cmov pat pse36 clflush mmx fxsr sse sse2 ss ht syscall nx pdpe1gb rdtscp lm constant_tsc arch_perfmon nopl xtopology tsc_reliable nonstop_tsc cpuid pni pclmulqdq ssse3 fma cx16 pcid sse4_1 sse4_2 x2apic movbe popcnt tsc_deadline_timer aes xsave avx f16c rdrand hypervisor lahf_lm abm 3dnowprefetch invpcid_single ssbd ibrs ibpb stibp fsgsbase tsc_adjust bmi1 avx2 smep bmi2 invpcid rdseed adx smap clflushopt xsaveopt xsavec xgetbv1 xsaves arat flush_l1d arch_capabilities\nbugs\t\t: spectre_v1 spectre_v2 spec_store_bypass mds swapgs itlb_multihit srbds\nbogomips\t: 7200.02\nclflush size\t: 64\ncache_alignment\t: 64\naddress sizes\t: 45 bits physical, 48 bits virtual\npower management:\n\nprocessor\t: 1\nvendor_id\t: GenuineIntel\ncpu family\t: 6\nmodel\t\t: 158\nmodel name\t: Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz\nstepping\t: 12\nmicrocode\t: 0xffffffff\ncpu MHz\t\t: 3600.011\ncache size\t: 16384 KB\nphysical id\t: 0\nsiblings\t: 6\ncore id\t\t: 1\ncpu cores\t: 6\napicid\t\t: 1\ninitial apicid\t: 1\nfpu\t\t: yes\nfpu_exception\t: yes\ncpuid level\t: 22\nwp\t\t: yes\nflags\t\t: fpu vme de pse tsc msr pae mce cx8 apic sep mtrr pge mca cmov pat pse36 clflush mmx fxsr sse sse2 ss ht syscall nx pdpe1gb rdtscp lm constant_tsc arch_perfmon nopl xtopology tsc_reliable nonstop_tsc cpuid pni pclmulqdq ssse3 fma cx16 pcid sse4_1 sse4_2 x2apic movbe popcnt tsc_deadline_timer aes xsave avx f16c rdrand hypervisor lahf_lm abm 3dnowprefetch invpcid_single ssbd ibrs ibpb stibp fsgsbase tsc_adjust bmi1 avx2 smep bmi2 invpcid rdseed adx smap clflushopt xsaveopt xsavec xgetbv1 xsaves arat flush_l1d arch_capabilities\nbugs\t\t: spectre_v1 spectre_v2 spec_store_bypass mds swapgs itlb_multihit srbds\nbogomips\t: 7200.02\nclflush size\t: 64\ncache_alignment\t: 64\naddress sizes\t: 45 bits physical, 48 bits virtual\npower management:\n\nprocessor\t: 2\nvendor_id\t: GenuineIntel\ncpu family\t: 6\nmodel\t\t: 158\nmodel name\t: Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz\nstepping\t: 12\nmicrocode\t: 0xffffffff\ncpu MHz\t\t: 3600.011\ncache size\t: 16384 KB\nphysical id\t: 0\nsiblings\t: 6\ncore id\t\t: 2\ncpu cores\t: 6\napicid\t\t: 2\ninitial apicid\t: 2\nfpu\t\t: yes\nfpu_exception\t: yes\ncpuid level\t: 22\nwp\t\t: yes\nflags\t\t: fpu vme de pse tsc msr pae mce cx8 apic sep mtrr pge mca cmov pat pse36 clflush mmx fxsr sse sse2 ss ht syscall nx pdpe1gb rdtscp lm constant_tsc arch_perfmon nopl xtopology tsc_reliable nonstop_tsc cpuid pni pclmulqdq ssse3 fma cx16 pcid sse4_1 sse4_2 x2apic movbe popcnt tsc_deadline_timer aes xsave avx f16c rdrand hypervisor lahf_lm abm 3dnowprefetch invpcid_single ssbd ibrs ibpb stibp fsgsbase tsc_adjust bmi1 avx2 smep bmi2 invpcid rdseed adx smap clflushopt xsaveopt xsavec xgetbv1 xsaves arat flush_l1d arch_capabilities\nbugs\t\t: spectre_v1 spectre_v2 spec_store_bypass mds swapgs itlb_multihit srbds\nbogomips\t: 7200.02\nclflush size\t: 64\ncache_alignment\t: 64\naddress sizes\t: 45 bits physical, 48 bits virtual\npower management:\n\nprocessor\t: 3\nvendor_id\t: GenuineIntel\ncpu family\t: 6\nmodel\t\t: 158\nmodel name\t: Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz\nstepping\t: 12\nmicrocode\t: 0xffffffff\ncpu MHz\t\t: 3600.011\ncache size\t: 16384 KB\nphysical id\t: 0\nsiblings\t: 6\ncore id\t\t: 3\ncpu cores\t: 6\napicid\t\t: 3\ninitial apicid\t: 3\nfpu\t\t: yes\nfpu_exception\t: yes\ncpuid level\t: 22\nwp\t\t: yes\nflags\t\t: fpu vme de pse tsc msr pae mce cx8 apic sep mtrr pge mca cmov pat pse36 clflush mmx fxsr sse sse2 ss ht syscall nx pdpe1gb rdtscp lm constant_tsc arch_perfmon nopl xtopology tsc_reliable nonstop_tsc cpuid pni pclmulqdq ssse3 fma cx16 pcid sse4_1 sse4_2 x2apic movbe popcnt tsc_deadline_timer aes xsave avx f16c rdrand hypervisor lahf_lm abm 3dnowprefetch invpcid_single ssbd ibrs ibpb stibp fsgsbase tsc_adjust bmi1 avx2 smep bmi2 invpcid rdseed adx smap clflushopt xsaveopt xsavec xgetbv1 xsaves arat flush_l1d arch_capabilities\nbugs\t\t: spectre_v1 spectre_v2 spec_store_bypass mds swapgs itlb_multihit srbds\nbogomips\t: 7200.02\nclflush size\t: 64\ncache_alignment\t: 64\naddress sizes\t: 45 bits physical, 48 bits virtual\npower management:\n\nprocessor\t: 4\nvendor_id\t: GenuineIntel\ncpu family\t: 6\nmodel\t\t: 158\nmodel name\t: Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz\nstepping\t: 12\nmicrocode\t: 0xffffffff\ncpu MHz\t\t: 3600.011\ncache size\t: 16384 KB\nphysical id\t: 0\nsiblings\t: 6\ncore id\t\t: 4\ncpu cores\t: 6\napicid\t\t: 4\ninitial apicid\t: 4\nfpu\t\t: yes\nfpu_exception\t: yes\ncpuid level\t: 22\nwp\t\t: yes\nflags\t\t: fpu vme de pse tsc msr pae mce cx8 apic sep mtrr pge mca cmov pat pse36 clflush mmx fxsr sse sse2 ss ht syscall nx pdpe1gb rdtscp lm constant_tsc arch_perfmon nopl xtopology tsc_reliable nonstop_tsc cpuid pni pclmulqdq ssse3 fma cx16 pcid sse4_1 sse4_2 x2apic movbe popcnt tsc_deadline_timer aes xsave avx f16c rdrand hypervisor lahf_lm abm 3dnowprefetch invpcid_single ssbd ibrs ibpb stibp fsgsbase tsc_adjust bmi1 avx2 smep bmi2 invpcid rdseed adx smap clflushopt xsaveopt xsavec xgetbv1 xsaves arat flush_l1d arch_capabilities\nbugs\t\t: spectre_v1 spectre_v2 spec_store_bypass mds swapgs itlb_multihit srbds\nbogomips\t: 7200.02\nclflush size\t: 64\ncache_alignment\t: 64\naddress sizes\t: 45 bits physical, 48 bits virtual\npower management:\n\nprocessor\t: 5\nvendor_id\t: GenuineIntel\ncpu family\t: 6\nmodel\t\t: 158\nmodel name\t: Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz\nstepping\t: 12\nmicrocode\t: 0xffffffff\ncpu MHz\t\t: 3600.011\ncache size\t: 16384 KB\nphysical id\t: 0\nsiblings\t: 6\ncore id\t\t: 5\ncpu cores\t: 6\napicid\t\t: 5\ninitial apicid\t: 5\nfpu\t\t: yes\nfpu_exception\t: yes\ncpuid level\t: 22\nwp\t\t: yes\nflags\t\t: fpu vme de pse tsc msr pae mce cx8 apic sep mtrr pge mca cmov pat pse36 clflush mmx fxsr sse sse2 ss ht syscall nx pdpe1gb rdtscp lm constant_tsc arch_perfmon nopl xtopology tsc_reliable nonstop_tsc cpuid pni pclmulqdq ssse3 fma cx16 pcid sse4_1 sse4_2 x2apic movbe popcnt tsc_deadline_timer aes xsave avx f16c rdrand hypervisor lahf_lm abm 3dnowprefetch invpcid_single ssbd ibrs ibpb stibp fsgsbase tsc_adjust bmi1 avx2 smep bmi2 invpcid rdseed adx smap clflushopt xsaveopt xsavec xgetbv1 xsaves arat flush_l1d arch_capabilities\nbugs\t\t: spectre_v1 spectre_v2 spec_store_bypass mds swapgs itlb_multihit srbds\nbogomips\t: 7200.02\nclflush size\t: 64\ncache_alignment\t: 64\naddress sizes\t: 45 bits physical, 48 bits virtual\npower management:";
                var result = File.ReadAllText("/proc/cpuinfo");
                var match = Regex.Match(result, @"model name.*:.(.*)");
                if (!match.Success || match.Groups.Count < 2)
                {
                    return null;
                }

                return match.Groups[1].ToString();
            }

            if (OperatingSystem.IsMacOS())
            {
                return GetProcessOutput("sysctl", "-n machdep.cpu.brand_string")?.TrimEnd('\r', '\n');
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        return null;
    }

    public static bool SelectFileOnExplorer(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            StartProcess("explorer.exe", $"/select,\"{filePath}\"");
        }
        else
        {
            var path = Path.GetDirectoryName(filePath);
            if (path is null) return false;
            StartProcess(path);
        }

        return true;
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using (Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) {}
            }
            else if (OperatingSystem.IsLinux())
            {
                using (Process.Start("xdg-open", url)) {}
            }
            else if (OperatingSystem.IsMacOS())
            {
                using (Process.Start("open", url)) {}
            }
            else
            {
                // throw 
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

    }

    public static void StartProcess(string name, string? arguments = null, bool waitForCompletion = false, int waitTimeout = Timeout.Infinite)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(name, arguments!) {UseShellExecute = true});
            if (process is null) return;
            if (waitForCompletion) process.WaitForExit(waitTimeout);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    public static void StartThisApplication(string? arguments = null)
    {
        var executable = Environment.ProcessPath;

        if (File.Exists(executable)) // Direct execute
        {
            StartProcess(executable, arguments);
        }
    }

    public static string GetExecutableName(string executable)
    {
        return OperatingSystem.IsWindows() ? $"{executable}.exe" : executable;
    }

    public static string? GetProcessOutput(string filename, string? arguments = null)
    {
        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = filename,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });
        if (proc is null) return null;
        var stringBuilder = new StringBuilder();
        while (!proc.HasExited)
        {
            stringBuilder.Append(proc.StandardOutput.ReadToEnd());
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Gets if is running under Linux and under AppImage format
    /// </summary>
    public static bool IsRunningLinuxAppImage(out string? path)
    {
        path = null;
        if (!OperatingSystem.IsLinux()) return false;
        path = Environment.GetEnvironmentVariable("APPIMAGE");
        return !string.IsNullOrWhiteSpace(path);
    }

    /// <summary>
    /// Gets if is running under Linux and under AppImage format
    /// </summary>
    /// <returns></returns>
    public static bool IsRunningLinuxAppImage() => IsRunningLinuxAppImage(out _);

    /// <summary>
    /// Gets if is running under MacOS and under app format
    /// </summary>
    public static bool IsRunningMacOSApp => OperatingSystem.IsMacOS() && AppContext.BaseDirectory.EndsWith(Path.Combine(".app", "Contents", $"MacOS{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// Gets the main name of the operative system
    /// </summary>
    public static string OperatingSystemName
    {
        get
        {
            if (OperatingSystem.IsWindows()) return "Windows";
            if (OperatingSystem.IsMacOS()) return "macOS";
            if (OperatingSystem.IsLinux()) return "Linux";
            if (OperatingSystem.IsFreeBSD()) return "FreeBSD";
            if (OperatingSystem.IsAndroid()) return "Android";
            if (OperatingSystem.IsIOS()) return "iOS";
            if (OperatingSystem.IsTvOS()) return "Tv OS";
            if (OperatingSystem.IsWatchOS()) return "WatchOS";
            if (OperatingSystem.IsBrowser()) return "Browser";
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets the main name of the operative system with architecture
    /// </summary>
    public static string OperatingSystemNameWithArch => $"{OperatingSystemName} {RuntimeInformation.OSArchitecture}";
}