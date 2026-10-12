/* Native IExplorerCommand bridge. No managed runtime is loaded by this DLL.
   The Windows COM surrogate owns its lifetime. Actual work runs in FolderAlias.exe. */
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <wchar.h>
#include <string.h>
typedef void IUnknown;
__declspec(dllimport) LPVOID WINAPI CoTaskMemAlloc(SIZE_T size);
__declspec(dllimport) void WINAPI CoTaskMemFree(LPVOID value);

static HMODULE module;
static LONG objects = 0, locks = 0;
static const GUID class_id = {0xed3017e2,0x5a5a,0x4cd5,{0xa7,0x14,0xec,0x67,0x64,0x28,0x42,0xa7}};
static const GUID iid_unknown = {0,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const GUID iid_factory = {1,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const GUID iid_command = {0xa08ce4d0,0xfa25,0x44ab,{0xb5,0x7c,0xc7,0xb1,0xc3,0x23,0xe0,0xb9}};

typedef struct Item Item;
typedef struct Items Items;
typedef struct Command Command;
typedef struct Factory Factory;
typedef struct ItemVtable {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Item*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Item*);
    ULONG (STDMETHODCALLTYPE *Release)(Item*);
    HRESULT (STDMETHODCALLTYPE *BindToHandler)(Item*, void*, REFGUID, REFIID, void**);
    HRESULT (STDMETHODCALLTYPE *GetParent)(Item*, Item**);
    HRESULT (STDMETHODCALLTYPE *GetDisplayName)(Item*, DWORD, LPWSTR*);
    HRESULT (STDMETHODCALLTYPE *GetAttributes)(Item*, DWORD, DWORD*);
    HRESULT (STDMETHODCALLTYPE *Compare)(Item*, Item*, DWORD, int*);
} ItemVtable;
struct Item { ItemVtable *v; };
typedef struct ItemsVtable {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Items*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Items*);
    ULONG (STDMETHODCALLTYPE *Release)(Items*);
    HRESULT (STDMETHODCALLTYPE *BindToHandler)(Items*, void*, REFGUID, REFIID, void**);
    HRESULT (STDMETHODCALLTYPE *GetPropertyStore)(Items*, DWORD, REFIID, void**);
    HRESULT (STDMETHODCALLTYPE *GetPropertyDescriptionList)(Items*, void*, REFIID, void**);
    HRESULT (STDMETHODCALLTYPE *GetAttributes)(Items*, DWORD, DWORD, DWORD*);
    HRESULT (STDMETHODCALLTYPE *GetCount)(Items*, DWORD*);
    HRESULT (STDMETHODCALLTYPE *GetItemAt)(Items*, DWORD, Item**);
    HRESULT (STDMETHODCALLTYPE *EnumItems)(Items*, void**);
} ItemsVtable;
struct Items { ItemsVtable *v; };
typedef struct CommandVtable {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Command*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Command*);
    ULONG (STDMETHODCALLTYPE *Release)(Command*);
    HRESULT (STDMETHODCALLTYPE *GetTitle)(Command*, Items*, LPWSTR*);
    HRESULT (STDMETHODCALLTYPE *GetIcon)(Command*, Items*, LPWSTR*);
    HRESULT (STDMETHODCALLTYPE *GetToolTip)(Command*, Items*, LPWSTR*);
    HRESULT (STDMETHODCALLTYPE *GetCanonicalName)(Command*, GUID*);
    HRESULT (STDMETHODCALLTYPE *GetState)(Command*, Items*, BOOL, DWORD*);
    HRESULT (STDMETHODCALLTYPE *Invoke)(Command*, Items*, void*);
    HRESULT (STDMETHODCALLTYPE *GetFlags)(Command*, DWORD*);
    HRESULT (STDMETHODCALLTYPE *EnumSubCommands)(Command*, void**);
} CommandVtable;
struct Command { CommandVtable *v; LONG refs; };
typedef struct FactoryVtable {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Factory*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Factory*);
    ULONG (STDMETHODCALLTYPE *Release)(Factory*);
    HRESULT (STDMETHODCALLTYPE *CreateInstance)(Factory*, IUnknown*, REFIID, void**);
    HRESULT (STDMETHODCALLTYPE *LockServer)(Factory*, BOOL);
} FactoryVtable;
struct Factory { FactoryVtable *v; LONG refs; };

static int equal_guid(REFIID a, const GUID *b) { return memcmp(a,b,sizeof(GUID)) == 0; }
static HRESULT copy_string(const WCHAR *value, LPWSTR *result) {
    SIZE_T size;
    if (!result) return E_POINTER;
    *result = NULL; size = (wcslen(value)+1) * sizeof(WCHAR);
    *result = (LPWSTR)CoTaskMemAlloc(size);
    if (!*result) return E_OUTOFMEMORY;
    memcpy(*result,value,size); return S_OK;
}
static HRESULT executable_path(WCHAR *path, DWORD capacity) {
    WCHAR *slash; DWORD length = GetModuleFileNameW(module,path,capacity);
    if (!length || length >= capacity) return E_FAIL;
    slash = wcsrchr(path,L'\\');
    if (!slash || (DWORD)(slash-path)+17 >= capacity) return E_FAIL;
    wcscpy(slash+1,L"FolderAlias.exe"); return S_OK;
}
static ULONG STDMETHODCALLTYPE command_add(Command *self) { return (ULONG)InterlockedIncrement(&self->refs); }
static ULONG STDMETHODCALLTYPE command_release(Command *self) {
    LONG n = InterlockedDecrement(&self->refs);
    if (!n) { HeapFree(GetProcessHeap(),0,self); InterlockedDecrement(&objects); }
    return (ULONG)n;
}
static HRESULT STDMETHODCALLTYPE command_query(Command *self, REFIID iid, void **value) {
    if (!value) return E_POINTER; *value = NULL;
    if (equal_guid(iid,&iid_unknown) || equal_guid(iid,&iid_command)) { *value=self; command_add(self); return S_OK; }
    return E_NOINTERFACE;
}
static HRESULT STDMETHODCALLTYPE command_title(Command *self, Items *items, LPWSTR *value) {
    return copy_string(L"\x8bbe\x7f6e\x4e2d\x6587\x663e\x793a\x540d\x2026",value);
}
static HRESULT STDMETHODCALLTYPE command_icon(Command *self, Items *items, LPWSTR *value) {
    WCHAR path[520],icon[524]; HRESULT hr=executable_path(path,510);
    if (FAILED(hr)) { if (value) *value=NULL; return hr; }
    wcscpy(icon,L"\""); wcscat(icon,path); wcscat(icon,L"\",0"); return copy_string(icon,value);
}
static HRESULT STDMETHODCALLTYPE command_tip(Command *self, Items *items, LPWSTR *value) {
    return copy_string(L"\x4fdd\x7559\x5b9e\x9645\x8def\x5f84\xff0c\x8bbe\x7f6e\x6587\x4ef6\x5939\x663e\x793a\x540d",value);
}
static HRESULT STDMETHODCALLTYPE command_name(Command *self, GUID *value) { if (!value) return E_POINTER; *value=class_id; return S_OK; }
static HRESULT STDMETHODCALLTYPE command_state(Command *self, Items *items, BOOL slow, DWORD *state) {
    DWORD count=0,attrs=0;
    if (!state) return E_POINTER; *state=2; /* ECS_HIDDEN */
    if (!items || FAILED(items->v->GetCount(items,&count)) || !count) return S_OK;
    if (FAILED(items->v->GetAttributes(items,1,0x60000000,&attrs)) || (attrs & 0x60000000) != 0x60000000) return S_OK;
    *state = count <= 64 ? 0 : 1; /* ECS_ENABLED / ECS_DISABLED */
    return S_OK;
}
static HRESULT STDMETHODCALLTYPE command_invoke(Command *self, Items *items, void *binding) {
    WCHAR exe[520],cwd[520],*command,*path=NULL,*slash;
    DWORD count=0,index,attrs; SIZE_T used,length; Item *item=NULL;
    HRESULT hr; STARTUPINFOW startup; PROCESS_INFORMATION process;
    if (!items || FAILED(items->v->GetCount(items,&count)) || !count || count>64) return E_INVALIDARG;
    hr=executable_path(exe,520); if (FAILED(hr)) return hr;
    command=(WCHAR*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,32767*sizeof(WCHAR));
    if (!command) return E_OUTOFMEMORY;
    wcscpy(command,L"\""); wcscat(command,exe); wcscat(command,L"\" --open"); used=wcslen(command);
    for (index=0;index<count;index++) {
        hr=items->v->GetItemAt(items,index,&item); if (FAILED(hr)) break;
        attrs=0; hr=item->v->GetAttributes(item,0x60000000,&attrs);
        if (SUCCEEDED(hr) && (attrs & 0x60000000)==0x60000000) hr=item->v->GetDisplayName(item,0x80058000,&path);
        else hr=E_INVALIDARG;
        item->v->Release(item); item=NULL;
        if (FAILED(hr)) break;
        length=wcslen(path);
        if (length<4 || length>180 || path[1]!=L':' || path[2]!=L'\\' || wcschr(path,L'"') || used+length+4>=32767) hr=E_INVALIDARG;
        else { wcscat(command,L" \""); wcscat(command,path); wcscat(command,L"\""); used+=length+3; }
        CoTaskMemFree(path); path=NULL;
        if (FAILED(hr)) break;
    }
    if (SUCCEEDED(hr)) {
        wcscpy(cwd,exe); slash=wcsrchr(cwd,L'\\'); if (slash) *slash=0;
        ZeroMemory(&startup,sizeof(startup)); ZeroMemory(&process,sizeof(process)); startup.cb=sizeof(startup);
        if (CreateProcessW(exe,command,NULL,NULL,FALSE,0,NULL,cwd,&startup,&process)) {
            CloseHandle(process.hThread); CloseHandle(process.hProcess); hr=S_OK;
        } else hr=HRESULT_FROM_WIN32(GetLastError());
    }
    if (path) CoTaskMemFree(path);
    HeapFree(GetProcessHeap(),0,command); return hr;
}
static HRESULT STDMETHODCALLTYPE command_flags(Command *self,DWORD *value) { if (!value) return E_POINTER; *value=0; return S_OK; }
static HRESULT STDMETHODCALLTYPE command_subcommands(Command *self,void **value) { if (!value) return E_POINTER; *value=NULL; return E_NOTIMPL; }
static CommandVtable command_vtable={command_query,command_add,command_release,command_title,command_icon,command_tip,command_name,command_state,command_invoke,command_flags,command_subcommands};
static ULONG STDMETHODCALLTYPE factory_add(Factory *self) { return (ULONG)InterlockedIncrement(&self->refs); }
static ULONG STDMETHODCALLTYPE factory_release(Factory *self) {
    LONG n=InterlockedDecrement(&self->refs);
    if (!n) { HeapFree(GetProcessHeap(),0,self); InterlockedDecrement(&objects); } return (ULONG)n;
}
static HRESULT STDMETHODCALLTYPE factory_query(Factory *self,REFIID iid,void **value) {
    if (!value) return E_POINTER; *value=NULL;
    if (equal_guid(iid,&iid_unknown)||equal_guid(iid,&iid_factory)) { *value=self; factory_add(self); return S_OK; } return E_NOINTERFACE;
}
static HRESULT STDMETHODCALLTYPE factory_create(Factory *self,IUnknown *outer,REFIID iid,void **value) {
    Command *command; HRESULT hr;
    if (!value) return E_POINTER; *value=NULL; if (outer) return CLASS_E_NOAGGREGATION;
    command=(Command*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(Command)); if (!command) return E_OUTOFMEMORY;
    command->v=&command_vtable; command->refs=1; InterlockedIncrement(&objects);
    hr=command_query(command,iid,value); command_release(command); return hr;
}
static HRESULT STDMETHODCALLTYPE factory_lock(Factory *self,BOOL lock) { if (lock) InterlockedIncrement(&locks); else InterlockedDecrement(&locks); return S_OK; }
static FactoryVtable factory_vtable={factory_query,factory_add,factory_release,factory_create,factory_lock};
__declspec(dllexport) HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID clsid,REFIID iid,void **value) {
    Factory *factory; HRESULT hr;
    if (!value) return E_POINTER; *value=NULL; if (!equal_guid(clsid,&class_id)) return CLASS_E_CLASSNOTAVAILABLE;
    factory=(Factory*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(Factory)); if (!factory) return E_OUTOFMEMORY;
    factory->v=&factory_vtable; factory->refs=1; InterlockedIncrement(&objects);
    hr=factory_query(factory,iid,value); factory_release(factory); return hr;
}
__declspec(dllexport) HRESULT STDMETHODCALLTYPE DllCanUnloadNow(void) { return objects==0 && locks==0 ? S_OK : S_FALSE; }
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved) { if (reason==DLL_PROCESS_ATTACH) { module=instance; DisableThreadLibraryCalls(instance); } return TRUE; }
