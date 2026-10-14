#ifndef STB_PLATFORM_H
#define STB_PLATFORM_H
#ifdef _WIN32
#include <windows.h>
#include <stdlib.h>
#include <stdio.h>
#define RTLD_NOW 0
#define RTLD_LOCAL 0
#define RTLD_DEFAULT NULL
static inline void *stb_open(const char *path, int flags) {
    (void)flags;
    int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, NULL, 0);
    if (!n) return NULL;
    wchar_t *wide = (wchar_t *)malloc((size_t)n * sizeof(wchar_t));
    if (!wide) return NULL;
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, wide, n);
    HMODULE module = LoadLibraryW(wide);
    free(wide);
    return (void *)module;
}
static inline void *stb_symbol(void *module, const char *name) {
    // Optional global VPI lookup is unavailable on this Windows implementation.
    return module ? (void *)GetProcAddress((HMODULE)module, name) : NULL;
}
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
