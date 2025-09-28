// Sqlite3MC.cs
// .NET 8 wrapper for sqlite3mc_x64.dll (Windows x64).
// Adapted from Unity version for .NET 8 console application

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UmaDecryptor.Database;

public static class Sqlite3MC
{
    // ---- DLL name: ensure sqlite3mc_x64.dll is available ----
    private const string DLL = "sqlite3mc_x64";

    // ---- Common SQLite constants ----
    public const int SQLITE_OK = 0;
    public const int SQLITE_ROW = 100;
    public const int SQLITE_DONE = 101;
    public const int SQLITE_OPEN_READWRITE = 0x00000002;
    public const int SQLITE_OPEN_CREATE = 0x00000004;

    // ---- Native Imports ----
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_open_v2")]
    private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr ppDb, int flags, IntPtr zVfs);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close")]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_errmsg")]
    private static extern IntPtr sqlite3_errmsg_ptr(IntPtr db);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_exec")]
    private static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string sql, IntPtr callback, IntPtr arg, out IntPtr errMsg);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_free")]
    private static extern void sqlite3_free(IntPtr ptr);

    // sqlite3mc plugin-specific
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3mc_config")]
    private static extern int sqlite3mc_config(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string paramName, int newValue);

    // Key: provide both byte[] and IntPtr signatures
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_key")]
    private static extern int sqlite3_key_bytes(IntPtr db, byte[] pKey, int nKey);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_key")]
    private static extern int sqlite3_key_ptr(IntPtr db, IntPtr pKey, int nKey);

    // Prepare/step/finalize
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
    private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
    private static extern int sqlite3_finalize(IntPtr stmt);

    // Column accessors
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
    private static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_blob")]
    private static extern IntPtr sqlite3_column_blob(IntPtr stmt, int iCol);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_bytes")]
    private static extern int sqlite3_column_bytes(IntPtr stmt, int iCol);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_int")]
    private static extern int sqlite3_column_int(IntPtr stmt, int iCol);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_int64")]
    private static extern long sqlite3_column_int64(IntPtr stmt, int iCol);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_count")]
    private static extern int sqlite3_column_count(IntPtr stmt);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_name")]
    private static extern IntPtr sqlite3_column_name(IntPtr stmt, int iCol);

    // ---- Helper: convert native C string (UTF-8) to C# string ----
    private static string? PtrToStringUTF8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        // find length
        int len = 0;
        while (Marshal.ReadByte(ptr, len) != 0) len++;
        if (len == 0) return string.Empty;
        byte[] buffer = new byte[len];
        Marshal.Copy(ptr, buffer, 0, len);
        return Encoding.UTF8.GetString(buffer);
    }

    // Get error message for DB handle
    public static string GetErrMsg(IntPtr db)
    {
        IntPtr p = sqlite3_errmsg_ptr(db);
        return PtrToStringUTF8(p) ?? $"(null errmsg, db={db})";
    }

    // ---- High-level wrappers ----

    /// <summary>Open a database. Throws Exception on failure. Flags default to read-write | create.</summary>
    public static IntPtr Open(string path, int flags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE)
    {
        int rc = sqlite3_open_v2(path, out IntPtr db, flags, IntPtr.Zero);
        if (rc != SQLITE_OK || db == IntPtr.Zero)
        {
            string em = db != IntPtr.Zero ? GetErrMsg(db) : "(no db handle)";
            throw new InvalidOperationException($"sqlite3_open_v2('{path}') failed rc={rc} errmsg={em}");
        }
        return db;
    }

    /// <summary>Close DB. Returns rc.</summary>
    public static int Close(IntPtr db)
    {
        if (db == IntPtr.Zero) return SQLITE_OK;
        return sqlite3_close(db);
    }

    /// <summary>Execute SQL (no binder). Return code; if errorMsg != null it contains message (freed automatically).</summary>
    public static int Exec(IntPtr db, string sql, out string? errorMsg)
    {
        errorMsg = null;
        int rc = sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out IntPtr errPtr);
        if (errPtr != IntPtr.Zero)
        {
            errorMsg = PtrToStringUTF8(errPtr);
            // free the error string allocated by sqlite3_exec
            sqlite3_free(errPtr);
        }
        return rc;
    }

    /// <summary>Set sqlite3mc config parameter (plugin). e.g. sqlite3mc_config(db, "cipher", index).</summary>
    public static int MC_Config(IntPtr db, string paramName, int newValue)
    {
        return sqlite3mc_config(db, paramName, newValue);
    }

    /// <summary>Set key from raw bytes.</summary>
    public static int Key_SetBytes(IntPtr db, byte[]? keyBytes)
    {
        if (keyBytes == null) return sqlite3_key_bytes(db, Array.Empty<byte>(), 0);
        return sqlite3_key_bytes(db, keyBytes, keyBytes.Length);
    }

    /// <summary>Validate whether DB can be read (try SELECT on sqlite_master).</summary>
    public static bool ValidateReadable(IntPtr db, out string? errmsg)
    {
        int rc = Exec(db, "SELECT name FROM sqlite_master LIMIT 1;", out errmsg);
        return rc == SQLITE_OK;
    }

    // Helper: prepare/step loop with callback
    public delegate void RowCallback(IntPtr stmt);
    public static void ForEachRow(string sql, IntPtr db, RowCallback rowCallback)
    {
        int rc = sqlite3_prepare_v2(db, sql, -1, out IntPtr stmt, IntPtr.Zero);
        if (rc != SQLITE_OK)
            throw new InvalidOperationException($"prepare failed rc={rc} errmsg={GetErrMsg(db)} sql={sql}");

        try
        {
            while (true)
            {
                rc = sqlite3_step(stmt);
                if (rc == SQLITE_ROW)
                {
                    rowCallback(stmt);
                }
                else if (rc == SQLITE_DONE)
                {
                    break;
                }
                else
                {
                    throw new InvalidOperationException($"step failed rc={rc} errmsg={GetErrMsg(db)}");
                }
            }
        }
        finally
        {
            sqlite3_finalize(stmt);
        }
    }

    // Column getters (UTF-8 text or blob)
    public static string? ColumnText(IntPtr stmt, int col)
    {
        IntPtr p = sqlite3_column_text(stmt, col);
        return PtrToStringUTF8(p);
    }

    public static byte[]? ColumnBlob(IntPtr stmt, int col)
    {
        IntPtr p = sqlite3_column_blob(stmt, col);
        if (p == IntPtr.Zero) return null;
        int len = sqlite3_column_bytes(stmt, col);
        if (len <= 0) return new byte[0];
        byte[] buf = new byte[len];
        Marshal.Copy(p, buf, 0, len);
        return buf;
    }

    public static int ColumnInt(IntPtr stmt, int col) => sqlite3_column_int(stmt, col);
    public static long ColumnInt64(IntPtr stmt, int col) => sqlite3_column_int64(stmt, col);
    
    public static int ColumnCount(IntPtr stmt) => sqlite3_column_count(stmt);
    
    public static string? ColumnName(IntPtr stmt, int col)
    {
        IntPtr p = sqlite3_column_name(stmt, col);
        return PtrToStringUTF8(p);
    }
}