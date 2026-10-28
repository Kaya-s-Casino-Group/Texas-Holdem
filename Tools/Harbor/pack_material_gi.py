"""Keep UV0 charts, isolate internal overlaps, pack UV1 with xatlas 0.0.11.
Run after Tools/Harbor/Material GI/1 Export UV0 Groups; requires numpy + xatlas.
Never writes Unity assets. Unity validates the source fingerprint before applying.
"""
import json,math,sys,collections
from pathlib import Path
import numpy as np
import xatlas
ROOT=Path(__file__).resolve().parents[2];WORK=ROOT/'output/harbor-material-gi'

def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def intersection(a,b):
    poly=list(a);sign=1 if cross(*b)>0 else -1
    for i,p in enumerate(b):
        q=b[(i+1)%3];out=[]
        for j,s in enumerate(poly):
            e=poly[(j+1)%len(poly)];ds=cross(p,q,s)*sign;de=cross(p,q,e)*sign
            if ds>=0:out.append(s)
            if (ds<0)!=(de<0):
                f=ds/(ds-de);out.append((s[0]+f*(e[0]-s[0]),s[1]+f*(e[1]-s[1])))
        poly=out
        if len(poly)<3:return 0.
    return abs(sum(cross(poly[0],poly[i],poly[i+1]) for i in range(1,len(poly)-1)))*.5
def overlaps(tris,labels=None,epsilon=1e-12):
    ts=tris.tolist();cells=collections.defaultdict(list);boxes=[];pairs=[]
    for i,t in enumerate(ts):
        if abs(cross(*t))<1e-16:boxes.append(None);continue
        b=(min(p[0] for p in t),min(p[1] for p in t),max(p[0] for p in t),max(p[1] for p in t));boxes.append(b);seen=set()
        keys=[(x,y) for x in range(math.floor(b[0]*64),math.floor(b[2]*64)+1) for y in range(math.floor(b[1]*64),math.floor(b[3]*64)+1)]
        for key in keys:
            for j in cells[key]:
                if j in seen:continue
                seen.add(j)
                if labels is not None and labels[j]!=labels[i]:continue
                c=boxes[j]
                if min(b[2],c[2])-max(b[0],c[0])<=1e-12 or min(b[3],c[3])-max(b[1],c[1])<=1e-12:continue
                if intersection(t,ts[j])>epsilon:pairs.append((j,i))
            cells[key].append(i)
    return pairs
def signed_area(t):return (t[:,1,0]-t[:,0,0])*(t[:,2,1]-t[:,0,1])-(t[:,1,1]-t[:,0,1])*(t[:,2,0]-t[:,0,0])
def charts(p,uv,isolated):
    parent=list(range(len(uv)));edge={};sign=np.sign(signed_area(uv))
    def find(a):
        while parent[a]!=a:parent[a]=parent[parent[a]];a=parent[a]
        return a
    for i in range(len(uv)):
        if i in isolated:continue
        corners=[tuple(np.round(p[i,j],6))+tuple(np.round(uv[i,j],7)) for j in range(3)]
        for j in range(3):
            k=tuple(sorted((corners[j],corners[(j+1)%3])))
            if k in edge:
                other=edge[k]
                if sign[i]==sign[other]:parent[find(i)]=find(other)
            else:edge[k]=i
    roots=[find(i) for i in range(len(uv))];mapping={v:i for i,v in enumerate(sorted(set(roots)))}
    return np.array([mapping[v] for v in roots],np.uint32)

def prepare(g,forced=()):
    p=np.array(g['positions'],np.float64).reshape(-1,3);idx=np.array(g['triangles'],np.uint32).reshape(-1,3);p=p[idx]
    uv=np.array(g['uv0'],np.float64).reshape(-1,2)[idx];areas=np.linalg.norm(np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0]),axis=1)*.5
    # Procedural water/seabed have no usable UV0. Use one planar XZ chart.
    planar=bool(np.all(np.abs(signed_area(uv))<1e-14))
    if planar:
        span=np.ptp(p.reshape(-1,3),axis=0);axes=np.argsort(span)[-2:];uv=p[:,:,axes].copy();uv-=uv.reshape(-1,2).min(axis=0);uv/=max(np.ptp(uv.reshape(-1,2),axis=0))
        return p,uv,areas,np.zeros(len(uv),np.uint32),0,0,True
    collapsed=set(np.flatnonzero(np.abs(signed_area(uv))<1e-14).tolist());labels=charts(p,uv,collapsed|set(forced));original=len(set(labels.tolist()));bad=set(collapsed)|set(forced)
    for a,b in overlaps(uv,labels):
        if a not in bad and b not in bad:bad.add(a if abs(signed_area(uv[[a]])[0])<=abs(signed_area(uv[[b]])[0]) else b)
    labels=charts(p,uv,bad)
    # Reproject only collapsed UV triangles into their own local plane.
    valid=(np.abs(signed_area(uv))>1e-14)&(areas>1e-12)
    density=math.sqrt(float(np.abs(signed_area(uv))[valid].sum()*.5/max(1e-12,areas[valid].sum())))
    for i in collapsed:
        ab=p[i,1]-p[i,0];ac=p[i,2]-p[i,0];length=np.linalg.norm(ab)
        if length>1e-12 and areas[i]>1e-16:
            x=float(np.dot(ab,ac)/length);y=2*areas[i]/length;uv[i]=np.array([[0,0],[length,0],[x,y]])*density
        else:uv[i]=[[0,0],[.0002,0],[0,.0002]]
    assert not overlaps(uv,labels),g['name']+' unresolved internal overlap'
    return p,uv,areas,labels,original,len(bad),False

def pack(g,source,forced=(),attempt=0):
    print('PREPARE '+g['name'],flush=True)
    p,uv,areas,labels,original,repaired,planar=prepare(g,forced)
    # Explicit chart ids prevent stacked copies from being stitched together.
    verts=[];indices=[];mapping={}
    for i,t in enumerate(uv):
        row=[]
        for v in t:
            key=(int(labels[i]),float(v[0]),float(v[1]))
            if key not in mapping:mapping[key]=len(verts);verts.append(v)
            row.append(mapping[key])
        indices.append(row)
    # xatlas uses absolute epsilon in UV chart discovery: normalize to texel-sized
    # coordinates so small but valid triangles are not discarded as degenerate.
    indices=np.array(indices,np.uint32);atlas=xatlas.Atlas();atlas.add_uv_mesh(np.array(verts,np.float32)*1024,indices,labels.reshape(-1,1))
    options=xatlas.PackOptions();options.resolution=int(min(source['atlasSize'],max(128,math.ceil(math.sqrt(max(g['surfaceArea'],.01)/.55)*source['lightmapResolution']*g['scaleInLightmap']))));options.padding=5;options.bilinear=True;options.rotate_charts=True;options.bruteForce=False
    print('PACK '+g['name'],flush=True)
    atlas.generate(pack_options=options,verbose=True)
    vm,tri,coords=atlas[0];assert np.array_equal(vm[tri],indices),'Triangle correspondence changed'
    result=coords[tri].astype(np.float64)
    # Explicit outer border as well as inter-chart padding. Cast to the exact
    # float32 format Unity will store before the final intersection test.
    border=np.array([4/atlas.width,4/atlas.height]);result=(border+result*(1-2*border)).astype(np.float32).astype(np.float64)
    np.savez(WORK/(g['name']+'.debug.npz'),uv=result,labels=labels,areas=areas,source=uv)
    (WORK/(g['name']+'.debug.json')).write_text(json.dumps(dict(atlases=atlas.atlas_count,charts=atlas.chart_count,coverage=float(np.abs(signed_area(result)).sum()*.5),width=atlas.width,height=atlas.height)))
    assert np.isfinite(result).all() and result.min()>=0 and result.max()<=1
    print('VALIDATE '+g['name'],flush=True)
    assert atlas.atlas_count==1,'Multiple atlases cannot share a single UV1 channel'
    hit=overlaps(result,epsilon=1e-10);deg=int(np.count_nonzero((np.abs(signed_area(result))<1e-14)&(areas>1e-10)))
    if hit and not deg and attempt<3:
        isolated=set(forced)
        for a,b in hit:isolated.add(a if abs(signed_area(result[[a]])[0])<=abs(signed_area(result[[b]])[0]) else b)
        return pack(g,source,isolated,attempt+1)
    if hit or deg:raise RuntimeError(f'{g["name"]}: {len(hit)} overlaps, {deg} collapsed')
    coverage=float(np.abs(signed_area(result)).sum()*.5)
    report=dict(name=g['name'],kind=g['kind'],sources=len(g['sources']),triangles=len(result),originalCharts=original,charts=atlas.get_mesh_chart_count(0),repairedFaces=repaired,planarFallback=planar,coverage=coverage,packWidth=atlas.width,packHeight=atlas.height,paddingPixels=4,overlaps=len(hit),collapsed=deg)
    print(json.dumps(report),flush=True)
    return dict(version=3,name=g['name'],hash=g['hash'],uv1=result.reshape(-1).tolist(),validated=True,overlaps=0,collapsed=0,charts=report['charts'],coverage=coverage),report

if __name__=='__main__':
    source=json.loads((WORK/'input.json').read_text());out=[];reports=[]
    for g in source['groups']:
        cachefile=WORK/(g['name']+'.json');reportfile=WORK/(g['name']+'.stats.json')
        if cachefile.exists() and json.loads(cachefile.read_text()).get('version')==3 and json.loads(cachefile.read_text())['hash']==g['hash']:
            item=json.loads(cachefile.read_text());report=json.loads(reportfile.read_text());print('cached',g['name'],flush=True)
        else:
            item,report=pack(g,source);cachefile.write_text(json.dumps(item,separators=(',',':')));reportfile.write_text(json.dumps(report,indent=2))
        out.append(item);reports.append(report)
    (WORK/'uv1.json').write_text(json.dumps(dict(groups=out),separators=(',',':')))
    (WORK/'packing-report.json').write_text(json.dumps(reports,indent=2))
