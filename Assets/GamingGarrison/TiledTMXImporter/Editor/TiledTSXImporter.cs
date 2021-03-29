using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GamingGarrison
{
    public class TiledTSXImporter
    {
        /// <summary>
        /// Uses given pixel values to calculate proportional pivot needed to sit the bottom left corner of the tile at the bottom left corner of the cell
        /// </summary>
        static Vector2 GetPivot(int imageWidth, int imageHeight, int cellWidth, int cellHeight)
        {
            Vector2 cellSize = new Vector2(cellWidth, cellHeight);
            Vector2 OneOverTileSize = new Vector2(1.0f / imageWidth, 1.0f / imageHeight);
            return Vector2.Scale(cellSize, OneOverTileSize) * 0.5f;
        }

        static bool CreateTilemapSprite(string targetPath, int cellWidth, int cellHeight, int pixelsPerUnit, TSX.Tileset tileset/*int width, int height, int tileWidth, int tileHeight, int tileCount*/, string subSpriteNameBase, out Sprite[] tileSprites)
        {
            TextureImporter ti = AssetImporter.GetAtPath(targetPath) as TextureImporter;

            TextureImporterSettings textureSettings = new TextureImporterSettings();
            ti.ReadTextureSettings(textureSettings);

            SpriteMeshType meshType = SpriteMeshType.FullRect;
            SpriteAlignment alignment = SpriteAlignment.Custom;
            Vector2 pivot = GetPivot(tileset.image.width, tileset.image.height, cellWidth, cellHeight);
            FilterMode filterMode = FilterMode.Point;
            SpriteImportMode importMode = SpriteImportMode.Multiple;

            if (textureSettings.spritePixelsPerUnit != pixelsPerUnit
                || textureSettings.spriteMeshType != meshType
                || textureSettings.spriteAlignment != (int)alignment
                || textureSettings.spritePivot != pivot
                || textureSettings.filterMode != filterMode
                || textureSettings.spriteMode != (int)importMode)
            {
                textureSettings.spritePixelsPerUnit = pixelsPerUnit;
                textureSettings.spriteMeshType = meshType;
                textureSettings.spriteAlignment = (int)alignment;
                textureSettings.spritePivot = pivot;
                textureSettings.filterMode = filterMode;
                textureSettings.spriteMode = (int)importMode;

                ti.SetTextureSettings(textureSettings);

                List<SpriteMetaData> newData = new List<SpriteMetaData>(tileset.tilecount);

                int i = 0;
                for (int y = tileset.image.height - tileset.margin; y > 0; y -= (tileset.tileheight + tileset.spacing))
                {
                    for (int x = tileset.margin; x < tileset.image.width; x += (tileset.tilewidth + tileset.spacing))
                    {
                        SpriteMetaData data = new SpriteMetaData();
                        data.name = subSpriteNameBase + "_" + i;
                        data.alignment = (int)alignment;
                        data.pivot = GetPivot(tileset.tilewidth, tileset.tileheight, cellWidth, cellHeight);
                        data.rect = new Rect(x, y - tileset.tileheight, tileset.tilewidth, tileset.tileheight);

                        newData.Add(data);
                        i++;
                        if (i >= tileset.tilecount)
                        {
                            break;
                        }
                    }
                    if (i >= tileset.tilecount)
                    {
                        break;
                    }
                }

                ti.spritesheet = newData.ToArray();

                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
            }

            Sprite[] subSprites = AssetDatabase.LoadAllAssetsAtPath(targetPath).OfType<Sprite>().ToArray();
            // For some reason Unity thinks it's smart to return the sub-sprites in random order...
            // ...and provide no API for retrieving sub-sprites by index >_<
            // so we have to manually sort them by the ids in their names
            Array.Sort<Sprite>(subSprites, new SpriteComparer());
            tileSprites = subSprites;
            return true;
        }

        class SpriteComparer : IComparer<Sprite>
        {
            public int Compare(Sprite a, Sprite b)
            {
                // Find last _ we put there
                int aUnderscorePos = a.name.LastIndexOf('_');
                string aNumberString = a.name.Substring(aUnderscorePos + 1);
                int aNumber = int.Parse(aNumberString);

                int bUnderscorePos = b.name.LastIndexOf('_');
                string bNumberString = b.name.Substring(bUnderscorePos + 1);
                int bNumber = int.Parse(bNumberString);

                // Compare the end numbers
                return Mathf.Clamp(aNumber - bNumber, -1, 1);
            }
        }
        

        static bool CreateTileSprite(string targetPath, int pixelsPerUnit, int cellWidth, int cellHeight, int imageWidth, int imageHeight)
        {
            TextureImporter ti = AssetImporter.GetAtPath(targetPath) as TextureImporter;

            TextureImporterSettings textureSettings = new TextureImporterSettings();
            ti.ReadTextureSettings(textureSettings);

            SpriteMeshType meshType = SpriteMeshType.FullRect;
            SpriteAlignment alignment = SpriteAlignment.Custom;
            Vector2 pivot = GetPivot(imageWidth, imageHeight, cellWidth, cellHeight);
            FilterMode filterMode = FilterMode.Point;

            if (textureSettings.spritePixelsPerUnit != pixelsPerUnit
                || textureSettings.spriteMeshType != meshType
                || textureSettings.spriteAlignment != (int)alignment
                || textureSettings.spritePivot != pivot
                || textureSettings.filterMode != filterMode)
            {
                textureSettings.spritePixelsPerUnit = pixelsPerUnit;
                textureSettings.spriteMeshType = meshType;
                textureSettings.spriteAlignment = (int)alignment;
                textureSettings.spritePivot = pivot;
                textureSettings.filterMode = filterMode;

                ti.SetTextureSettings(textureSettings);

                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
            }

            return true;
        }

        // If you're getting "error CS0246: The type of namespace name 'AnimatedTile' could not be found", then you need the prerequisite AnimatedTile class from
        // https://github.com/Unity-Technologies/2d-techdemos/blob/master/Assets/Tilemap/Tiles/Animated%20Tile/Scripts/AnimatedTile.cs
        // and put it somewhere in your project.
        static ImportedTile CreateAnimatedTileAsset(Sprite tileSprite, Sprite[] animatedSprites, float animationSpeed, string tileAssetPath, Tile.ColliderType colliderType)
        {
            AnimatedTile tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(tileAssetPath);

            if (tile == null)
            {
                tile = AnimatedTile.CreateInstance<AnimatedTile>();
                tile.m_AnimatedSprites = animatedSprites;
                tile.m_AnimationStartTime = 0.0f;
                tile.m_MinSpeed = animationSpeed;
                tile.m_MaxSpeed = animationSpeed;
                tile.sprite = tileSprite;
                tile.colliderType = colliderType;

                AssetDatabase.CreateAsset(tile, tileAssetPath);
            }
            else if ((tile.m_AnimatedSprites == null) == (animatedSprites == null)
                || tile.m_AnimatedSprites == null
                || animatedSprites == null
                || !tile.m_AnimatedSprites.SequenceEqual<Sprite>(animatedSprites)
                || tile.m_MinSpeed != animationSpeed
                || tile.m_MaxSpeed != animationSpeed
                || tile.sprite != tileSprite
                || tile.colliderType != colliderType)
            {
                tile.m_AnimatedSprites = animatedSprites;
                tile.m_MinSpeed = animationSpeed;
                tile.m_MaxSpeed = animationSpeed;
                tile.sprite = tileSprite;
                tile.colliderType = colliderType;
                EditorUtility.SetDirty(tile);
            }

            return new ImportedTile(tile, tileAssetPath);
        }

        static ImportedTile CreateTileAsset(Sprite tileSprite, string tileAssetPath, Tile.ColliderType colliderType)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);

            if (tile == null)
            {
                tile = Tile.CreateInstance<Tile>();
                tile.sprite = tileSprite;
                tile.colliderType = colliderType;

                AssetDatabase.CreateAsset(tile, tileAssetPath);
            }
            else if (tile.sprite != tileSprite || tile.colliderType != colliderType)
            {
                tile.sprite = tileSprite;
                tile.colliderType = colliderType;
                EditorUtility.SetDirty(tile);
            }

            return new ImportedTile(tile, tileAssetPath);
        }

        static void CreateSingleImageTilesetPaths(TSX.Tileset tileset, string pathWithoutFile, string tilesetSpriteTargetDir, string tilesetTileTargetDir, out string imageSourcePath, out string imageTargetPath, out string[] tileTargetPaths)
        {
            imageSourcePath = tileset.image.source;
            imageSourcePath = Path.Combine(pathWithoutFile, imageSourcePath);

            string imageName = Path.GetFileName(imageSourcePath);
            imageTargetPath = Path.Combine(tilesetSpriteTargetDir, imageName);

            tileTargetPaths = new string[tileset.tilecount];
            for (int i = 0; i < tileset.tilecount; i++)
            {
                string tileName = Path.GetFileNameWithoutExtension(imageSourcePath) + "_" + i + ".asset";
                tileTargetPaths[i] = Path.Combine(tilesetTileTargetDir, tileName);
            }
        }

        static void CreateTilePaths(TSX.Tile[] tiles, string pathWithoutFile, string tilesetSpriteTargetDir, string tilesetTileTargetDir, string[] imageSourcePaths, string[] imageTargetPaths, string[] tileTargetPaths)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                TSX.Tile tile = tiles[i];
                string imageSourcePath = tile.image.source;
                imageSourcePath = Path.Combine(pathWithoutFile, imageSourcePath);
                imageSourcePaths[i] = imageSourcePath;

                string imageName = Path.GetFileName(imageSourcePath);
                string imageTargetPath = Path.Combine(tilesetSpriteTargetDir, imageName);
                imageTargetPaths[i] = imageTargetPath;

                string tileName = Path.GetFileNameWithoutExtension(imageSourcePath) + ".asset";
                tileTargetPaths[i] = Path.Combine(tilesetTileTargetDir, tileName);
            }
        }

        static void CopyImages(string[] imageSourcePaths, string[] imageTargetPaths)
        {
            for (int i = 0; i < imageSourcePaths.Length; i++)
            {
                string imageSourcePath = imageSourcePaths[i];
                string imageTargetPath = imageTargetPaths[i];

                EditorUtility.DisplayProgressBar("Copying...", imageSourcePath + " to " + imageTargetPath, (float)i / (float)imageSourcePath.Length);

                if (File.Exists(imageSourcePath) && (!File.Exists(imageTargetPath) || (!File.GetLastWriteTime(imageSourcePath).Equals(File.GetLastWriteTime(imageTargetPath)))))
                {
                    File.Copy(imageSourcePath, imageTargetPath, true);
                    AssetDatabase.ImportAsset(imageTargetPath, ImportAssetOptions.ForceSynchronousImport);

                    // Try and make sure we import the texture as a Sprite!
                    TextureImporter importer = AssetImporter.GetAtPath(imageTargetPath) as TextureImporter;
                    if (importer != null && importer.spriteImportMode == SpriteImportMode.None)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        static bool CreateSpriteAssets(TSX.Tile[] tiles, string tilesetName, int pixelsPerUnit, int cellWidth, int cellHeight, string[] imageTargetPaths, Sprite[] tileSprites)
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < tiles.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Importing " + tilesetName + " tileset sprites", null, (float)i / (float)tiles.Length);
                if (File.Exists(imageTargetPaths[i]))
                {
                    TSX.Tile tile = tiles[i];
                    bool success = CreateTileSprite(imageTargetPaths[i], pixelsPerUnit, cellWidth, cellHeight, tile.image.width, tile.image.height);
                    if (!success)
                    {
                        return false;
                    }
                    Sprite newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(imageTargetPaths[i]);
                    Debug.Assert(newSprite != null);
                    tileSprites[i] = newSprite;
                }
            }
            AssetDatabase.StopAssetEditing();
            return true;
        }

        static bool CreateTileAssets(TSX.Tile[] tiles, bool singleImageTileset, string tilesetName, int pixelsPerUnit, Sprite[] tileSprites, string[] tileTargetPaths, out ImportedTile[] outputTiles)
        {
            AssetDatabase.StartAssetEditing();
            Debug.Assert(tileSprites != null && tileTargetPaths != null);
            outputTiles = new ImportedTile[tileTargetPaths.Length];
            if (tileSprites.Length != tileTargetPaths.Length)
            {
                Debug.Log("We have " + tileSprites.Length + " sprites but trying to create " + tileTargetPaths.Length + " tiles");
                AssetDatabase.StopAssetEditing();
                return false;
            }

            bool success = true;
            for (int i = 0; i < tileTargetPaths.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Importing " + tilesetName + " tileset tiles", null, (float)i / (float)tileTargetPaths.Length);
                if (tileSprites[i] != null)
                {
                    Tile.ColliderType colliderType = Tile.ColliderType.None;

                    // Tile Collision
                    if (!singleImageTileset && tiles != null)
                    {
                        TSX.Tile tile = tiles[i];
                        if (tile.HasCollisionData())
                        {
                            colliderType = Tile.ColliderType.Sprite;
                        }
                    }

                    // Tile Animation
                    TSX.Tile animationTile = null;
                    if (tiles != null)
                    {
                        if (singleImageTileset)
                        {
                            // Have to find relevant tile
                            foreach (TSX.Tile tile in tiles)
                            {
                                if (tile.id == i && tile.animation != null)
                                {
                                    animationTile = tile;
                                    break;
                                }
                            }
                        }
                        else if (tiles[i].animation != null)
                        {
                            animationTile = tiles[i];
                        }
                    }

                    if (animationTile != null)
                    {
                        TSX.Frame[] frames = animationTile.animation.frames;
                        Sprite[] animationSprites = new Sprite[frames.Length];
                        for (int f = 0; f < frames.Length; f++)
                        {
                            animationSprites[f] = tileSprites[frames[f].tileid];
                        }
                        //Just have to assume a constant animation speed ATM due to how Unity's Tilemap animation works :(
                        float animationSpeed = 1.0f / (frames[0].duration / 1000.0f);
                        ImportedTile newTile = CreateAnimatedTileAsset(tileSprites[i], animationSprites, animationSpeed, tileTargetPaths[i], colliderType);
                        success = newTile != null;
                        if (!success)
                        {
                            break;
                        }
                        outputTiles[i] = newTile;
                    }
                    else
                    {
                        ImportedTile newTile = CreateTileAsset(tileSprites[i], tileTargetPaths[i], colliderType);
                        success = newTile != null;
                        if (!success)
                        {
                            break;
                        }
                        outputTiles[i] = newTile;
                    }
                }
                else
                {
                    Debug.LogError("Sprite for tile " + tileTargetPaths[i] + " is null when creating Tile");
                }
            }
            AssetDatabase.StopAssetEditing();
            return success;
        }

        public static ImportedTileset ImportFromTilesetReference(TMX.TilesetReference tilesetReference, string baseFolder, string tilesetDir, int cellWidth, int cellHeight, int pixelsPerUnit)
        {
            // The source path in the tileset reference is recorded relative to the tmx
            TSX.Tileset actualTileset;
            ImportedTile[] importedTiles;
            if (tilesetReference.source != null)
            {
                string tsxPath = Path.Combine(baseFolder, tilesetReference.source);
                importedTiles = TiledTSXImporter.ImportTSXFile(tsxPath, tilesetDir, cellWidth, cellHeight, pixelsPerUnit, out actualTileset);
            }
            else
            {
                importedTiles = TiledTSXImporter.ImportEmbeddedTileset(tilesetReference, tilesetDir, baseFolder, cellWidth, cellHeight, pixelsPerUnit, out actualTileset);
            }

            if (importedTiles == null || (importedTiles.Length > 0 && importedTiles[0] == null))
            {
                Debug.LogError("Failed to import tileset " + (tilesetReference.source != null ? tilesetReference.source : tilesetReference.name) + " properly...");
                return null;
            }

            return new ImportedTileset(importedTiles, tilesetReference.firstgid, actualTileset);
        }

        static ImportedTile[] ImportEmbeddedTileset(TMX.TilesetReference embeddedTileset, string tilesetDir, string baseFolder, int cellWidth, int cellHeight, int pixelsPerUnit, out TSX.Tileset tilesetOut)
        {
            TSX.Tileset tileset = new TSX.Tileset(embeddedTileset);
            tilesetOut = tileset;

            if (!ImportUtils.CreateAssetFolderIfMissing(tilesetDir, true))
            {
                return null;
            }

            Debug.Log("Loading embedded tileset = " + tileset.name);

            return ImportTileset(tileset, tilesetDir, baseFolder, cellWidth, cellHeight, pixelsPerUnit);
        }

        static ImportedTile[] ImportTSXFile(string path, string tilesetDir, int cellWidth, int cellHeight, int pixelsPerUnit, out TSX.Tileset tilesetOut)
        {
            tilesetOut = null;
            if (!ImportUtils.CreateAssetFolderIfMissing(tilesetDir, true))
            {
                return null;
            }

            Debug.Log("Loading TSX file from " + path + " into " + tilesetDir);

            TSX.Tileset tileset = ImportUtils.ReadXMLIntoObject<TSX.Tileset>(path);
            if (tileset == null)
            {
                return null;
            }
            tilesetOut = tileset;

            Debug.Log("Loading tileset = " + tileset.name);

            return ImportTileset(tileset, tilesetDir, Path.GetDirectoryName(path), cellWidth, cellHeight, pixelsPerUnit);
        }

        static ImportedTile[] ImportTileset(TSX.Tileset tileset, string tilesetDir, string sourceTilesetDirectory, int cellWidth, int cellHeight, int pixelsPerUnit)
        {
            string tilesetSpriteTargetDir = tilesetDir + Path.DirectorySeparatorChar + tileset.name;
            if (!ImportUtils.CreateAssetFolderIfMissing(tilesetSpriteTargetDir, false))
            {
                return null;
            }
            string tilesetTileTargetDir = tilesetDir + Path.DirectorySeparatorChar + tileset.name + Path.DirectorySeparatorChar + "TileAssets";
            if (!ImportUtils.CreateAssetFolderIfMissing(tilesetTileTargetDir, false))
            {
                return null;
            }

            TSX.Tile[] tiles = tileset.tiles;

            string[] imageTargetPaths = null;
            Sprite[] tileSprites = null;
            string[] tileTargetPaths = null;
            bool singleImageTileset = tileset.IsSingleImageTileset();
            if (singleImageTileset)
            {
                if (tileset.image != null)
                {
                    string imageSourcePath;
                    string imageTargetPath;
                    CreateSingleImageTilesetPaths(tileset, sourceTilesetDirectory, tilesetSpriteTargetDir, tilesetTileTargetDir, out imageSourcePath, out imageTargetPath, out tileTargetPaths);
                    CopyImages(new string[] { imageSourcePath }, new string[] { imageTargetPath });

                    string subSpriteNameBase = Path.GetFileNameWithoutExtension(imageSourcePath);
                    if (!CreateTilemapSprite(imageTargetPath, cellWidth, cellHeight, pixelsPerUnit, tileset/*tileset.image.width, tileset.image.height, tileset.tilewidth, tileset.tileheight, tileset.tilecount*/, subSpriteNameBase, out tileSprites))
                    {
                        return null;
                    }
                }
                else
                {
                    Debug.LogError("Tileset " + tileset.name + " is empty!");
                }
            }
            else
            {
                string[] imageSourcePaths = new string[tiles.Length];
                imageTargetPaths = new string[tiles.Length];
                tileTargetPaths = new string[tiles.Length];
                CreateTilePaths(tiles, sourceTilesetDirectory, tilesetSpriteTargetDir, tilesetTileTargetDir, imageSourcePaths, imageTargetPaths, tileTargetPaths);
                CopyImages(imageSourcePaths, imageTargetPaths);

                tileSprites = new Sprite[tiles.Length];
                CreateSpriteAssets(tiles, tileset.name, pixelsPerUnit, cellWidth, cellHeight, imageTargetPaths, tileSprites);
            }

            if (tileSprites == null)
            {
                Debug.LogError("Tile sprites ended up null when importing tileset: " + tileset.name);
                return null;
            }
            if (tileSprites.Length == 0)
            {
                Debug.LogError("0 tile sprites found from texture assets for tileset: " + tileset.name);
                return null;
            }

            ImportedTile[] tileAssets;
            bool success = CreateTileAssets(tiles, singleImageTileset, tileset.name, pixelsPerUnit, tileSprites, tileTargetPaths, out tileAssets);
            EditorUtility.ClearProgressBar();

            if (!success)
            {
                return null;
            }

            CreatePalette(tileset.name, tilesetTileTargetDir, tileAssets, singleImageTileset, tileset.tilewidth, tileset.tileheight, tileset.columns, cellWidth, cellHeight);
            return tileAssets;
        }

        static void CreatePalette(string tilesetName, string tilesetTileTargetDir, ImportedTile[] tileAssets, bool singleImageTileset, int tileWidth, int tileHeight, int columns, int cellWidth, int cellHeight)
        {
            GameObject newPaletteGO = new GameObject(tilesetName, typeof(Grid));
            newPaletteGO.GetComponent<Grid>().cellSize = new Vector3(1.0f, 1.0f, 0.0f);
            GameObject paletteTilemapGO = new GameObject("Layer1", typeof(Tilemap), typeof(TilemapRenderer));
            paletteTilemapGO.transform.SetParent(newPaletteGO.transform);

            paletteTilemapGO.GetComponent<TilemapRenderer>().enabled = false;

            Tilemap paletteTilemap = paletteTilemapGO.GetComponent<Tilemap>();
            paletteTilemap.tileAnchor = TiledTSXImporter.GetPivot(tileWidth, tileHeight, cellWidth, cellHeight);
            if (columns <= 0)
            {
                columns = 5;
            }

            if (singleImageTileset)
            {
                for (int i = 0; i < tileAssets.Length; i++)
                {
                    Sprite sprite = tileAssets[i].tile.sprite;
                    Rect rect = sprite.rect;
                    int x = (int)rect.x / tileWidth;
                    int y = (int)rect.y / tileHeight;
                    paletteTilemap.SetTile(new Vector3Int(x, y, 0), tileAssets[i].tile);
                }
            }
            else
            {
                int x = 0;
                int y = 0;
                for (int i = 0; i < tileAssets.Length; i++)
                {
                    paletteTilemap.SetTile(new Vector3Int(x, y, 0), tileAssets[i].tile);
                    x++;
                    if (x >= columns)
                    {
                        x = 0;
                        y--;
                    }
                }
            }
            string palettePath = tilesetTileTargetDir + Path.DirectorySeparatorChar + tilesetName + ".prefab";
            palettePath = palettePath.Replace('\\', '/');
            UnityEngine.Object newPrefab = PrefabUtility.CreateEmptyPrefab(palettePath);
            PrefabUtility.ReplacePrefab(newPaletteGO, newPrefab, ReplacePrefabOptions.Default);
            GameObject.DestroyImmediate(newPaletteGO);

            GridPalette gridPalette = ScriptableObject.CreateInstance<GridPalette>();
            gridPalette.cellSizing = GridPalette.CellSizing.Automatic;
            gridPalette.name = "Palette Settings";
            AssetDatabase.AddObjectToAsset(gridPalette, palettePath);
            AssetDatabase.ImportAsset(palettePath);
        }
    }
}
