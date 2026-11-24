#!/usr/bin/env python3
"""Zmenší textury ve VRM/GLB (max 1024, normály 512, bez velkého náhledu)."""
import json, struct, sys
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", "pillow"])
    from PIL import Image


def load_glb(path):
    data = Path(path).read_bytes()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    assert magic == b"glTF", magic
    off = 12
    chunks = []
    while off + 8 <= length:
        clen, ctype = struct.unpack_from("<I4s", data, off)
        off += 8
        chunks.append((ctype, data[off:off + clen]))
        off += clen
    js = json.loads(chunks[0][1].decode("utf-8"))
    blob = chunks[1][1] if len(chunks) > 1 else b""
    return js, blob


def max_dim_for(name: str) -> int:
    n = (name or "").lower()
    if "thumbnail" in n:
        return 128
    if "matcap" in n or "shader_none" in n:
        return 256
    if "nml" in n or "normal" in n:
        return 512
    if "body" in n or "tops" in n or "face_00" in n:
        return 1024
    return 512


def main():
    src, dst = Path(sys.argv[1]), Path(sys.argv[2])
    js, blob = load_glb(src)
    views = js["bufferViews"]
    images = js.get("images", [])
    new_parts = []
    new_views = []
    # copy non-image buffer views first? Easier: rebuild entire BIN
    # Map old view index -> we'll rewrite image views only and keep others by copying.

    # Collect which views are images
    image_views = {im["bufferView"] for im in images if "bufferView" in im}

    # We will concatenate: first all resized images in image order, then all other views in original order
    # and remap indices. Accessors, meshes etc. reference bufferViews — must remap everything.

    old_to_new = {}
    out = bytearray()

    def align4(b: bytearray):
        while len(b) % 4:
            b.append(0)

    def add_view(data_bytes, ev=None):
        align4(out)
        offset = len(out)
        out.extend(data_bytes)
        view = {"buffer": 0, "byteOffset": offset, "byteLength": len(data_bytes)}
        if ev is not None:
            view["target"] = ev  # unused
        new_views.append(view)
        return len(new_views) - 1

    # Process all old views in order, replacing image views with resized PNGs
    name_by_view = {im["bufferView"]: im.get("name", "") for im in images}

    for i, v in enumerate(views):
        off = v.get("byteOffset", 0)
        ln = v["byteLength"]
        raw = blob[off:off + ln]
        if i in image_views:
            name = name_by_view.get(i, "")
            img = Image.open(BytesIO(raw))
            md = max_dim_for(name)
            w, h = img.size
            if max(w, h) > md:
                scale = md / max(w, h)
                nw, nh = max(1, int(round(w * scale))), max(1, int(round(h * scale)))
                img = img.resize((nw, nh), Image.Resampling.LANCZOS)
            buf = BytesIO()
            img.save(buf, format="PNG", optimize=True)
            raw = buf.getvalue()
            print(f"  {name}: {w}x{h} -> {img.size[0]}x{img.size[1]}  {ln/1024:.0f}KB -> {len(raw)/1024:.0f}KB")
        # keep extra view fields except offset/length
        align4(out)
        offset = len(out)
        out.extend(raw)
        nv = {"buffer": 0, "byteOffset": offset, "byteLength": len(raw)}
        if "target" in v:
            nv["target"] = v["target"]
        if "byteStride" in v:
            nv["byteStride"] = v["byteStride"]
        new_views.append(nv)
        old_to_new[i] = len(new_views) - 1

    js["bufferViews"] = new_views
    js["buffers"] = [{"byteLength": len(out)}]

    # Drop VRM extension? Keep it; Blender glTF importer ignores unknown extensions.
    # Rename to .glb
    json_bytes = json.dumps(js, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "

    def chunk(ctype, data):
        return struct.pack("<I4s", len(data), ctype) + data

    total = 12 + 8 + len(json_bytes) + 8 + len(out)
    header = struct.pack("<4sII", b"glTF", 2, total)
    Path(dst).parent.mkdir(parents=True, exist_ok=True)
    Path(dst).write_bytes(header + chunk(b"JSON", json_bytes) + chunk(b"BIN\x00", bytes(out)))
    print(f"Wrote {dst} ({Path(dst).stat().st_size/1024/1024:.2f} MB)")


if __name__ == "__main__":
    main()
