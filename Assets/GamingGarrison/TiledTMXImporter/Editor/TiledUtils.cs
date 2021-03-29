﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GamingGarrison
{
    public class TiledUtils
    {
        const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        const uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
        const uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;

        public static void FindTileDataAndMatrix(uint gid, ImportedTileset[] importedTilesets, int cellWidth, int cellHeight,
            out ImportedTile importedTile, out TSX.Tile tilesetTile, out Matrix4x4 matrix)
        {
            importedTile = null;
            tilesetTile = null;
            matrix = Matrix4x4.identity;

            bool flippedHorizontally = (gid & FLIPPED_HORIZONTALLY_FLAG) != 0;
            bool flippedVertically = (gid & FLIPPED_VERTICALLY_FLAG) != 0;
            bool flippedDiagonally = (gid & FLIPPED_DIAGONALLY_FLAG) != 0;

            gid &= ~(FLIPPED_HORIZONTALLY_FLAG | FLIPPED_VERTICALLY_FLAG | FLIPPED_DIAGONALLY_FLAG);

            ImportedTileset tilesetContainingID = null;
            for (int j = importedTilesets.Length - 1; j >= 0; --j)
            {
                int firstGID = importedTilesets[j].firstGID;
                if (firstGID <= gid)
                {
                    tilesetContainingID = importedTilesets[j];
                    break;
                }
            }

            if (tilesetContainingID != null)
            {
                int relativeID = (int)gid - tilesetContainingID.firstGID;

                if (tilesetContainingID.tileset.IsSingleImageTileset())
                {
                    // A single-image-tileset will just order tiles from 0-n
                    importedTile = tilesetContainingID.tiles[relativeID];
                    tilesetTile = null;
                }
                else
                {
                    for (int t = 0; t < tilesetContainingID.tileset.tiles.Length; t++)
                    {
                        TSX.Tile tile = tilesetContainingID.tileset.tiles[t];
                        int id = tile.id;
                        if (id == relativeID)
                        {
                            importedTile = tilesetContainingID.tiles[t];
                            tilesetTile = tile;
                            break;
                        }
                    }
                }

                if (importedTile != null)
                {
                    if (importedTile.tile == null) // Load the tile asset if we haven't already
                    {
                        importedTile.tile = AssetDatabase.LoadAssetAtPath<Tile>(importedTile.path);
                    }
                    if (flippedHorizontally || flippedVertically || flippedDiagonally)
                    {
                        if (flippedDiagonally)
                        {
                            matrix = Matrix4x4.Rotate(Quaternion.Euler(0.0f, 0.0f, 90.0f));
                            matrix = Matrix4x4.Scale(new Vector3(1.0f, -1.0f, 1.0f)) * matrix;
                        }

                        if (flippedHorizontally)
                        {
                            matrix = Matrix4x4.Scale(new Vector3(-1.0f, 1.0f, 1.0f)) * matrix;
                        }
                        if (flippedVertically)
                        {
                            matrix = Matrix4x4.Scale(new Vector3(1.0f, -1.0f, 1.0f)) * matrix;
                        }

                        Rect rect = importedTile.tile.sprite.rect;
                        rect.x = -cellWidth * 0.5f;
                        rect.y = -cellHeight * 0.5f;

                        Vector2[] corners = new Vector2[4] {
                            new Vector2(rect.x, rect.y),
                            new Vector2(rect.x + rect.width, rect.y),
                            new Vector2(rect.x, rect.y + rect.height),
                            new Vector2(rect.x + rect.width, rect.y + rect.height)};

                        for (int i = 0; i < corners.Length; i++)
                        {
                            corners[i] = matrix * corners[i];
                        }
                        Vector2 bottomLeftCorner = corners[0];
                        for (int i = 1; i < corners.Length; i++)
                        {
                            if (corners[i].x < bottomLeftCorner.x)
                            {
                                bottomLeftCorner.x = corners[i].x;
                            }
                            if (corners[i].y < bottomLeftCorner.y)
                            {
                                bottomLeftCorner.y = corners[i].y;
                            }
                        }
                        Vector2 offsetNeededUnits = new Vector2(-0.5f, -0.5f) - new Vector2(bottomLeftCorner.x / (float)cellWidth, bottomLeftCorner.y / (float)cellHeight);
                        matrix = Matrix4x4.Translate(offsetNeededUnits) * matrix;
                    }
                }
            }
        }

        public static bool LoadDataFromPlainTiles(TMX.Tile[] tiles, int width, int height, out uint[] gIDData)
        {
            if (tiles.Length != width * height)
            {
                Debug.LogError("The plain tiles array length isn't equal to the width times height in the TMX layer");
                gIDData = null;
                return false;
            }
            gIDData = new uint[tiles.Length];

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null)
                {
                    Debug.LogError("Null plain tile detected");
                    return false;
                }
                gIDData[i] = tiles[i].gid;
            }

            return true;
        }

        public static bool LoadDataFromCSV(string csv, int width, int height, out uint[] gIDData)
        {
            string[] numbersAsStrings = csv.Split(',');
            if (numbersAsStrings.Length != width * height)
            {
                Debug.LogError("The CSV length isn't equal to the width times height in the TMX layer");
                gIDData = null;
                return false;
            }

            gIDData = new uint[numbersAsStrings.Length];

            uint value;
            for (int i = 0; i < numbersAsStrings.Length; i++)
            {
                bool worked = uint.TryParse(numbersAsStrings[i], out value);
                if (!worked)
                {
                    Debug.LogError("Could not parse GID " + numbersAsStrings[i]);
                    return false;
                }
                gIDData[i] = value;
            }

            return true;
        }

        public static bool LoadDataFromBytes(byte[] data, int width, int height, out uint[] gIDData)
        {
            if (data.Length != (width * height * 4))
            {
                Debug.LogError("The byte data length isn't equal to the width times height in the TMX layer * 4");
                gIDData = null;
                return false;
            }

            gIDData = new uint[data.Length / 4];
            for (int i = 0; i < gIDData.Length; i++)
            {
                int bytePos = i * 4;
                gIDData[i] = (uint)data[bytePos++] | (uint)data[bytePos++] << 8 | (uint)data[bytePos++] << 16 | (uint)data[bytePos++] << 24;
            }

            return true;
        }
    }
}
