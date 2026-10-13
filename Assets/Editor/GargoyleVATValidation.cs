using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class GargoyleVATValidation
{
    const string BindReference = "Blender/DragonGargoyle/Fracture/VAT/DragonGargoyle_Collapse/DragonGargoyle_Collapse_bind_mesh.json";
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
    static string Key(float u, float v, float id) => $"{Mathf.RoundToInt(u*100000)}:{Mathf.RoundToInt(v*100000)}:{Mathf.RoundToInt(id)}";
    static Vector3 XYZ(Color c) => new Vector3(c.r,c.g,c.b);
    static Vector3 Swap(Vector3 v) => GargoyleVATImport.ToUnity(v);
    static Quaternion QuaternionFrom(Color c)
    {
        var q = new Quaternion(c.r*2-1,c.g*2-1,c.b*2-1,c.a*2-1);
        return Quaternion.Normalize(q);
    }

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Validate GPU Decode")]
    public static void Validate()
    {
        var data = GargoyleVATImport.Metadata;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GargoyleVATImport.MeshPath);
        var exterior = AssetDatabase.LoadAssetAtPath<Material>(GargoyleVATImport.SurfacePath);
        Require(mesh != null && exterior != null, "Configure VAT assets first.");
        GargoyleVATImport.ValidateChannels(mesh,data);
        foreach (var m in new[] {exterior})
        {
            Require(m.shader.name == GargoyleVATImport.ShaderName && m.passCount == 4 && m.shaderKeywords.Length == 0,"Invalid VAT material shader.");
            bool async = ShaderUtil.allowAsyncCompilation;
            try { ShaderUtil.allowAsyncCompilation = false; for (int pass=0;pass<m.passCount;++pass) ShaderUtil.CompilePass(m,pass,true); }
            finally { ShaderUtil.allowAsyncCompilation = async; }
            Require(!ShaderUtil.ShaderHasError(m.shader),"VAT shader compilation failed: " + string.Join("; ",ShaderUtil.GetShaderMessages(m.shader).Select(x=>x.message)));
        }
        var position = (Texture2D)exterior.GetTexture("_VATPosition");
        var rotation = (Texture2D)exterior.GetTexture("_VATRotation");
        int w = (int)data["texture"]["width"], h = (int)data["texture"]["height"], count = (int)data["frame_count"];
        foreach (var t in new[] {position,rotation})
        {
            Require(t != null && t.width == w && t.height == h && t.mipmapCount == 1 &&
                t.filterMode == FilterMode.Point && t.wrapMode == TextureWrapMode.Clamp &&
                (t.format == TextureFormat.RGBAHalf || t.format == TextureFormat.RGBAFloat),"VAT texture layout/precision changed.");
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
            Require(!ti.sRGBTexture && ti.alphaSource == TextureImporterAlphaSource.FromInput && !ti.alphaIsTransparency,
                "VAT must be linear RGBA data with independent alpha.");
        }
        var report = new JObject { ["graphicsDevice"] = SystemInfo.graphicsDeviceType.ToString(),
            ["subMeshes"] = mesh.subMeshCount, ["materialSlots"] = 1,
            ["surfaceMarker"] = "COLOR.r: exterior=0, interior=1; every triangle constant",
            ["vertices"] = mesh.vertexCount, ["triangles"] = mesh.triangles.Length/3,
            ["textures"] = $"{w}x{h}, {position.format}, linear RGBA, point, clamp, no mips",
            ["shaderPasses"] = "GBuffer, ShadowCaster, DepthOnly, DepthNormals compiled",
            ["coordinateBasis"] = "Blender XYZ -> Unity XZY; quaternion xyz -> -XZY" };
        ValidateReference(mesh,report);
        var p = mesh.vertices; var n = mesh.normals; var tng = mesh.tangents;
        var xy = mesh.uv3; var zid = mesh.uv4;
        var min = GargoyleVATImport.Vector(data["position_min"]); var range = GargoyleVATImport.Vector(data["position_max_minus_min"]);
        var posPixels = position.GetPixels(); var rotPixels = rotation.GetPixels();
        Action<float,Vector3[],Vector3[],Vector4[]> decode = (frame,po,no,to) => {
            float f = Mathf.Clamp(frame,0,count-1); int lo = Mathf.FloorToInt(f), hi = Mathf.Min(lo+1,count-1); float b = f-lo;
            for (int i=0;i<p.Length;++i)
            {
                int id = Mathf.RoundToInt(zid[i].y), a=(h-1-lo)*w+id, c=(h-1-hi)*w+id;
                var cp = min + Vector3.Scale(Vector3.Lerp(XYZ(posPixels[a]),XYZ(posPixels[c]),b),range);
                // Interpolate the encoded quaternion before normalization, like the format contract.
                var qa = new Vector4(rotPixels[a].r*2-1,rotPixels[a].g*2-1,rotPixels[a].b*2-1,rotPixels[a].a*2-1);
                var qb = new Vector4(rotPixels[c].r*2-1,rotPixels[c].g*2-1,rotPixels[c].b*2-1,rotPixels[c].a*2-1);
                if (Vector4.Dot(qa,qb)<0) qb=-qb;
                var qv = Vector4.Lerp(qa,qb,b).normalized;
                var q = new Quaternion(qv.x,qv.y,qv.z,qv.w);
                var rest = new Vector3(xy[i].x,xy[i].y,zid[i].x);
                // CPU reference stays in Blender axes; GPU rotates in Unity axes.
                po[i] = Swap(cp + q * (Swap(p[i])-rest));
                no[i] = Swap(q * Swap(n[i]));
                var tv = Swap(q * Swap(new Vector3(tng[i].x,tng[i].y,tng[i].z)));
                to[i] = new Vector4(tv.x,tv.y,tv.z,tng[i].w);
            }
        };
        var expectedP = new Vector3[p.Length]; var expectedN = new Vector3[p.Length]; var expectedT = new Vector4[p.Length];
        float firstPivotError = 0, firstRotationError = 0;
        for (int id=0;id<(int)data["piece_count"];++id)
        {
            var pivot = GargoyleVATImport.Vector(data["pieces"][id]["pivot_rest"]);
            firstPivotError = Mathf.Max(firstPivotError,Distance(min+Vector3.Scale(XYZ(posPixels[(h-1)*w+id]),range),pivot));
            firstRotationError = Mathf.Max(firstRotationError,Quaternion.Angle(QuaternionFrom(rotPixels[(h-1)*w+id]),Quaternion.identity));
        }
        Require(firstPivotError < .001f && firstRotationError < .2f,"EXR row orientation or bind rotation is incorrect.");
        report["firstFramePivotErrorMetres"] = firstPivotError; report["firstFrameRotationErrorDegrees"] = firstRotationError;
        for (int frame=0;frame<count;++frame)
        {
            decode(frame,expectedP,expectedN,expectedT);
            Require(expectedP.All(mesh.bounds.Contains),"Animation bounds fail at frame offset " + frame);
        }
        report["animationBounds"] = $"Every imported vertex at all {count} frames fits Mesh.bounds (including half-float margin)";
        var probeMesh = UnityEngine.Object.Instantiate(mesh);
        probeMesh.subMeshCount = 1; probeMesh.SetIndices(Enumerable.Range(0,p.Length).ToArray(),MeshTopology.Points,0,false);
        var shader = Shader.Find("Hidden/Validation/RigidVAT"); Require(shader != null,"GPU validation shader is missing.");
        var mat = new Material(shader); mat.CopyPropertiesFromMaterial(exterior);
        int width = 256, height = Mathf.CeilToInt(p.Length/(float)width);
        var rt = new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear) {filterMode=FilterMode.Point}; rt.Create();
        var readback = new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
        var active = RenderTexture.active;
        float maxP=0,maxN=0,maxT=0;
        float[] frames = {-5,0,23,23.5f,50.25f,109,130};
        try
        {
            foreach (float frame in frames)
            {
                decode(frame,expectedP,expectedN,expectedT);
                mat.SetFloat("_VATProgress",frame/Mathf.Max(count-1,1)); mat.SetFloat("_VATEnabled",1);
                mat.SetVector("_ValidationSize",new Vector4(width,height,0,0));
                for (int mode=0;mode<3;++mode)
                {
                    RenderTexture.active=rt; GL.Clear(false,true,Color.clear);
                    mat.SetFloat("_ValidationMode",mode); Require(mat.SetPass(0),"GPU validation pass failed.");
                    Graphics.DrawMeshNow(probeMesh,Matrix4x4.identity);
                    readback.ReadPixels(new Rect(0,0,width,height),0,0); readback.Apply(false,false);
                    var actual = readback.GetPixels();
                    for (int i=0;i<p.Length;++i)
                    {
                        var v=XYZ(actual[i]);
                        if (mode==0) maxP=Mathf.Max(maxP,Distance(v,expectedP[i]));
                        else if (mode==1) maxN=Mathf.Max(maxN,Distance(v,expectedN[i]));
                        else maxT=Mathf.Max(maxT,(new Vector4(actual[i].r,actual[i].g,actual[i].b,actual[i].a)-expectedT[i]).magnitude);
                    }
                }
            }
            // Exercise the actual GPU COLOR fetch, not only the imported CPU array.
            mat.SetFloat("_ValidationMode",3); RenderTexture.active=rt;GL.Clear(false,true,Color.clear);
            mat.SetPass(0);Graphics.DrawMeshNow(probeMesh,Matrix4x4.identity);
            readback.ReadPixels(new Rect(0,0,width,height),0,0);readback.Apply(false,false);
            var surfacePixels=readback.GetPixels();var surfaceColors=mesh.colors32;float markerError=0;
            for (int i=0;i<p.Length;++i) markerError=Mathf.Max(markerError,Mathf.Abs(surfacePixels[i].r-surfaceColors[i].r/255f));
            report["gpuSurfaceMarkerError"]=markerError;
            Require(markerError<.000001f,"GPU COLOR.r surface fetch differs from the imported marker.");
            // The undeformed path must also work with a legacy mesh lacking UV1–3.
            probeMesh.uv2=Array.Empty<Vector2>(); probeMesh.uv3=Array.Empty<Vector2>(); probeMesh.uv4=Array.Empty<Vector2>();
            mat.SetFloat("_VATEnabled",0); mat.SetFloat("_ValidationMode",0);
            RenderTexture.active=rt; GL.Clear(false,true,Color.clear); mat.SetPass(0); Graphics.DrawMeshNow(probeMesh,Matrix4x4.identity);
            readback.ReadPixels(new Rect(0,0,width,height),0,0); readback.Apply(false,false);
            var restPixels=readback.GetPixels(); float disabledError=0;
            for (int i=0;i<p.Length;++i) disabledError=Mathf.Max(disabledError,Distance(XYZ(restPixels[i]),p[i]));
            report["disabledVATPositionError"] = disabledError;
            report["gpuMaximumPositionErrorMetres"] = maxP; report["gpuMaximumNormalError"] = maxN; report["gpuMaximumTangentError"] = maxT;
            if (disabledError >= .00001f)
            {
                report["restSample"] = JArray.FromObject(restPixels.Take(5).Select(c=>new[]{c.r,c.g,c.b,c.a}));
                report["expectedRestSample"] = JArray.FromObject(p.Take(5).Select(c=>new[]{c.x,c.y,c.z}));
            }
            Directory.CreateDirectory("Library/GargoyleVATValidation");
            File.WriteAllText("Library/GargoyleVATValidation/report.json",report.ToString());
            Require(disabledError<.00001f,"Disabled VAT readback mismatch: " + disabledError);
        }
        finally
        { RenderTexture.active=active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(readback); UnityEngine.Object.DestroyImmediate(mat); UnityEngine.Object.DestroyImmediate(probeMesh); }
        report["gpuFrameOffsets"] = new JArray(frames);
        report["gpuMaximumPositionErrorMetres"] = maxP; report["gpuMaximumNormalError"] = maxN; report["gpuMaximumTangentError"] = maxT;
        Directory.CreateDirectory("Library/GargoyleVATValidation");
        File.WriteAllText("Library/GargoyleVATValidation/report.json",report.ToString());
        Require(maxP<.00001f && maxN<.00001f && maxT<.00001f,"GPU VAT readback mismatch: " + report);
        Debug.Log("Gargoyle VAT GPU validation passed: " + report);
    }

    static void ValidateReference(Mesh mesh, JObject report)
    {
        if (!File.Exists(BindReference)) { report["blenderBindReference"] = "Not present; texture, channel and GPU checks still run"; return; }
        var reference = JObject.Parse(File.ReadAllText(BindReference));
        var sourceP = reference["positions"].Select(GargoyleVATImport.Vector).ToArray();
        var sourceN = reference["normals"].Select(GargoyleVATImport.Vector).ToArray();
        var sourceUV = reference["uv_channels"]["UVMap"]; var ids=reference["piece_ids"];
        var dict = new Dictionary<string,List<int>>();
        for (int i=0;i<sourceP.Length;++i)
        {
            string k=Key((float)sourceUV[i][0],(float)sourceUV[i][1],(float)ids[i]);
            if (!dict.TryGetValue(k,out var list)) dict[k]=list=new List<int>(); list.Add(i);
        }
        var p=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var zid=mesh.uv4;
        float ep=0,en=0;
        for (int i=0;i<p.Length;++i)
        {
            Require(dict.TryGetValue(Key(uv[i].x,uv[i].y,zid[i].y),out var list),"Surface UV0 was changed by the importer.");
            int nearest=list.OrderBy(j=>Distance(p[i],Swap(sourceP[j]))+Distance(n[i],Swap(sourceN[j]))).First();
            ep=Mathf.Max(ep,Distance(p[i],Swap(sourceP[nearest]))); en=Mathf.Max(en,Distance(n[i],Swap(sourceN[nearest])));

        }
        report["blenderBindPositionErrorMetres"]=ep; report["blenderBindNormalError"]=en;
        report["surfaceUV0"]="All imported vertices match the Blender UV0/position/normal tuples";
        // The canonical JSON stores one tangent per control point. FBX stores
        // corner tangents and Unity splits those vertices (13390 -> 15564), so
        // that JSON cannot be used as an exact tangent reference. GPU checks
        // below use the actual imported tangent and preserve its handedness.
        report["tangentReference"]="FBX corner tangents retained; animated GPU tangents compared with CPU rotation of imported tangents";
        var masks=reference["surface_mask"];var colors=mesh.colors32;
        Require(masks!=null,"Blender bind reference has no surface marker.");
        for (int i=0;i<p.Length;++i)
        {
            var matches=dict[Key(uv[i].x,uv[i].y,zid[i].y)].Where(j=>Distance(p[i],Swap(sourceP[j]))<.00001f && Distance(n[i],Swap(sourceN[j]))<.0001f);
            Require(matches.Any(j=>Mathf.Abs(colors[i].r/255f-(float)masks[j])<.00001f),"Imported COLOR.r differs from Blender surface marker.");
        }
        report["blenderSurfaceMarker"]="All imported vertex markers match the bind reference";
        Require(ep<.00001f && en<.0001f,"FBX axis conversion or vertex normals differ from the Blender bind mesh: " + report);
    }
}
