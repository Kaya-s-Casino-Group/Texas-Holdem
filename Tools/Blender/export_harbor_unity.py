"""Run inside the authored Blender scene; mesh coordinates are baked Unity world space."""
import bpy, os, json, struct, re, math
from mathutils import Vector, Matrix

ROOT = r'D:\Texas-Holdem'
ASSET = 'Assets/Art/Environment/Harbor'
OUT = os.path.join(ROOT, ASSET)
for folder in ['Source', 'Textures', 'Meshes', 'Materials', 'Prefabs']:
    os.makedirs(os.path.join(OUT, folder), exist_ok=True)
scene = bpy.data.scenes['Scene']
bpy.context.window.scene = scene
exec(compile(open(os.path.join(ROOT, 'Tools/Blender/prepare_harbor_assets.py'), encoding='utf-8').read(), 'prepare_harbor_assets.py', 'exec'))
C = Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
def vec(v): return list(C @ Vector(v))
def clean(s): return re.sub(r'[^A-Za-z0-9_]+','_',s).strip('_')
def ancestry(o):
    result=[]
    while o: result.append(o); o=o.parent
    return result
def excluded(o):
    return any(a.type=='ARMATURE' or any(k in a.name for k in ['Palamedes','SkeletonWraith']) for a in ancestry(o))
meshes=[o for o in scene.objects if o.type=='MESH' and not o.hide_render and not excluded(o)]
lights=[o for o in scene.objects if o.type=='LIGHT' and not o.hide_render]
manifest={'version':1,'coordinateConversion':'(-x,z,-y), reversed triangle winding; all mesh transforms identity', 'objects':[], 'materials':[], 'lights':[], 'excludedCharacters':[o.name for o in scene.objects if excluded(o)],'camera':{}}
matids={}; images={}; counters={}
def save_image(im):
    if not im:return ''
    if im in images:return images[im]
    filename=clean(im.name.removeprefix('T_'))+'.png'
    filename='T_'+filename
    path=ASSET+'/Textures/'+filename
    old=im.filepath_raw;fmt=im.file_format
    try:im.filepath_raw=os.path.join(ROOT,path);im.file_format='PNG';im.save()
    finally:im.filepath_raw=old;im.file_format=fmt
    images[im]=path
    return path
def image_from(socket):
    if not socket.is_linked:return None
    node=socket.links[0].from_node
    if node.type=='TEX_IMAGE':return node.image
    if node.type=='NORMAL_MAP':return image_from(node.inputs['Color'])
    return None
def material(mat,kind,flag):
    key=(mat.name if mat else 'Default',kind,flag['name'] if flag else '')
    if key in matids:return matids[key]
    idx=len(manifest['materials']);name='M_Harbor_'+clean(key[0])+('_Flag_'+str(idx) if flag else '')
    spec={'name':name,'kind':kind,'baseMap':'','normalMap':'','color':[1,1,1,1],'roughness':.85,'metallic':0,'emission':0,'cutoff':0,'normalStrength':1,'flagAmplitude':0,'flagSpeed':1,'flagDirection':[0,0,1],'flagSide':[1,0,0]}
    if mat and mat.use_nodes:
        nodes=mat.node_tree.nodes
        bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
        if bs:
            spec['baseMap']=save_image(image_from(bs.inputs['Base Color']))
            if not spec['baseMap']:spec['color']=list(bs.inputs['Base Color'].default_value)
            spec['normalMap']=save_image(image_from(bs.inputs['Normal']))
            spec['roughness']=float(bs.inputs['Roughness'].default_value)
            spec['metallic']=float(bs.inputs['Metallic'].default_value)
            spec['emission']=float(bs.inputs['Emission Strength'].default_value)
        if kind=='Backdrop' or not bs:
            tex=next((n for n in nodes if n.type=='TEX_IMAGE' and n.image),None)
            if tex:spec['baseMap']=save_image(tex.image)
            em=next((n for n in nodes if n.type=='EMISSION'),None)
            if em:
                strength=float(em.inputs['Strength'].default_value)
                spec['color']=[strength]*3+[1]
                if not tex:spec['color']=[float(v)*strength for v in em.inputs['Color'].default_value[:3]]+[1]
            spec['cutoff']=.35 if kind=='Backdrop' and 'Sky' not in mat.name else 0
    if flag:
        spec.update(flagAmplitude=flag['amplitude'],flagSpeed=float(flag.get('speed',1)),flagDirection=flag['normal'],flagSide=flag['side'])
    manifest['materials'].append(spec);matids[key]=idx
    return idx

deps=bpy.context.evaluated_depsgraph_get()
for o in sorted(meshes,key=lambda o:o.name):
    oldname=o.get('unity_source_name',o.name)
    water=oldname=='HarborWater_Surface'
    backdrop='painted_v2' in oldname
    isflag=bool(o.data.shape_keys and not water and any('Flag_' in k.name for k in o.data.shape_keys.key_blocks))
    kind='Water' if water else 'Backdrop' if backdrop else 'Flag' if isflag else 'Emission' if 'GlowCore' in oldname else 'Static'
    parent=o.parent
    stem=clean(parent.get('unity_source_name',parent.name).removeprefix('ENV_').split('_Placement')[0] if parent else oldname)
    if oldname.startswith('tripo_node_2bc'):stem='Tavern'
    if isflag:stem='Banner_Cloth'
    if water:stem='Water_Surface'
    if backdrop:stem=clean(oldname.replace('_painted_v2',''))
    count=counters.get(stem,0)+1;counters[stem]=count
    name=o.name if o.name.startswith('SM_Harbor_') else 'SM_Harbor_'+stem+f'_{count:03d}'
    o['unity_source_name']=oldname;o.name=name;o.data.name=name+'_Mesh'
    # Animated objects export rest/basis vertices, never Blender animation or solidify volume.
    ev=None
    if water or isflag:me=o.data
    else:ev=o.evaluated_get(deps);me=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    me.calc_loop_triangles();mw=o.matrix_world;nm=mw.to_3x3().inverted().transposed();normals=me.corner_normals;uv=me.uv_layers.active
    basis=o.data.shape_keys.key_blocks[0] if o.data.shape_keys and (water or isflag) else None
    flag=None
    if isflag:
        coords=[v.co for v in basis.data];mins=[min(p[i] for p in coords) for i in range(3)];maxs=[max(p[i] for p in coords) for i in range(3)];span=[maxs[i]-mins[i] for i in range(3)];axis=min(range(3),key=lambda a:span[a]);side=1 if axis==0 else 0
        direction=Vector([float(a==axis) for a in range(3)]);sidevec=Vector([float(a==side) for a in range(3)])
        # Actual world offset matches source shape-key amplitudes with inherited scale baked.
        amp=.026 if 'Tattered' in oldname else .012
        flag={'name':name,'amplitude':amp*(mw.to_3x3()@direction).length,'normal':vec((mw.to_3x3()@direction).normalized()),'side':vec((mw.to_3x3()@sidevec).normalized()),'speed':float(o.get('flag_wind_speed',1))}
    material_indices=[material(slot.material,kind,flag) for slot in o.material_slots]
    if not material_indices:material_indices=[material(None,kind,flag)]
    lookup={};vertices=[];submeshes=[[] for _ in material_indices]
    for tri in me.loop_triangles:
        triidx=[]
        for li in tri.loops:
            vi=me.loops[li].vertex_index;co=basis.data[vi].co if basis else me.vertices[vi].co
            p=C@(mw@co);normal=(C@(nm@normals[li].vector)).normalized();t=uv.data[li].uv if uv else (0,0)
            color=(0,0,0,1)
            if flag:
                down=(maxs[2]-co.z)/max(1e-6,span[2]);pin=.17 if 'Tattered' in oldname else .12;w=max(0,(down-pin)/(1-pin))**1.6;across=(co[side]-(mins[side]+maxs[side])/2)/max(1e-6,span[side]);color=(w,down,across,1)
            key=(vi,round(normal.x,5),round(normal.y,5),round(normal.z,5),round(t[0],6),round(t[1],6))
            if key not in lookup:
                lookup[key]=len(vertices);vertices.append((*p,*normal,*t,*color))
            triidx.append(lookup[key])
        # Negative object determinants need an additional winding flip after conversion.
        if mw.to_3x3().determinant()>0:triidx.reverse()
        submeshes[min(tri.material_index,len(submeshes)-1)].extend(triidx)
    path=ASSET+'/Source/'+name+'.bytes'
    with open(os.path.join(ROOT,path),'wb') as f:
        f.write(struct.pack('<ii',0x48415242,len(vertices)))
        for v in vertices:f.write(struct.pack('<12f',*v))
        f.write(struct.pack('<i',len(submeshes)))
        for indices in submeshes:f.write(struct.pack('<i',len(indices)));f.write(struct.pack('<%di'%len(indices),*indices))
    manifest['objects'].append({'name':name,'sourceName':oldname,'path':path,'kind':kind,'materials':material_indices})
    if ev:ev.to_mesh_clear()

for i,o in enumerate(lights):
    old=o.name
    if not old.startswith('L_Harbor_'):o.name='L_Harbor_'+clean(old)+f'_{i+1:02d}'
    o.data.name=o.name+'_Light'
    d=o.data
    manifest['lights'].append({'name':o.name,'type':d.type,'position':vec(o.matrix_world.translation),'forward':vec(o.matrix_world.to_quaternion()@Vector((0,0,-1))),'up':vec(o.matrix_world.to_quaternion()@Vector((0,1,0))),'color':list(d.color),'intensity':float(d.energy if d.type=='SUN' else d.energy/10),'range':float(max(3,math.sqrt(d.energy)*1.2)),'radius':float(getattr(d,'shadow_soft_size',.1))})
cam=scene.camera
manifest['camera']={'position':vec(cam.matrix_world.translation),'forward':vec(cam.matrix_world.to_quaternion()@Vector((0,0,-1))),'up':vec(cam.matrix_world.to_quaternion()@Vector((0,1,0))),'fov':math.degrees(cam.data.angle_y),'near':cam.data.clip_start,'far':cam.data.clip_end}
water=next((o for o in meshes if o.get('gerstner_parameters')),None)
manifest['waves']=json.loads(water['gerstner_parameters']) if water else []
manifest['waveStrength']=float(water.get('gerstner_strength',1)) if water else 1
manifest['waveSpeed']=float(water.get('gerstner_speed',.8)) if water else .8
with open(os.path.join(OUT,'Source/Harbor_Layout.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,indent=2)
for o in scene.objects:
    if o.type=='EMPTY' and not excluded(o):
        original=o.get('unity_source_name',o.name);o['unity_source_name']=original
        o.name='ENV_'+clean(original.removeprefix('ENV_').replace('_Placement',''))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
print('HARBOR_EXPORT_COMPLETE',len(manifest['objects']),'meshes',len(manifest['materials']),'materials',len(lights),'lights',len(manifest['excludedCharacters']),'character objects excluded')
