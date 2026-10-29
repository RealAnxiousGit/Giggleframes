"""Print the exception code/address of a Windows minidump and the module it falls in."""
import struct, sys
d = open(sys.argv[1], 'rb').read()
nstreams, rva = struct.unpack_from('<II', d, 8)
streams = {}
for i in range(nstreams):
    t, size, r = struct.unpack_from('<III', d, rva + 12 * i)
    streams[t] = (r, size)
def u16str(r):
    n = struct.unpack_from('<I', d, r)[0]
    return d[r + 4:r + 4 + n].decode('utf-16le')
mods = []
r, _ = streams[4]
n = struct.unpack_from('<I', d, r)[0]
for i in range(n):
    base, size = struct.unpack_from('<QI', d, r + 4 + 108 * i)
    name_rva = struct.unpack_from('<I', d, r + 4 + 108 * i + 20)[0]
    mods.append((base, size, u16str(name_rva)))
def where(a):
    for b, s, nm in mods:
        if b <= a < b + s: return f"{nm.split(chr(92))[-1]}+0x{a-b:x}"
    return "?"
r, _ = streams[6]
tid, _, code, flags, rec, addr, nparams = struct.unpack_from('<IIIIQQI', d, r)
params = struct.unpack_from('<4xQQ', d, r + 8 + 32) if nparams else ()
print(f"thread {tid} code 0x{code:08x} at 0x{addr:x} = {where(addr)} params {[hex(p) for p in params]}")
# context: RSP for a crude stack scan
ctx_size, ctx_rva = struct.unpack_from('<II', d, r + 8 + 152)
rsp = struct.unpack_from('<Q', d, ctx_rva + 0x98)[0]
# find memory containing rsp (stream 9 = Memory64List, 5 = MemoryList)
hits = []
if 5 in streams:
    r5, _ = streams[5]; n5 = struct.unpack_from('<I', d, r5)[0]
    for i in range(n5):
        start, sz, mrva = struct.unpack_from('<QII', d, r5 + 4 + 16 * i)
        if start <= rsp < start + sz:
            off = mrva + (rsp - start)
            for k in range(0, min(sz - (rsp - start), 0x4000), 8):
                v = struct.unpack_from('<Q', d, off + k)[0]
                w = where(v)
                if w != "?" and not w.startswith(("ntdll", "KERNEL")): hits.append(w)
print("stack scan:", hits[:40])
