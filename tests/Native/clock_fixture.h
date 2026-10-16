/** @file clock_fixture.h
 * @brief Deterministic fake GPI callbacks for ownership/failure tests, never a simulator adapter.
 */
typedef struct fake_callback { cs_callback fn; void *data; uint64_t delay; } fake_callback;
static fake_callback *fake_timer, *fake_write;
static int fake_registration_count, fake_write_count, fake_value;
static cs_callback fake_on_write;
/** @brief Allocate one synthetic callback, optionally failing a selected timer registration. */
static void *fake_register(cs_callback cb, void *data, uint64_t delay, int timer) {
    if (!getenv("CS_FAKE_CLOCK")) return NULL;
    if (timer) {
        ++fake_registration_count;
        const char *fail = getenv("CS_FAKE_CLOCK_FAIL_AT");
        if (fail && atoi(fail) == fake_registration_count) return NULL;
    } else if (getenv("CS_FAKE_CLOCK_FAIL_RW")) return NULL;
    fake_callback *value = malloc(sizeof(*value));
    if (!value) return NULL;
    *value = (fake_callback){cb, data, delay};
    if (timer) fake_timer = value; else fake_write = value;
    return value;
}
/** @brief Remove synthetic ownership or leave it intact to model a backend removal failure. */
static int fake_remove(void *cb) {
    if (!getenv("CS_FAKE_CLOCK")) return 0;
    if (getenv("CS_FAKE_CLOCK_REMOVE_FAIL")) return 1;
    if (fake_timer == cb) fake_timer = NULL;
    if (fake_write == cb) fake_write = NULL;
    free(cb); return 0;
}
/** @brief Fire exactly one callback; detach first so rearming/cancellation is reentrant. */
void stb_test_fire(int timer) {
    fake_callback *cb = timer ? fake_timer : fake_write;
    if (!cb) return;
    if (timer) fake_timer = NULL; else fake_write = NULL;
    cb->fn(cb->data); free(cb);
}
/** @brief Report the next timer's delay for the independent pulse oracle. */
uint64_t stb_test_delay(void) { return fake_timer ? fake_timer->delay : 0; }
/** @brief Count live native callback registrations. */
int stb_test_pending(void) { return (fake_timer != NULL) + (fake_write != NULL); }
/** @brief Count applied clock writes, independent of registration count. */
int stb_test_writes(void) { return fake_write_count; }
/** @brief Return the last applied scalar value. */
int stb_test_value(void) { return fake_value; }
/** @brief Install a reentrant cancellation hook invoked by the synthetic setter. */
void stb_test_write_hook(cs_callback cb) { fake_on_write = cb; }
/** @brief Reset counters only when no callbacks are owned. */
void stb_test_reset(void) { fake_registration_count = fake_write_count = fake_value = 0; fake_on_write = NULL; }
