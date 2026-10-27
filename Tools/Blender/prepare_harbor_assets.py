"""Semantic datablock names and portable packed dependencies, run in live Blender."""
import bpy, re, os, json

def semantic(s):
    s=s.replace('_Material','').replace('_Placement','')
    return re.sub(r'[^A-Za-z0-9_]+','_',s).strip('_')

def character(o):
    while o:
        if o.type=='ARMATURE' or any(s in o.name for s in ('Palamedes','SkeletonWraith')):return True
        o=o.parent
    return False

for o in sorted(bpy.data.objects,key=lambda x:x.name):
    if o.type!='MESH' or character(o):continue
    if 'tripo' in o.name.lower():
        stem=semantic(o.parent.name if o.parent else next((s.material.name for s in o.material_slots if s.material),'Environment'))
        o['unity_source_name']=o.get('unity_source_name',o.name)
        o.name='SM_Library_'+stem
    o.data.name=o.name+'_Mesh'
    if o.data.shape_keys:o.data.shape_keys.name=o.name+'_Deformation'

seen=set()
for mat in sorted(bpy.data.materials,key=lambda x:x.name):
    if not mat.use_nodes or any(s in mat.name for s in ('Palamedes','SkeletonWraith')):continue
    nodes=mat.node_tree.nodes
    if 'tripo' in mat.name.lower():
        tex=next((n.image for n in nodes if n.type=='TEX_IMAGE' and n.image),None)
        # Source asset folder retains the descriptive product name.
        folder=os.path.basename(os.path.dirname(os.path.dirname(bpy.path.abspath(tex.filepath)))) if tex else 'Environment'
        mat.name=semantic(folder.replace('+','_').replace('3d_model',''))+'_Material'
    stem=semantic(mat.name)
    for node in nodes:
        if node.type!='TEX_IMAGE' or not node.image or node.image in seen:continue
        im=node.image
        if not im.name.lower().startswith('tripo'):continue
        targets=[(l.to_node.type,l.to_socket.name) for out in node.outputs for l in out.links]
        label=(im.filepath+' '+node.label).lower()
        role='Normal' if any(t=='NORMAL_MAP' for t,s in targets) or 'normal' in label else 'Roughness' if any(s=='Roughness' for t,s in targets) or 'roughness' in label else 'Metallic' if any(s=='Metallic' for t,s in targets) or 'metallic' in label else 'BaseColor' if any(s in ('Base Color','Color') for t,s in targets) else 'SurfaceMap'
        im.name='T_Harbor_'+stem+'_'+role;seen.add(im)

# Mesh datablocks are already embedded; localize any linked IDs before packing textures.
for datablock in list(bpy.data.user_map()):
    if getattr(datablock,'library',None) and hasattr(datablock,'make_local'):datablock.make_local()
# Remove only abandoned import datablocks whose images are missing and which have no
# object users. Live scene/library assets and valid unused assets are preserved.
def missing(im):
    return im.source=='FILE' and not im.packed_file and not os.path.exists(bpy.path.abspath(im.filepath))
stale_mats=[m for m in bpy.data.materials if m.use_nodes and any(n.type=='TEX_IMAGE' and n.image and missing(n.image) for n in m.node_tree.nodes)]
for mesh in list(bpy.data.meshes):
    if mesh.users==0 and any(m in stale_mats for m in mesh.materials):bpy.data.meshes.remove(mesh)
for mat in stale_mats:
    if mat.users==0:bpy.data.materials.remove(mat)
for im in list(bpy.data.images):
    if im.users==0 and missing(im):bpy.data.images.remove(im)
for mesh in bpy.data.meshes:
    if 'tripo' in mesh.name.lower():
        mat=next((m for m in mesh.materials if m),None)
        if mat:mesh.name='SM_Library_'+semantic(mat.name)+'_Mesh'
bpy.ops.file.pack_all()
external=[i.name for i in bpy.data.images if i.users and i.source in {'FILE','TILED'} and not i.packed_file]
linked=[i.name for i in bpy.data.user_map() if getattr(i,'library',None)]
if external or linked:raise RuntimeError('Unpacked dependencies: '+str(external+linked))
print('PACK_VERIFIED',sum(bool(i.packed_file) for i in bpy.data.images),'images; linked data:',len(linked))
