/** @file transport.inc
 * @brief Optional VPI vector transport and ordered numeric transfer batches.
 * Included by abi.cpp after its checked GPI bindings. No private GPI layout is used.
 */
#include <unordered_map>
#include <memory>
#include <new>

namespace {
/** @brief Process-local VPI exports and owned name-resolved handles, confined to the simulator thread. */
struct DirectVpi {
    decltype(&vpi_handle_by_name) by_name = nullptr;
    decltype(&vpi_get) get = nullptr;
    decltype(&vpi_get_value) read = nullptr;
    decltype(&vpi_put_value) write = nullptr;
    decltype(&vpi_free_object) release = nullptr;
    decltype(&vpi_chk_error) error = nullptr;
    std::unordered_map<void *, vpiHandle> handles;
};
DirectVpi direct_vpi;

/** @brief Match a complete simulator family name, allowing conventional version delimiters. */
bool direct_product_is(const char *product, const char *family, size_t length) {
    if (!product || strncmp(product, family, length) != 0) return false;
    const char next = product[length];
    return next == '\0' || next == ' ' || next == '\t' || next == '(';
}

/** @brief Read vector words immediately; VPI owns and may reuse the returned storage on the next call. */
int32_t direct_read(vpiHandle object, uint32_t bits, uint8_t *buffer) {
    s_vpi_value value{};
    value.format = vpiVectorVal;
    if (direct_vpi.error) direct_vpi.error(nullptr);
    direct_vpi.read(object, &value);
    if ((direct_vpi.error && direct_vpi.error(nullptr)) || !value.value.vector) return 7;
    const uint32_t n = (bits + 7) / 8;
    for (uint32_t i = 0; i < n; ++i) {
        const auto &word = value.value.vector[i / 4];
        const uint32_t shift = (i % 4) * 8;
        const uint8_t a = static_cast<uint32_t>(word.aval) >> shift;
        const uint8_t b = static_cast<uint32_t>(word.bval) >> shift;
        buffer[i] = a & ~b;
        buffer[n + i] = b;
        buffer[2 * n + i] = b & ~a;
    }
    // Some simulators leave unused high vector bits undefined. Canonicalize all planes.
    if (bits % 8) {
        const uint8_t mask = (1u << (bits % 8)) - 1;
        buffer[n - 1] &= mask; buffer[2 * n - 1] &= mask; buffer[3 * n - 1] &= mask;
    }
    return 0;
}

/** @brief Convert canonical byte planes to VPI aval/bval without constructing text.
 * The caller has validated width, constants, action, padding and plane disjointness.
 * VPI consumes the per-call scratch synchronously; no buffer survives this function.
 */
int32_t direct_write(vpiHandle object, uint32_t bits, const uint8_t *buffer, int32_t action) {
    const uint32_t words = (bits + 31) / 32, n = (bits + 7) / 8;
    s_vpi_vecval local[128]{};
    std::unique_ptr<s_vpi_vecval[]> heap;
    if (words > 128) {
        heap.reset(new (std::nothrow) s_vpi_vecval[words]{});
        if (!heap) return 4;
    }
    auto *vectors = heap ? heap.get() : local;
    for (uint32_t i = 0; i < n; ++i) {
        const uint32_t shift = (i % 4) * 8;
        const uint32_t a = buffer[i] | (buffer[n + i] & ~buffer[2 * n + i]);
        vectors[i / 4].aval |= static_cast<PLI_INT32>(a << shift);
        vectors[i / 4].bval |= static_cast<PLI_INT32>(static_cast<uint32_t>(buffer[n + i]) << shift);
    }
    s_vpi_value value{}; value.format = vpiVectorVal; value.value.vector = vectors;
    s_vpi_time time{}; time.type = vpiSimTime;
    if (direct_vpi.error) direct_vpi.error(nullptr);
    // Match the pinned VPI adapter's release behavior: release its current value.
    if (action == GPI_RELEASE) direct_vpi.read(object, &value);
    const int flags = action == GPI_DEPOSIT ? vpiInertialDelay : action == GPI_FORCE ? vpiForceFlag
        : action == GPI_RELEASE ? vpiReleaseFlag : vpiNoDelay;
    direct_vpi.write(object, &value, flags == vpiNoDelay ? nullptr : &time, flags);
    return direct_vpi.error && direct_vpi.error(nullptr) ? 7 : 0;
}
} // namespace

/** @brief Probe direct transport once for a live known-VPI signal; failure preserves string fallback.
 * The managed capability policy calls this only for Icarus/Verilator. Native validation
 * rejects other product families as well. A separate name-resolved VPI handle is owned
 * by this cache; GPI handles are never reinterpreted or released. Return 0 enabled,
 * 1 unsupported/unavailable, 2 invalid width/kind, 4 allocation failure.
 */
int32_t cs_v1_enable_direct(void *signal) {
    if (!signal) return 2;
    if (direct_vpi.handles.contains(signal)) return 0;
    const char *product = p_gpi_get_simulator_product();
    if (!direct_product_is(product, "Icarus Verilog", 14) && !direct_product_is(product, "Verilator", 9)) return 1;
    const auto kind = p_gpi_get_object_type(static_cast<gpi_sim_hdl>(signal));
    if (kind != GPI_LOGIC && kind != GPI_LOGIC_ARRAY && kind != GPI_PACKED && kind != GPI_INTEGER && kind != GPI_ENUM) return 2;
    auto &v = direct_vpi;
    if (!v.by_name) {
        v.by_name = reinterpret_cast<decltype(v.by_name)>(dlsym(RTLD_DEFAULT, "vpi_handle_by_name"));
        v.get = reinterpret_cast<decltype(v.get)>(dlsym(RTLD_DEFAULT, "vpi_get"));
        v.read = reinterpret_cast<decltype(v.read)>(dlsym(RTLD_DEFAULT, "vpi_get_value"));
        v.write = reinterpret_cast<decltype(v.write)>(dlsym(RTLD_DEFAULT, "vpi_put_value"));
        v.release = reinterpret_cast<decltype(v.release)>(dlsym(RTLD_DEFAULT, "vpi_free_object"));
        v.error = reinterpret_cast<decltype(v.error)>(dlsym(RTLD_DEFAULT, "vpi_chk_error"));
    }
    if (!v.by_name || !v.get || !v.read || !v.write || !v.release) return 1;
    const char *name = p_gpi_get_signal_fullname_str(static_cast<gpi_sim_hdl>(signal));
    if (!name) return 1;
    vpiHandle object = v.by_name(const_cast<PLI_BYTE8 *>(name), nullptr);
    if (!object) return 1;
    const int bits = p_gpi_get_num_elems(static_cast<gpi_sim_hdl>(signal));
    if (bits < 1 || bits > 1048576 || v.get(vpiSize, object) != bits) { v.release(object); return 2; }
    try { v.handles.emplace(signal, object); }
    catch (const std::bad_alloc &) { v.release(object); return 4; }
    return 0;
}

/** @brief Release only handles acquired by the direct transport cache, before simulator termination. */
void cs_v1_clear_direct(void) {
    for (const auto &entry : direct_vpi.handles) direct_vpi.release(entry.second);
    direct_vpi.handles.clear();
}

/** @brief Validate a bounded numeric batch before issuing any ordered writes.
 * Only Deposit/Immediate are accepted. The caller schedules Deposit at ReadWrite.
 * Duplicates retain input order; per-handle coalescing occurs in the managed queue.
 */
int32_t cs_v1_write_u32_batch(const cs_u32_write *items, uint32_t count) {
    if ((!items && count) || count > 1048576) return 1;
    for (uint32_t i = 0; i < count; ++i) {
        const auto &item = items[i];
        if (!item.signal || (item.action != GPI_DEPOSIT && item.action != GPI_NO_DELAY)) return 1;
        if (p_gpi_is_constant(static_cast<gpi_sim_hdl>(item.signal))) return 6;
        const int bits = p_gpi_get_num_elems(static_cast<gpi_sim_hdl>(item.signal));
        if (bits < 1 || bits > 32) return 2;
        if (bits < 32 && item.value >> bits) return 1;
    }
    for (uint32_t i = 0; i < count; ++i)
        p_gpi_set_signal_value_int(static_cast<gpi_sim_hdl>(items[i].signal), static_cast<int32_t>(items[i].value), static_cast<gpi_set_action>(items[i].action));
    return 0;
}

/** @brief Sample known unsigned values in caller order without advancing simulator time.
 * Returns packed-plane status, or 3 for unresolved X/Z. Discard all output on error.
 * Widths and destinations are explicit; this is a snapshot within one simulator phase,
 * not an HDL-atomic transaction or a lock against simulator callbacks.
 */
int32_t cs_v1_read_u32_batch(void *const *signals, const uint32_t *widths, uint32_t *values, uint32_t count) {
    if ((!signals || !widths || !values) && count) return 1;
    if (count > 1048576) return 1;
    // Preserve the documented all-or-nothing output contract. Most protocol
    // snapshots fit on the stack; large batches use an owned temporary buffer.
    uint32_t small[32]{};
    uint32_t *scratch = count <= 32 ? small : static_cast<uint32_t *>(malloc(static_cast<size_t>(count) * sizeof(uint32_t)));
    if (!scratch) return 4;
    int32_t result = 0;
    for (uint32_t i = 0; i < count; ++i) {
        const uint32_t bits = widths[i];
        if (bits < 1 || bits > 32) { result = 2; break; }
        const uint32_t n = (bits + 7) / 8;
        uint32_t value = 0;
        // Cached direct-VPI handles already have checked object type and width.
        // Read one aval/bval word without constructing three byte planes.
        const auto cached = direct_vpi.handles.find(signals[i]);
        if (cached != direct_vpi.handles.end() && direct_vpi.read) {
            s_vpi_value v{};
            v.format = vpiVectorVal;
            if (direct_vpi.error) direct_vpi.error(nullptr);
            direct_vpi.read(cached->second, &v);
            if ((direct_vpi.error && direct_vpi.error(nullptr)) || !v.value.vector) {
                result = 7; break;
            }
            const uint32_t mask = bits == 32 ? 0xffffffffu : (1u << bits) - 1u;
            if ((static_cast<uint32_t>(v.value.vector[0].bval) & mask) != 0) {
                result = 3; break;
            }
            value = static_cast<uint32_t>(v.value.vector[0].aval) & mask;
        } else {
            uint8_t planes[12]{};
            const int status = cs_v1_read_planes(signals[i], bits, planes, 3 * n);
            if (status) { result = status; break; }
            for (uint32_t j = 0; j < n; ++j) {
                if (planes[n + j] || planes[2 * n + j]) { result = 3; break; }
                value |= static_cast<uint32_t>(planes[j]) << (j * 8);
            }
        }
        if (result) break;
        scratch[i] = value;
    }
    if (!result && count) memcpy(values, scratch, static_cast<size_t>(count) * sizeof(uint32_t));
    if (scratch != small) free(scratch);
    return result;
}
