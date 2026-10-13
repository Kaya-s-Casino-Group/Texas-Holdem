"""Statue Fracture: deterministic Voronoi solids + Blender rigid-body collapse.
Install the statue_fracture addon. Sidebar: 3D View > Destruction.
No third-party addon required. Work only on owned copies/collections.
"""
import bpy
import bmesh
import json
import math
import random
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from bpy.props import BoolProperty, EnumProperty, IntProperty, FloatProperty, StringProperty, PointerProperty

PREFIX = 'SF_'
_BUILD = None


def enum_set(owner, key, value):
    valid = [i.identifier for i in owner.bl_rna.properties[key].enum_items]
    if value not in valid:
        raise RuntimeError(f'{key}: {value} is not supported; available: {valid}')
    setattr(owner, key, value)


def activate(obj):
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def owned_collection(scene, name):
    coll = bpy.data.collections.get(name)
    if coll and not coll.get('statue_fracture_owned'):
        raise RuntimeError(f'Collection {name} is not owned by this tool.')
    if coll is None:
        coll = bpy.data.collections.new(name)
        coll['statue_fracture_owned'] = True
        scene.collection.children.link(coll)
    return coll


def topology(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    result = {'vertices_welded': len(bm.verts), 'boundary_edges': sum(e.is_boundary for e in bm.edges),
              'nonmanifold_edges': sum(not e.is_manifold for e in bm.edges)}
    bm.free()
    return result


def apply_modifier(obj, mod):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def original_surface(source):
    mesh = source.data
    mesh.calc_loop_triangles()
    verts = [v.co.copy() for v in mesh.vertices]
    triangles = [tuple(t.vertices) for t in mesh.loop_triangles]
    uv = mesh.uv_layers.active
    texcoords = [[uv.data[i].uv.copy() for i in t.loops] for t in mesh.loop_triangles] if uv else None
    return verts, triangles, texcoords, BVHTree.FromPolygons(verts, triangles, all_triangles=True)


def transfer_uv(mesh, original):
    verts, triangles, texcoords, tree = original
    if not texcoords:
        return
    layer = mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    from mathutils.geometry import barycentric_transform
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        hit, _, tri, _ = tree.find_nearest(point)
        if hit is None:
            continue
        a, b, c = [verts[i] for i in triangles[tri]]
        ua, ub, uc = [Vector((u.x, u.y, 0)) for u in texcoords[tri]]
        uv = barycentric_transform(hit, a, b, c, ua, ub, uc)
        layer.data[loop.index].uv = (uv.x, uv.y)


def surface_snapshot(mesh):
    """Keep triangle-local attributes; nearest-surface transfer loses UV seams."""
    import numpy as np
    mesh.calc_loop_triangles()
    normals = [n.vector.copy() for n in mesh.corner_normals]
    triangles = []
    for tri in mesh.loop_triangles:
        poly = mesh.polygons[tri.polygon_index]
        triangles.append({'points':[mesh.vertices[i].co.copy() for i in tri.vertices],
                          'normals':[normals[i] for i in tri.loops],
                          'uv':{layer.name:[layer.data[i].uv.copy() for i in tri.loops]
                                for layer in mesh.uv_layers},
                          'smooth':poly.use_smooth, 'material':poly.material_index})
    return {'triangles':triangles,
            'array':np.asarray([[tuple(p) for p in t['points']] for t in triangles], dtype=np.float64),
            'uv_names':[layer.name for layer in mesh.uv_layers]}


def clip_points(polygon, origin, normal):
    """Sutherland–Hodgman in 3D, retaining barycentric weights when supplied."""
    if not polygon:
        return []
    result=[]
    prev=polygon[-1];dprev=(prev[0]-origin).dot(normal)
    for current in polygon:
        dcurr=(current[0]-origin).dot(normal)
        if (dcurr<=0)!=(dprev<=0):
            t=dprev/(dprev-dcurr)
            result.append((prev[0].lerp(current[0],t),prev[1].lerp(current[1],t)))
        if dcurr<=0:
            result.append(current)
        prev,dprev=current,dcurr
    # Plane intersections can land on an existing vertex. Do not emit zero edges.
    clean=[]
    for item in result:
        if not clean or (item[0]-clean[-1][0]).length_squared>1e-18:
            clean.append(item)
    if len(clean)>1 and (clean[0][0]-clean[-1][0]).length_squared<1e-18:
        clean.pop()
    return clean


def winding_inside(surface, point):
    """Generalized winding handles the source's intersecting shells and tiny holes.

    Used only to classify newly added interior faces; never rebuilds exterior.
    """
    import numpy as np
    vectors=surface['array']-np.asarray(point)[None,None,:]
    a,b,c=vectors[:,0],vectors[:,1],vectors[:,2]
    la,lb,lc=[np.linalg.norm(x,axis=1) for x in (a,b,c)]
    num=np.einsum('ij,ij->i',a,np.cross(b,c))
    den=(la*lb*lc + np.einsum('ij,ij->i',a,b)*lc +
         np.einsum('ij,ij->i',b,c)*la + np.einsum('ij,ij->i',c,a)*lb)
    return abs(float(np.sum(2*np.arctan2(num,den))/(4*math.pi)))>.5


def cell_planes(seeds, index):
    point=seeds[index]
    return [(j,(point+q)*.5,(q-point).normalized())
            for j,q in sorted(enumerate(seeds),key=lambda item:(item[1]-point).length_squared)
            if j!=index]


def shared_cut_faces(job, i, j):
    """Triangulate one actual source cross-section; both pieces share the result.

    CDT constraints are original triangle/plane intersections, not voxel edges.
    This also supports branch points in the nonmanifold wing membranes.
    """
    from mathutils.geometry import delaunay_2d_cdt
    key=tuple(sorted((i,j)))
    if key in job['cap_cache']:
        return job['cap_cache'][key]
    i,j=key;seeds=job['seeds'];origin=(seeds[i]+seeds[j])*.5
    normal=(seeds[j]-seeds[i]).normalized()
    axis=Vector((1,0,0)) if abs(normal.x)<.8 else Vector((0,1,0))
    u=normal.cross(axis).normalized();v=normal.cross(u).normalized()
    radius=(job['hi']-job['lo']).length*2
    rectangle=[origin+u*x+v*y for x,y in [(-radius,-radius),(radius,-radius),(radius,radius),(-radius,radius)]]
    constraints=[(o,n) for other,o,n in cell_planes(seeds,i) if other!=j]
    # Bound the infinite cell face by the source AABB as well.
    for k in range(3):
        direction=Vector();direction[k]=1
        high=job['hi'].copy();high[k]+=.001
        low=job['lo'].copy();low[k]-=.001
        constraints.extend([(high,direction),(low,-direction)])
    polygon=[(p,Vector()) for p in rectangle]
    for o,n in constraints:
        polygon=clip_points(polygon,o,n)
        if len(polygon)<3:
            job['cap_cache'][key]=[]
            return []
    coords=[];edges=[];lookup={}
    def add(p):
        projected=Vector(((p-origin).dot(u),(p-origin).dot(v)))
        quantized=tuple(round(x/1e-8) for x in projected)
        if quantized not in lookup:
            lookup[quantized]=len(coords);coords.append(projected)
        return lookup[quantized]
    boundary=[add(p) for p,_ in polygon]
    edges.extend((boundary[k-1],boundary[k]) for k in range(len(boundary)))
    for triangle in job['surface']['triangles']:
        points=triangle['points'];distances=[(p-origin).dot(normal) for p in points]
        if min(distances)>0 or max(distances)<0:
            continue
        hits=[]
        for k,p in enumerate(points):
            q=points[k-1];d=distances[k];e=distances[k-1]
            if abs(d)<1e-9:hits.append(p)
            if d*e<0:hits.append(q.lerp(p,e/(e-d)))
        distinct=[]
        for p in hits:
            if all((p-q).length_squared>1e-16 for q in distinct):distinct.append(p)
        if len(distinct)!=2:continue
        a,b=distinct
        for o,n in constraints:
            da=(a-o).dot(n);db=(b-o).dot(n)
            if da>0 and db>0:
                a=b;break
            if (da>0)!=(db>0):
                hit=a.lerp(b,da/(da-db))
                if da>0:a=hit
                else:b=hit
        if (a-b).length_squared>1e-16:
            x,y=add(a),add(b)
            if x!=y:edges.append((x,y))
    verts,_,faces,_,_,_=delaunay_2d_cdt(coords,list(set(edges)),[],0,1e-8,False)
    result=[]
    for face in faces:
        points=[origin+u*verts[k].x+v*verts[k].y for k in face]
        if len(points)!=3:continue
        center=sum(points,Vector())/3
        if any((center-o).dot(n)>2e-7 for o,n in constraints):continue
        if not winding_inside(job['surface'],center):continue
        if (points[1]-points[0]).cross(points[2]-points[0]).length<1e-12:continue
        if (points[1]-points[0]).cross(points[2]-points[0]).dot(normal)<0:points.reverse()
        result.append((points,normal))
    job['cap_cache'][key]=result
    return result


def original_surface_piece(job,index,proxy,center):
    """Subdivide only triangles crossed by cell boundaries. Preserve all UV layers
    and custom split normals through original-triangle barycentric interpolation.
    """
    surface=job['surface'];planes=cell_planes(job['seeds'],index)
    vertices=[];faces=[];records=[];lookup={}
    def add_face(points,triangle_id,weights,normal=None):
        ids=[]
        for p in points:
            key=tuple(round(x/1e-8) for x in p)
            if key not in lookup:lookup[key]=len(vertices);vertices.append(p-center)
            ids.append(lookup[key])
        if len(set(ids))<3:return
        faces.append(ids);records.append((triangle_id,weights,normal))
    for triangle_id,triangle in enumerate(surface['triangles']):
        polygon=[(p.copy(),Vector(tuple(float(k==j) for k in range(3))))
                 for j,p in enumerate(triangle['points'])]
        for _,o,n in planes:
            if not polygon:break
            distances=[(p-o).dot(n) for p,_ in polygon]
            if max(distances)<=0:continue
            if min(distances)>0:polygon=[];break
            polygon=clip_points(polygon,o,n)
        if len(polygon)<3:continue
        # A full source triangle remains one triangle. New cuts use a triangle fan.
        for k in range(1,len(polygon)-1):
            tri=[polygon[0],polygon[k],polygon[k+1]]
            if (tri[1][0]-tri[0][0]).cross(tri[2][0]-tri[0][0]).length<1e-12:continue
            add_face([p for p,_ in tri],triangle_id,[w for _,w in tri])
    interior_index=len(job['source'].data.materials)
    for j,_,_ in planes:
        for points,normal in shared_cut_faces(job,index,j):
            reverse=index>j
            add_face(list(reversed(points)) if reverse else points,-1,[Vector()]*3,-normal if reverse else normal)
    mesh=bpy.data.meshes.new(f'SF_Surface_{index:03d}')
    mesh.from_pydata(vertices,[],faces);mesh.update()
    for material in job['source'].data.materials:mesh.materials.append(material)
    mesh.materials.append(interior_material())
    # Blender can reallocate CustomData whenever a layer is added (also when
    # material/sharp flags first appear). Acquire handles only after allocation.
    for poly,(triangle_id,_,_) in zip(mesh.polygons,records):
        triangle=surface['triangles'][triangle_id] if triangle_id>=0 else None
        poly.material_index=triangle['material'] if triangle else interior_index
        poly.use_smooth=triangle['smooth'] if triangle else False
    for name in surface['uv_names']:mesh.uv_layers.new(name=name)
    mesh.attributes.new('sf_source_triangle','INT','FACE')
    mesh.attributes.new('sf_barycentric','FLOAT_VECTOR','CORNER')
    # Blender 4.5 free corner normals retain the full vector. The older
    # normals_split_custom_set encodes INT16 tangent-space angles and can
    # introduce visible errors on this asset's highly skewed face normals.
    mesh.attributes.new('custom_normal','FLOAT_VECTOR','CORNER')
    uv_layers={name:mesh.uv_layers[name] for name in surface['uv_names']}
    attribution=mesh.attributes['sf_source_triangle']
    barycentric=mesh.attributes['sf_barycentric']
    normals=mesh.attributes['custom_normal']
    for poly,(triangle_id,weights,cap_normal) in zip(mesh.polygons,records):
        attribution.data[poly.index].value=triangle_id
        triangle=surface['triangles'][triangle_id] if triangle_id>=0 else None
        for loop_index,weight in zip(poly.loop_indices,weights):
            barycentric.data[loop_index].vector=weight
            if triangle:
                normal=sum((n*w for n,w in zip(triangle['normals'],weight)),Vector()).normalized()
                for name,layer in uv_layers.items():
                    layer.data[loop_index].uv=sum((uv*w for uv,w in zip(triangle['uv'][name],weight)),Vector((0,0)))
            else:normal=cap_normal
            normals.data[loop_index].vector=normal
    mesh.update()
    obj=bpy.data.objects.new(f'SF_Piece_{index:03d}',mesh)
    job['collection'].objects.link(obj)
    obj.parent=proxy;obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
    return obj


def validate_surface(job):
    """Account for every original triangle and compare each corner to its source.

    Render surfaces may inherit the source's nonmanifold edges; watertightness is
    required of the collision proxies, not obtained by changing exterior faces.
    """
    triangles=job['surface']['triangles'];areas=[0.0]*len(triangles);counts=[0]*len(triangles)
    max_position=max_uv=max_normal=0.0;uncut=set();caps=0
    source_inverse=job['source'].matrix_world.inverted()
    for obj in job['pieces']:
        mesh=obj.data;ids=mesh.attributes['sf_source_triangle'];bary=mesh.attributes['sf_barycentric']
        rest=Matrix([obj['rest_matrix'][i:i+4] for i in range(0,16,4)])
        normals=mesh.corner_normals
        for poly in mesh.polygons:
            tid=ids.data[poly.index].value
            if tid<0:caps+=1;continue
            triangle=triangles[tid]
            points=[source_inverse@rest@mesh.vertices[mesh.loops[l].vertex_index].co for l in poly.loop_indices]
            areas[tid]+=(points[1]-points[0]).cross(points[2]-points[0]).length*.5
            counts[tid]+=1;whole=True
            for l,p in zip(poly.loop_indices,points):
                weights=Vector(bary.data[l].vector)
                expected=sum((v*w for v,w in zip(triangle['points'],weights)),Vector())
                max_position=max(max_position,(p-expected).length)
                expected_normal=sum((n*w for n,w in zip(triangle['normals'],weights)),Vector()).normalized()
                max_normal=max(max_normal,(normals[l].vector-expected_normal).length)
                for name,values in triangle['uv'].items():
                    expected_uv=sum((uv*w for uv,w in zip(values,weights)),Vector((0,0)))
                    max_uv=max(max_uv,(mesh.uv_layers[name].data[l].uv-expected_uv).length)
                if max(weights)<.999999:whole=False
            if whole:uncut.add(tid)
    original=[(t['points'][1]-t['points'][0]).cross(t['points'][2]-t['points'][0]).length*.5 for t in triangles]
    errors=[abs(a-b)/max(b,1e-12) for a,b in zip(areas,original)]
    result={'source_triangles':len(triangles),'uncut_original_triangles':len(uncut),
            'exterior_triangles':sum(counts),'interior_triangles':caps,
            'source_triangles_missing':sum(c==0 for c in counts),
            'max_triangle_area_relative_error':max(errors),
            'total_surface_area_relative_error':abs(sum(areas)-sum(original))/sum(original),
            'max_rest_position_error':max_position,'max_uv_error':max_uv,
            'max_corner_normal_vector_error':max_normal}
    if result['source_triangles_missing'] or max(errors)>1e-3 or max_position>1e-5 or max_uv>1e-5 or max_normal>2e-3:
        raise RuntimeError(f'Original surface preservation failed: {result}')
    job['scene']['sf_surface_validation']=json.dumps(result)
    return result


def fit_collision_hull(proxy,render_piece):
    """Enclose the actual preserved surface, including thin wing tips.

    Repaired-solid volume remains the mass estimate. Collision bounds use the
    original surface points so voxel shrinkage cannot make visible pieces sink.
    """
    bm=bmesh.new()
    for vertex in render_piece.data.vertices:bm.verts.new(vertex.co)
    bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    if not bm.faces or any(not e.is_manifold for e in bm.edges):
        bm.free();raise RuntimeError('Original-surface collision hull is not closed.')
    mesh=bpy.data.meshes.new(proxy.name+'_Hull');bm.to_mesh(mesh);bm.free();mesh.update()
    old=proxy.data;proxy.data=mesh
    if old.users==0:bpy.data.meshes.remove(old)


def interior_material():
    mat = bpy.data.materials.get('SF_StoneInterior') or bpy.data.materials.new('SF_StoneInterior')
    mat['sf_fracture_interior'] = True
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (.09, .105, .12, 1)
    bsdf.inputs['Roughness'].default_value = .92
    mat.diffuse_color = (.09, .105, .12, 1)
    return mat


def volume(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    result = abs(bm.calc_volume(signed=True))
    bm.free()
    return result


def inside(tree, p):
    direction = Vector((1, .173, .071)).normalized()
    ray = p.copy()
    hits = 0
    for _ in range(64):
        hit, _, _, _ = tree.ray_cast(ray, direction)
        if hit is None:
            break
        hits += 1
        ray = hit + direction * 1e-5
    return hits % 2 == 1


def generate_seeds(mesh, count, seed):
    mesh.calc_loop_triangles()
    tree = BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],
                               [tuple(t.vertices) for t in mesh.loop_triangles], all_triangles=True)
    lo = Vector(tuple(min(v.co[k] for v in mesh.vertices) for k in range(3)))
    hi = Vector(tuple(max(v.co[k] for v in mesh.vertices) for k in range(3)))
    rng = random.Random(seed)
    candidates = []
    for _ in range(7000):
        p = Vector(tuple(rng.uniform(lo[k], hi[k]) for k in range(3)))
        if inside(tree, p):
            candidates.append(p)
        if len(candidates) >= max(600, count * 18):
            break
    if len(candidates) < count:
        raise RuntimeError('Could not sample enough interior points; check the repaired solid.')
    # Farthest point sampling covers wings and head instead of concentrating in the torso.
    selected = [candidates.pop(rng.randrange(len(candidates)))]
    nearest = [(p - selected[0]).length_squared for p in candidates]
    while len(selected) < count:
        index = max(range(len(candidates)), key=lambda i: nearest[i])
        chosen = candidates.pop(index)
        nearest.pop(index)
        selected.append(chosen)
        nearest = [min(d, (p - chosen).length_squared) for p, d in zip(candidates, nearest)]
    return selected, lo, hi


def clear_collection(coll):
    for obj in list(coll.objects):
        mesh = obj.data if obj.type == 'MESH' else None
        bpy.data.objects.remove(obj, do_unlink=True)
        if mesh and mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def begin_fracture(source=None):
    global _BUILD
    scene = bpy.context.scene
    cfg = scene.statue_fracture
    source = source or bpy.context.active_object
    if source is None or source.get('statue_fracture_owned'):
        source = scene.objects.get(cfg.source_name)
    if source is None or source.type != 'MESH' or source.get('statue_fracture_owned'):
        raise RuntimeError('Select the original source mesh, not a generated piece.')
    if scene.rigidbody_world and scene.rigidbody_world.point_cache.is_baked:
        with bpy.context.temp_override(point_cache=scene.rigidbody_world.point_cache):
            bpy.ops.ptcache.free_bake()
    cfg.source_name = source.name
    fragments = owned_collection(scene, f'SF_Pieces_{scene.name}')
    work = owned_collection(scene, f'SF_Work_{scene.name}')
    proxies = owned_collection(scene, f'SF_Collision_{scene.name}')
    clear_collection(fragments)
    clear_collection(proxies)
    clear_collection(work)
    source.hide_set(False)
    source.hide_render = False
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = 'SF_RepairedSolid'
    obj['statue_fracture_owned'] = True
    work.objects.link(obj)
    original = original_surface(source)
    surface = surface_snapshot(source.data) if cfg.preserve_surface else None
    check_before = topology(obj.data)
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    if check_before['nonmanifold_edges']:
        mod = obj.modifiers.new('Repair Solid', 'REMESH')
        enum_set(mod, 'mode', 'VOXEL')
        mod.voxel_size = cfg.voxel_size
        mod.use_smooth_shade = True
        apply_modifier(obj, mod)
        # Reduce tessellation after making a closed volume. UV0 is reconstructed below.
        if len(obj.data.polygons) > 18000:
            dec = obj.modifiers.new('Preview Tessellation', 'DECIMATE')
            dec.ratio = max(.1, 18000 / len(obj.data.polygons))
            apply_modifier(obj, dec)
        if not cfg.preserve_surface:
            transfer_uv(obj.data, original)
    check_after = topology(obj.data)
    if check_after['nonmanifold_edges']:
        raise RuntimeError(f'Repair did not produce a closed solid: {check_after}')
    if not obj.data.materials:
        raise RuntimeError('Source has no exterior material.')
    while len(obj.data.materials) > 1:
        obj.data.materials.pop(index=len(obj.data.materials)-1)
    obj.data.materials.append(interior_material())
    for p in obj.data.polygons:
        p.material_index = 0
    seeds, lo, hi = generate_seeds(obj.data, cfg.piece_count, cfg.seed)
    _BUILD = {'scene':scene,'source':source,'solid':obj,'collection':fragments,'work':work,
              'seeds':seeds,'lo':lo,'hi':hi,'next':0,'pieces':[],
              'before':check_before,'after':check_after,'source_volume':volume(obj.data),
              'surface':surface,'cap_cache':{},'proxies':proxies}
    cfg.status = f'Closed solid ready; {cfg.piece_count} cells'
    print(json.dumps({'topology_before':check_before,'topology_after':check_after,
                      'repaired_polygons':len(obj.data.polygons),'seed_count':len(seeds)}))
    return _BUILD


def fracture_batch(batch_size=6):
    global _BUILD
    job = _BUILD
    if not job:
        raise RuntimeError('Generate a working solid first.')
    for index in range(job['next'], min(job['next']+batch_size,len(job['seeds']))):
        piece = job['solid'].copy()
        piece.data = job['solid'].data.copy()
        piece.name = f'SF_Piece_{index:03d}'
        job['collection'].objects.link(piece)
        # Clip the closed statue against each Voronoi halfspace and explicitly
        # cap every cut loop. This avoids Exact Boolean failures on thin wings.
        bm = bmesh.new()
        bm.from_mesh(piece.data)
        point = job['seeds'][index]
        neighbors = sorted((q for q in job['seeds'] if q != point),
                           key=lambda q: (q-point).length_squared)
        for other in neighbors:
            if not bm.faces:
                break
            result = bmesh.ops.bisect_plane(bm,
                geom=list(bm.verts)+list(bm.edges)+list(bm.faces), dist=1e-7,
                plane_co=(point+other)*.5, plane_no=(other-point).normalized(),
                clear_outer=True, clear_inner=False)
            cut = [edge for edge in result['geom_cut']
                   if isinstance(edge,bmesh.types.BMEdge) and edge.is_valid and edge.is_boundary]
            if cut:
                caps = bmesh.ops.holes_fill(bm,edges=cut,sides=0)
                for face in caps['faces']:
                    face.material_index=1
                    face.smooth=False
        # Remove loose remnants left when an entire disconnected component is cut away.
        loose = [vertex for vertex in bm.verts if not vertex.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        if any(not edge.is_manifold for edge in bm.edges):
            bm.free()
            raise RuntimeError(f'Cell {index} is not closed; inspect its cutting planes.')
        bm.to_mesh(piece.data)
        bm.free()
        piece.data.update()
        if not piece.data.polygons or volume(piece.data)<1e-7:
            data=piece.data;bpy.data.objects.remove(piece,do_unlink=True);bpy.data.meshes.remove(data)
            continue
        piece['statue_fracture_owned']=True
        piece['piece_id']=len(job['pieces'])
        piece['piece_volume']=volume(piece.data)
        # Keep world-space rest geometry identical when moving each origin to its pivot.
        center=sum((v.co for v in piece.data.vertices),Vector())/len(piece.data.vertices)
        piece.data.transform(Matrix.Translation(-center))
        piece.matrix_world = piece.matrix_world @ Matrix.Translation(center)
        piece['rest_matrix']=[float(x) for row in piece.matrix_world for x in row]
        if job['surface']:
            proxy=piece
            proxy.name=f'SF_Collision_{index:03d}'
            proxy['physics_piece_id']=proxy['piece_id'];del proxy['piece_id']
            job['collection'].objects.unlink(proxy);job['proxies'].objects.link(proxy)
            piece=original_surface_piece(job,index,proxy,center)
            piece['statue_fracture_owned']=True;piece['piece_id']=proxy['physics_piece_id']
            piece['piece_volume']=proxy['piece_volume'];piece['rest_matrix']=list(proxy['rest_matrix'])
            piece['physics_object']=proxy.name
            fit_collision_hull(proxy,piece)
            proxy.hide_render=True;proxy.hide_set(True)
        job['pieces'].append(piece)
    job['next']=min(job['next']+batch_size,len(job['seeds']))
    job['scene'].statue_fracture.status=f'Fractured {job["next"]}/{len(job["seeds"])} cells'
    if job['next']==len(job['seeds']):
        total=sum(o['piece_volume'] for o in job['pieces'])
        err=abs(total-job['source_volume'])/max(job['source_volume'],1e-8)
        if err>.04:
            raise RuntimeError(f'Fracture volume mismatch: {err:.1%}; inspect pieces before simulating.')
        job['source'].hide_set(True);job['source'].hide_render=True
        job['solid'].hide_set(True);job['solid'].hide_render=True
        job['scene']['sf_volume_error']=err
        job['scene']['sf_topology_before']=json.dumps(job['before'])
        job['scene']['sf_topology_after']=json.dumps(job['after'])
        if job['surface']:validate_surface(job)
        label='original-surface pieces / closed proxies' if job['surface'] else 'closed pieces'
        job['scene'].statue_fracture.status=f'{len(job["pieces"])} {label}; volume error {err:.2%}'
        activate(job['pieces'][0])
    return {'processed':job['next'],'total':len(job['seeds']),'pieces':len(job['pieces'])}


def get_pieces(scene):
    return sorted((o for o in scene.objects if 'piece_id' in o and o.get('statue_fracture_owned')),
                  key=lambda o:o['piece_id'])


def physics_object(piece):
    return bpy.data.objects[piece['physics_object']] if 'physics_object' in piece else piece


def set_key_interpolation(obj):
    if obj.animation_data and obj.animation_data.action:
        # Blender 4.5 legacy actions expose fcurves for this single-object setup.
        for fcurve in obj.animation_data.action.fcurves:
            for point in fcurve.keyframe_points:
                point.interpolation='CONSTANT' if 'kinematic' in fcurve.data_path else 'LINEAR'


def setup_simulation():
    scene=bpy.context.scene;cfg=scene.statue_fracture;pieces=get_pieces(scene)
    if not pieces:
        raise RuntimeError('Generate fragments before setting up simulation.')
    scene.frame_set(1)
    if scene.rigidbody_world and scene.rigidbody_world.point_cache.is_baked:
        with bpy.context.temp_override(point_cache=scene.rigidbody_world.point_cache):
            bpy.ops.ptcache.free_bake()
    stage=owned_collection(scene,f'SF_Stage_{scene.name}')
    floor=next((o for o in stage.objects if o.get('sf_floor')),None)
    if floor is None:
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0,0,-.05))
        floor=bpy.context.object;floor.name='SF_TableCollision'
        for coll in list(floor.users_collection):coll.objects.unlink(floor)
        stage.objects.link(floor)
        floor.dimensions=(3,3,.1)
        activate(floor);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        floor['statue_fracture_owned']=True;floor['sf_floor']=True
        mat=bpy.data.materials.new('SF_TablePreview');mat.use_nodes=True
        bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bsdf.inputs['Base Color'].default_value=(.065,.043,.029,1);bsdf.inputs['Roughness'].default_value=.8
        floor.data.materials.append(mat)
    activate(floor)
    if floor.rigid_body is None:bpy.ops.rigidbody.object_add()
    enum_set(floor.rigid_body,'type','PASSIVE');enum_set(floor.rigid_body,'collision_shape','BOX')
    floor.rigid_body.friction=.8
    source=bpy.data.objects.get(cfg.source_name)
    source_matrix=source.matrix_world if source else Matrix.Identity(4)
    heights=[]
    for obj in pieces:
        rest=Matrix([obj['rest_matrix'][i:i+4] for i in range(0,16,4)])
        heights.append((source_matrix.inverted() @ rest.translation).z)
    lo,hi=min(heights),max(heights)
    rng=random.Random(cfg.seed+53)
    for piece,height in zip(pieces,heights):
        obj=physics_object(piece)
        activate(obj)
        if obj.rigid_body is None:bpy.ops.rigidbody.object_add()
        rb=obj.rigid_body
        enum_set(rb,'type','ACTIVE');enum_set(rb,'collision_shape','CONVEX_HULL')
        rb.use_margin=True;rb.collision_margin=cfg.collision_margin
        rb.mass=max(.015,obj['piece_volume']*1600)
        rb.friction=cfg.friction;rb.restitution=.03;rb.linear_damping=.22;rb.angular_damping=.3
        rb.use_deactivation=True
        obj.animation_data_clear()
        obj.rotation_mode='QUATERNION'
        rest=Matrix([obj['rest_matrix'][i:i+4] for i in range(0,16,4)])
        obj.matrix_world=rest
        normalized=(height-lo)/max(hi-lo,.001)
        release=cfg.release_frame+round((1-normalized)*cfg.stagger_frames)
        obj['release_frame']=release
        piece['release_frame']=release
        rb.kinematic=True
        obj.keyframe_insert(data_path='rigid_body.kinematic',frame=1)
        obj.keyframe_insert(data_path='rigid_body.kinematic',frame=release-1)
        obj.keyframe_insert(data_path='location',frame=1)
        obj.keyframe_insert(data_path='rotation_quaternion',frame=1)
        obj.keyframe_insert(data_path='location',frame=release-4)
        obj.keyframe_insert(data_path='rotation_quaternion',frame=release-4)
        # A short directed motion provides a gentle collapse rather than an explosion.
        offset=Vector((rng.uniform(-.003,.003),cfg.kick*normalized,rng.uniform(0,.001)))
        obj.location=rest.translation+offset
        obj.keyframe_insert(data_path='location',frame=release-1)
        obj.keyframe_insert(data_path='rotation_quaternion',frame=release-1)
        rb.kinematic=False
        obj.keyframe_insert(data_path='rigid_body.kinematic',frame=release)
        set_key_interpolation(obj)
        if obj!=piece:obj.hide_set(True)
    world=scene.rigidbody_world
    world.substeps_per_frame=cfg.substeps;world.solver_iterations=40
    world.point_cache.frame_start=1;world.point_cache.frame_end=cfg.end_frame
    scene.frame_start=1;scene.frame_end=cfg.end_frame;scene.render.fps=30
    scene.gravity=(0,0,-9.81)
    scene.frame_set(1)
    cfg.status=f'{len(pieces)} rigid bodies; release frames {cfg.release_frame}–{cfg.release_frame+cfg.stagger_frames}'
    activate(pieces[0])
    return {'pieces':len(pieces),'release_range':[cfg.release_frame,cfg.release_frame+cfg.stagger_frames],
            'substeps':world.substeps_per_frame,'end_frame':cfg.end_frame}


def bake_simulation():
    scene=bpy.context.scene;world=scene.rigidbody_world
    if world is None:raise RuntimeError('Set up simulation first.')
    if world.point_cache.is_baked:
        with bpy.context.temp_override(point_cache=world.point_cache):bpy.ops.ptcache.free_bake()
    scene.frame_set(1)
    with bpy.context.temp_override(point_cache=world.point_cache):bpy.ops.ptcache.bake(bake=True)
    if not world.point_cache.is_baked:raise RuntimeError('Rigid-body cache was not baked.')
    scene.statue_fracture.status='Rigid-body simulation baked'
    return {'baked':world.point_cache.is_baked,'frames':[scene.frame_start,scene.frame_end]}


def export_motion(path):
    scene=bpy.context.scene;pieces=get_pieces(scene);saved=scene.frame_current
    if not scene.rigidbody_world or not scene.rigidbody_world.point_cache.is_baked:
        raise RuntimeError('Bake the physics cache before exporting motion.')
    frames=[]
    try:
        for frame in range(scene.frame_start,scene.frame_end+1):
            scene.frame_set(frame);dg=bpy.context.evaluated_depsgraph_get()
            poses=[]
            for obj in pieces:
                loc,rot,_=obj.evaluated_get(dg).matrix_world.decompose()
                poses.append({'id':obj['piece_id'],'position':list(loc),'rotation_wxyz':list(rot)})
            frames.append({'frame':frame,'pieces':poses})
    finally:scene.frame_set(saved)
    data={'schema':'statue-rigid-motion-v1','coordinates':'Blender world Z-up','fps':scene.render.fps,
          'piece_count':len(pieces),'pieces':[{'id':o['piece_id'],'name':o.name,'rest_matrix':list(o['rest_matrix']),
          'release_frame':o['release_frame'],'volume':o['piece_volume']} for o in pieces], 'frames':frames}
    out=Path(bpy.path.abspath(path));out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(data,separators=(',',':')))
    return str(out)


def load_vat_backend():
    """Works from disk or the two embedded Text blocks after reopening a .blend."""
    import sys
    import types
    if __package__:
        from . import rigid_vat
        return rigid_vat
    module=types.ModuleType('statue_fracture_rigid_vat')
    script=Path(globals().get('__file__','')).with_name('rigid_vat.py') if globals().get('__file__') else None
    if script and script.is_file():
        code=script.read_text();module.__file__=str(script)
    else:
        text=bpy.data.texts.get('Statue_Rigid_VAT.py')
        if not text:raise RuntimeError('Open rigid_vat.py alongside the tool or embed Statue_Rigid_VAT.py in this project.')
        code=text.as_string();module.__file__='Statue_Rigid_VAT.py'
    exec(compile(code,module.__file__,'exec'),module.__dict__)
    sys.modules[module.__name__]=module
    return module


class SF_Settings(bpy.types.PropertyGroup):
    preserve_surface:BoolProperty(name='保留原外表面（仅切割裂缝）',default=True,
        description='原三角面直接分割并插值 UV / 自定义法线；体素实体仅用作隐藏的物理代理')
    piece_count:IntProperty(name='碎块数量',default=36,min=8,max=120)
    seed:IntProperty(name='随机种子',default=23,min=0)
    voxel_size:FloatProperty(name='实体参考体素尺寸',default=.009,min=.003,max=.04,precision=4,
        description='保留外表面模式下仅影响种子和质量参考，不改变显示网格或碰撞包围范围')
    release_frame:IntProperty(name='开始崩塌帧',default=24,min=8)
    stagger_frames:IntProperty(name='分批释放帧数',default=12,min=0,max=60)
    end_frame:IntProperty(name='结束帧',default=110,min=45,max=600)
    kick:FloatProperty(name='倾倒位移',default=.012,min=0,max=.08,precision=3)
    collision_margin:FloatProperty(name='碰撞余量',default=.0003,min=.00001,max=.01,precision=5)
    friction:FloatProperty(name='摩擦',default=.7,min=0,max=1)
    substeps:IntProperty(name='每帧子步',default=12,min=1,max=120)
    output_path:StringProperty(name='动画数据',subtype='FILE_PATH',default='//fracture_motion.json',
        options={'PATH_SUPPORTS_BLEND_RELATIVE'})
    vat_directory:StringProperty(name='VAT 工作目录',subtype='DIR_PATH',default='//VAT/',
        options={'PATH_SUPPORTS_BLEND_RELATIVE'})
    vat_name:StringProperty(name='VAT / 动画名称',default='DragonGargoyle_Collapse')
    vat_precision:EnumProperty(name='VAT 精度',items=[('16','EXR 16 位','Half float，导出后自动验证误差'),
        ('32','EXR 32 位','Full float，适合高精度验证')],default='16')
    vat_scene_range:BoolProperty(name='使用场景帧范围',default=True)
    vat_start:IntProperty(name='VAT 起始帧',default=1,min=1)
    vat_end:IntProperty(name='VAT 结束帧',default=110,min=1)
    vat_open_preview:BoolProperty(name='导出后打开纹理预览',default=True)
    unity_export:BoolProperty(name='同时导出到 Unity',default=True)
    unity_directory:StringProperty(name='Unity 资源目录覆盖',subtype='DIR_PATH',
        options={'PATH_SUPPORTS_BLEND_RELATIVE'},
        description='留空使用插件首选项中的 Unity 项目和资源目录')
    source_name:StringProperty(name='源模型')
    status:StringProperty(name='状态',default='选择雕像模型后生成碎块')


class SF_OT_Inspect(bpy.types.Operator):
    bl_idname='statue_fracture.inspect';bl_label='检查模型'
    def execute(self,context):
        obj=context.active_object
        if not obj or obj.type!='MESH':self.report({'ERROR'},'请选择网格');return {'CANCELLED'}
        result=topology(obj.data);context.scene.statue_fracture.status=str(result)
        self.report({'INFO'},str(result));return {'FINISHED'}


class SF_OT_Generate(bpy.types.Operator):
    bl_idname='statue_fracture.generate';bl_label='生成预破碎网格'
    def execute(self,context):
        try:
            begin_fracture()
            while _BUILD['next']<len(_BUILD['seeds']):fracture_batch(6)
        except Exception as exc:self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


class SF_OT_Simulate(bpy.types.Operator):
    bl_idname='statue_fracture.simulate';bl_label='设置 / 重置模拟'
    def execute(self,context):
        try:setup_simulation()
        except Exception as exc:self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


class SF_OT_Bake(bpy.types.Operator):
    bl_idname='statue_fracture.bake';bl_label='烘焙刚体动画'
    def execute(self,context):
        try:bake_simulation()
        except Exception as exc:self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


class SF_OT_Export(bpy.types.Operator):
    bl_idname='statue_fracture.export';bl_label='导出碎块运动数据'
    def execute(self,context):
        try:export_motion(context.scene.statue_fracture.output_path)
        except Exception as exc:self.report({'ERROR'},str(exc));return {'CANCELLED'}
        self.report({'INFO'},'碎块运动数据已导出');return {'FINISHED'}


class SF_OT_ExportVAT(bpy.types.Operator):
    bl_idname='statue_fracture.export_vat';bl_label='导出刚体 VAT + 验证'
    def execute(self,context):
        scene=context.scene;cfg=scene.statue_fracture
        try:
            if cfg.unity_export:
                if not __package__:raise RuntimeError('请安装 Statue Fracture 插件以使用 Unity 导出。')
                from . import unity_export
                folder,project=unity_export.destination(cfg)  # Reject invalid paths before expensive baking.
                work=Path(bpy.path.abspath(cfg.vat_directory)).resolve()
                if work.is_relative_to((project/'Assets').resolve()):
                    raise RuntimeError('VAT 工作目录应放在 Assets 外，Unity 资源目录由插件单独发布。')
            source=scene.objects.get(cfg.source_name)
            if source is None:raise RuntimeError('Generate fragments from the source mesh first.')
            result=load_vat_backend().export(scene,get_pieces(scene),source,cfg)
            if cfg.unity_export:
                folder=unity_export.publish(result['output'],Path(result['output']).name,cfg)
                cfg.status += ' | Unity: '+folder
            if cfg.vat_open_preview:context.window.scene=bpy.data.scenes[result['preview_scene']]
        except Exception as exc:self.report({'ERROR'},str(exc));return {'CANCELLED'}
        self.report({'INFO'},f'VAT 已导出并验证：{result["width"]} × {result["height"]}');return {'FINISHED'}


class SF_OT_OpenVAT(bpy.types.Operator):
    bl_idname='statue_fracture.open_vat';bl_label='打开 VAT 纹理预览'
    def execute(self,context):
        name=context.scene.get('sf_vat_preview_scene')
        if not name or name not in bpy.data.scenes:self.report({'ERROR'},'请先导出 VAT');return {'CANCELLED'}
        context.window.scene=bpy.data.scenes[name];return {'FINISHED'}


class SF_OT_ReturnSimulation(bpy.types.Operator):
    bl_idname='statue_fracture.return_simulation';bl_label='返回破碎模拟场景'
    def execute(self,context):
        name=context.scene.get('sf_vat_source_scene')
        if not name or name not in bpy.data.scenes:self.report({'ERROR'},'源场景不存在');return {'CANCELLED'}
        context.window.scene=bpy.data.scenes[name];return {'FINISHED'}


class SF_PT_Panel(bpy.types.Panel):
    bl_label='雕像预破碎';bl_idname='SF_PT_statue_fracture';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Destruction'
    def draw(self,context):
        ui=self.layout;cfg=context.scene.statue_fracture
        if context.scene.get('sf_vat_source_scene'):
            ui.label(text='刚体 VAT 纹理预览')
            ui.label(text='时间轴播放；超出末帧保持最后姿态')
            ui.label(text='预览由 EXR 纹理解码驱动')
            ui.operator('statue_fracture.return_simulation')
            return
        ui.label(text='预破碎 → 刚体崩塌 → 烘焙')
        ui.operator('statue_fracture.inspect')
        for prop in ('preserve_surface','piece_count','seed','voxel_size'):ui.prop(cfg,prop)
        if cfg.preserve_surface:ui.label(text='外表面保留；仅裂缝交线增点')
        ui.operator('statue_fracture.generate')
        ui.separator()
        for prop in ('release_frame','stagger_frames','end_frame','kick','collision_margin','friction','substeps'):ui.prop(cfg,prop)
        ui.operator('statue_fracture.simulate');ui.operator('statue_fracture.bake')
        ui.separator();ui.prop(cfg,'output_path');ui.operator('statue_fracture.export')
        ui.separator();box=ui.box();box.label(text='刚体 VAT（每碎块 / 每帧一像素）')
        for prop in ('vat_directory','vat_name','vat_precision','vat_scene_range'):box.prop(cfg,prop)
        if not cfg.vat_scene_range:
            row=box.row(align=True);row.prop(cfg,'vat_start');row.prop(cfg,'vat_end')
        box.prop(cfg,'vat_open_preview');box.prop(cfg,'unity_export')
        if cfg.unity_export:
            box.prop(cfg,'unity_directory')
            if __package__:
                from . import unity_export
                prefs=unity_export.preferences()
                if prefs and not cfg.unity_directory:
                    box.prop(prefs,'unity_project');box.prop(prefs,'unity_assets_folder')
                try:
                    path,_=unity_export.destination(cfg)
                    for line in (str(path)[i:i+42] for i in range(0,len(str(path)),42)):
                        box.label(text=line)
                except RuntimeError as exc:box.label(text=str(exc),icon='ERROR')
        box.operator('statue_fracture.export_vat')
        box.operator('statue_fracture.open_vat')
        for line in (cfg.status[i:i+42] for i in range(0,len(cfg.status),42)):ui.label(text=line)


CLASSES=(SF_Settings,SF_OT_Inspect,SF_OT_Generate,SF_OT_Simulate,SF_OT_Bake,SF_OT_Export,
         SF_OT_ExportVAT,SF_OT_OpenVAT,SF_OT_ReturnSimulation,SF_PT_Panel)

def register():
    # Re-running the embedded Text must replace operators/panel cleanly and keep
    # each scene's settings. Operator RNA identifiers differ from Python names.
    saved = {}
    # addon_utils enables addons under _RestrictData during startup/preferences.
    # Registration must not depend on live scenes being available there.
    scenes=getattr(bpy.data,'scenes',())
    if hasattr(bpy.types.Scene,'statue_fracture'):
        saved = {scene.name: {key:getattr(scene.statue_fracture,key)
                 for key in SF_Settings.__annotations__ if hasattr(scene.statue_fracture,key)} for scene in scenes}
        del bpy.types.Scene.statue_fracture
    for cls in reversed(CLASSES):
        if issubclass(cls,bpy.types.Operator):
            namespace,operator=cls.bl_idname.split('.')
            previous=bpy.types.Operator.bl_rna_get_subclass_py(namespace.upper()+'_OT_'+operator,None)
        elif issubclass(cls,bpy.types.Panel):
            previous=bpy.types.Panel.bl_rna_get_subclass_py(cls.bl_idname,None)
        else:
            previous=bpy.types.PropertyGroup.bl_rna_get_subclass_py(cls.__name__,None)
        if previous:
            bpy.utils.unregister_class(previous)
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.statue_fracture=PointerProperty(type=SF_Settings)
    for scene in scenes:
        for key,value in saved.get(scene.name,{}).items():
            setattr(scene.statue_fracture,key,value)


def unregister():
    if hasattr(bpy.types.Scene,'statue_fracture'):del bpy.types.Scene.statue_fracture
    for cls in reversed(CLASSES):bpy.utils.unregister_class(cls)


if __name__=='__main__':register()
