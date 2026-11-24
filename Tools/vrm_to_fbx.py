# Blender script: import optimized GLB (from VRM) and export a rigged FBX + textures.
import bpy
import os
import sys

argv = sys.argv
argv = argv[argv.index("--") + 1:] if "--" in argv else []
src = argv[0]
dst = argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)

print("[delta] Import glTF:", src)
bpy.ops.import_scene.gltf(filepath=src)

# Drop leftover / helper objects that are not part of the character
for o in list(bpy.data.objects):
    if o.type == "MESH" and "ico" in o.name.lower():
        print("[delta] remove", o.name)
        bpy.data.objects.remove(o, do_unlink=True)

tex_dir = os.path.join(os.path.dirname(dst), "Textures")
os.makedirs(tex_dir, exist_ok=True)

def cap_for(name):
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

for img in list(bpy.data.images):
    w, h = img.size
    if w <= 0 or h <= 0:
        continue
    md = cap_for(img.name)
    if max(w, h) > md:
        scale = md / max(w, h)
        nw, nh = max(1, int(round(w * scale))), max(1, int(round(h * scale)))
        print(f"[delta] scale image {img.name} {w}x{h} -> {nw}x{nh}")
        img.scale(nw, nh)
    safe = "".join(c if c.isalnum() or c in "._-" else "_" for c in img.name)
    if not safe.lower().endswith((".png", ".jpg", ".jpeg")):
        safe += ".png"
    path = os.path.join(tex_dir, safe)
    img.filepath_raw = path
    img.file_format = "PNG"
    try:
        img.save()
        print("[delta] saved", path)
    except Exception as e:
        print("[delta] save failed", img.name, e)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
print("[delta] mesh objects:", len(meshes), [o.name for o in meshes])
print("[delta] armatures:", len(armatures), [o.name for o in armatures])

hair = [o for o in meshes if "hair" in o.name.lower()]
if len(hair) > 1:
    bpy.ops.object.select_all(action="DESELECT")
    for o in hair:
        o.select_set(True)
    bpy.context.view_layer.objects.active = hair[0]
    bpy.ops.object.join()
    print("[delta] joined hair objects:", len(hair))

os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=False,
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
)
print("[delta] exported", dst)
