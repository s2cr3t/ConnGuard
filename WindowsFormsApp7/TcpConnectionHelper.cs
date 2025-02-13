using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace WindowsFormsApp7
{
    public static class TcpConnectionHelper
    {
        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort,
            int ipVersion, TCP_TABLE_CLASS tblClass, uint reserved = 0);

        // 获取TCP连接与PID映射
        public static Dictionary<string, int> GetPidMapping()
        {
            Dictionary<string, int> mapping = new Dictionary<string, int>();
            int buffSize = 0;

            // 获取表的大小
            GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS);

            IntPtr buffTable = Marshal.AllocHGlobal(buffSize);
            try
            {
                uint ret = GetExtendedTcpTable(buffTable, ref buffSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS);
                if (ret != 0)
                    return mapping;

                int rowsCount = Marshal.ReadInt32(buffTable);
                IntPtr rowPtr = (IntPtr)((long)buffTable + 4);
                for (int i = 0; i < rowsCount; i++)
                {
                    MIB_TCPROW_OWNER_PID tcpRow = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                    IPEndPoint localEP = new IPEndPoint(tcpRow.LocalAddr,
                        BitConverter.ToUInt16(new byte[2] { tcpRow.LocalPort[1], tcpRow.LocalPort[0] }, 0));
                    IPEndPoint remoteEP = new IPEndPoint(tcpRow.RemoteAddr,
                        BitConverter.ToUInt16(new byte[2] { tcpRow.RemotePort[1], tcpRow.RemotePort[0] }, 0));

                    string key = $"TCP-{localEP}-{remoteEP}";
                    if (!mapping.ContainsKey(key))
                    {
                        mapping.Add(key, (int)tcpRow.OwningPid);
                    }

                    rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(tcpRow));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffTable);
            }
            return mapping;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint State;
            public uint LocalAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] LocalPort;
            public uint RemoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] RemotePort;
            public uint OwningPid;
        }


        public enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }
    }
}
