using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Marble.Editor
{
    // Only the explicitly selected assets from the user's downloaded package are accepted.
    public static class SimpleTownImportTools
    {
        [Serializable] public sealed class Entry { public string path,sha256; }
        [Serializable] sealed class Manifest { public Entry[] entries; }
        public static Entry[] Entries()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root,"Documentation/asset-audit/simpletown.json")));
            if(manifest==null || manifest.entries==null || manifest.entries.Length!=2)throw new InvalidDataException("Simple Town source manifest missing.");
            foreach(var entry in manifest.entries)
            {
                if(!entry.path.StartsWith("Assets/Marble/Resources/Downloaded/SimpleTown/",StringComparison.Ordinal) || entry.path.Contains(".."))throw new InvalidDataException("Unexpected Simple Town path.");
                using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(Path.Combine(root,entry.path)))
                    if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=entry.sha256)throw new InvalidDataException("Simple Town source bytes changed: "+entry.path);
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
                    if(importer==null)throw new InvalidDataException("Building model not imported: "+entry.path);
                    if(importer.materialImportMode!=ModelImporterMaterialImportMode.None){importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();}
                }
                else if(AssetImporter.GetAtPath(entry.path) as TextureImporter==null)throw new InvalidDataException("Building texture not imported: "+entry.path);
            }
        }
    }
}
