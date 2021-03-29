﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Serialization;
using System;

namespace GamingGarrison
{
    namespace TMX
    {
        [XmlRoot(ElementName = "map")]
        public class Map
        {
            [XmlAttribute]
            public string version;

            [XmlAttribute]
            public string tiledversion;

            [XmlAttribute]
            public string orientation;

            [XmlAttribute]
            public string renderorder;

            [XmlAttribute]
            public int width;

            [XmlAttribute]
            public int height;

            [XmlAttribute]
            public int tilewidth;

            [XmlAttribute]
            public int tileheight;

            [XmlAttribute]
            public string backgroundcolor;

            [XmlAttribute]
            public bool infinite = false;

            [XmlAttribute]
            public int nextobjectid;

            [XmlElement(ElementName = "tileset")]
            public TilesetReference[] tilesets;

            [XmlElement(ElementName = "layer")]
            public Layer[] layers;

            [XmlElement(ElementName = "objectgroup")]
            public ObjectGroup[] objectgroups;

            [XmlElement]
            public Properties properties;
        }

        public class TilesetReference
        {
            [XmlAttribute]
            public int firstgid;

            [XmlAttribute]
            public string source;

            // An embedded tileset will need all these fields instead of source
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public int tilewidth;

            [XmlAttribute]
            public int tileheight;

            [XmlAttribute]
            public int tilecount;

            [XmlAttribute]
            public int columns;

            [XmlElement]
            public TSX.Grid grid;

            [XmlElement(ElementName = "tile")]
            public TSX.Tile[] tiles;

            [XmlElement]
            public TSX.Image image;
        }

        public class Layer
        {
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public int width;

            [XmlAttribute]
            public int height;

            [XmlAttribute]
            public bool visible = true;

            /// <summary>
            /// Between 0 and 1
            /// </summary>
            [XmlAttribute]
            public float opacity = 1.0f;

            [XmlElement]
            public Properties properties;

            [XmlElement]
            public Data data;
            
        }

        public class Properties
        {
            [XmlElement]
            public Property[] property;

            public IDictionary<string, string> ToDictionary()
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                foreach(Property p in property)
                {
                    dictionary.Add(p.name, p.value);
                }
                return dictionary;
            }

            public static Properties MergeTemplateAndInstance(Properties template, Properties instance)
            {
                if (template == null && instance == null)
                {
                    return null;
                }
                if (template == null)
                {
                    return instance;
                }
                else if (instance == null)
                {
                    return template;
                }

                Properties properties = new Properties();
                List<Property> combinedProperties = new List<Property>();
                foreach (Property templateProperty in template.property)
                {
                    combinedProperties.Add(templateProperty.ShallowClone());
                }
                foreach(Property instanceProperty in instance.property)
                {
                    bool foundExistingProperty = false;
                    foreach (Property existingProperty in combinedProperties)
                    {
                        if (existingProperty.name == instanceProperty.name && existingProperty.type == instanceProperty.type)
                        {
                            foundExistingProperty = true;
                            existingProperty.value = instanceProperty.value;
                            break;
                        }
                    }
                    if (!foundExistingProperty)
                    {
                        combinedProperties.Add(instanceProperty);
                    }
                }

                properties.property = combinedProperties.ToArray();
                return properties;
            }
        }

        public class Property
        {
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public string type;

            [XmlAttribute]
            public string value;

            public Property ShallowClone()
            {
                return (Property)this.MemberwiseClone();
            }
        }

        public class Data
        {
            /// <summary>
            /// Can be "base64" or "csv"
            /// </summary>
            [XmlAttribute]
            public string encoding;

            /// <summary>
            /// If encoding == "base64", then compression can be set to "gzip" or "zlib"
            /// </summary>
            [XmlAttribute]
            public string compression;

            [XmlText]
            public string text;

            [XmlElement(ElementName = "tile")]
            public Tile[] tiles;

            [XmlElement(ElementName = "chunk")]
            public Chunk[] chunks;
        }

        /// <summary>
        /// Used for infinite maps
        /// </summary>
        public class Chunk
        {
            [XmlAttribute]
            public int x;

            [XmlAttribute]
            public int y;

            [XmlAttribute]
            public int width;

            [XmlAttribute]
            public int height;

            /// <summary>
            /// Can be "base64" or "csv"
            /// Pretty sure the layer data value is used
            /// </summary>
            [XmlAttribute]
            public string encoding;

            /// <summary>
            /// If encoding == "base64", then compression can be set to "gzip" or "zlib"
            /// Pretty sure the layer data value is used
            /// </summary>
            [XmlAttribute]
            public string compression;

            [XmlText]
            public string text;

            [XmlElement(ElementName = "tile")]
            public Tile[] tiles;
        }

        /// <summary>
        /// Just a GID of a tile instance
        /// </summary>
        [XmlType(AnonymousType = true)]
        public class Tile
        {
            [XmlAttribute]
            public uint gid;
        }

        [XmlType(AnonymousType = true)]
        public class ObjectGroup
        {
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public string color;

            // Ignoring x, y, width and height as the documentation says they are "meaningless" apparently

            [XmlAttribute]
            public float opacity = 1.0f;

            [XmlAttribute]
            public bool visible = true;

            // offsetx and offsety default to 0 since 0.14, so just ignore these...

            [XmlAttribute]
            public string draworder = "topdown";

            [XmlElement(ElementName = "object")]
            public Object[] objects;

            [XmlElement]
            public Properties properties;
        }

        // Objects can be mixed with object templates, so we need this nullable structure to correctly do the combining.
        [XmlType(AnonymousType = true)]
        public class Object
        {
            [XmlAttribute(AttributeName = "id")]
            public string idWrapper;
            public int? id { get { return idWrapper == null ? (int?)null : int.Parse(idWrapper); } set { idWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute]
            public string name;

            /// <summary>
            /// Currently unused by the importer.  Seems more of an edit-time functionality.
            /// </summary>
            [XmlAttribute]
            public string type;

            [XmlAttribute(AttributeName = "x")]
            public string xWrapper;
            public float? x { get { return xWrapper == null ? (float?)null : float.Parse(xWrapper); } set { xWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute(AttributeName = "y")]
            public string yWrapper;
            public float? y { get { return yWrapper == null ? (float?)null : float.Parse(yWrapper); } set { yWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute(AttributeName = "width")]
            public string widthWrapper;
            public float? width { get { return widthWrapper == null ? (float?)null : float.Parse(widthWrapper); } set { widthWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute(AttributeName = "height")]
            public string heightWrapper;
            public float? height { get { return heightWrapper == null ? (float?)null : float.Parse(heightWrapper); } set { heightWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute(AttributeName = "rotation")]
            public string rotationWrapper;
            public float? rotation { get { return rotationWrapper == null ? (float?)null : float.Parse(rotationWrapper); } set { rotationWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute(AttributeName = "gid")]
            public string gidWrapper;
            public uint? gid { get { return gidWrapper == null ? (uint?)null : uint.Parse(gidWrapper); } set { gidWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute(AttributeName = "visible")]
            public string visibleWrapper;
            public bool? visible { get { return visibleWrapper == null ? (bool?)null : bool.Parse(visibleWrapper); } set { visibleWrapper = (value.HasValue ? value.ToString() : null); } }

            [XmlAttribute]
            public string template;

            [XmlElement]
            public Properties properties;

            [XmlElement]
            public Ellipse ellipse;

            [XmlElement]
            public Polygon polygon;

            [XmlElement]
            public Polyline polyline;

            [XmlElement]
            public Text text;

            public void InitialiseUnsetValues()
            {
                if (!id.HasValue)
                {
                    id = 0;
                }
                if (!x.HasValue)
                {
                    x = 0.0f;
                }
                if (!y.HasValue)
                {
                    y = 0.0f;
                }
                if (!width.HasValue)
                {
                    width = 0.0f;
                }
                if (!height.HasValue)
                {
                    height = 0.0f;
                }
                if (!rotation.HasValue)
                {
                    rotation = 0.0f;
                }
                if (!gid.HasValue)
                {
                    gid = 0;
                }
                if (!visible.HasValue)
                {
                    visible = true;
                }
            }

            public Object GetVersionWithTemplateApplied(Object templateObject)
            {
                Object newObject = (Object)templateObject.MemberwiseClone();
                if (id.HasValue)
                {
                    newObject.id = id;
                }
                if (name != null)
                {
                    newObject.name = name;
                }
                if (type != null)
                {
                    newObject.type = type;
                }
                if (x.HasValue)
                {
                    newObject.x = x;
                }
                if (y.HasValue)
                {
                    newObject.y = y;
                }
                if (width.HasValue)
                {
                    newObject.width = width;
                }
                if (height.HasValue)
                {
                    newObject.height = height;
                }
                if (rotation.HasValue)
                {
                    newObject.rotation = rotation;
                }
                if (gid.HasValue)
                {
                    newObject.gid = gid;
                }
                if (visible.HasValue)
                {
                    newObject.visible = visible;
                }
                if (templateObject.template != null)
                {
                    Debug.LogError("I don't support templates inside templates!  What madness is this!");
                }
                else
                {
                    newObject.template = template;
                }
                if (properties != null)
                {
                    newObject.properties = Properties.MergeTemplateAndInstance(templateObject.properties, properties);
                }
                if (ellipse != null)
                {
                    newObject.ellipse = ellipse;
                }
                if (polygon != null)
                {
                    newObject.polygon = polygon;
                }
                if (polyline != null)
                {
                    newObject.polyline = polyline;
                }
                if (text != null)
                {
                    newObject.text = text;
                }
                return newObject;
            }
        }

        public class Ellipse
        {

        }

        public class Polygon
        {
            [XmlAttribute]
            public string points;
        }

        public class Polyline
        {
            [XmlAttribute]
            public string points;
        }

        public class Text
        {
            [XmlAttribute]
            public string fontfamily;

            [XmlAttribute]
            public int pixelsize;

            [XmlAttribute]
            public bool wrap;

            [XmlAttribute]
            public string color;

            [XmlAttribute]
            public bool bold;

            [XmlAttribute]
            public bool italic;

            [XmlAttribute]
            public bool underline;

            [XmlAttribute]
            public bool strikeout;

            [XmlAttribute]
            public bool kerning;

            [XmlAttribute]
            public string halign;

            [XmlAttribute]
            public string valign;

            [XmlText]
            public string text;
        }
    }
}
