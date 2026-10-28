"""Run with Blender 4.5 in background against BarrelCrab.blend. Source stays intact."""
import bpy
import json
from pathlib import Path

ROOT = Path('F:/Texas-Holdem')
OUT = ROOT / 'Assets/Art/Characters/BarrelCrab/Models/BarrelCrab.fbx'
rig = bpy.data.objects['RIG_BarrelCrab']
scene = bpy.context.scene
assert len(bpy.data.actions) == 6
retract = bpy.data.actions['BarrelCrab_Retract_IntoShell']
rig.animation_data.action = retract
rig.animation_data.action_slot = retract.slots[0]
scene.frame_set(32)
pose = {p.name: (p.location.copy(), p.rotation_euler.copy()) for p in rig.pose.bones}
rig.animation_data.action = None
for frame in (1, 25):
    for pb in rig.pose.bones:
        pb.location, pb.rotation_euler = pose[pb.name]
        pb.keyframe_insert(data_path='location', frame=frame, group=pb.name)
        pb.keyframe_insert(data_path='rotation_euler', frame=frame, group=pb.name)
rig.animation_data.action.name = 'BarrelCrab_Shelled_Idle'
rig.animation_data.action.use_fake_user = True
idle = bpy.data.actions['BarrelCrab_Idle_Loop']
rig.animation_data.action = idle
rig.animation_data.action_slot = idle.slots[0]
scene.frame_set(1)
meshes = [o for o in scene.objects if o.type == 'MESH']
eyes = [bpy.data.objects[f'SM_BarrelCrab_Part_{i:02d}'] for i in (24,27)]
assert all(len(o.vertex_groups) == 1 and o.vertex_groups[0].name == 'Body' for o in eyes)
body = [o for o in meshes if o not in eyes]
def join(parts, name):
    for o in bpy.context.selected_objects: o.select_set(False)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name
    return obj
body = join(body, 'SM_BarrelCrab_Body')
eyes = join(eyes, 'SM_BarrelCrab_Eyes')
eye_material = bpy.data.materials.new('M_BarrelCrab_Eyes')
eyes.data.materials.clear()
eyes.data.materials.append(eye_material)
for o in bpy.context.selected_objects: o.select_set(False)
for o in (rig,body,eyes): o.select_set(True)
bpy.context.view_layer.objects.active = rig
clips = [
    ('Idle','BarrelCrab_Idle_Loop',1,49,True),
    ('Attack','BarrelCrab_Attack_Claw',1,24,False),
    ('Hit','BarrelCrab_Hit_Normal',1,20,False),
    ('Retract','BarrelCrab_Retract_IntoShell',1,20,False),
    ('ShelledIdle','BarrelCrab_Shelled_Idle',1,25,True),
    ('ShelledHit','BarrelCrab_Hit_Shelled',1,18,False),
    ('Emerge','BarrelCrab_Emerge_FromShell',1,32,False),
]
bpy.ops.export_scene.fbx(filepath=str(OUT), use_selection=True,
    object_types={'ARMATURE','MESH'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=False, add_leaf_bones=False, use_armature_deform_only=False,
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_step=1, bake_anim_simplify_factor=0, path_mode='STRIP',
    mesh_smooth_type='OFF', use_triangles=True)
manifest = {'fps':24,'clips':[dict(state=s,take=t,first=f,last=l,loop=loop) for s,t,f,l,loop in clips],
    'eyes_source_parts':[24,27], 'mesh_vertices':{o.name:len(o.data.vertices) for o in (body,eyes)},
    'bones':[b.name for b in rig.data.bones], 'source':'ArtSource/Harbor/Enemies/BarrelCrab/BarrelCrab.blend',
    'attack_candidate':'v7, fast recovery', 'forward_source':'-Y', 'units':'metres'}
(OUT.parent/'BarrelCrab.export.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print(json.dumps(manifest))
