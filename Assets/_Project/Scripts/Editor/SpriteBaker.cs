using System.IO;
using Arrows.Utils;
using UnityEditor;
using UnityEngine;

namespace Arrows.Editor
{
    /// <summary>Bakes the procedural UI shapes into sprite assets so scenes can reference them.</summary>
    public static class SpriteBaker
    {
        public const string Folder = "Assets/_Project/Art/UI";

        public static Sprite GetOrBake(ProceduralSprites.Shape shape)
        {
            string path = $"{Folder}/{shape}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            Directory.CreateDirectory(Folder);
            var tex = ProceduralSprites.CreateTexture(shape, readable: true);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = ProceduralSprites.BorderOf(shape);
            importer.spritePixelsPerUnit = ProceduralSprites.SizeOf(shape);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
