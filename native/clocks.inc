/** @file clocks.inc
 * @brief Simulator-thread native clocks with paired-pulse updates and owned callbacks.
 * Timer callbacks schedule deposits at ReadWrite, matching managed SimClock phase
 * semantics while removing per-edge managed tasks, delegates and interop crossings.
 */
namespace {
/** @brief One clock owns at most one future timer and one pending ReadWrite callback. */
struct NativeClockState {
    gpi_sim_hdl signal;
    uint64_t high, low, cycle_high;
    cs_callback failure;
    void *data;
    gpi_cb_hdl timer = nullptr, write = nullptr;
    bool level = false, stopped = false, dispose = false;
    unsigned depth = 0;
};

/** @brief Defer destruction across synchronous reentrant cancellation during a signal write. */
void leave_clock(NativeClockState *clock) {
    if (--clock->depth == 0 && clock->dispose && !clock->timer && !clock->write) delete clock;
}

/** @brief Notify managed ownership of registration failure; it stops remaining callbacks. */
void fail_clock(NativeClockState *clock) {
    clock->stopped = true;
    clock->failure(clock->data);
}

/** @brief Apply one clock edge only in the simulator's writable phase. */
int clock_write(void *data) {
    auto *clock = static_cast<NativeClockState *>(data);
    ++clock->depth;
    clock->write = nullptr; // GPI owns disposal of the currently firing callback.
    if (!clock->stopped) p_gpi_set_signal_value_int(clock->signal, clock->level ? 1 : 0, GPI_DEPOSIT);
    leave_clock(clock);
    return 0;
}

/** @brief Advance a pulse and arm the next edge; period updates take effect on the next full cycle. */
int clock_timer(void *data) {
    auto *clock = static_cast<NativeClockState *>(data);
    ++clock->depth;
    clock->timer = nullptr;
    if (!clock->stopped) {
        clock->level = !clock->level;
        clock->write = p_gpi_register_readwrite_callback(clock_write, clock);
        if (!clock->write) fail_clock(clock);
        if (!clock->stopped) {
            uint64_t delay;
            if (clock->level) delay = clock->cycle_high;
            else { clock->cycle_high = clock->high; delay = clock->low; }
            clock->timer = p_gpi_register_timed_callback(clock_timer, clock, delay);
            if (!clock->timer) fail_clock(clock);
        }
    }
    leave_clock(clock);
    return 0;
}
} // namespace

/** @brief Start with a low pulse (initial low deposit is issued by managed code).
 * Returns null on validation/allocation/registration failure and sets status.
 * Callback/data remain caller-owned until stop returns success; all calls are on
 * the simulator thread. Only validated Icarus/Verilator use this optional engine.
 */
void *cs_v1_clock_start(void *signal, uint64_t high, uint64_t low, cs_callback failure, void *data, int32_t *status) {
    if (!status) return nullptr;
    *status = 1;
    if (!signal || !high || !low || !failure) return nullptr;
    if (p_gpi_get_num_elems(static_cast<gpi_sim_hdl>(signal)) != 1) { *status = 2; return nullptr; }
    if (p_gpi_is_constant(static_cast<gpi_sim_hdl>(signal))) { *status = 6; return nullptr; }
    auto *clock = new (std::nothrow) NativeClockState{static_cast<gpi_sim_hdl>(signal), high, low, high, failure, data};
    if (!clock) { *status = 4; return nullptr; }
    clock->timer = p_gpi_register_timed_callback(clock_timer, clock, low);
    if (!clock->timer) { delete clock; *status = 8; return nullptr; }
    *status = 0;
    return clock;
}

/** @brief Change the next full cycle; the current low/high pulse pair keeps its captured timing. */
int32_t cs_v1_clock_update(void *handle, uint64_t high, uint64_t low) {
    if (!handle || !high || !low) return 1;
    auto *clock = static_cast<NativeClockState *>(handle);
    if (clock->stopped || clock->dispose) return 1;
    clock->high = high; clock->low = low;
    return 0;
}

/** @brief Stop future and queued edges, retaining storage if callback deregistration fails.
 * Return 0 means ownership has ended; the caller must never use the handle again.
 * On failure the stopped object remains alive so a late callback cannot access freed
 * storage. It may be retried; managed code treats the failure as fatal rather than
 * silently allowing a callback leak. Reentrant stop defers deletion to callback exit.
 */
int32_t cs_v1_clock_stop(void *handle) {
    if (!handle) return 0;
    auto *clock = static_cast<NativeClockState *>(handle);
    clock->stopped = true;
    int status = 0;
    if (clock->timer) {
        if (p_gpi_remove_cb(clock->timer) == 0) clock->timer = nullptr;
        else status = 8;
    }
    if (clock->write) {
        if (p_gpi_remove_cb(clock->write) == 0) clock->write = nullptr;
        else status = 8;
    }
    if (status) return status;
    clock->dispose = true;
    if (!clock->depth) delete clock;
    return 0;
}
