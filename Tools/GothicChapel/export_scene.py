import bpy, json, math, os, numpy as np
from mathutils import Vector
from pathlib import Path

# Run this saved script in Blender with the authored chapel scene open.
ROOT=str(Path(__file__).resolve().parents[2])+os.sep
ASSET='Assets/Art/Models/Environment/GothicChapel/'
OUT=ROOT+ASSET
for folder in ("Source", "Textures"):
    os.makedirs(OUT+folder, exist_ok=True)
os.makedirs(ROOT+"output/gothic-scene/unity-export", exist_ok=True)
scene=bpy.context.scene
meshes=[o for o in scene.objects if o.type=='MESH' and not o.hide_render]
def vec(v): return {'x':float(v[0]),'y':float(v[2]),'z':float(v[1])}
def uv(v): return {'x':float(v[0]),'y':float(v[1])}
def image_array(im):
    a=np.empty(len(im.pixels),np.float32);im.pixels.foreach_get(a)
    return a.reshape(im.size[1],im.size[0],4)
def write_image(im,name):
    path=ASSET+'Textures/'+name+'.png'
    original_path=im.filepath_raw;original_format=im.file_format
    try:
        im.filepath_raw=ROOT+path;im.file_format='PNG';im.save()
    finally:
        im.filepath_raw=original_path;im.file_format=original_format
    return path
def image_node(socket):
    if not socket.is_linked:return None
    n=socket.links[0].from_node
    if n.type=='TEX_IMAGE':return n
    if n.type=='NORMAL_MAP':return image_node(n.inputs['Color'])
    return None
def scalar_array(socket,size):
    if not socket.is_linked:return np.full(size,float(socket.default_value),np.float32)
    link=socket.links[0];n=link.from_node
    if n.type=='TEX_IMAGE':a=image_array(n.image)[:,:,0]
    elif n.type=='SEPARATE_COLOR':
        tex=image_node(n.inputs[0]);idx=list(n.outputs).index(link.from_socket);a=image_array(tex.image)[:,:,idx]
    else:raise RuntimeError('Unsupported scalar node '+n.type)
    if a.shape!=size:
        yy=(np.arange(size[0])*a.shape[0]/size[0]).astype(int);xx=(np.arange(size[1])*a.shape[1]/size[1]).astype(int);a=a[yy[:,None],xx]
    return a
def bake_base(owner,mat):
    # Bake only albedo: no scene lights, shadows, or view transform in the texture.
    temp=owner.copy();temp.data=owner.data.copy();scene.collection.objects.link(temp)
    copy=mat.copy();temp.data.materials.clear();temp.data.materials.append(copy)
    for p in temp.data.polygons:p.material_index=0
    nt=copy.node_tree;bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
    output=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
    emission=nt.nodes.new('ShaderNodeEmission');base=bs.inputs['Base Color']
    if base.is_linked:nt.links.new(base.links[0].from_socket,emission.inputs[0])
    else:emission.inputs[0].default_value=base.default_value
    nt.links.new(emission.outputs[0],output.inputs['Surface'])
    image=bpy.data.images.new('ExportBake_'+mat.name,width=2048,height=2048,alpha=True)
    image.colorspace_settings.name='sRGB'
    target=nt.nodes.new('ShaderNodeTexImage');target.image=image
    for n in nt.nodes:n.select=False
    target.select=True;nt.nodes.active=target
    bpy.ops.object.select_all(action='DESELECT');temp.select_set(True);bpy.context.view_layer.objects.active=temp
    engine=scene.render.engine;samples=scene.cycles.samples;device=scene.cycles.device
    try:
        scene.render.engine='CYCLES';scene.cycles.samples=1;scene.cycles.device='CPU'
        scene.render.bake.margin=16
        bpy.ops.object.bake(type='EMIT',use_clear=True,margin=16)
        path=write_image(image,'TEX_'+mat.name.removeprefix('MAT_')+'_BaseColor')
    finally:
        scene.render.engine=engine;scene.cycles.samples=samples;scene.cycles.device=device
        data=temp.data;bpy.data.objects.remove(temp,do_unlink=True);bpy.data.meshes.remove(data)
        bpy.data.materials.remove(copy);bpy.data.images.remove(image)
    return path

manifest={'version':1,'units':'meters','coordinateConversion':'Blender (x,y,z) -> Unity (x,z,y); reverse triangle winding','materials':[],'objects':[]}
owners={}
for o in meshes:
    for slot in o.material_slots:
        if slot.material:owners.setdefault(slot.material,o)
for mat,owner in owners.items():
    nt=mat.node_tree;bs=next((n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'),None)
    spec={'name':mat.name,'baseMap':'','normalMap':'','maskMap':'','color':[1,1,1,1],'normalStrength':0.0,'metallic':1.0,'smoothness':1.0,'cutoff':0.0,'unlit':0.0,'bakeEmission':0.0}
    if bs:
        base=bs.inputs['Base Color'];tex=image_node(base)
        if tex:spec['baseMap']=write_image(tex.image,'TEX_'+mat.name.removeprefix('MAT_')+'_BaseColor')
        elif base.is_linked:spec['baseMap']=bake_base(owner,mat)
        else:spec['color']=list(base.default_value)
        normal=image_node(bs.inputs['Normal'])
        if normal:
            spec['normalMap']=write_image(normal.image,'TEX_'+mat.name.removeprefix('MAT_')+'_Normal')
            nm=bs.inputs['Normal'].links[0].from_node
            spec['normalStrength']=float(nm.inputs['Strength'].default_value)
        # R=metallic, G=occlusion, B=reserved, A=smoothness; independent of albedo.
        size=(1024,1024);rgba=np.ones((*size,4),np.float32)
        rgba[:,:,0]=scalar_array(bs.inputs['Metallic'],size)
        rgba[:,:,3]=1-np.clip(scalar_array(bs.inputs['Roughness'],size),0,1)
        mask=bpy.data.images.new('ExportMask',width=size[1],height=size[0],alpha=True)
        mask.colorspace_settings.name='Non-Color';mask.pixels.foreach_set(rgba.ravel())
        spec['maskMap']=write_image(mask,'TEX_'+mat.name.removeprefix('MAT_')+'_Mask');bpy.data.images.remove(mask)
    else:
        # Billboard source images retain alpha; preserve simple atmosphere tint nodes.
        tex=next(n for n in nt.nodes if n.type=='TEX_IMAGE');spec['baseMap']=write_image(tex.image,'TEX_'+mat.name.removeprefix('MAT_')+'_BaseColor')
        em=next(n for n in nt.nodes if n.type=='EMISSION');strength=em.inputs['Strength'].default_value
        spec['unlit']=1;spec['color']=[strength,strength,strength,1];spec['cutoff']=0 if mat.name.endswith('Sky') else .45
        mix=next((n for n in nt.nodes if n.type=='MIX_RGB'),None)
        if mix:
            spec['atmosphere']=list(mix.inputs[2].default_value);spec['atmosphereBlend']=float(mix.inputs[0].default_value)
    manifest['materials'].append(spec)
    print('EXPORTED MATERIAL',mat.name,flush=True)

deps=bpy.context.evaluated_depsgraph_get()
for o in meshes:
    ev=o.evaluated_get(deps);me=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    try:
        me.calc_loop_triangles()
        # Blender 4.5 exposes evaluated split corner normals.
        normals=me.corner_normals
        mw=o.matrix_world;nm=mw.to_3x3().inverted().transposed();origin=mw.translation
        uv0=me.uv_layers.active
        vertices=[];norm=[];texcoords=[];indices=[[] for _ in o.material_slots];lookup={}
        if not indices:indices=[[]]
        for tri in me.loop_triangles:
            corners=[]
            for li in tri.loops:
                vi=me.loops[li].vertex_index;p=mw@me.vertices[vi].co-origin;n=(nm@normals[li].vector).normalized();t=uv0.data[li].uv if uv0 else (0,0)
                key=(vi,round(n.x,6),round(n.y,6),round(n.z,6),round(t[0],7),round(t[1],7))
                if key not in lookup:
                    lookup[key]=len(vertices);vertices.append(vec(p));norm.append(vec(n));texcoords.append(uv(t))
                corners.append(lookup[key])
            indices[min(tri.material_index,len(indices)-1)].extend([corners[0],corners[2],corners[1]])
        category=o.users_collection[0].name
        record={'name':o.name,'category':category,'position':vec(origin),'materials':[slot.material.name for slot in o.material_slots],'backdrop':o.name.startswith('SM_Backdrop'),'foliage':o.name.startswith('SM_Ivy'),'vertices':vertices,'normals':norm,'uv0':texcoords,'submeshes':[{'indices':v} for v in indices]}
        path='Source/'+o.name+'.json'
        with open(OUT+path,'w') as f:json.dump(record,f,separators=(',',':'))
        manifest['objects'].append({'name':o.name,'path':ASSET+path,'category':category,'position':vec(origin),'backdrop':record['backdrop'],'foliage':record['foliage']})
    finally:ev.to_mesh_clear()
cam=scene.camera;mw=cam.matrix_world
manifest['camera']={'name':cam.name,'position':vec(mw.translation),'forward':vec(mw.to_quaternion()@Vector((0,0,-1))),'up':vec(mw.to_quaternion()@Vector((0,1,0))),'verticalFov':math.degrees(2*math.atan(cam.data.sensor_width/(2*cam.data.lens)/(scene.render.resolution_x/scene.render.resolution_y))),'near':cam.data.clip_start,'far':cam.data.clip_end,'aspect':scene.render.resolution_x/scene.render.resolution_y}
with open(OUT+'Source/GothicChapel_Layout.json','w') as f:json.dump(manifest,f,indent=2)
with open(ROOT+'output/gothic-scene/unity-export/layout-lock.json','w') as f:json.dump(manifest,f,indent=2)
print('EXPORT COMPLETE',len(meshes),'meshes',len(manifest['materials']),'materials')
