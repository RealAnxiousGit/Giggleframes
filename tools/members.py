"""Print fields and methods of IL2CPP interop classes from ilspycmd output."""
import re, sys, pathlib
for p in sys.argv[1:]:
    src = pathlib.Path(p).read_text(encoding="utf-8", errors="ignore")
    head = re.search(r'^(public|internal)[^\n]*\b(class|struct|enum)\s+\w+[^\n]*', src, re.M)
    print("==", pathlib.Path(p).stem, "|", head.group(0) if head else "")
    fields = re.findall(r'NativeFieldInfoPtr_(\w+)', src)
    print("  fields:", ", ".join(dict.fromkeys(fields)))
    for m in dict.fromkeys(re.findall(r'NativeMethodInfoPtr_(\w+)', src)):
        print("  m:", m)
    if "enum" in (head.group(0) if head else ""):
        print("  enum:", re.findall(r'^\s*(\w+)\s*=\s*(-?\d+)', src, re.M))
