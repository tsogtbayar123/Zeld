using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Serialization;
using System;

namespace GamingGarrison
{
    namespace TSX
    {
        [XmlRoot(ElementName = "tileset")]
        public class Tileset
        {
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public int tilewidth;

            [XmlAttribute]
            public int tileheight;

            [XmlAttribute]
            public int spacing;

            [XmlAttribute]
            public int margin;

            [XmlAttribute]
            public int tilecount;

            [XmlAttribute]
            public int columns;

            [XmlElement]
            public Grid grid;

            [XmlElement(ElementName = "tile")]
            public Tile[] tiles;

            [XmlElement]
            public TSX.Image image;

            public Tileset(TMX.TilesetReference embeddedTileset)
            {
                this.name = embeddedTileset.name;
                this.tilewidth = embeddedTileset.tilewidth;
                this.tileheight = embeddedTileset.tileheight;
                this.tilecount = embeddedTileset.tilecount;
                this.columns = embeddedTileset.columns;
                this.grid = embeddedTileset.grid;
                this.tiles = embeddedTileset.tiles;
                this.image = embeddedTileset.image;
            }

            public Tileset()
            {

            }

            public bool IsSingleImageTileset()
            {
                return tiles == null || image != null;
            }
        }

        public class Grid
        {
            [XmlAttribute]
            public string orientation;

            [XmlAttribute]
            public int width;

            [XmlAttribute]
            public int height;
        }

        // I've put this annotation on the types that have the same name as TMXTypes,
        // as .net 4.6 would complain.  Namespacing or using different type names would break the parsing.
        [XmlType(AnonymousType = true)] 
        public class Tile
        {
            [XmlAttribute]
            public int id;

            [XmlElement]
            public Image image;

            [XmlElement]
            public ObjectGroup objectgroup;

            [XmlElement]
            public Animation animation;

            [XmlElement]
            public TMX.Properties properties;

            public bool HasCollisionData()
            {
                return objectgroup != null && objectgroup.objects != null && objectgroup.objects.Length > 0;
            }
        }

        public class Image
        {
            [XmlAttribute]
            public int width;

            [XmlAttribute]
            public int height;

            [XmlAttribute]
            public string source;

            /// <summary>
            /// Defines a specific colour that is treated as transparent (example value: "#FF00FF" for magenta).
            /// Up until Tiled 0.12, this value is written out without a # but this is planned to change
            /// </summary>
            [XmlAttribute]
            public string trans;
        }

        [XmlType(AnonymousType = true)]
        public class ObjectGroup
        {
            [XmlAttribute]
            public string draworder = "topdown";

            [XmlAttribute]
            public string color;

            [XmlAttribute]
            public float opacity = 1.0f;

            [XmlElement(ElementName = "object")]
            public Object[] objects;

            [XmlElement]
            public TMX.Properties properties;
        }

        /// <summary>
        /// Can be used here to represent a collision object on a tile
        /// </summary>
        [XmlType(AnonymousType = true)]
        public class Object
        {
            [XmlAttribute]
            public int id;

            [XmlAttribute]
            public float x;

            [XmlAttribute]
            public float y;

            [XmlAttribute]
            public float width;

            [XmlAttribute]
            public float height;
        }

        public class Animation
        {
            [XmlElement(ElementName = "frame")]
            public Frame[] frames;
        }

        public class Frame
        {
            /// <summary>
            /// The local ID of a tile within the parent tileset
            /// </summary>
            [XmlAttribute]
            public int tileid;

            /// <summary>
            /// How long (in milliseconds) this frame should be displayed before advancing to the next frame
            /// </summary>
            [XmlAttribute]
            public int duration;
        }
    }
}
