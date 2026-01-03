import bpy
import bmesh
import mathutils
import os

# Clear default scene
bpy.ops.wm.read_factory_settings(use_empty=True)

# Load the blend file
blend_path = "/Users/nolangriffis/Downloads/uploads_files_2263490_Oak+forest (1)/untitled.blend"
bpy.ops.wm.open_mainfile(filepath=blend_path)

# Get all mesh objects
mesh_objects = [obj for obj in bpy.data.objects if obj.type == 'MESH']

print(f"Found {len(mesh_objects)} mesh objects:")
for obj in mesh_objects:
    print(f"  - {obj.name}: location={obj.location}")

# Calculate the combined bounding box of all meshes in WORLD space
min_x, min_y, min_z = float('inf'), float('inf'), float('inf')
max_x, max_y, max_z = float('-inf'), float('-inf'), float('-inf')

for obj in mesh_objects:
    # Get world-space bounding box corners
    for corner in obj.bound_box:
        world_corner = obj.matrix_world @ mathutils.Vector(corner)
        min_x = min(min_x, world_corner.x)
        min_y = min(min_y, world_corner.y)
        min_z = min(min_z, world_corner.z)
        max_x = max(max_x, world_corner.x)
        max_y = max(max_y, world_corner.y)
        max_z = max(max_z, world_corner.z)

print(f"\nCombined bounding box:")
print(f"  Min: ({min_x:.3f}, {min_y:.3f}, {min_z:.3f})")
print(f"  Max: ({max_x:.3f}, {max_y:.3f}, {max_z:.3f})")

# The center X/Y and minimum Z (bottom of tree)
center_x = (min_x + max_x) / 2
center_y = (min_y + max_y) / 2
bottom_z = min_z

print(f"\nTree base should be at: ({center_x:.3f}, {center_y:.3f}, {bottom_z:.3f})")

# We need to move all objects so the center bottom is at origin
offset = mathutils.Vector((-center_x, -center_y, -bottom_z))
print(f"Applying offset: {offset}")

# Apply the offset to each object
for obj in mesh_objects:
    obj.location += offset

# Apply transforms to bake the location into the mesh
bpy.ops.object.select_all(action='DESELECT')
for obj in mesh_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_objects[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Verify the fix
print("\nAfter fix - verifying bounds:")
min_z_new = float('inf')
center_x_new = 0
center_y_new = 0
count = 0
for obj in mesh_objects:
    for corner in obj.bound_box:
        world_corner = obj.matrix_world @ mathutils.Vector(corner)
        min_z_new = min(min_z_new, world_corner.z)
        center_x_new += world_corner.x
        center_y_new += world_corner.y
        count += 1

center_x_new /= count
center_y_new /= count
print(f"  New center X/Y: ({center_x_new:.3f}, {center_y_new:.3f})")
print(f"  New bottom Z: {min_z_new:.6f}")

# Export path - ensure directory exists
export_dir = "/Users/nolangriffis/Documents/producer-tour-unity/Creator World Alpha/Assets/Art/Models/Environment/Trees"
os.makedirs(export_dir, exist_ok=True)
export_path = os.path.join(export_dir, "OakTree.fbx")

# Select all mesh objects for export
bpy.ops.object.select_all(action='DESELECT')
for obj in mesh_objects:
    obj.select_set(True)

# Export as FBX
bpy.ops.export_scene.fbx(
    filepath=export_path,
    use_selection=True,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    use_mesh_modifiers=True,
    mesh_smooth_type='FACE',
    use_mesh_edges=False,
    path_mode='COPY',
    embed_textures=True,
    axis_forward='-Z',
    axis_up='Y'
)

print(f"\nExported to: {export_path}")

# Also copy textures
import shutil
texture_src = "/Users/nolangriffis/Downloads/uploads_files_2263490_Oak+forest (1)/texture"
texture_files = [
    "TexturesCom_Branches0012_1_masked_S.png",
    "TexturesCom_PineBark2_1K_albedo.tif",
    "TexturesCom_PineBark2_1K_normal.tif",
    "TexturesCom_PineBark2_1K_ao.tif",
    "TexturesCom_PineBark2_1K_roughness.tif",
    "TexturesCom_PineBark2_1K_height.tif"
]

for tex_file in texture_files:
    src = os.path.join(texture_src, tex_file)
    dst = os.path.join(export_dir, tex_file)
    if os.path.exists(src):
        shutil.copy2(src, dst)
        print(f"Copied: {tex_file}")

print("\nDone! Tree pivot is now at the base center.")
