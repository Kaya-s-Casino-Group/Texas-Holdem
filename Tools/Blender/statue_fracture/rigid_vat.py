"""Independent rigid VAT exporter for the statue fracture tool (Blender 4.5+).

Design reference: OpenVAT's reference mesh / texel UV / remap sidecar / decoder
preview workflow. This is a piece-transform format, not OpenVAT standard VAT.
No upstream source or node groups are included or required.
"""
import bpy
import json
import math
import re
import shutil
import tempfile
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

SCHEMA = 'statue-rigid-vat-v1'
REFERENCE = 'https://github.com/sharpen3d/openvat/tree/43b519620c710c9081a72eba253222d6fe6f931f/Development/openvat'
SURFACE_COLOR = 'VAT_SURFACE'
RESERVED_UV = ('VAT_UV', 'VAT_PIVOT_XY', 'VAT_PIVOT_Z_ID')


def enum_set(owner, key, value):
    options = [item.identifier for item in owner.bl_rna.properties[key].enum_items]
    if value not in options:
        raise RuntimeError(f'{key}: {value} unsupported; available: {options}')
    setattr(owner, key, value)


def power_two(count):
    return 1 << max(0, (count - 1).bit_length())


def rest_matrix(piece):
    values = piece['rest_matrix']
    return Matrix([values[i:i+4] for i in range(0, 16, 4)])


def prepare_mesh(scene, pieces, anchor, width, height):
    """Merge static render triangles; split export vertices only at attribute seams."""
    anchor_inv = anchor.inverted()
    uv_names = [uv.name for uv in pieces[0].data.uv_layers]
    if not uv_names or len(uv_names) > 5 or any(name in RESERVED_UV for name in uv_names):
        raise RuntimeError('Need 1–5 original UV layers, without reserved VAT layer names.')
    positions=[];normals=[];pivots=[];piece_ids=[];surface_mask=[];texcoords={name:[] for name in uv_names}
    faces=[];material_ids=[];materials=[];ranges=[];triangle_surface=[]
    for piece in pieces:
        if piece.data.shape_keys or any(m.show_viewport for m in piece.modifiers):
            raise RuntimeError(f'{piece.name}: rigid VAT requires a fixed render mesh without deformation modifiers.')
        mesh=piece.data
        if [u.name for u in mesh.uv_layers] != uv_names:
            raise RuntimeError('All pieces must have the same original UV layers.')
        if any(len(p.vertices)!=3 for p in mesh.polygons):
            raise RuntimeError('Triangulate render pieces before exporting rigid VAT.')
        local=anchor_inv @ rest_matrix(piece)
        normal_matrix=local.to_3x3().inverted().transposed()
        pivot=local.translation.copy();pid=int(piece['piece_id']);begin=len(positions)
        slots=[]
        for material in mesh.materials:
            if material is None:raise RuntimeError('Empty material slots are not supported.')
            if material not in materials:materials.append(material)
            slots.append(materials.index(material))
        lookup={};corner_normals=mesh.corner_normals
        for polygon in mesh.polygons:
            material=mesh.materials[polygon.material_index]
            interior=bool(material.get('sf_fracture_interior') or material.name=='SF_StoneInterior')
            face=[]
            for loop_index in polygon.loop_indices:
                vertex_index=mesh.loops[loop_index].vertex_index
                normal=(normal_matrix @ corner_normals[loop_index].vector).normalized()
                uvs=[mesh.uv_layers[name].data[loop_index].uv.copy() for name in uv_names]
                # Exact corner attributes, with original vertex identity: no surface weld.
                key=(vertex_index, tuple(normal), tuple(tuple(uv) for uv in uvs), interior)
                if key not in lookup:
                    lookup[key]=len(positions)
                    positions.append(tuple(local @ mesh.vertices[vertex_index].co))
                    normals.append(tuple(normal));pivots.append(tuple(pivot));piece_ids.append(pid);surface_mask.append(int(interior))
                    for name,uv in zip(uv_names,uvs):texcoords[name].append(tuple(uv))
                face.append(lookup[key])
            faces.append(face);material_ids.append(slots[polygon.material_index]);triangle_surface.append(int(interior))
        ranges.append({'id':pid,'source':piece.name,'vertex_start':begin,'vertex_count':len(positions)-begin,
                       'pivot_rest':list(pivot),'rest_local_row_major':[float(x) for row in local for x in row],
                       'release_frame':int(piece.get('release_frame',scene.frame_start))})
    exterior=[m for m in materials if not (m.get('sf_fracture_interior') or m.name=='SF_StoneInterior')]
    interior=[m for m in materials if m.get('sf_fracture_interior') or m.name=='SF_StoneInterior']
    if len(exterior)!=1 or len(interior)!=1:
        raise RuntimeError('Single-material VAT needs one original surface material and one fracture interior material.')
    source_materials=[m.name for m in materials]
    combined=combined_material(exterior[0],interior[0])
    data={'positions':np.asarray(positions,dtype=np.float64), 'normals':np.asarray(normals,dtype=np.float64),
          'pivots':np.asarray(pivots,dtype=np.float64),'piece_ids':np.asarray(piece_ids,dtype=np.int32),
          'triangles':np.asarray(faces,dtype=np.int32),'material_ids':[0]*len(faces),
          'surface_mask':np.asarray(surface_mask,dtype=np.uint8),'triangle_surface':triangle_surface,
          'source_materials':source_materials,
          'uv':{name:np.asarray(values,dtype=np.float64) for name,values in texcoords.items()},
          'materials':[combined],'pieces':ranges}
    count=len(positions)
    lookup_uv=np.column_stack(((data['piece_ids']+.5)/width,np.full(count,(height-.5)/height)))
    data['uv']['VAT_UV']=lookup_uv
    data['uv']['VAT_PIVOT_XY']=data['pivots'][:,:2].copy()
    data['uv']['VAT_PIVOT_Z_ID']=np.column_stack((data['pivots'][:,2],data['piece_ids']))
    return data


def combined_material(surface, interior):
    """One Blender preview material, with the same binary surface selector as Unity."""
    name=surface.name+'_VAT';old=bpy.data.materials.get(name)
    if old is not None and not old.get('sf_vat_owned'):
        raise RuntimeError('The generated VAT material name is occupied by a user material.')
    material=surface.copy();material.name=name;material['sf_vat_owned']=True
    material['sf_vat_surface']=True
    material.use_nodes=True;nodes=material.node_tree.nodes;links=material.node_tree.links
    output=next((n for n in nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output),None)
    if output is None or not output.inputs['Surface'].is_linked:
        raise RuntimeError('The original material needs an active surface shader for the VAT preview.')
    exterior=output.inputs['Surface'].links[0].from_socket
    original=next(n for n in interior.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    stone=nodes.new('ShaderNodeBsdfPrincipled')
    for input_name in ('Base Color','Metallic','Roughness'):
        stone.inputs[input_name].default_value=original.inputs[input_name].default_value
    color=nodes.new('ShaderNodeVertexColor');color.layer_name=SURFACE_COLOR
    separate=nodes.new('ShaderNodeSeparateRGB');links.new(color.outputs['Color'],separate.inputs[0])
    mix=nodes.new('ShaderNodeMixShader');links.new(separate.outputs['R'],mix.inputs[0])
    links.new(exterior,mix.inputs[1]);links.new(stone.outputs['BSDF'],mix.inputs[2])
    links.new(mix.outputs[0],output.inputs['Surface'])
    # Refresh owned previews without accumulating .001/.002 materials each bake.
    if old is not None:
        old.user_remap(material);bpy.data.materials.remove(old)
    material.name=name
    return material


def capture_motion(scene, pieces, anchor, start, end):
    anchor_inv=anchor.inverted();positions=[];rotations=[];matrices=[];previous=[None]*len(pieces)
    rest=[rest_matrix(piece) for piece in pieces]
    for frame in range(start,end+1):
        scene.frame_set(frame);dg=bpy.context.evaluated_depsgraph_get();row_pos=[];row_rot=[];row_mat=[]
        for index,piece in enumerate(pieces):
            current=piece.evaluated_get(dg).matrix_world.copy()
            local=anchor_inv @ current
            delta=local @ (anchor_inv @ rest[index]).inverted()
            linear=np.asarray(delta.to_3x3(),dtype=np.float64)
            if not np.isfinite(linear).all() or np.max(np.abs(linear.T@linear-np.eye(3)))>2e-4 or np.linalg.det(linear)<.999:
                raise RuntimeError(f'{piece.name}, frame {frame}: varying scale/shear is unsupported by rigid VAT.')
            rotation=delta.to_quaternion().normalized()
            # q and -q are equivalent, but adjacent texture rows must share a hemisphere.
            if previous[index] is not None and rotation.dot(previous[index])<0:rotation.negate()
            elif previous[index] is None and rotation.w<0:rotation.negate()
            previous[index]=rotation.copy()
            row_pos.append(tuple(local.translation));row_rot.append((rotation.x,rotation.y,rotation.z,rotation.w))
            row_mat.append(np.asarray(local,dtype=np.float64))
        positions.append(row_pos);rotations.append(row_rot);matrices.append(row_mat)
    positions=np.asarray(positions,dtype=np.float64);rotations=np.asarray(rotations,dtype=np.float64)
    rotations/=np.linalg.norm(rotations,axis=2,keepdims=True)
    if not np.isfinite(positions).all() or not np.isfinite(rotations).all():
        raise RuntimeError('Nonfinite animation samples.')
    return positions,rotations,np.asarray(matrices,dtype=np.float64)


def build_bind_object(scene, data, anchor, name):
    collection_name=f'SF_VAT_Output_{scene.name}'
    collection=bpy.data.collections.get(collection_name)
    if collection is not None and not collection.get('statue_fracture_owned'):
        raise RuntimeError('VAT output collection belongs to another tool.')
    if collection is None:
        collection=bpy.data.collections.new(collection_name);collection['statue_fracture_owned']=True
        scene.collection.children.link(collection)
    for old in list(collection.objects):
        if not old.get('sf_vat_owned'):raise RuntimeError('VAT output collection contains a user object.')
        mesh=old.data;bpy.data.objects.remove(old,do_unlink=True)
        if mesh.users==0:bpy.data.meshes.remove(mesh)
    mesh=bpy.data.meshes.new(name+'_BindMesh');mesh.from_pydata(data['positions'].tolist(),[],data['triangles'].tolist());mesh.update()
    colors=mesh.color_attributes.new(name=SURFACE_COLOR,type='BYTE_COLOR',domain='POINT')
    for i,mask in enumerate(data['surface_mask']):colors.data[i].color=(float(mask),0,0,1)
    mesh.color_attributes.active_color_index=0;mesh.color_attributes.render_color_index=0
    for material in data['materials']:mesh.materials.append(material)
    for p,mat in zip(mesh.polygons,data['material_ids']):p.material_index=mat;p.use_smooth=True
    for uv_name in data['uv']:mesh.uv_layers.new(name=uv_name)
    for key,kind,domain in [('custom_normal','FLOAT_VECTOR','CORNER'),('sf_rest_normal','FLOAT_VECTOR','CORNER'),
                            ('sf_vat_lookup','FLOAT_VECTOR','POINT'),('sf_vat_pivot','FLOAT_VECTOR','POINT'),('sf_piece_id','INT','POINT')]:
        mesh.attributes.new(key,kind,domain)
    # Acquire handles after allocating every CustomData layer.
    for uv_name,values in data['uv'].items():
        layer=mesh.uv_layers[uv_name]
        for loop in mesh.loops:layer.data[loop.index].uv=values[loop.vertex_index]
    for key in ('custom_normal','sf_rest_normal'):
        attribute=mesh.attributes[key]
        for loop in mesh.loops:attribute.data[loop.index].vector=data['normals'][loop.vertex_index]
    attribute=mesh.attributes['sf_vat_lookup']
    for i,uv in enumerate(data['uv']['VAT_UV']):attribute.data[i].vector=(uv[0],uv[1],0)
    attribute=mesh.attributes['sf_vat_pivot']
    for i,pivot in enumerate(data['pivots']):attribute.data[i].vector=pivot
    attribute=mesh.attributes['sf_piece_id']
    for i,pid in enumerate(data['piece_ids']):attribute.data[i].value=int(pid)
    mesh.uv_layers.active_index=0;mesh.update();mesh.calc_tangents(uvmap=next(iter(data['uv'])))
    tangents=np.zeros((len(data['positions']),4),dtype=np.float64)
    for loop in mesh.loops:tangents[loop.vertex_index]=(*loop.tangent,loop.bitangent_sign)
    data['tangents']=tangents;mesh.free_tangents()
    obj=bpy.data.objects.new(name+'_BindPose',mesh);obj['statue_fracture_owned']=True;obj['sf_vat_owned']=True
    obj.matrix_world=anchor;collection.objects.link(obj)
    obj.hide_render=True;obj.hide_set(True)
    return obj


def write_texture(path, pixels, precision):
    """Write raw RGBA data with independent alpha, then reload the actual EXR."""
    height,width,_=pixels.shape
    image=bpy.data.images.new(path.stem,width=width,height=height,alpha=True,float_buffer=True)
    enum_set(image,'alpha_mode','CHANNEL_PACKED');image.colorspace_settings.name='Non-Color'
    image.use_half_precision=False;image.pixels.foreach_set(np.asarray(pixels,dtype=np.float32).ravel())
    settings_scene=bpy.data.scenes.new('SF_VAT_LinearWrite')
    try:
        fmt=settings_scene.render.image_settings
        enum_set(fmt,'file_format','OPEN_EXR');enum_set(fmt,'color_mode','RGBA');enum_set(fmt,'color_depth',precision)
        enum_set(fmt,'exr_codec','ZIP');settings_scene.render.dither_intensity=0
        # EXR saves scene-linear values. Dynamic OCIO view enums are not exposed
        # by RNA's static enum_items; a view transform is unnecessary here.
        image.save_render(str(path),scene=settings_scene)
    finally:
        bpy.data.scenes.remove(settings_scene);bpy.data.images.remove(image)
    loaded=bpy.data.images.load(str(path),check_existing=False)
    loaded.use_half_precision=False;loaded.colorspace_settings.name='Non-Color';enum_set(loaded,'alpha_mode','CHANNEL_PACKED')
    result=np.empty(width*height*4,dtype=np.float32);loaded.pixels.foreach_get(result)
    loaded.pack()
    loaded['sf_vat_owned']=True
    return loaded,result.reshape(height,width,4)


def rotate_vectors(q, vectors):
    # XYZW quaternion, broadcastable over vertices.
    t=2*np.cross(q[...,:3],vectors)
    return vectors+q[...,3,None]*t+np.cross(q[...,:3],t)


def texture_poses(pos_texture, rot_texture, metadata, frame_offset):
    count=metadata['piece_count'];height=metadata['texture']['height']
    t=np.clip(float(frame_offset),0,metadata['frame_count']-1);lo=int(math.floor(t));hi=min(lo+1,metadata['frame_count']-1);fraction=t-lo
    a=pos_texture[height-1-lo,:count,:3];b=pos_texture[height-1-hi,:count,:3]
    positions=(a*(1-fraction)+b*fraction)*np.asarray(metadata['position_max_minus_min'])+np.asarray(metadata['position_min'])
    a=rot_texture[height-1-lo,:count].astype(np.float64)*2-1
    b=rot_texture[height-1-hi,:count].astype(np.float64)*2-1
    # Defensive shortest-path interpolation, including after EXR quantization.
    b=np.where(np.sum(a*b,axis=1,keepdims=True)<0,-b,b)
    rotation=a*(1-fraction)+b*fraction;rotation/=np.linalg.norm(rotation,axis=1,keepdims=True)
    return positions,rotation


def validate_textures(data, positions, rotations, matrices, pos_texture, rot_texture, metadata):
    ids=data['piece_ids'];maximum_position=maximum_normal=maximum_tangent=maximum_rotation=0.0
    lower=np.full(3,np.inf);upper=np.full(3,-np.inf);max_rest_error=0.0
    # Recover each vertex's original piece-local position for independent matrix comparison.
    original_local=np.empty_like(data['positions'])
    for piece in data['pieces']:
        mask=ids==piece['id'];rest=np.asarray(piece['rest_local_row_major']).reshape(4,4)
        original_local[mask]=(data['positions'][mask]-rest[:3,3])@np.linalg.inv(rest[:3,:3]).T
    for frame in range(metadata['frame_count']):
        pivots,q=texture_poses(pos_texture,rot_texture,metadata,frame)
        decoded=pivots[ids]+rotate_vectors(q[ids],data['positions']-data['pivots'])
        expected=np.empty_like(decoded)
        for piece in data['pieces']:
            mask=ids==piece['id'];current=matrices[frame,piece['id']]
            expected[mask]=original_local[mask]@current[:3,:3].T+current[:3,3]
        maximum_position=max(maximum_position,float(np.linalg.norm(decoded-expected,axis=1).max()))
        if frame==0:max_rest_error=float(np.linalg.norm(expected-data['positions'],axis=1).max())
        lower=np.minimum(lower,expected.min(axis=0));upper=np.maximum(upper,expected.max(axis=0))
        decoded_normal=rotate_vectors(q[ids],data['normals']);expected_normal=rotate_vectors(rotations[frame,ids],data['normals'])
        maximum_normal=max(maximum_normal,float(np.linalg.norm(decoded_normal-expected_normal,axis=1).max()))
        decoded_tangent=rotate_vectors(q[ids],data['tangents'][:,:3]);expected_tangent=rotate_vectors(rotations[frame,ids],data['tangents'][:,:3])
        maximum_tangent=max(maximum_tangent,float(np.linalg.norm(decoded_tangent-expected_tangent,axis=1).max()))
        # q/-q are identical rotations; avoid arccos float precision near one.
        dot=np.clip(np.abs(np.sum(q*rotations[frame],axis=1)),0,1)
        maximum_rotation=max(maximum_rotation,float(np.degrees(2*np.arccos(dot)).max()))
    tolerance=(.003 if metadata['texture']['component_bits']==16 else 2e-5)*max(1,float(np.linalg.norm(upper-lower)))
    if maximum_position>tolerance or maximum_rotation>(.2 if metadata['texture']['component_bits']==16 else .01):
        raise RuntimeError(f'Saved texture precision check failed: position {maximum_position}, rotation {maximum_rotation} degrees.')
    if max_rest_error>1e-5:
        raise RuntimeError('VAT start frame must be an intact rest frame; choose a frame before the collapse starts.')
    # Test the interpolation path at representative half frames, including the last frame clamp.
    for offset in (0.5,metadata['frame_count']*.33+.5,metadata['frame_count']-1.5,metadata['frame_count']+10):
        p,q=texture_poses(pos_texture,rot_texture,metadata,offset)
        if not np.isfinite(p).all() or not np.isfinite(q).all():raise RuntimeError('Invalid interpolated VAT pose.')
        if np.max(np.abs(np.linalg.norm(q,axis=1)-1))>1e-6:raise RuntimeError('Interpolated quaternion is not normalized.')
    return {'saved_exr_decoded':True,'frames_checked':metadata['frame_count'],'vertices_checked_per_frame':len(ids),
            'max_position_error':maximum_position,'max_rotation_error_degrees':maximum_rotation,
            'max_normal_vector_error':maximum_normal,'max_tangent_vector_error':maximum_tangent,
            'rest_pose_error':max_rest_error,'bounds_min':lower.tolist(),'bounds_max':upper.tolist(),
            'half_frame_interpolation_finite':True,'last_frame_clamped':True}


def make_decoder(position_image, rotation_image, metadata):
    group=bpy.data.node_groups.new('SF_RigidVAT_Decoder','GeometryNodeTree');group['sf_vat_owned']=True
    group.interface.new_socket(name='Geometry',in_out='INPUT',socket_type='NodeSocketGeometry')
    group.interface.new_socket(name='Geometry',in_out='OUTPUT',socket_type='NodeSocketGeometry')
    nodes=group.nodes;links=group.links
    def node(kind,label=''):
        n=nodes.new(kind);n.label=label;return n
    def connect(value,socket):
        if isinstance(value,bpy.types.NodeSocket):links.new(value,socket)
        else:socket.default_value=value
    def scalar(operation,a,b=0):
        n=node('ShaderNodeMath');enum_set(n,'operation',operation);connect(a,n.inputs[0]);connect(b,n.inputs[1]);return n.outputs[0]
    def vector(operation,a,b=(0,0,0)):
        n=node('ShaderNodeVectorMath');enum_set(n,'operation',operation);connect(a,n.inputs[0]);connect(b,n.inputs[1]);return n.outputs['Vector']
    def scale(a,factor):
        n=node('ShaderNodeVectorMath');enum_set(n,'operation','SCALE');connect(a,n.inputs[0]);connect(factor,n.inputs['Scale']);return n.outputs['Vector']
    def attr(name):
        n=node('GeometryNodeInputNamedAttribute',name);enum_set(n,'data_type','FLOAT_VECTOR');n.inputs['Name'].default_value=name;return n.outputs['Attribute']
    def uv(offset):
        combine=node('ShaderNodeCombineXYZ');connect(scalar('DIVIDE',offset,-metadata['texture']['height']),combine.inputs['Y'])
        return vector('ADD',attr('sf_vat_lookup'),combine.outputs['Vector'])
    def sample(image,coordinate):
        n=node('GeometryNodeImageTexture',image.name);enum_set(n,'interpolation','Closest');enum_set(n,'extension','EXTEND')
        n.inputs['Image'].default_value=image;connect(coordinate,n.inputs['Vector']);return n.outputs['Color'],n.outputs['Alpha']
    def mix_vector(a,b,f):return vector('ADD',scale(a,scalar('SUBTRACT',1,f)),scale(b,f))
    incoming=node('NodeGroupInput');outgoing=node('NodeGroupOutput');time=node('GeometryNodeInputSceneTime')
    offset=scalar('SUBTRACT',time.outputs['Frame'],metadata['frame_start'])
    offset=scalar('MINIMUM',scalar('MAXIMUM',offset,0),metadata['frame_count']-1)
    lo=scalar('FLOOR',offset);hi=scalar('MINIMUM',scalar('ADD',lo,1),metadata['frame_count']-1);fraction=scalar('SUBTRACT',offset,lo)
    uv0,uv1=uv(lo),uv(hi)
    p0,_=sample(position_image,uv0);p1,_=sample(position_image,uv1)
    pivot=vector('ADD',vector('MULTIPLY',mix_vector(p0,p1,fraction),tuple(metadata['position_max_minus_min'])),tuple(metadata['position_min']))
    q0,w0=sample(rotation_image,uv0);q1,w1=sample(rotation_image,uv1)
    xyz0=scale(vector('SUBTRACT',q0,(.5,.5,.5)),2)
    xyz1=scale(vector('SUBTRACT',q1,(.5,.5,.5)),2)
    w0=scalar('SUBTRACT',scalar('MULTIPLY',w0,2),1)
    w1=scalar('SUBTRACT',scalar('MULTIPLY',w1,2),1)
    dot=node('ShaderNodeVectorMath');enum_set(dot,'operation','DOT_PRODUCT');connect(xyz0,dot.inputs[0]);connect(xyz1,dot.inputs[1])
    negative=scalar('LESS_THAN',scalar('ADD',dot.outputs['Value'],scalar('MULTIPLY',w0,w1)),0)
    sign=scalar('SUBTRACT',1,scalar('MULTIPLY',2,negative))
    xyz=mix_vector(xyz0,scale(xyz1,sign),fraction)
    w=scalar('ADD',scalar('MULTIPLY',w0,scalar('SUBTRACT',1,fraction)),scalar('MULTIPLY',scalar('MULTIPLY',w1,sign),fraction))
    separate=node('ShaderNodeSeparateXYZ');connect(xyz,separate.inputs[0])
    rotation=node('FunctionNodeQuaternionToRotation','Normalized interpolated rigid rotation');connect(w,rotation.inputs['W'])
    for key in ('X','Y','Z'):connect(separate.outputs[key],rotation.inputs[key])
    def rotated(value):
        n=node('FunctionNodeRotateVector');connect(value,n.inputs['Vector']);connect(rotation.outputs['Rotation'],n.inputs['Rotation']);return n.outputs['Vector']
    position=node('GeometryNodeInputPosition');rest_offset=vector('SUBTRACT',position.outputs['Position'],attr('sf_vat_pivot'))
    deformed=vector('ADD',pivot,rotated(rest_offset))
    set_position=node('GeometryNodeSetPosition');links.new(incoming.outputs['Geometry'],set_position.inputs['Geometry']);connect(deformed,set_position.inputs['Position'])
    normal=node('GeometryNodeSetMeshNormal');enum_set(normal,'mode','FREE');enum_set(normal,'domain','CORNER')
    links.new(set_position.outputs['Geometry'],normal.inputs['Mesh']);connect(rotated(attr('sf_rest_normal')),normal.inputs['Custom Normal'])
    links.new(normal.outputs['Mesh'],outgoing.inputs['Geometry'])
    # Arrange the generated graph for inspection without loading external node groups.
    for i,n in enumerate(nodes):n.location=((i%9)*220,-(i//9)*180)
    return group


def create_preview(source_scene, bind, images, metadata, anchor):
    name=source_scene.name.replace('_Fracture_Preview','')+'_VAT_Preview'
    old=bpy.data.scenes.get(name)
    if old is not None:
        if not old.get('sf_vat_owned'):raise RuntimeError(f'{name} is not a tool-owned preview scene.')
        if bpy.context.window.scene==old:bpy.context.window.scene=source_scene
        objects=list(old.objects);groups=[m.node_group for o in objects for m in o.modifiers if m.type=='NODES' and m.node_group and m.node_group.get('sf_vat_owned')]
        bpy.data.scenes.remove(old)
        for obj in objects:
            if obj.users==0:
                mesh=obj.data if obj.type=='MESH' else None
                bpy.data.objects.remove(obj)
                if mesh and mesh.users==0:bpy.data.meshes.remove(mesh)
        for group in groups:
            if group.users==0:
                old_images=[n.inputs['Image'].default_value for n in group.nodes if n.bl_idname=='GeometryNodeImageTexture' and n.inputs['Image'].default_value]
                bpy.data.node_groups.remove(group)
                for image in set(old_images):
                    if image.users==0 and image.get('sf_vat_owned'):bpy.data.images.remove(image)
    scene=bpy.data.scenes.new(name);scene['sf_vat_owned']=True;scene['sf_vat_source_scene']=source_scene.name
    scene['sf_vat_metadata']=json.dumps(metadata);scene.world=source_scene.world
    scene.frame_start=metadata['frame_start'];scene.frame_end=metadata['frame_end'];scene.render.fps=source_scene.render.fps;scene.render.fps_base=source_scene.render.fps_base
    scene.render.resolution_x=source_scene.render.resolution_x;scene.render.resolution_y=source_scene.render.resolution_y
    scene.render.resolution_percentage=100;scene.render.engine=source_scene.render.engine
    for source in source_scene.objects:
        if source.type in {'CAMERA','LIGHT'} or source.get('sf_floor'):
            obj=bpy.data.objects.new(source.name+'_VATPreview',source.data);obj.matrix_world=source.matrix_world.copy();scene.collection.objects.link(obj)
            if source==source_scene.camera:scene.camera=obj
    obj=bpy.data.objects.new('SF_VAT_TexturePreview',bind.data);obj.matrix_world=anchor;scene.collection.objects.link(obj)
    obj['sf_vat_owned']=True;obj['sf_vat_preview']=True
    modifier=obj.modifiers.new('Rigid VAT — EXR texture decoder','NODES');modifier.node_group=make_decoder(*images,metadata)
    scene.frame_set(metadata['frame_start']);source_scene['sf_vat_preview_scene']=scene.name
    return scene,obj


def export_fbx(obj,path):
    selected=list(bpy.context.selected_objects);active=bpy.context.view_layer.objects.active;hidden=obj.hide_get()
    matrix=obj.matrix_world.copy()
    try:
        for item in selected:item.select_set(False)
        obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj
        # The file's mesh-local data and pivot UV values use the same reference space.
        obj.matrix_world=Matrix.Identity(4)
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
            use_mesh_modifiers=False,mesh_smooth_type='OFF',use_tspace=True,bake_anim=False,colors_type='LINEAR',
            add_leaf_bones=False,path_mode='STRIP',global_scale=1.0,apply_unit_scale=True,
            bake_space_transform=False,axis_forward='-Z',axis_up='Y')
    finally:
        obj.matrix_world=matrix;obj.select_set(False);obj.hide_set(hidden)
        for item in selected:
            if item.name in bpy.context.view_layer.objects and not item.hide_get():item.select_set(True)
        if active and active.name in bpy.context.view_layer.objects:bpy.context.view_layer.objects.active=active


def validate_fbx(path,data):
    """Inspect the actual binary payload, before any importer can change normals."""
    from io_scene_fbx import parse_fbx
    root,version=parse_fbx.parse(str(path))
    objects=next(e for e in root.elems if e.id==b'Objects')
    geometry=next(e for e in objects.elems if e.id==b'Geometry' and e.props[-1]==b'Mesh')
    def prop(parent,key):return next(e for e in parent.elems if e.id==key).props[0]
    vertices=np.asarray(prop(geometry,b'Vertices')).reshape(-1,3)
    polygon_indices=np.asarray(prop(geometry,b'PolygonVertexIndex'))
    indices=np.where(polygon_indices<0,-polygon_indices-1,polygon_indices)
    if vertices.shape!=data['positions'].shape or not np.array_equal(indices,data['triangles'].ravel()):
        raise RuntimeError('FBX changed vertex order or triangle connectivity.')
    pos_error=float(np.linalg.norm(vertices-data['positions'],axis=1).max())
    def corners(layer,value_key,index_key,components):
        values=np.asarray(prop(layer,value_key)).reshape(-1,components)
        reference=prop(layer,b'ReferenceInformationType');mapping=prop(layer,b'MappingInformationType')
        if reference==b'IndexToDirect':values=values[np.asarray(prop(layer,index_key),dtype=np.int32)]
        elif reference!=b'Direct':raise RuntimeError('Unsupported FBX attribute indexing.')
        if mapping in {b'ByVertice',b'ByControlPoint'}:values=values[indices]
        elif mapping!=b'ByPolygonVertex':raise RuntimeError('Unsupported FBX attribute domain.')
        return values
    layer=next(e for e in geometry.elems if e.id==b'LayerElementNormal')
    normal_error=float(np.linalg.norm(corners(layer,b'Normals',b'NormalsIndex',3)-data['normals'][indices],axis=1).max())
    uv_errors={}
    for layer in (e for e in geometry.elems if e.id==b'LayerElementUV'):
        name=prop(layer,b'Name').decode('utf8')
        uv_errors[name]=float(np.max(np.abs(corners(layer,b'UV',b'UVIndex',2)-data['uv'][name][indices])))
    color_layer=next(e for e in geometry.elems if e.id==b'LayerElementColor')
    color_error=float(np.max(np.abs(corners(color_layer,b'Colors',b'ColorIndex',4)-
        np.column_stack((data['surface_mask'][indices],np.zeros(len(indices)),np.zeros(len(indices)),np.ones(len(indices)))))))
    material_layer=next(e for e in geometry.elems if e.id==b'LayerElementMaterial')
    if len(data['materials'])!=1 or any(prop(material_layer,b'Materials')) or color_error>1e-6:
        raise RuntimeError('FBX lost the binary surface marker or single material slot.')
    if any(len(set(data['surface_mask'][face]))!=1 for face in data['triangles']):
        raise RuntimeError('A triangle interpolates across exterior/interior surface types.')
    if set(uv_errors)!=set(data['uv']) or pos_error>1e-5 or normal_error>1e-5 or max(uv_errors.values())>1e-5:
        raise RuntimeError(f'FBX attribute preservation failed: {pos_error}, {normal_error}, {uv_errors}')
    return {'fbx_version':version,'fbx_vertex_order_and_connectivity_preserved':True,
            'fbx_material_slots':1,'fbx_surface_color_error':color_error,
            'surface_marker_constant_per_triangle':True,'exterior_triangles':data['triangle_surface'].count(0),
            'interior_triangles':data['triangle_surface'].count(1),
            'fbx_max_position_error':pos_error,'fbx_max_normal_error':normal_error,'fbx_uv_max_errors':uv_errors}


def validate_preview(scene,obj,data,pos_pixels,rot_pixels,metadata):
    saved_scene=bpy.context.window.scene;saved_frame=scene.frame_current
    results=[];count=metadata['frame_count'];start=metadata['frame_start']
    offsets=sorted(set([0,min(23,count-1),count*.32,count*.5+.5,count*.77,count-1,count+10]))
    try:
        bpy.context.window.scene=scene
        for offset in offsets:
            frame=start+offset;scene.frame_set(int(frame),subframe=frame-int(frame))
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
            try:
                coords=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',coords)
                if len(mesh.materials)!=1 or any(p.material_index for p in mesh.polygons):
                    raise RuntimeError('VAT preview changed the single material slot.')
                if any(abs(c.color[0]-int(mask))>1e-6 for c,mask in zip(mesh.color_attributes[SURFACE_COLOR].data,data['surface_mask'])):
                    raise RuntimeError('VAT preview changed the surface marker.')
                if len(mesh.vertices)!=len(data['positions']):raise RuntimeError('Decoder changed vertex count.')
                p,q=texture_poses(pos_pixels,rot_pixels,metadata,offset);ids=data['piece_ids']
                expected=p[ids]+rotate_vectors(q[ids],data['positions']-data['pivots'])
                position_error=float(np.linalg.norm(coords.reshape(-1,3)-expected,axis=1).max())
                expected_normal=rotate_vectors(q[ids],data['normals'])
                actual_normal=np.asarray([n.vector[:] for n in mesh.corner_normals])
                indices=np.asarray([loop.vertex_index for loop in mesh.loops])
                normal_error=float(np.linalg.norm(actual_normal-expected_normal[indices],axis=1).max())
                if position_error>2e-5 or normal_error>2e-5:
                    raise RuntimeError(f'Geometry Nodes decoder differs from EXR data: {position_error}, {normal_error}')
                results.append({'frame':frame,'max_position_error_vs_texture':position_error,'max_normal_error_vs_texture':normal_error})
            finally:evaluated.to_mesh_clear()
    finally:
        scene.frame_set(saved_frame);bpy.context.window.scene=saved_scene
    return {'geometry_nodes_texture_decode_checked':True,'preview_checks':results}


def export(scene,pieces,source,settings):
    if not pieces or [int(o['piece_id']) for o in pieces]!=list(range(len(pieces))):
        raise RuntimeError('Need stable consecutive piece IDs; regenerate fractures first.')
    world=scene.rigidbody_world
    if not world or not world.point_cache.is_baked:raise RuntimeError('Bake rigid-body simulation before exporting VAT.')
    start,end=(scene.frame_start,scene.frame_end) if settings.vat_scene_range else (settings.vat_start,settings.vat_end)
    if end<start or start<world.point_cache.frame_start or end>world.point_cache.frame_end:
        raise RuntimeError('VAT range must be inside the baked physics range.')
    width=power_two(len(pieces));height=power_two(end-start+1)
    if max(width,height)>8192:raise RuntimeError('VAT exceeds 8192 pixels; shorten the animation range.')
    anchor=source.matrix_world.copy()
    if anchor.determinant()<=0 or max(anchor.to_scale())-min(anchor.to_scale())>1e-5:
        raise RuntimeError('Apply mirrored/nonuniform source scale before rigid VAT export.')
    saved=scene.frame_current
    name=re.sub(r'[^A-Za-z0-9_-]+','_',settings.vat_name).strip('_') or 'DragonGargoyle'
    target=Path(bpy.path.abspath(settings.vat_directory))/name;target.parent.mkdir(parents=True,exist_ok=True)
    # Publish the manifest only after the entire staged package passes validation.
    # Failed bakes therefore leave an earlier successful export intact.
    output=Path(tempfile.mkdtemp(prefix=f'.{name}_bake_',dir=target.parent))
    try:
        data=prepare_mesh(scene,pieces,anchor,width,height)
        positions,rotations,matrices=capture_motion(scene,pieces,anchor,start,end)
        for piece in data['pieces']:
            rest=np.asarray(piece['rest_local_row_major']).reshape(4,4)
            if np.max(np.abs(matrices[0,piece['id']]-rest))>1e-5:
                raise RuntimeError('VAT start frame must be intact; choose a frame before the collapse starts.')
        minimum=positions.min(axis=(0,1));maximum=positions.max(axis=(0,1));span=maximum-minimum
        encoded=(positions-minimum)/np.where(span>1e-12,span,1)
        pos_pixels=np.zeros((height,width,4),dtype=np.float32);rot_pixels=np.zeros_like(pos_pixels)
        # Pad time rows with the final pose; columns outside piece_count stay unused.
        pos_pixels[:,:len(pieces),:3]=encoded[-1];pos_pixels[:,:len(pieces),3]=1
        rot_pixels[:,:len(pieces)]=rotations[-1]*.5+.5
        for frame in range(end-start+1):
            row=height-1-frame;pos_pixels[row,:len(pieces),:3]=encoded[frame]
            rot_pixels[row,:len(pieces)]=rotations[frame]*.5+.5
        metadata={'schema':SCHEMA,'reference_implementation':REFERENCE,'encoding':'rigid piece transforms',
            'frame_start':start,'frame_end':end,'frame_count':end-start+1,'fps':scene.render.fps/scene.render.fps_base,
            'piece_count':len(pieces),'vertex_count':len(data['positions']),'triangle_count':len(data['triangles']),
            'position_min':minimum.tolist(),'position_max':maximum.tolist(),'position_max_minus_min':span.tolist(),
            'texture':{'width':width,'height':height,'component_bits':int(settings.vat_precision),
                'format':'OpenEXR RGBA','linear':True,'alpha':'independent data channel','mipmaps':False,
                'filter':'point/nearest','wrap':'clamp','row_origin':'bottom-left','first_frame_row':height-1,
                'frame_row_formula':'height - 1 - (frame - frame_start)','piece_column_formula':'piece_id',
                'padding':'unused piece columns; remaining time rows repeat final pose'},
            'rotation':{'channels':'XYZW','encoding':'rgba = quaternion_xyzw * 0.5 + 0.5',
                'reference':'rotation delta from saved rest pose','signs':'adjacent frames same hemisphere',
                'interpolation':'shortest-path normalized linear interpolation'},
            'uv_channels':[{'name':key,'index':i} for i,key in enumerate(data['uv'])],
            'pivot':'VAT_PIVOT_XY.xy, VAT_PIVOT_Z_ID.x; all in bind mesh local coordinates',
            'lookup':'VAT_UV.xy is texel center of piece_id at first frame; subtract frame_offset/height from y',
            'decode':'P = pivot_current + rotate(q_delta, P_rest - pivot_rest); N/T = rotate(q_delta, N/T_rest)',
            'coordinates':{'space':'bind mesh local XYZ, Blender right-handed Z-up','unit':'Blender scene units',
                'unit_scale_metres':scene.unit_settings.scale_length,'anchor_world_row_major':[float(x) for row in anchor for x in row],
                'fbx_axis_forward':'-Z','fbx_axis_up':'Y','fbx_bake_space_transform':False,
                'note':'Keep mesh-local axes for decoding. If importer bakes an axis conversion, apply the same basis to pivots, positions, rotations, normals and tangents.'},
            'animations':{name:{'startFrame':start,'endFrame':end,'framerate':scene.render.fps/scene.render.fps_base,
                'looping':False,'end_behavior':'hold last frame','duration_seconds':(end-start)/(scene.render.fps/scene.render.fps_base)}},
            'pieces':data['pieces'],'materials':[m.name for m in data['materials']],
            'source_materials':data['source_materials'],
            'surface_mask':{'attribute':SURFACE_COLOR,'semantic':'COLOR.r','exterior':0,'interior':1,
                'storage':'linear vertex color RGBA; R=surface type, G/B=0, A=1',
                'constant_per_triangle':True,'material_slots':1},
            'files':{'mesh':f'{name}.fbx','position':f'{name}_position.exr','rotation':f'{name}_rotation.exr',
                'bind_mesh':f'{name}_bind_mesh.json','validation':f'{name}_validation.json'}}
        bind=build_bind_object(scene,data,anchor,name)
        pos_image,pos_disk=write_texture(output/metadata['files']['position'],pos_pixels,settings.vat_precision)
        rot_image,rot_disk=write_texture(output/metadata['files']['rotation'],rot_pixels,settings.vat_precision)
        validation=validate_textures(data,positions,rotations,matrices,pos_disk,rot_disk,metadata)
        metadata['animation_bounds']={'min':validation['bounds_min'],'max':validation['bounds_max'],
                                      'padding':max(.003,validation['max_position_error']*2)}
        mesh_data={'schema':'statue-rigid-vat-bind-mesh-v1','coordinate_space':metadata['coordinates']['space'],
            'positions':data['positions'].tolist(),'normals':data['normals'].tolist(),'tangents_xyzw':data['tangents'].tolist(),
            'triangles':data['triangles'].tolist(),'triangle_materials':data['material_ids'],
            'surface_mask':data['surface_mask'].tolist(),'triangle_surface':data['triangle_surface'],
            'uv_channels':{key:values.tolist() for key,values in data['uv'].items()},'piece_ids':data['piece_ids'].tolist()}
        (output/metadata['files']['bind_mesh']).write_text(json.dumps(mesh_data,separators=(',',':')))
        export_fbx(bind,output/metadata['files']['mesh'])
        validation.update(validate_fbx(output/metadata['files']['mesh'],data))
        preview,preview_object=create_preview(scene,bind,(pos_image,rot_image),metadata,anchor)
        validation.update(validate_preview(preview,preview_object,data,pos_disk,rot_disk,metadata))
        validation['preview_has_no_rigid_world']=preview.rigidbody_world is None
        validation['source_cache_still_baked']=world.point_cache.is_baked
        validation['texture_bytes_uncompressed']=width*height*4*(int(settings.vat_precision)//8)*2
        validation['file_sizes_bytes']={key:(output/file).stat().st_size for key,file in metadata['files'].items() if (output/file).exists()}
        (output/metadata['files']['validation']).write_text(json.dumps(validation,indent=2))
        (output/f'{name}_vat.json').write_text(json.dumps(metadata,indent=2))
        target.mkdir(parents=True,exist_ok=True)
        manifest=output/f'{name}_vat.json'
        for file in output.iterdir():
            if file!=manifest:file.replace(target/file.name)
        manifest.replace(target/manifest.name)
        for image,key in ((pos_image,'position'),(rot_image,'rotation')):
            # PackedFile remembers its original source path separately. Repack
            # from the published file so saving/reopening never refers to the
            # deleted staging directory. Keep paths portable with the .blend.
            if image.packed_file:image.unpack(method='REMOVE')
            path=str((target/metadata['files'][key]).resolve())
            image.filepath=bpy.path.relpath(path) if bpy.data.filepath else path
            image.pack()
        text=bpy.data.texts.get('Statue_VAT_Manifest.json') or bpy.data.texts.new('Statue_VAT_Manifest.json')
        text.clear();text.write(json.dumps(metadata,indent=2));scene['sf_vat_output']=str(target)
        for material in list(bpy.data.materials):
            if material.get('sf_vat_surface') and material.users==0:bpy.data.materials.remove(material)
        scene.statue_fracture.status=f'VAT exported: {len(pieces)} pieces, {width} x {height}, EXR{settings.vat_precision}'
        return {'output':str(target),'preview_scene':preview.name,'preview_object':preview_object.name,
                'width':width,'height':height,'validation':validation}
    finally:
        scene.frame_set(saved)
        shutil.rmtree(output)
