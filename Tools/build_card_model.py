"""Run through Blender MCP in Blender 4.5; build a shared card mesh and art validation scene."""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path('/Users/linxi/Documents/UnityProjects/Texas Hold’em')
WIDTH, HEIGHT, THICKNESS = .064, .096, .00035
RADIUS, EDGE_BEVEL, CORNER_SEGMENTS = .0008, .000035, 4
BLEND = ROOT / 'Blender/Cards/FourBeasts_Card.blend'
FBX = ROOT / 'Assets/Art/Models/Cards/Card_Rounded.fbx'
VALIDATION = ROOT / 'ArtSource/Cards/ModelValidation'
for folder in [BLEND.parent, FBX.parent, VALIDATION]:
    folder.mkdir(parents=True, exist_ok=True)

# Add an isolated scene; preserve all pre-existing user objects and scenes.
scene = bpy.data.scenes.new('FourBeasts_Card_Validation')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
collection = bpy.data.collections.new('Card_Model_Validation')
scene.collection.children.link(collection)

def contour(inset, z):
    w, h, r = WIDTH - 2*inset, HEIGHT - 2*inset, RADIUS-inset
    result = []
    centers = [(w/2-r, h/2-r), (-w/2+r, h/2-r), (-w/2+r, -h/2+r), (w/2-r, -h/2+r)]
    for corner, (cx, cy) in enumerate(centers):
        for segment in range(CORNER_SEGMENTS+1):
            angle = math.radians(corner*90 + segment*90/CORNER_SEGMENTS)
            result.append((cx+r*math.cos(angle), cy+r*math.sin(angle), z))
    return result

rings = [contour(EDGE_BEVEL, THICKNESS/2), contour(0, THICKNESS/2-EDGE_BEVEL),
         contour(0, -THICKNESS/2+EDGE_BEVEL), contour(EDGE_BEVEL, -THICKNESS/2)]
n = len(rings[0])
verts = [v for ring in rings for v in ring] + [(0,0,THICKNESS/2), (0,0,-THICKNESS/2)]
faces, tags = [], []
for i in range(n):
    j = (i+1) % n
    faces.append((4*n, i, j)); tags.append((0, 0))
    faces.append((4*n+1, 3*n+j, 3*n+i)); tags.append((1, 0))
for ring in range(3):
    for i in range(n):
        j = (i+1) % n
        a, b = ring*n, (ring+1)*n
        faces.append((a+i, b+i, b+j, a+j)); tags.append((0, 1))
mesh = bpy.data.meshes.new('Card_Rounded_SharedMesh')
mesh.from_pydata(verts, [], faces)
mesh.update()
uv = mesh.uv_layers.new(name='CardLocalUV')
surface = mesh.uv_layers.new(name='CardSurface')
for polygon, tag in zip(mesh.polygons, tags):
    polygon.use_smooth = bool(tag[1])
    for loop in polygon.loop_indices:
        x,y,z = mesh.vertices[mesh.loops[loop].vertex_index].co
        # Back UVs are oriented upright when the card is turned around its long axis.
        uv.data[loop].uv = (max(0,min(1,(-x if tag[0] else x)/WIDTH+.5)), max(0,min(1,y/HEIGHT+.5)))
        surface.data[loop].uv = tag
mesh.uv_layers.active_index = 0
uv.active_render = True
mesh.calc_loop_triangles()

mat = bpy.data.materials.new('FourBeasts_Card_BlenderValidation')
mat.use_nodes = True
mat.diffuse_color = (.65,.48,.28,1)
nodes, links = mat.node_tree.nodes, mat.node_tree.links
bsdf = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
bsdf.inputs[2].default_value = .86
bsdf.inputs[13].default_value = .035
local = nodes.new('ShaderNodeUVMap'); local.uv_map = uv.name
flags = nodes.new('ShaderNodeUVMap'); flags.uv_map = surface.name
split = nodes.new('ShaderNodeSeparateXYZ')
links.new(flags.outputs[0], split.inputs[0])
textures = []
for filename in ['spades_k.png', 'card_back.png']:
    texture = nodes.new('ShaderNodeTexImage')
    texture.image = bpy.data.images.load(str(ROOT / 'Assets/Art/Cards/FourBeasts' / filename), check_existing=True)
    texture.interpolation = 'Closest'
    texture.extension = 'EXTEND'
    links.new(local.outputs[0], texture.inputs[0])
    textures.append(texture)
faces_mix = nodes.new('ShaderNodeMixRGB'); faces_mix.blend_type = 'MIX'
links.new(split.outputs[0], faces_mix.inputs[0])
links.new(textures[0].outputs[0], faces_mix.inputs[1]); links.new(textures[1].outputs[0], faces_mix.inputs[2])
edge_mix = nodes.new('ShaderNodeMixRGB'); edge_mix.blend_type = 'MIX'
links.new(split.outputs[1], edge_mix.inputs[0]); links.new(faces_mix.outputs[0], edge_mix.inputs[1])
# Linear values for a warm, matte paper edge.
edge_mix.inputs[2].default_value = (.57,.43,.26,1)
links.new(edge_mix.outputs[0], bsdf.inputs[0])
mesh.materials.append(mat)

card = bpy.data.objects.new('Card_Rounded', mesh)
collection.objects.link(card)
card['width_m'] = WIDTH; card['height_m'] = HEIGHT; card['thickness_m'] = THICKNESS
card['corner_radius_m'] = RADIUS; card['edge_bevel_m'] = EDGE_BEVEL
card['uv0'] = 'Normalized local card coordinates; back orientation corrected'
card['uv1'] = 'x=isBack, y=isEdge; front=(0,0), back=(1,0), edge=(0,1)'
card['material_note'] = 'Blender validation only; Unity uses a future shared atlas shader'
card['asset_role'] = 'Reusable base mesh; no particular card atlas region is baked into UV0'

bpy.context.view_layer.update()
# Check the mesh before exporting; all dimensions are in metres.
bm = bmesh.new(); bm.from_mesh(mesh)
assert all(edge.is_manifold for edge in bm.edges), 'Non-manifold card shell'
volume = bm.calc_volume(signed=True)
assert volume > 0, 'Inverted shell winding'
bm.free()
assert abs(card.dimensions.x-WIDTH)<1e-6 and abs(card.dimensions.y-HEIGHT)<1e-6
assert len(mesh.materials)==1 and all(poly.material_index==0 for poly in mesh.polygons)
assert all(0<=item.uv.x<=1 and 0<=item.uv.y<=1 for item in uv.data)
for polygon, tag in zip(mesh.polygons, tags):
    if tag == (0,0): assert polygon.normal.z > .99
    if tag == (1,0): assert polygon.normal.z < -.99

# Export only the reusable card at the origin, not the display stage.
for obj in bpy.context.view_layer.objects: obj.select_set(False)
card.select_set(True); bpy.context.view_layer.objects.active = card
bpy.ops.export_scene.fbx(filepath=str(FBX), use_selection=True, object_types={'MESH'},
    global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    mesh_smooth_type='OFF', use_tspace=True, use_triangles=True, bake_anim=False,
    use_custom_props=True, path_mode='RELATIVE', embed_textures=False)

# Keep an untransformed reusable source in its own scene.
card.name = 'Card_Front_Validation'
base_scene = bpy.data.scenes.new('Card_Model')
base_scene.unit_settings.system = 'METRIC'
base_scene.unit_settings.scale_length = 1
base = bpy.data.objects.new('Card_Rounded', mesh)
base_scene.collection.objects.link(base)
for key, value in card.items(): base[key] = value
# Linked display copies demonstrate both sides without duplicating the mesh.
card.location = (-.043, 0, .012)
card.rotation_euler = (math.radians(-5),math.radians(-13),math.radians(-5))
back = bpy.data.objects.new('Card_Back_Validation', mesh); collection.objects.link(back)
back.location = (.043,0,.012)
back.rotation_euler = (math.radians(5),math.pi-math.radians(13),math.radians(5))

ground_mesh = bpy.data.meshes.new('Validation_GroundMesh')
ground_mesh.from_pydata([(-1,-1,0),(1,-1,0),(1,1,0),(-1,1,0)],[],[(0,1,2,3)])
ground_mesh.update()
ground = bpy.data.objects.new('Validation_Ground',ground_mesh); collection.objects.link(ground)
ground_mat = bpy.data.materials.new('Validation_Ground_Matte'); ground_mat.use_nodes=True
shader=next(node for node in ground_mat.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
shader.inputs[0].default_value=(.017,.024,.031,1); shader.inputs[2].default_value=.95
shader.inputs[13].default_value=.1
ground_mesh.materials.append(ground_mat)

world = bpy.data.worlds.new('Card_Validation_World'); scene.world=world; world.use_nodes=True
background=next(node for node in world.node_tree.nodes if node.type=='BACKGROUND')
background.inputs[0].default_value=(.28,.31,.36,1); background.inputs[1].default_value=.15

def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

camera_data=bpy.data.cameras.new('Card_Validation_Camera'); camera_data.type='ORTHO'
camera_data.ortho_scale=.205; camera_data.clip_start=.001; camera_data.clip_end=10
camera=bpy.data.objects.new('Card_Validation_Camera',camera_data); collection.objects.link(camera)
camera.location=(0,-.135,.30); aim(camera,(0,0,.005)); scene.camera=camera
for name,location,power,size in [('Key',(-.11,-.08,.24),1.8,.18),('Fill',(.16,.07,.18),.8,.18)]:
    data=bpy.data.lights.new('Card_Validation_'+name,type='AREA'); data.energy=power; data.shape='DISK'; data.size=size
    light=bpy.data.objects.new(data.name,data); collection.objects.link(light)
    light.location=location; aim(light,(0,0,.008))
scene.render.resolution_x=1920; scene.render.resolution_y=1080; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
scene.render.filepath=str(VALIDATION/'card_front_back.png')
if hasattr(scene,'eevee'): scene.eevee.taa_render_samples=32
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.shading.type='RENDERED'
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.region_3d.view_camera_zoom=12
for image in [texture.image for texture in textures]: image.pack()
summary={'dimensions_m':[WIDTH,HEIGHT,THICKNESS], 'corner_radius_m':RADIUS,
    'edge_bevel_m':EDGE_BEVEL,'blender_vertices':len(mesh.vertices),
    'triangles':len(mesh.loop_triangles),'material_slots':len(mesh.materials),
    'uv_layers':[layer.name for layer in mesh.uv_layers], 'manifold':True,'positive_volume_m3':volume,
    'point_sampling':all(texture.interpolation=='Closest' for texture in textures),
    'fbx':str(FBX),'blend':str(BLEND),'front':'spades_k','back':'card_back'}
(VALIDATION/'mesh_validation.json').write_text(json.dumps(summary,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
print(json.dumps(summary))

# Render the stage and a true 960x540 check, then restore authoring output settings.
bpy.ops.render.render(write_still=True)
scene.render.resolution_x, scene.render.resolution_y = 960, 540
scene.render.filepath = str(VALIDATION/'card_960x540.png')
bpy.ops.render.render(write_still=True)
scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
scene.render.filepath = str(VALIDATION/'card_front_back.png')
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
