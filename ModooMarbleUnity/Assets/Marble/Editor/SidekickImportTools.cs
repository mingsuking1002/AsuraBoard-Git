using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Marble.Editor
{
    public static class SidekickImportTools
    {
        [Serializable] public sealed class Entry {public string path,sha256;}
        [Serializable] sealed class Manifest {public Entry[] entries;}
        public static Entry[] Entries()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root,"Documentation/asset-audit/sidekick.json")));
            if(manifest==null || manifest.entries==null || manifest.entries.Length!=165)throw new InvalidDataException("Sidekick source manifest missing.");
            foreach(var entry in manifest.entries)
            {
                if(!entry.path.StartsWith("Assets/Marble/Resources/Downloaded/Sidekick/",StringComparison.Ordinal) || entry.path.Contains(".."))throw new InvalidDataException("Unexpected Sidekick path.");
                using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(Path.Combine(root,entry.path)))
                    if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=entry.sha256)throw new InvalidDataException("Sidekick source bytes changed: "+entry.path);
            }
            return manifest.entries;
        }
        public static void Prepare()
        {
            foreach(var entry in Entries())
            {
                if(entry.path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))
                {
                    var importer=AssetImporter.GetAtPath(entry.path) as ModelImporter;
                    if(importer==null)throw new InvalidDataException("Character model not imported: "+entry.path);
                    if(!importer.isReadable || !importer.importBlendShapes || importer.materialImportMode!=ModelImporterMaterialImportMode.None || importer.optimizeGameObjects)
                    {importer.isReadable=true;importer.importBlendShapes=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.optimizeGameObjects=false;importer.SaveAndReimport();}
                }
                else
                {
                    var importer=AssetImporter.GetAtPath(entry.path) as TextureImporter;
                    if(importer==null)throw new InvalidDataException("Character palette not imported.");
                    if(!importer.isReadable || importer.textureCompression!=TextureImporterCompression.Uncompressed)
                    {importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Point;importer.SaveAndReimport();}
                }
            }
        }
        public static void Audit()
        {
            Prepare();string report="";
            foreach(string name in new[]{"SK_HUMN_BASE_01_01HEAD_HU01","SK_HUMN_BASE_02_01HEAD_HU01","SK_SCFI_CIVL_09_10TORS_HU01","SK_HUMN_BASE_01_10TORS_HU01","SK_FANT_KNGT_17_22AHED_HU01"})
            {
                var model=Resources.Load<GameObject>("Downloaded/Sidekick/"+name);var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;
                report+=name+"\nBounds "+mesh.bounds+"\nBlends "+string.Join(",",Enumerable.Range(0,mesh.blendShapeCount).Select(mesh.GetBlendShapeName))+"\nBones "+string.Join(",",skin.bones.Select(b=>b==null?"null":b.name))+"\n";
                report+="UV0 "+string.Join(",",mesh.uv.Take(8).Select(v=>v.ToString("F4")))+"\nUV1 "+string.Join(",",mesh.uv2.Take(8).Select(v=>v.ToString("F4")))+"\n";
            }
            var texture=Resources.Load<Texture2D>("Downloaded/Sidekick/T_Starter_01ColorMap");report+="Palette "+texture.width+" x "+texture.height+"\n";
            report+="Skin pixels "+texture.GetPixels32().Count(c=>c.r==213 && c.g==165 && c.b==123)+"\n";
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Validation/sidekick-audit.txt"));File.WriteAllText(path,report);
        }
    }
}
