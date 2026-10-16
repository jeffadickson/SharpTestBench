/** @file icarus_layouts.cpp
 * @brief Optional Icarus elaboration target exporting packed dimensions before VVP flattening.
 * Uses only ivl_target.h APIs. The compiler owns all handles/strings for the call.
 * This helper is separate from the simulation host and does not change runtime ABI.
 */
#include <ivl_target.h>
#include <cstdint>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
/** @brief Encode compiler-owned identifiers as JSON, including escaped HDL names. */
void quote(std::ostream &out, const char *value) {
    out << '"';
    for (const unsigned char *p = reinterpret_cast<const unsigned char *>(value); *p; ++p) {
        if (*p == '"' || *p == '\\') out << '\\' << static_cast<char>(*p);
        else if (*p < 32 || *p >= 127) {
            constexpr char hex[] = "0123456789abcdef";
            out << "\\u00" << hex[*p >> 4] << hex[*p & 15];
        } else out << static_cast<char>(*p);
    }
    out << '"';
}

/** @brief Walk scopes with component paths; dots inside escaped identifiers are not separators. */
void scope(std::ostream &out, ivl_scope_t object, std::vector<std::string> &path, bool &first) {
    if (path.size() >= 64) throw std::runtime_error("Scope depth exceeds 64");
    path.emplace_back(ivl_scope_basename(object));
    for (unsigned i = 0; i < ivl_scope_sigs(object); ++i) {
        const auto signal = ivl_scope_sig(object, i);
        const unsigned rank = ivl_signal_packed_dimensions(signal);
        if (!rank || ivl_signal_local(signal)) continue;
        if (rank > 64) throw std::runtime_error("Packed rank exceeds 64");
        if (!first) out << ',';
        first = false;
        out << "{\"path\":[";
        for (const auto &part : path) { quote(out, part.c_str()); out << ','; }
        quote(out, ivl_signal_basename(signal));
        out << "],\"width\":" << ivl_signal_width(signal) << ",\"dimensions\":[";
        std::uint64_t product = 1;
        for (unsigned d = 0; d < rank; ++d) {
            const int left = ivl_signal_packed_msb(signal, d), right = ivl_signal_packed_lsb(signal, d);
            const std::int64_t delta = static_cast<std::int64_t>(left) - right;
            const auto length = static_cast<std::uint64_t>(delta < 0 ? -delta : delta) + 1;
            if (length > 1048576 || product > 1048576 / length)
                throw std::runtime_error("Packed width exceeds 1048576");
            product *= length;
            if (d) out << ',';
            out << '[' << left << ',' << right << ']';
        }
        if (product != ivl_signal_width(signal)) throw std::runtime_error("Dimension product differs from signal width");
        out << "]}";
    }
    for (size_t i = 0; i < ivl_scope_childs(object); ++i) scope(out, ivl_scope_child(object, i), path, first);
    path.pop_back();
}
} // namespace

/** @brief Emit a complete elaborated layout document; exceptions never cross the C target ABI. */
extern "C" int target_design(ivl_design_t design) {
    try {
        const char *filename = ivl_design_flag(design, "-o");
        if (!filename) return 1;
        std::ofstream output(filename);
        output.exceptions(std::ios::failbit | std::ios::badbit);
        output << "{\"schema\":1,\"provider\":\"icarus-target\",\"signals\":[";
        ivl_scope_t *roots = nullptr; unsigned count = 0;
        ivl_design_roots(design, &roots, &count);
        bool first = true; std::vector<std::string> path;
        for (unsigned i = 0; i < count; ++i) scope(output, roots[i], path, first);
        output << "]}\n";
        output.close();
        return 0;
    } catch (...) { return 1; }
}

/** @brief Identify the target to Icarus; unknown target queries are explicitly unavailable. */
extern "C" const char *target_query(const char *key) {
    return key && std::string(key) == "version" ? "SharpTestBench packed layout exporter 1" : nullptr;
}
