import bpy, os
from mathutils import Matrix
root=r'F:\Texas-Holdem'
base=os.path.join(root,'Assets','Art','Characters','SkeletonWraith')
for d in ['Models','Textures','Materials','Shaders','Animation','Prefabs','Editor']:
    os.makedirs(os.path.join(base,d),exist_ok=True)
source=os.path.join(root,'ArtSource','Characters','SkeletonWraith')
os.makedirs(source,exist_ok=True)
r=bpy.data.objects['Monster_Rig'];r.name='SkeletonWraith_Rig';r.data.name='SkeletonWraith_Skeleton'
bpy.data.objects['tripo_node_1fc3a5da.005'].name='SkeletonWraith_Body'
bpy.data.objects['tripo_node_1fc3a5da.093'].name='SkeletonWraith_Cloak'
for a in bpy.data.actions:
    label={'01':'Idle','02':'Attack','03':'Hit','04':'PowerUp'}.get(a.name[:2])
    if label:a.name='SkeletonWraith_'+label;a.use_fake_user=True
for o in bpy.context.scene.objects:
    if o.type=='MESH':
        for v in o.data.vertices:
            weights=sorted([(g.group,g.weight) for g in v.groups],key=lambda x:-x[1])[:4]
            total=sum(w for g,w in weights)
            for g in list(v.groups):o.vertex_groups[g.group].remove([v.index])
            for g,w in weights:o.vertex_groups[g].add([v.index],w/total,'REPLACE')
        for mat in o.data.materials:
            mat.name='SkeletonWraith_Surface'
for image in bpy.data.images:
    if image.size[0]>0 and image.type!='RENDER_RESULT':
        image.filepath_raw=os.path.join(base,'Textures','SkeletonWraith_BaseColor.png')
        image.file_format='PNG';image.save()
r.animation_data.action=None
for pb in r.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
for o in bpy.context.scene.objects:o.select_set(o.type in {'ARMATURE','MESH'})
bpy.context.view_layer.objects.active=r
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source,'SkeletonWraith.blend'),copy=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(base,'Models','SkeletonWraith.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0,bake_anim_step=1.0,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP',use_mesh_modifiers=True)
print('SKELETON_WRAITH_EXPORT_COMPLETE',flush=True)
