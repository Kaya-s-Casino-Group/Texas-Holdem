"""Generate Unity import UV1 caches using xatlas 0.0.11 (numpy required).
Input: Tools/Art Assets/Forest Ruins/Audit Lightmap UV1 + Audit Lightmap Allocation.
Only the temporary unwrap proxy is smoothed. Original vertex attributes stay in Unity.
"""
import sys, json, hashlib, math
from pathlib import Path
import numpy as np
import xatlas
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'Assets/Art/Models/Environment/ForestRuins'
report=json.load(open(ROOT/'Library/ForestRuinsLightmapUV/lightmap_uv_audit.json'))
allocation=json.load(open(ROOT/'Library/ForestRuinsLightmapUV/allocation.json'))
actual={r['name']:max(r['atlas']['x'],r['atlas']['y'])*512 for r in allocation['renderers'] if r['index']==0}
manifest=json.load(open(BASE/'ForestRuins_Manifest.json'))
# Initially process the combined scene layout; modular meshes keep their existing UV1.
models=[m for m in report['meshes'] if m['path']==manifest['assemblyPath']]
cache={'sourceHash':hashlib.sha256((ROOT/manifest['assemblyPath']).read_bytes()).hexdigest(),'meshes':[]}
stats=[]
for m in models:
 v=np.array([[p[k] for k in ('x','y','z')] for p in m['positions']],np.float32);t=np.array(m['triangles'],np.uint32).reshape(-1,3);corners=v[t].reshape(-1,3)
 if m['kind'] in ('backdrop','cutout'):
  uv=np.array([[u['x'],u['y']] for u in m['uv1']],np.float32)[t].reshape(-1,2);charts=0
 else:
  _,idx,remap=np.unique(np.round(v,5),axis=0,return_index=True,return_inverse=True);proxy=v[idx].copy();pt=remap[t].astype(np.uint32)
  if m['kind']=='stone':
   edges=np.unique(np.sort(np.concatenate([pt[:,[0,1]],pt[:,[1,2]],pt[:,[2,0]]]),axis=1),axis=0);edges=np.concatenate([edges,edges[:,::-1]]);degree=np.bincount(edges[:,0],minlength=len(proxy))
   for _ in range(24):
    avg=np.zeros_like(proxy);np.add.at(avg,edges[:,0],proxy[edges[:,1]]);proxy=.5*proxy+.5*avg/np.maximum(degree[:,None],1)
  atlas=xatlas.Atlas();atlas.add_mesh(proxy.astype(np.float32),pt)
  chart=xatlas.ChartOptions();chart.max_iterations=4;chart.max_cost=10;chart.normal_deviation_weight=.2;chart.straightness_weight=.5;chart.normal_seam_weight=0;chart.fix_winding=True
  pack=xatlas.PackOptions();pack.resolution=128;pack.padding=max(2,math.ceil(256/max(24,actual.get(m['name'],128))));pack.bilinear=True;pack.bruteForce=True
  atlas.generate(chart_options=chart,pack_options=pack)
  vm,tris,uvs=atlas[0];assert np.all(vm[tris]==pt),(m['name'],'triangle correspondence')
  uv=uvs[tris].reshape(-1,2);charts=atlas.get_mesh_chart_count(0)
 # xatlas can discard microscopic triangles on a smoothed proxy. Give those
 # original faces separate, nondegenerate islands in a reserved edge strip.
 a=uv.reshape(-1,3,2)
 signed=(a[:,1,0]-a[:,0,0])*(a[:,2,1]-a[:,0,1])-(a[:,1,1]-a[:,0,1])*(a[:,2,0]-a[:,0,0])
 ignored=np.flatnonzero(np.abs(signed)<1e-12)
 if len(ignored):
  uv[:,0]*=.85
  for j,face in enumerate(ignored):
   y=(j+.5)/len(ignored);d=min(.02,.2/len(ignored))
   uv[face*3:face*3+3]=[[.91,y],[.91+d,y],[.91,y+d]]
 a=uv.reshape(-1,3,2);area=np.abs((a[:,1,0]-a[:,0,0])*(a[:,2,1]-a[:,0,1])-(a[:,1,1]-a[:,0,1])*(a[:,2,0]-a[:,0,0])).sum()/2
 cache['meshes'].append({'name':m['name'],'positions':[dict(zip(('x','y','z'),map(float,p))) for p in corners],'uv':[dict(zip(('x','y'),map(float,p))) for p in uv]})
 stats.append({'name':m['name'],'charts':charts,'coverage':float(area)})
 print(m['name'],charts,round(float(area),3),flush=True)
folder=BASE/'Editor/LightmapUV';folder.mkdir(exist_ok=True)
(folder/'SM_FR_EnvironmentLayout.json').write_text(json.dumps(cache,separators=(',',':')))
(ROOT/'Library/ForestRuinsLightmapUV/compact_stats.json').write_text(json.dumps(stats,indent=2))
