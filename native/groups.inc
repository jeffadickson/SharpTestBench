/** @file groups.inc
 * @brief Bounded packed transfer groups using existing simulator operations.
 * Descriptors and buffers remain caller-owned. No pointer is retained, no private
 * simulator layout is assumed, and no call advances time or promises HDL atomicity.
 */
namespace {
/** @brief Check every descriptor before reading/writing; subtraction avoids offset overflow. */
int32_t validate_group(const cs_plane_transfer *items, uint32_t count, const void *buffer, uint32_t length) {
    if (count > 65536 || length > 67108864 || (!items && count) || (!buffer && length)) return 1;
    for (uint32_t i = 0; i < count; ++i) {
        const auto &item = items[i];
        if (!item.signal || !item.bits || item.bits > 1048576) return 1;
        const uint32_t size = 3 * ((item.bits + 7) / 8);
        if (item.offset > length || size > length - item.offset) return 1;
        if (p_gpi_get_num_elems(static_cast<gpi_sim_hdl>(item.signal)) != static_cast<int>(item.bits)) return 2;
    }
    return 0;
}
} // namespace

/** @brief Copy each member to its explicit offset; discard the complete snapshot on any failure.
 * Overlapping destinations are applied in input order; the managed layout disallows
 * overlap and duplicate handles. GPI/VPI scratch never survives a scalar operation.
 */
int32_t cs_v1_read_plane_batch(const cs_plane_transfer *items, uint32_t count, uint8_t *buffer, uint32_t length) {
    const int32_t validation = validate_group(items, count, buffer, length);
    if (validation) return validation;
    for (uint32_t i = 0; i < count; ++i) {
        const auto &item = items[i];
        const int32_t status = cs_v1_read_planes(item.signal, item.bits, buffer + item.offset, 3 * ((item.bits + 7) / 8));
        if (status) return status;
    }
    return 0;
}

/** @brief Prevalidate every canonical value/constant, then submit deposits in input order.
 * Validation failures issue no writes. A later allocation or simulator failure can
 * leave earlier writes applied: this is an ordered transfer, not a rollback transaction.
 * The runtime owns phase scheduling and per-signal last-write-wins coalescing.
 */
int32_t cs_v1_write_plane_batch(const cs_plane_transfer *items, uint32_t count, const uint8_t *buffer, uint32_t length) {
    const int32_t validation = validate_group(items, count, buffer, length);
    if (validation) return validation;
    for (uint32_t i = 0; i < count; ++i) {
        const auto &item = items[i];
        const int32_t status = validate_write_planes(item.signal, item.bits, buffer + item.offset, 3 * ((item.bits + 7) / 8), GPI_DEPOSIT);
        if (status) return status;
    }
    for (uint32_t i = 0; i < count; ++i) {
        const auto &item = items[i];
        const int32_t status = cs_v1_write_planes(item.signal, item.bits, buffer + item.offset, 3 * ((item.bits + 7) / 8), GPI_DEPOSIT);
        if (status) return status;
    }
    return 0;
}
