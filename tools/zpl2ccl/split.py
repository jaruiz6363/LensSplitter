"""Split an over-long generated CCL function into chunk functions.

The generated code is one statement per line; a block is a header line (for/if), a '{' line,
its body and a '}' line, optionally followed by 'else', '{', body, '}'. Every variable is
global, so any run of whole statements can move into a function of its own. A run that
contains the macro's early exit sets z_stop, and every call to it is followed by a check.
"""
import sys, re

LIMIT = 200

def parse(lines, i=0):
    """Parse from lines[i] until an unmatched '}' (not consumed) or the end."""
    nodes = []
    while i < len(lines):
        s = lines[i].strip()
        if s == "}":
            return nodes, i
        if (s.startswith("for (") or s.startswith("if ")) and not s.endswith("}") and i + 1 < len(lines) and lines[i + 1].strip() == "{":
            body, j = parse(lines, i + 2)
            assert lines[j].strip() == "}", (i, lines[j])
            j += 1
            els = None
            if j + 1 < len(lines) and lines[j].strip() == "else" and lines[j + 1].strip() == "{":
                els, k = parse(lines, j + 2)
                assert lines[k].strip() == "}"
                j = k + 1
            nodes.append(("block", s, body, els))
            i = j
            continue
        nodes.append(("stmt", s))
        i += 1
    return nodes, i

def size(n):
    if n[0] == "stmt":
        return 1
    return 3 + sum(size(c) for c in n[2]) + (3 + sum(size(c) for c in n[3]) if n[3] is not None else 0)

def has_return(nodes):
    for n in nodes:
        if n[0] == "stmt" and "return (0);" in n[1]:
            return True
        if n[0] == "block" and (has_return(n[2]) or (n[3] is not None and has_return(n[3]))):
            return True
    return False

chunks = []   # (name, lines)

def emit(nodes, depth):
    out = []
    for n in nodes:
        pad = "\t" * depth
        if n[0] == "stmt":
            out.append(pad + n[1])
        else:
            out.append(pad + n[1]); out.append(pad + "{")
            out += emit(n[2], depth + 1)
            out.append(pad + "}")
            if n[3] is not None:
                out.append(pad + "else"); out.append(pad + "{")
                out += emit(n[3], depth + 1)
                out.append(pad + "}")
    return out

def split(nodes):
    """Return nodes whose own size is within LIMIT, moving runs into chunk functions."""
    # first make every single node small enough, by splitting inside big blocks
    fixed = []
    for n in nodes:
        if n[0] == "block" and size(n) > LIMIT:
            n = ("block", n[1], split(n[2]), split(n[3]) if n[3] is not None else None)
        fixed.append(n)
    if sum(size(n) for n in fixed) <= LIMIT:
        return fixed
    # then group runs into chunks
    out, run = [], []
    def flush():
        if not run:
            return
        name = "z_part%d" % (len(chunks) + 1)
        body = emit(run, 1)
        chunks.append((name, body))
        out.append(("stmt", name + "();"))
        if has_return(run):
            out.append(("stmt", "if (z_stop) return (0);"))
        run.clear()
    cur = 0
    for n in fixed:
        s = size(n)
        if run and cur + s > LIMIT:
            flush(); cur = 0
        run.append(n); cur += s
    flush()
    return out

src = open(sys.argv[1], encoding="utf-8").read()
start = src.index("static int\nz_main()\n{\n")
body_start = start + len("static int\nz_main()\n{\n")
end = src.index("\n\treturn (0);\n}\n", body_start)
body = src[body_start:end].split("\n")
body = [l.replace("return (0);", "z_stop = 1; return (0);") for l in body]
nodes, k = parse(body)
assert k == len(body), "unparsed tail at %d: %s" % (k, body[k] if k < len(body) else "")
top = split(nodes)
main_lines = emit(top, 1)

chunk_text = ""
for name, lines_ in chunks:
    chunk_text += "static int\n%s()\n{\n%s\n\treturn (0);\n}\n\n" % (name, "\n".join(lines_))
new_main = "static int\nz_main()\n{\n" + "\n".join(main_lines) + "\n\treturn (0);\n}\n"
out = src[:start] + "static int    z_stop;\n\n" + chunk_text + new_main + src[end + len("\n\treturn (0);\n}\n"):]
open(sys.argv[1], "w", encoding="utf-8").write(out)
print("chunks:", len(chunks), "largest:", max(len(l) for _, l in chunks), "main lines:", len(main_lines))
