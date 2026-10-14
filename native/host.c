// Linux x86-64 host for SharpTestBench. hostfxr remains loaded for process lifetime.
#include <stdio.h>
#include <stdlib.h>
#include <dlfcn.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>
#include "abi.h"

static const char *required(const char *name) {
    const char *value = getenv(name);
    if (!value || !*value) {
        fprintf(stderr, "SharpTestBench: required environment variable %s is missing\n", name);
        return NULL;
    }
    return value;
}

int sharptestbench_initialize(void) {
    const char *fxr = required("CS_HOSTFXR");
    const char *config = required("CS_CONFIG");
    const char *assembly = required("CS_ASSEMBLY");
    const char *entry_type = required("CS_ENTRY_TYPE");
    if (!fxr || !config || !assembly || !entry_type) return 10;
    const char *abi_error = cs_bind_gpi();
    if (abi_error) { fprintf(stderr, "SharpTestBench: %s\n", abi_error); return 11; }
    void *lib = dlopen(fxr, RTLD_NOW | RTLD_LOCAL);
    if (!lib) { fprintf(stderr, "hostfxr: %s\n", dlerror()); return 1; }
    hostfxr_initialize_for_runtime_config_fn init =
        (hostfxr_initialize_for_runtime_config_fn)dlsym(lib, "hostfxr_initialize_for_runtime_config");
    hostfxr_get_runtime_delegate_fn get =
        (hostfxr_get_runtime_delegate_fn)dlsym(lib, "hostfxr_get_runtime_delegate");
    hostfxr_close_fn close = (hostfxr_close_fn)dlsym(lib, "hostfxr_close");
    if (!init || !get || !close) { fprintf(stderr, "hostfxr: missing required exports\n"); return 2; }
    hostfxr_handle ctx = NULL;
    int rc = init(config, NULL, &ctx);
    if (rc < 0 || !ctx) { fprintf(stderr, "hostfxr init %x\n", rc); return 3; }
    load_assembly_and_get_function_pointer_fn load = NULL;
    rc = get(ctx, hdt_load_assembly_and_get_function_pointer, (void **)&load);
    close(ctx);
    if (rc < 0 || !load) { fprintf(stderr, "hostfxr delegate %x\n", rc); return 4; }
    component_entry_point_fn entry = NULL;
    rc = load(assembly, entry_type, "Initialize", UNMANAGEDCALLERSONLY_METHOD, NULL, (void **)&entry);
    if (rc < 0 || !entry) { fprintf(stderr, "load assembly %x\n", rc); return 5; }
    return entry(NULL, 0);
}
