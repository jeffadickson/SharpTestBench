/** @file platform.h
 * @brief Private loader portability helpers; no simulator or managed ownership.
 * Modules are intentionally retained for process lifetime. Calls and diagnostic
 * reads are serialized on the simulator thread; error text is borrowed storage.
 */
#ifndef STB_PLATFORM_H
#define STB_PLATFORM_H
#ifdef _WIN32
#include <windows.h>
#include <stdlib.h>
#include <stdio.h>
#define RTLD_NOW 0
#define RTLD_LOCAL 0
#define RTLD_DEFAULT NULL
/** @brief Load a DLL from a strict UTF-8 path, returning null on failure.
 * @details Temporary UTF-16 storage is freed on every path. Preserve the loader's
 * last-error value across cleanup so the caller receives the original diagnostic.
 */
static inline void *stb_open(const char *path, int flags) {
    (void)flags;
    int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, NULL, 0);
    if (!n) return NULL;
    wchar_t *wide = (wchar_t *)malloc((size_t)n * sizeof(wchar_t));
    if (!wide) { SetLastError(ERROR_NOT_ENOUGH_MEMORY); return NULL; }
    HMODULE module = NULL;
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, wide, n))
        module = LoadLibraryW(wide);
    DWORD error = GetLastError();
    free(wide);
    SetLastError(error);
    return (void *)module;
}
/** @brief Resolve a borrowed export address from a loaded DLL.
 * @details A null module means optional global lookup, unsupported on Windows.
 */
static inline void *stb_symbol(void *module, const char *name) {
    // Optional global VPI lookup is unavailable on this Windows implementation.
    return module ? (void *)GetProcAddress((HMODULE)module, name) : NULL;
}
/** @brief Format the last Windows loader error; overwritten by the next call. */
static inline const char *stb_error(void) {
    static char message[64];
    snprintf(message, sizeof message, "Windows loader error %lu", (unsigned long)GetLastError());
    return message;
}
#define dlopen stb_open
#define dlsym stb_symbol
#define dlerror stb_error
#define STB_EXPORT __declspec(dllexport)
#else
#include <dlfcn.h>
#define STB_EXPORT __attribute__((visibility("default")))
#endif
#endif
