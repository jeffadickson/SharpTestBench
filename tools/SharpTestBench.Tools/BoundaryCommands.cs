using System.Runtime.InteropServices;
namespace SharpTestBench.Tools;
internal sealed class NativeProbe : IDisposable
{
    readonly nint library;
    public NativeProbe(string path) => library = NativeLibrary.Load(Path.GetFullPath(path));
    public T Function<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
    public void Dispose() => NativeLibrary.Free(library);
}
internal static class BoundaryCommands
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint Bind();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Read(nint signal, uint width, nint output, uint bytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Write(nint signal, uint width, nint output, uint bytes, int action);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int BatchWrite(nint entries, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int BatchRead(nint handles, nint widths, nint values, uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Count();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate ulong Delay();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Callback(nint data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate nint Start(nint signal, ulong high, ulong low, Callback callback, nint data, out int status);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Stop(nint clock);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Update(nint clock, ulong high, ulong low);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Fire(int timer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Reset();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Hook(Callback callback);
    [StructLayout(LayoutKind.Sequential)] struct Entry { public nint Signal; public uint Value; public int Action; }
    static int checks;
    static void Check<T>(T actual, T expected) { if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new InvalidDataException($"Native boundary expected {expected}, observed {actual}"); checks++; }
    static void Bytes(nint pointer, byte[] expected) { var actual = new byte[expected.Length]; Marshal.Copy(pointer, actual, 0, actual.Length); Check(actual.SequenceEqual(expected), true); }
    [DllImport("libc", EntryPoint = "setenv", SetLastError = true)] static extern int SetNative([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);
    [DllImport("libc", EntryPoint = "unsetenv", SetLastError = true)] static extern int UnsetNative([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    static void Set(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        if (!OperatingSystem.IsWindows() && (value is null ? UnsetNative(name) : SetNative(name, value, 1)) != 0) throw new IOException("Unable to set native fixture environment: " + name);
    }
    public static void Execute(string command, string[] arguments)
    {
        if (arguments.Length != 1) throw new ArgumentException(command + " requires the host library path");
        checks = 0;
        using var host = new NativeProbe(arguments[0]); using var fake = new NativeProbe(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(arguments[0]))!, "libgpi.so"));
        Check(host.Function<Bind>("cs_bind_gpi")(), nint.Zero);
        if (command == "packed-boundary") Packed(host, fake); else if (command == "clock-boundary") Clock(host, fake); else throw new ArgumentException("Unknown boundary command");
        Console.WriteLine($"PASS {(command == "clock-boundary" ? "native-clock-boundary" : command)}: {checks} checks");
    }
    static void Packed(NativeProbe host, NativeProbe fake)
    {
        var read = host.Function<Read>("cs_v1_read_planes"); var write = host.Function<Write>("cs_v1_write_planes");
        var guarded = Marshal.AllocHGlobal(8);
        try
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xaa, 8).ToArray(), 0, guarded, 8); var pointer = guarded + 1;
            Check(read(1, 9, pointer, 6), 0); Bytes(guarded, [0xaa, 9, 1, 0x66, 0, 0x22, 0, 0xaa]);
            Check(read(1, 9, 0, 6), 1); Check(read(0, 9, pointer, 6), 1); Check(read(1, 0, pointer, 0), 1); Check(read(1, 1048577, pointer, 6), 1); Check(read(1, 9, pointer, 5), 1); Check(read(1, 8, pointer, 3), 2);
            Check(write(1, 9, pointer, 6, 0), 0); Check(write(1, 9, pointer, 6, 99), 1); Check(write(1, 8, pointer, 3, 0), 2);
            Marshal.WriteByte(guarded, 2, 0x80); Check(write(1, 9, pointer, 6, 0), 1); Marshal.WriteByte(guarded, 2, 1);
            Marshal.WriteByte(guarded, 1, 11); Check(write(1, 9, pointer, 6, 0), 1); Marshal.WriteByte(guarded, 1, 9);
            Marshal.WriteByte(guarded, 5, 0xa2); Check(write(1, 9, pointer, 6, 0), 1); Marshal.WriteByte(guarded, 5, 0x22);
            Set("CS_FAKE_CONSTANT", "1"); Check(write(1, 9, pointer, 6, 0), 6); Set("CS_FAKE_CONSTANT", null);
            Set("CS_FAKE_LOGIC", "UX01ZWLH-"); Check(read(1, 9, pointer, 6), 3); Set("CS_FAKE_LOGIC", "101"); Check(read(1, 9, pointer, 6), 5);
            Check(Marshal.ReadByte(guarded), (byte)0xaa); Check(Marshal.ReadByte(guarded, 7), (byte)0xaa);
            foreach (var text in new[] { "U01", "U000000000" }) { Set("CS_FAKE_LOGIC", text); Check(read(1, 9, pointer, 6), 5); }
        }
        finally { Marshal.FreeHGlobal(guarded); }
        foreach (uint width in new uint[] { 1, 7, 8, 127, 128, 129, 4096, 4097, 8193 })
        {
            Set("CS_FAKE_WIDTH", width.ToString(System.Globalization.CultureInfo.InvariantCulture)); var text = new string(Enumerable.Range(0, (int)width).Select(index => "01xZ"[index % 4]).ToArray()); Set("CS_FAKE_LOGIC", text);
            var bytes = (int)((width + 7) / 8); var storage = Marshal.AllocHGlobal(3 * bytes + 2);
            try
            {
                Marshal.Copy(Enumerable.Repeat((byte)0xaa, 3 * bytes + 2).ToArray(), 0, storage, 3 * bytes + 2); Check(read(1, width, storage + 1, (uint)(3 * bytes)), 0);
                var expected = new byte[3 * bytes + 2]; expected[0] = expected[^1] = 0xaa;
                for (int index = 0; index < width; index++) { var symbol = text[^(index + 1)]; var mask = (byte)(1 << index % 8); if (symbol == '1') expected[1 + index / 8] |= mask; if (symbol is 'x' or 'Z') expected[1 + bytes + index / 8] |= mask; if (symbol == 'Z') expected[1 + 2 * bytes + index / 8] |= mask; }
                Bytes(storage, expected); Check(write(1, width, storage + 1, (uint)(3 * bytes), 0), 0);
            }
            finally { Marshal.FreeHGlobal(storage); }
        }
        Set("CS_FAKE_WIDTH", "9"); Set("CS_FAKE_LOGIC", "100000001");
        var batchWrite = host.Function<BatchWrite>("cs_v1_write_u32_batch"); var batchRead = host.Function<BatchRead>("cs_v1_read_u32_batch"); var writes = fake.Function<Count>("stb_test_writes");
        var size = Marshal.SizeOf<Entry>(); var entries = Marshal.AllocHGlobal(size * 2); var handles = Marshal.AllocHGlobal(nint.Size * 2); var widths = Marshal.AllocHGlobal(8); var values = Marshal.AllocHGlobal(8);
        try
        {
            void EntryAt(int index, uint value, int action = 0) => Marshal.StructureToPtr(new Entry { Signal = 1, Value = value, Action = action }, entries + size * index, false);
            EntryAt(0, 256); EntryAt(1, 512); var before = writes(); Check(batchWrite(entries, 2), 1); Check(writes(), before);
            EntryAt(1, 1); Check(batchWrite(entries, 2), 0); Check(writes(), before + 2); EntryAt(1, 1, 1); Check(batchWrite(entries, 2), 1); Check(writes(), before + 2);
            Check(batchWrite(0, 0), 0); Check(batchWrite(0, 1), 1); Check(batchWrite(entries, 1048577), 1);
            Marshal.WriteIntPtr(handles, 1); Marshal.WriteIntPtr(handles, nint.Size, 1); Marshal.WriteInt32(widths, 9); Marshal.WriteInt32(widths, 4, 9);
            Check(batchRead(handles, widths, values, 2), 0); Check(Marshal.ReadInt32(values), 257); Check(Marshal.ReadInt32(values, 4), 257);
            Set("CS_FAKE_LOGIC", "10000000X"); Check(batchRead(handles, widths, values, 2), 3); Marshal.WriteInt32(widths, 33); Check(batchRead(handles, widths, values, 2), 2); Check(batchRead(0, 0, 0, 0), 0); Check(batchRead(0, 0, 0, 1), 1);
        }
        finally { Marshal.FreeHGlobal(entries); Marshal.FreeHGlobal(handles); Marshal.FreeHGlobal(widths); Marshal.FreeHGlobal(values); }
    }
    static void Clock(NativeProbe host, NativeProbe fake)
    {
        var start = host.Function<Start>("cs_v1_clock_start"); var stop = host.Function<Stop>("cs_v1_clock_stop"); var update = host.Function<Update>("cs_v1_clock_update");
        var delay = fake.Function<Delay>("stb_test_delay"); var fire = fake.Function<Fire>("stb_test_fire"); var reset = fake.Function<Reset>("stb_test_reset"); var pending = fake.Function<Count>("stb_test_pending"); var value = fake.Function<Count>("stb_test_value"); var writes = fake.Function<Count>("stb_test_writes");
        Set("CS_FAKE_CLOCK", "1"); Set("CS_FAKE_WIDTH", "1"); nint owned = 0; int failures = 0; var callbackResults = new List<int>();
        Callback failed = _ => { failures++; callbackResults.Add(stop(owned)); owned = 0; return 0; };
        Callback cancel = _ => { callbackResults.Add(stop(owned)); owned = 0; return 0; };
        void Begin(ulong high = 4, ulong low = 5, int expected = 0) { owned = start(1, high, low, failed, 0, out var status); Check(status, expected); Check(owned != 0, expected == 0); }
        Begin(0, 5, 1); Set("CS_FAKE_WIDTH", "9"); Begin(expected: 2); Set("CS_FAKE_WIDTH", "1"); Set("CS_FAKE_CONSTANT", "1"); Begin(expected: 6); Set("CS_FAKE_CONSTANT", null);
        Set("CS_FAKE_CLOCK_FAIL_AT", "1"); Begin(expected: 8); Set("CS_FAKE_CLOCK_FAIL_AT", null); reset();
        Begin(); Check(delay(), 5UL); Check(update(owned, 2, 5), 0); fire(1); Check(delay(), 4UL); Check(pending(), 2); fire(0); Check(value(), 1); fire(1); Check(delay(), 5UL); fire(0); Check(value(), 0); fire(1); Check(delay(), 2UL);
        Check(stop(owned), 0); owned = 0; Check(pending(), 0); Check(writes(), 2);
        foreach (var (variable, setting) in new[] { ("CS_FAKE_CLOCK_FAIL_RW", "1"), ("CS_FAKE_CLOCK_FAIL_AT", "2") }) { reset(); Set(variable, setting); Begin(); fire(1); Check(owned, nint.Zero); Check(pending(), 0); Set(variable, null); }
        Check(failures, 2); reset(); Begin(); fake.Function<Hook>("stb_test_write_hook")(cancel); fire(1); fire(0); Check(owned, nint.Zero); Check(pending(), 0);
        reset(); Begin(); Set("CS_FAKE_CLOCK_REMOVE_FAIL", "1"); Check(stop(owned), 8); fire(1); Check(writes(), 0); Set("CS_FAKE_CLOCK_REMOVE_FAIL", null); Check(stop(owned), 0); owned = 0; Check(pending(), 0);
        foreach (var result in callbackResults) Check(result, 0); GC.KeepAlive(failed); GC.KeepAlive(cancel);
    }
}
