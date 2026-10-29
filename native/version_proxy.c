// Giggleframes launch helper: a version.dll that sits next to Giggleland.exe.
//
// Unity picks its graphics API from the command line before any BepInEx code runs, so without this
// the mod has to close the game and start it again whenever the saved Render API isn't the default
// (DirectX 11). UnityPlayer.dll imports version.dll, so Windows loads this copy from the game folder
// and runs DllMain before the game's own startup code has read its command line. If the mod has
// saved a Render API in BepInEx\config\giggleframes-launch.txt and the command line doesn't already
// force one, this adds that argument, and Unity starts on the right API the first time.
//
// If the API can't start (a GPU without DirectX 12), Unity quits before BepInEx loads, so the mod
// can't notice. This counts launches it changed in BepInEx\cache\giggleframes-launch.pending, which
// the mod deletes as soon as it loads; after two in a row that never got that far it stops adding the
// argument, the game starts on DirectX 11, and the mod saves DirectX 11.
//
// Every version.dll function is forwarded to the system copy. No C runtime: kernel32 only.

#include <windows.h>
#include <winternl.h>

#define FWD(name) __pragma(comment(linker, "/export:" #name "=\\\\.\\GLOBALROOT\\SystemRoot\\System32\\version.dll." #name))
FWD(GetFileVersionInfoA)
FWD(GetFileVersionInfoByHandle)
FWD(GetFileVersionInfoExA)
FWD(GetFileVersionInfoExW)
FWD(GetFileVersionInfoSizeA)
FWD(GetFileVersionInfoSizeExA)
FWD(GetFileVersionInfoSizeExW)
FWD(GetFileVersionInfoSizeW)
FWD(GetFileVersionInfoW)
FWD(VerFindFileA)
FWD(VerFindFileW)
FWD(VerInstallFileA)
FWD(VerInstallFileW)
FWD(VerLanguageNameA)
FWD(VerLanguageNameW)
FWD(VerQueryValueA)
FWD(VerQueryValueW)

// Only these can be added; anything else in the file is ignored.
static const char *Allowed[] = { "-force-d3d11", "-force-d3d12" };

// The compiler turns the copy loops below into memcpy calls.
#pragma function(memcpy)
void *memcpy(void *d, const void *s, size_t n) { BYTE *o = d; const BYTE *i = s; while (n--) *o++ = *i++; return d; }

static int Len(const char *s) { int n = 0; while (s[n]) n++; return n; }
static int LenW(const wchar_t *s) { int n = 0; while (s[n]) n++; return n; }
static wchar_t Lower(wchar_t c) { return c >= 'A' && c <= 'Z' ? c + 32 : c; }

static int ContainsW(const wchar_t *s, const wchar_t *sub)
{
    for (; *s; s++)
    {
        int i = 0;
        while (sub[i] && Lower(s[i]) == sub[i]) i++;
        if (!sub[i]) return 1;
    }
    return 0;
}

// <game folder>\rel
static BOOL GamePath(wchar_t *path, const wchar_t *rel)
{
    DWORD n = GetModuleFileNameW(NULL, path, MAX_PATH);
    if (n == 0 || n >= MAX_PATH) return FALSE;
    while (n > 0 && path[n - 1] != '\\') n--;
    for (int i = 0; rel[i]; i++) path[n++] = rel[i];
    path[n] = 0;
    return TRUE;
}

// Launches changed in a row without the mod loading; counts this one.
static int CountPending(void)
{
    wchar_t path[MAX_PATH + 64];
    if (!GamePath(path, L"BepInEx\\cache\\giggleframes-launch.pending")) return 0;
    HANDLE f = CreateFileW(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, OPEN_ALWAYS, 0, NULL);
    if (f == INVALID_HANDLE_VALUE) return 0;   // no cache folder yet: nothing to count against
    char c = '0';
    DWORD got = 0;
    if (!ReadFile(f, &c, 1, &got, NULL) || got != 1 || c < '0' || c > '9') c = '0';
    int n = c - '0';
    if (n < 2)
    {
        c = (char)('0' + n + 1);
        SetFilePointer(f, 0, NULL, FILE_BEGIN);
        WriteFile(f, &c, 1, &got, NULL);
        SetEndOfFile(f);
    }
    CloseHandle(f);
    return n;
}

// The saved argument, or NULL.
static const char *SavedArg(void)
{
    wchar_t path[MAX_PATH + 64];
    if (!GamePath(path, L"BepInEx\\config\\giggleframes-launch.txt")) return NULL;

    HANDLE f = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (f == INVALID_HANDLE_VALUE) return NULL;
    char buf[64];
    DWORD got = 0;
    BOOL ok = ReadFile(f, buf, sizeof buf - 1, &got, NULL);
    CloseHandle(f);
    if (!ok) return NULL;
    while (got > 0 && (buf[got - 1] == '\r' || buf[got - 1] == '\n' || buf[got - 1] == ' ')) got--;
    buf[got] = 0;
    for (int i = 0; i < (int)(sizeof Allowed / sizeof *Allowed); i++)
    {
        const char *a = Allowed[i];
        int j = 0;
        while (a[j] && a[j] == buf[j]) j++;
        if (!a[j] && !buf[j]) return a;
    }
    return NULL;
}

// kernelbase keeps its own copies of the command line (what GetCommandLineW/A return). Find the
// UNICODE_STRING / ANSI_STRING in its writable data that points at the current buffer and repoint it.
static void Repoint(void *oldBuf, USHORT oldLen, void *newBuf, USHORT newLen, USHORT newMax)
{
    HMODULE kb = GetModuleHandleW(L"kernelbase.dll");
    if (!kb) return;
    IMAGE_NT_HEADERS *nt = (IMAGE_NT_HEADERS *)((BYTE *)kb + ((IMAGE_DOS_HEADER *)kb)->e_lfanew);
    IMAGE_SECTION_HEADER *sec = IMAGE_FIRST_SECTION(nt);
    for (int s = 0; s < nt->FileHeader.NumberOfSections; s++, sec++)
    {
        if (!(sec->Characteristics & IMAGE_SCN_MEM_WRITE)) continue;
        BYTE *p = (BYTE *)kb + sec->VirtualAddress;
        SIZE_T size = sec->Misc.VirtualSize;
        for (SIZE_T o = 8; o + 8 <= size; o += 8)
        {
            if (*(void **)(p + o) != oldBuf) continue;
            USHORT *str = (USHORT *)(p + o - 8);   // { Length, MaximumLength, (pad), Buffer }
            if (str[0] != oldLen) continue;
            str[0] = newLen;
            str[1] = newMax;
            *(void **)(p + o) = newBuf;
        }
    }
}

static void AddArg(void)
{
    const char *arg = SavedArg();
    if (!arg) return;
    wchar_t *oldW = GetCommandLineW();
    char *oldA = GetCommandLineA();
    if (ContainsW(oldW, L"-force-")) return;   // launch options or our own restart already pick one
    if (CountPending() >= 2)
    {
        SetEnvironmentVariableW(L"GIGGLEFRAMES_LAUNCH_ARG", L"gave-up");
        return;
    }

    int lw = LenW(oldW), la = Len(oldA), lg = Len(arg);
    HANDLE heap = GetProcessHeap();
    wchar_t *newW = HeapAlloc(heap, 0, (lw + lg + 2) * sizeof(wchar_t));
    char *newA = HeapAlloc(heap, 0, la + lg + 2);
    if (!newW || !newA) return;
    for (int i = 0; i < lw; i++) newW[i] = oldW[i];
    for (int i = 0; i < la; i++) newA[i] = oldA[i];
    newW[lw] = ' ';
    newA[la] = ' ';
    for (int i = 0; i <= lg; i++) { newW[lw + 1 + i] = (wchar_t)arg[i]; newA[la + 1 + i] = arg[i]; }

    USHORT nw = (USHORT)((lw + 1 + lg) * sizeof(wchar_t)), na = (USHORT)(la + 1 + lg);
    Repoint(oldW, (USHORT)(lw * sizeof(wchar_t)), newW, nw, nw + sizeof(wchar_t));
    Repoint(oldA, (USHORT)la, newA, na, na + 1);

    RTL_USER_PROCESS_PARAMETERS *pp = NtCurrentTeb()->ProcessEnvironmentBlock->ProcessParameters;
    if (pp->CommandLine.Buffer == oldW)
    {
        pp->CommandLine.Buffer = newW;
        pp->CommandLine.Length = nw;
        pp->CommandLine.MaximumLength = nw + sizeof(wchar_t);
    }
    // Tells the mod (RenderApi.CheckStartup) that the argument came from here.
    SetEnvironmentVariableW(L"GIGGLEFRAMES_LAUNCH_ARG", GetCommandLineW() == newW ? L"applied" : L"failed");
}

BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(h);
        AddArg();
    }
    return TRUE;
}
