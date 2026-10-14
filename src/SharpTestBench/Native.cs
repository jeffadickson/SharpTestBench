using System.Runtime.InteropServices;

namespace SharpTestBench;
internal static class Native
{
    const string Lib = "sharptestbench_host";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int Callback(nint data);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_start_of_sim_time_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_start_of_sim_time_callback(Callback cb, nint data);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_timed_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_timed_callback(Callback cb, nint data, ulong delay);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_value_change_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_value_change_callback(Callback cb, nint data, nint signal, int edge);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_readonly_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_readonly_callback(Callback cb, nint data);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_readwrite_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_readwrite_callback(Callback cb, nint data);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_nexttime_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_nexttime_callback(Callback cb, nint data);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_remove_cb", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_remove_cb(nint cb);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_root_handle", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_root_handle([MarshalAs(UnmanagedType.LPUTF8Str)] string? name);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_handle_by_name", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_handle_by_name(nint parent, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int mode);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_handle_by_index", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_handle_by_index(nint parent, int index);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_num_elems", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_get_num_elems(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_range_left", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_get_range_left(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_range_right", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_get_range_right(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_object_type", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_get_object_type(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_name_str", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_signal_name_str(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_value_binstr", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_signal_value_binstr(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_value_real", CallingConvention = CallingConvention.Cdecl)]
    internal static extern double gpi_get_signal_value_real(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_value_str", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_signal_value_str(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_set_signal_value_binstr", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_set_signal_value_binstr(nint signal, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int action);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_set_signal_value_real", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_set_signal_value_real(nint signal, double value, int action);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_set_signal_value_str", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_set_signal_value_str(nint signal, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int action);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_iterate", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_iterate(nint parent, int kind);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_next", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_next(nint iterator);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_sim_time", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_get_sim_time(out uint hi, out uint lo);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_sim_precision", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_get_sim_precision(out int precision);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_finish", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_finish();
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_is_signed", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_is_signed(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_simulator_product", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_simulator_product();
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_set_signal_value_int", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_set_signal_value_int(nint signal, int value, int action);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_range_dir", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_get_range_dir(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_is_constant", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int gpi_is_constant(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_value_long", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long gpi_get_signal_value_long(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_set_signal_value_str", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gpi_set_signal_value_str_bytes(nint signal, byte[] value, int action);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_register_end_of_sim_time_callback", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_register_end_of_sim_time_callback(Callback cb, nint data);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_fullname_str", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_signal_fullname_str(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_signal_type_str", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_signal_type_str(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_definition_name", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_definition_name(nint signal);
    [DllImport(Lib, EntryPoint = "cs_v1_gpi_get_definition_file", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint gpi_get_definition_file(nint signal);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint cs_abi_version();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint cs_bind_gpi();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong cs_abi_features();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int cs_v1_vpi_packed_ranges(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, uint bits, [Out] int[] pairs, uint capacity, out uint count);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe int cs_v1_read_planes(nint signal, uint bits, byte* buffer, uint length);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe int cs_v1_write_planes(nint signal, uint bits, byte* buffer, uint length, int action);
    internal static void VerifyAbi()
    {
        uint actual = cs_abi_version();
        if (actual != 1)
            throw new NotSupportedException($"SharpTestBench native ABI {actual}; expected 1");
        try
        {
            if ((cs_abi_features() & 1) == 0)
                throw new NotSupportedException("Native host lacks packed-plane support; upgrade the host");
        }
        catch (EntryPointNotFoundException e)
        {
            throw new NotSupportedException("Native host predates packed-plane support; upgrade the host", e);
        }

        nint error = cs_bind_gpi();
        if (error != 0)
            throw new NotSupportedException("Incompatible GPI: " + Text(error));
    }

    internal static string Text(nint p) => Marshal.PtrToStringUTF8(p) ?? throw new InvalidOperationException("Null native string");
}
