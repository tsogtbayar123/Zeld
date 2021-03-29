using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Tilemaps;
using System;

namespace GamingGarrison
{
    public class TiledTMXImporter
    {
        static bool FillTilemapFromData(Tilemap tilemap, int startX, int startY, int width, int height, uint[] data, ImportedTileset[] importedTilesets, int cellWidth, int cellHeight)
        {
            bool anyTilesWithCollision = false;
            for (int i = 0; i < data.Length; i++)
            {
                uint value = data[i];

                ImportedTile importedTile;
                TSX.Tile tilesetTile;
                Matrix4x4 matrix;
                TiledUtils.FindTileDataAndMatrix(value, importedTilesets, cellWidth, cellHeight, out importedTile, out tilesetTile, out matrix);

                if (importedTile != null && importedTile.tile != null)
                {
                    int x = startX + (i % width);
                    int y = -(startY + ((i / width) + 1));

                    Vector3Int pos = new Vector3Int(x, y, 0);

                    tilemap.SetTile(pos, importedTile.tile);
                    tilemap.SetTransformMatrix(pos, matrix);

                    if (tilesetTile != null && tilesetTile.HasCollisionData())
                    {
                        anyTilesWithCollision = true;
                    }
                }
                else if (value > 0)
                {
                    Debug.LogError("Could not find tile " + value + " in tilemap " + tilemap.name);
                    if (ImportUtils.s_validationMode)
                    {
                        return false;
                    }
                }
            }

            if (anyTilesWithCollision)
            {
                if (tilemap.gameObject.GetComponent<TilemapCollider2D>() == null)
                {
                    tilemap.gameObject.AddComponent<TilemapCollider2D>();
                }
            }

            return true;
        }

        static bool AddChunkToTilemap(Tilemap layerTilemap, string encoding, string compression, TMX.Tile[] plainTiles, string dataText,
            int x, int y, int width, int height, ImportedTileset[] importedTilesets, int cellWidth, int cellHeight)
        {
            uint[] gIDData = null;
            if (encoding == null)
            {
                TiledUtils.LoadDataFromPlainTiles(plainTiles, width, height, out gIDData);
            }
            else if (encoding.Equals("csv"))
            {
                if (!TiledUtils.LoadDataFromCSV(dataText, width, height, out gIDData))
                {
                    Debug.LogError("Layer data for layer " + layerTilemap.gameObject.name + " could not be csv decoded");
                    return false;
                }
            }
            else if (encoding.Equals("base64"))
            {
                byte[] decoded = Convert.FromBase64String(dataText);
                if (decoded == null)
                {
                    Debug.LogError("Layer data for layer " + layerTilemap.gameObject.name + " could not be base64 decoded");
                    return false;
                }
                if (compression != null)
                {
                    if (compression.Equals("zlib"))
                    {
                        decoded = ImportUtils.DecompressZLib(decoded);
                    }
                    else if (compression.Equals("gzip"))
                    {
                        decoded = ImportUtils.DecompressGZip(decoded);
                    }
                }
                if (!TiledUtils.LoadDataFromBytes(decoded, width, height, out gIDData))
                {
                    Debug.LogError("Layer data for layer " + layerTilemap.gameObject.name + " could not created from loaded byte data");
                    return false;
                }
            }

            if (gIDData == null)
            {
                Debug.LogError("Layer data for layer " + layerTilemap.gameObject.name + " could not be decoded");
                return false;
            }

            bool worked = FillTilemapFromData(layerTilemap, x, y, width, height, gIDData, importedTilesets, cellWidth, cellHeight);
            return worked;
        }

        static bool ImportTileLayer(TMX.Layer layer, GameObject newGrid, int layerID, ImportedTileset[] importedTilesets, ITilemapImportOperation[] importOperations,
            TilemapRenderer.SortOrder tileSortOrder, int cellWidth, int cellHeight, bool infinite)
        {
            if (infinite != (layer.data.chunks != null))
            {
                Debug.LogError("Our map infinite setting is " + infinite + " but our chunks value is " + layer.data.chunks);
                return false;
            }
            GameObject newLayer = new GameObject(layer.name, typeof(Tilemap), typeof(TilemapRenderer));
            newLayer.transform.SetParent(newGrid.transform, false);
            Tilemap layerTilemap = newLayer.GetComponent<Tilemap>();
            layerTilemap.tileAnchor = new Vector2(0.5f, 0.5f);
            if (layer.opacity < 1.0f)
            {
                layerTilemap.color = new Color(1.0f, 1.0f, 1.0f, layer.opacity);
            }

            if (layer.data.chunks != null)
            {
                for (int c = 0; c < layer.data.chunks.Length; c++)
                {
                    TMX.Chunk chunk = layer.data.chunks[c];
                    bool success = AddChunkToTilemap(layerTilemap, layer.data.encoding, layer.data.compression, chunk.tiles, chunk.text,
                        chunk.x, chunk.y, chunk.width, chunk.height, importedTilesets, cellWidth, cellHeight);
                    if (!success)
                    {
                        return false;
                    }
                }
            }
            else
            {
               bool success = AddChunkToTilemap(layerTilemap, layer.data.encoding, layer.data.compression, layer.data.tiles, layer.data.text,
                    0, 0, layer.width, layer.height, importedTilesets, cellWidth, cellHeight);
                if (!success)
                {
                    return false;
                }
            }
            
            TilemapRenderer renderer = newLayer.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = layerID;
            renderer.sortOrder = tileSortOrder;
            if (!layer.visible)
            {
                renderer.enabled = false;
            }

            IDictionary<string, string> properties = (layer.properties == null ? new Dictionary<string, string>() : layer.properties.ToDictionary());

            foreach (ITilemapImportOperation operation in importOperations)
            {
                operation.HandleCustomProperties(newLayer, properties);
            }
            Undo.RegisterCreatedObjectUndo(newLayer, "Import tile layer");
            return true;
        }

        static TMX.Object ApplyTemplate(TMX.Object mapObject, string tmxParentFolder, string tilesetDir, int cellWidth, int cellHeight, int pixelsPerUnit,
            Dictionary<string, ImportedTemplate> importedTemplates, out ImportedTileset replacementTileset)
        {
            // Template path seems to be relative to the TMX
            string templatePath = Path.GetFullPath(Path.Combine(tmxParentFolder, mapObject.template));

            ImportedTemplate importedTemplate;
            if (importedTemplates.ContainsKey(templatePath))
            {
                importedTemplate = importedTemplates[templatePath];
            }
            else
            {
                importedTemplate = TiledTXImporter.LoadTXFile(templatePath, tilesetDir, cellWidth, cellHeight, pixelsPerUnit);
                importedTemplates.Add(templatePath, importedTemplate);
            }

            if (!mapObject.gid.HasValue)
            {
                replacementTileset = importedTemplate.m_importedTileset;
            }
            else
            {
                replacementTileset = null; // If the instance has a gid, assume it uses the normal map tilesets
            }
            return mapObject.GetVersionWithTemplateApplied(importedTemplate.m_template.templateObject);
        }

        static bool ImportMapObject(TMX.Object mapObject, ImportedTileset[] importedTilesets, Dictionary<string, ImportedTemplate> importedTemplates, GameObject newObjectLayer, int mapTileWidth, int mapTileHeight, int sortingLayer,
            ITilemapImportOperation[] importOperations, string tilesetDir, int cellWidth, int cellHeight, int pixelsPerUnit, string tmxParentFolder)
        {

            ImportedTileset replacementTileset = null;

            if (mapObject.template != null)
            {
                // Fill out empty object fields with data from the template.
                // The template could have a tile from it's own tileset reference, so in that case, use the template tileset instead, and the template object GID
                TMX.Object combinedMapObject = ApplyTemplate(mapObject, tmxParentFolder, tilesetDir, cellWidth, cellHeight, pixelsPerUnit, importedTemplates, out replacementTileset);

                if (combinedMapObject == null)
                {
                    Debug.LogError("Could not load template for map object " + mapObject);
                    return false;
                }
                else
                {
                    mapObject = combinedMapObject;
                }
            }
            mapObject.InitialiseUnsetValues(); // We need special code to set defaults here, because setting them before merging with a template would give incorrect results.

            // Use the template's tileset (and the gid that's been set by ApplyTemplate)
            if (replacementTileset != null)
            {
                importedTilesets = new ImportedTileset[] { replacementTileset };
            }

            ImportedTile importedTile;
            TSX.Tile tilesetTile; // Unused
            Matrix4x4 matrix;
            TiledUtils.FindTileDataAndMatrix(mapObject.gid.Value, importedTilesets, cellWidth, cellHeight, out importedTile, out tilesetTile, out matrix);

            Vector2 pixelsToUnits = new Vector2(1.0f / mapTileWidth, -1.0f / mapTileHeight);

            GameObject newObject = new GameObject(mapObject.name);
            newObject.transform.SetParent(newObjectLayer.transform, false);

            // So we gain the tile rotation/flipping
            newObject.transform.FromMatrix(matrix);

            Vector2 corner = Vector2.Scale(new Vector2(mapObject.x.Value, mapObject.y.Value), pixelsToUnits);

            if (importedTile != null)
            {
                Tile unityTile = importedTile.tile;
                Vector2 pivotProportion = new Vector2(unityTile.sprite.pivot.x / unityTile.sprite.rect.width, unityTile.sprite.pivot.y / unityTile.sprite.rect.height);
                Vector3 pivotWorldPosition = corner + Vector2.Scale(new Vector2(mapObject.width.Value * pivotProportion.x, mapObject.height.Value * -pivotProportion.y), pixelsToUnits);
                newObject.transform.localPosition += pivotWorldPosition;

                SpriteRenderer renderer = newObject.AddComponent<SpriteRenderer>();
                renderer.sprite = unityTile.sprite;
                renderer.sortingOrder = sortingLayer;
                if (unityTile.colliderType == Tile.ColliderType.Sprite)
                {
                    newObject.AddComponent<PolygonCollider2D>();
                }
                else if (unityTile.colliderType == Tile.ColliderType.Grid)
                {
                    newObject.AddComponent<BoxCollider2D>();
                }
                Vector2 scale = new Vector2(mapObject.width.Value / unityTile.sprite.rect.width, mapObject.height.Value / unityTile.sprite.rect.height);
                newObject.transform.localScale = Vector3.Scale(newObject.transform.localScale, new Vector3(scale.x, scale.y, 1.0f));
            }
            else
            {
                Vector3 pivotWorldPosition = corner + Vector2.Scale(new Vector2(mapObject.width.Value * 0.5f, mapObject.height.Value * 0.5f), pixelsToUnits);
                newObject.transform.localPosition += pivotWorldPosition;
                // If no tile used, must be a non-tile object of some sort (collision or text)
                if (mapObject.ellipse != null)
                {
                    EllipseCollider2D collider = newObject.AddComponent<EllipseCollider2D>();
                    collider.RadiusX = (mapObject.width.Value * 0.5f) / mapTileWidth;
                    collider.RadiusY = (mapObject.height.Value * 0.5f) / mapTileHeight;
                }
                else if (mapObject.polygon != null)
                {
                    PolygonCollider2D collider = newObject.AddComponent<PolygonCollider2D>();
                    string points = mapObject.polygon.points;
                    collider.points = ImportUtils.PointsFromString(points, pixelsToUnits);
                }
                else if (mapObject.polyline != null)
                {
                    EdgeCollider2D collider = newObject.AddComponent<EdgeCollider2D>();
                    string points = mapObject.polyline.points;
                    collider.points = ImportUtils.PointsFromString(points, pixelsToUnits);
                }
                else if (mapObject.text != null)
                {
                    TextMesh textMesh = newObject.AddComponent<TextMesh>();
                    textMesh.text = mapObject.text.text;
                    textMesh.anchor = TextAnchor.MiddleCenter;

                    Color color = Color.white;
                    if (mapObject.text.color != null)
                    {
                        ColorUtility.TryParseHtmlString(mapObject.text.color, out color);
                    }
                    textMesh.color = color;

                    // Saving an OS font as an asset in unity (through script) is seemingly impossible right now in a platform independent way
                    // So we'll skip fonts for now

                    textMesh.fontSize = (int)mapObject.text.pixelsize; // Guess a good resolution for the font
                    float targetWorldTextHeight = (float)mapObject.text.pixelsize / (float)mapTileHeight;
                    textMesh.characterSize = targetWorldTextHeight * 10.0f / textMesh.fontSize;

                    Renderer renderer = textMesh.gameObject.GetComponent<MeshRenderer>();
                    renderer.sortingOrder = sortingLayer;
                    renderer.sortingLayerID = SortingLayer.GetLayerValueFromName("Default");
                }
                else
                {
                    // Regular box collision
                    BoxCollider2D collider = newObject.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(mapObject.width.Value / mapTileWidth, mapObject.height.Value / mapTileHeight);
                }
            }

            if (mapObject.rotation != 0.0f)
            {
                newObject.transform.RotateAround(corner, new Vector3(0.0f, 0.0f, 1.0f), -mapObject.rotation.Value);
            }

            if (mapObject.visible == false)
            {
                Renderer renderer = newObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            IDictionary<string, string> properties = (mapObject.properties == null ? new Dictionary<string, string>() : mapObject.properties.ToDictionary());

            foreach (ITilemapImportOperation operation in importOperations)
            {
                operation.HandleCustomProperties(newObject, properties);
            }
            return true;
        }

        public static bool ImportTMXFile(string path, string tilesetDir, Grid targetGrid)
        {
            string tmxParentFolder = Path.GetDirectoryName(path);
            string filename = Path.GetFileNameWithoutExtension(path);

            TMX.Map map = ImportUtils.ReadXMLIntoObject<TMX.Map>(path);
            if (map == null)
            {
                return false;
            }
            if (map.backgroundcolor != null)
            {
                Color backgroundColor;
                if (ColorUtility.TryParseHtmlString(map.backgroundcolor, out backgroundColor))
                {
                    Camera.main.backgroundColor = backgroundColor;
                }
            }
            if (map.tilesets != null)
            {
                // First we need to load (or import) all the tilesets referenced by the TMX file...
                int cellWidth = map.tilewidth;
                int cellHeight = map.tileheight;
                int pixelsPerUnit = Mathf.Max(map.tilewidth, map.tileheight);
                ImportedTileset[] importedTilesets = new ImportedTileset[map.tilesets.Length];
                for (int i = 0; i < map.tilesets.Length; i++)
                {
                    importedTilesets[i] = TiledTSXImporter.ImportFromTilesetReference(map.tilesets[i], tmxParentFolder, tilesetDir, cellWidth, cellHeight, pixelsPerUnit);
                    if (importedTilesets[i] == null || importedTilesets[i].tiles == null || importedTilesets[i].tiles[0] == null)
                    {
                        Debug.LogError("Imported tileset is incomplete");
                        return false;
                    }
                }


                // Set up the Grid to store everything in
                GameObject newGrid = null;
                if (targetGrid != null)
                {
                    newGrid = targetGrid.gameObject;
                    for (int i = newGrid.transform.childCount - 1; i >= 0; --i)
                    {
                        Undo.DestroyObjectImmediate(newGrid.transform.GetChild(i).gameObject);
                    }
                }
                else
                {
                    newGrid = new GameObject(filename, typeof(Grid));
                    newGrid.GetComponent<Grid>().cellSize = new Vector3(1.0f, 1.0f, 0.0f);
                    Undo.RegisterCreatedObjectUndo(newGrid, "Import map to new Grid");
                }

                ITilemapImportOperation[] importOperations = ImportUtils.GetObjectsThatImplementInterface<ITilemapImportOperation>();
                TilemapRenderer.SortOrder sortOrder = TilemapRenderer.SortOrder.TopLeft;
                if (map.renderorder.Equals("right-down"))
                {
                    sortOrder = TilemapRenderer.SortOrder.TopLeft;
                }
                else if (map.renderorder.Equals("right-up"))
                {
                    sortOrder = TilemapRenderer.SortOrder.BottomLeft;
                }
                else if (map.renderorder.Equals("left-down"))
                {
                    sortOrder = TilemapRenderer.SortOrder.TopRight;
                }
                else if (map.renderorder.Equals("left-up"))
                {
                    sortOrder = TilemapRenderer.SortOrder.BottomRight;
                }
                // Load all the tile layers
                if (map.layers != null)
                {
                    for (int i = 0; i < map.layers.Length; i++)
                    {
                        TMX.Layer layer = map.layers[i];

                        bool success = ImportTileLayer(layer, newGrid, i, importedTilesets, importOperations, sortOrder, cellWidth, cellHeight, map.infinite);
                        if (!success)
                        {
                            return false;
                        }
                    }
                }

                Dictionary<string, ImportedTemplate> importedTemplates = new Dictionary<string, ImportedTemplate>();

                // Load all the objects
                if (map.objectgroups != null)
                {
                    for (int g = 0; g < map.objectgroups.Length; g++)
                    {
                        TMX.ObjectGroup objectGroup = map.objectgroups[g];

                        if (objectGroup != null && objectGroup.objects != null && objectGroup.objects.Length > 0)
                        {
                            GameObject newObjectLayer = new GameObject(objectGroup.name);
                            newObjectLayer.transform.SetParent(newGrid.transform, false);
                            Undo.RegisterCreatedObjectUndo(newObjectLayer, "Import object layer");

                            int sortingLayer = (map.layers == null ? 0 : map.layers.Length) + g;

                            for (int i = 0; i < objectGroup.objects.Length; i++)
                            {
                                TMX.Object mapObject = objectGroup.objects[i];

                                bool success = ImportMapObject(mapObject, importedTilesets, importedTemplates, newObjectLayer, map.tilewidth, map.tileheight, sortingLayer, importOperations, tilesetDir, cellWidth, cellHeight, pixelsPerUnit, tmxParentFolder);
                                if (!success)
                                {
                                    return false;
                                }
                            }
                            Color objectColour = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                            if (objectGroup.color != null)
                            {
                                ColorUtility.TryParseHtmlString(objectGroup.color, out objectColour);
                            }
                            if (objectGroup.opacity != 1.0f)
                            {
                                objectColour.a = objectGroup.opacity;
                            }
                            SpriteRenderer[] renderers = newObjectLayer.GetComponentsInChildren<SpriteRenderer>();
                            foreach (SpriteRenderer r in renderers)
                            {
                                r.color = objectColour;
                            }
                            if (!objectGroup.visible)
                            {
                                foreach (SpriteRenderer r in renderers)
                                {
                                    r.enabled = false;
                                }
                            }

                            IDictionary<string, string> properties = (objectGroup.properties == null ? new Dictionary<string, string>() : objectGroup.properties.ToDictionary());

                            foreach (ITilemapImportOperation operation in importOperations)
                            {
                                operation.HandleCustomProperties(newObjectLayer, properties);
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
