/** @file host.cpp
 * @brief Load the .NET runtime inside the HDL simulator and enter the managed test host.
 *
 * The simulator calls this C entry point once during bootstrap on its own thread.
 * GPI and hostfxr stay loaded for process lifetime: unloading either could invalidate
 * live callbacks/delegates. A hostfxr context is closed after delegate acquisition;
 * closing that context does not unload CoreCLR. No C++ exception may cross this ABI.
 */
#include <stdio.h>
#include <stdlib.h>
#include "platform.h"
#include <hostfxr.h>
#include <coreclr_delegates.h>
#include "abi.h"

/** @brief Read a required environment value, returning borrowed storage or null.
 * @details Missing/empty values produce a named stderr diagnostic. The environment
 * must remain unchanged during initialization; Windows runtime paths are subsequently
 * read through _wgetenv to preserve UTF-16 rather than an ANSI conversion.
 */
static const char *required(const char *name) {
    const char *value = getenv(name);
    if (!value || !*value) {
        fprintf(stderr, "SharpTestBench: required environment variable %s is missing\n", name);
        return nullptr;
    }
    return value;
}

/** @brief Bootstrap the managed component using the versioned simulator adapter.
 * @return Managed entry result, or native bootstrap status: 1 loader failure,
 * 2 missing hostfxr exports, 3 runtime initialization, 4 delegate acquisition,
 * 5 managed entry resolution, 10 missing configuration, 11 GPI binding failure.
 * @details Managed Initialize is UnmanagedCallersOnly and owns managed exception
 * reporting. Its returned entry pointer uses the CoreCLR-declared calling convention.
 * This export retains C linkage so the simulator can resolve its literal name.
 */
extern "C" STB_EXPORT int sharptestbench_initialize(void) {
    const char *fxr = required("CS_HOSTFXR");
    const char *config = required("CS_CONFIG");
    const char *assembly = required("CS_ASSEMBLY");
    const char *entry_type = required("CS_ENTRY_TYPE");
    if (!fxr || !config || !assembly || !entry_type) return 10;

    // Fail before starting CoreCLR if the pinned simulator bridge is incomplete.
    const char *abi_error = cs_bind_gpi();
    if (abi_error) {
        fprintf(stderr, "SharpTestBench: %s\n", abi_error);
        return 11;
    }
#ifdef _WIN32
    void *lib = reinterpret_cast<void *>(LoadLibraryW(_wgetenv(L"CS_HOSTFXR")));
#else
    void *lib = dlopen(fxr, RTLD_NOW | RTLD_LOCAL);
#endif
    if (!lib) { fprintf(stderr, "hostfxr: %s\n", dlerror()); return 1; }
    auto init = reinterpret_cast<hostfxr_initialize_for_runtime_config_fn>(dlsym(lib, "hostfxr_initialize_for_runtime_config"));
    auto get = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(dlsym(lib, "hostfxr_get_runtime_delegate"));
    auto close = reinterpret_cast<hostfxr_close_fn>(dlsym(lib, "hostfxr_close"));
    if (!init || !get || !close) { fprintf(stderr, "hostfxr: missing required exports\n"); return 2; }
#ifdef _WIN32
    // hostfxr's char_t is UTF-16 on Windows and UTF-8 on POSIX systems.
    const char_t *runtime_config = _wgetenv(L"CS_CONFIG");
    const char_t *managed_assembly = _wgetenv(L"CS_ASSEMBLY");
    const char_t *managed_type = _wgetenv(L"CS_ENTRY_TYPE");
    const char_t *method = L"Initialize";
#else
    const char_t *runtime_config = config;
    const char_t *managed_assembly = assembly;
    const char_t *managed_type = entry_type;
    const char_t *method = "Initialize";
#endif
    hostfxr_handle context = nullptr;
    int rc = init(runtime_config, nullptr, &context);
    if (rc < 0 || !context) {
        // A failed initializer may still hand back a context; close it if supplied.
        if (context) close(context);
        fprintf(stderr, "hostfxr init %x\n", static_cast<unsigned>(rc));
        return 3;
    }
    void *delegate = nullptr;
    rc = get(context, hdt_load_assembly_and_get_function_pointer, &delegate);
    close(context);
    if (rc < 0 || !delegate) { fprintf(stderr, "hostfxr delegate %x\n", static_cast<unsigned>(rc)); return 4; }
    auto load = reinterpret_cast<load_assembly_and_get_function_pointer_fn>(delegate);

    // Use a void* output slot; do not alias a function-pointer object as void**.
    void *entry_address = nullptr;
    rc = load(managed_assembly, managed_type, method, UNMANAGEDCALLERSONLY_METHOD, nullptr, &entry_address);
    if (rc < 0 || !entry_address) { fprintf(stderr, "load assembly %x\n", static_cast<unsigned>(rc)); return 5; }
    auto entry = reinterpret_cast<component_entry_point_fn>(entry_address);
    return entry(nullptr, 0);
}
