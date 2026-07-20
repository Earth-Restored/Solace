using System.Diagnostics;
using Google.Protobuf.Collections;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Solace.TileRenderer.Vector;

// can't use https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles because it's not aot compatible because ofc it isn't
// https://github.com/NetTopologySuite/NetTopologySuite.IO.VectorTiles/blob/develop/src/NetTopologySuite.IO.VectorTiles.Mapbox/MapboxTileReader.cs

internal sealed class MapboxTileReader
{
    private readonly GeometryFactory _factory;

    public MapboxTileReader()
        : this(new GeometryFactory(new PrecisionModel(), 4326))
    {
    }

    public MapboxTileReader(GeometryFactory factory)
    {
        _factory = factory;
    }

    public VectorTile Read(Stream stream, Tile tileDefinition)
        => Read(stream, tileDefinition, null);

    public VectorTile Read(Stream stream, Tile tileDefinition, string? idAttributeName)
    {
        var tile = global::VectorTile.Tile.Parser.ParseFrom(stream);

        var vectorTile = new VectorTile
        {
            TileId = tileDefinition.Id,
        };

        foreach (var layer2 in tile.Layers)
        {
            var tgs = new TileGeometryTransform(tileDefinition, layer2.Extent);
            var layer = new Layer
            {
                Name = layer2.Name
            };

            foreach (var feature in layer2.Features)
            {
                var item = ReadFeature(tgs, layer2, feature, idAttributeName);
                layer.Features.Add(item);
            }

            vectorTile.Layers.Add(layer);
        }

        return vectorTile;
    }

    private Feature ReadFeature(TileGeometryTransform tgs, global::VectorTile.Tile.Types.Layer mbTileLayer, global::VectorTile.Tile.Types.Feature mbTileFeature, string? idAttributeName)
    {
        var geometry = ReadGeometry(tgs, mbTileFeature.Type, mbTileFeature.Geometry);
        var attributes = ReadAttributeTable(mbTileFeature, mbTileLayer.Keys, mbTileLayer.Values);

        //Check to see if an id value is already captured in the attributes, if not, add it.
        if (!string.IsNullOrEmpty(idAttributeName) && !mbTileLayer.Keys.Contains(idAttributeName))
        {
            var id = mbTileFeature.Id;
            attributes.Add(idAttributeName, id);
        }

        return new Feature(geometry, attributes);
    }

    private Geometry? ReadGeometry(TileGeometryTransform tgs, global::VectorTile.Tile.Types.GeomType type, RepeatedField<uint> geometry)
        => type switch
        {
            global::VectorTile.Tile.Types.GeomType.Point => ReadPoint(tgs, geometry),
            global::VectorTile.Tile.Types.GeomType.Linestring => ReadLineString(tgs, geometry),
            global::VectorTile.Tile.Types.GeomType.Polygon => ReadPolygon(tgs, geometry),
            _ => null,
        };

    private Geometry? ReadPoint(TileGeometryTransform tgs, RepeatedField<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, forPoint: true);

        return CreatePuntal(sequences);
    }

    private Geometry? ReadLineString(TileGeometryTransform tgs, RepeatedField<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY);

        return CreateLineal(sequences);
    }

    private Geometry ReadPolygon(TileGeometryTransform tgs, RepeatedField<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, 1);
        return CreatePolygonal(sequences);
    }

    private Geometry? CreatePuntal(CoordinateSequence[] sequences)
    {
        if (sequences is null or { Length: 0, })
        {
            return null;
        }

        var points = new Point[sequences.Length];
        for (var i = 0; i < sequences.Length; i++)
        {
            points[i] = _factory.CreatePoint(sequences[i]);
        }

        if (points.Length is 1)
        {
            return points[0];
        }

        return _factory.CreateMultiPoint(points);
    }

    private Geometry? CreateLineal(CoordinateSequence[] sequences)
    {
        if (sequences is null or { Length: 0, })
        {
            return null;
        }

        var lineStrings = new LineString[sequences.Length];
        for (var i = 0; i < sequences.Length; i++)
        {
            lineStrings[i] = _factory.CreateLineString(sequences[i]);
        }

        if (lineStrings.Length is 1)
        {
            return lineStrings[0];
        }

        return _factory.CreateMultiLineString(lineStrings);
    }

    private Geometry CreatePolygonal(CoordinateSequence[] sequences)
    {
        var polygons = new List<Polygon>();

        LinearRing? shell = null;
        var holes = new List<LinearRing>();

        for (var i = 0; i < sequences.Length; i++)
        {
            var ring = _factory.CreateLinearRing(sequences[i]);

            if (!ring.IsCCW)
            {
                // Shell rings should be CW (https://docs.mapbox.com/vector-tiles/specification/#winding-order)
                if (shell is not null)
                {
                    polygons.Add(_factory.CreatePolygon(shell, [.. holes]));
                    holes.Clear();
                }

                shell = ring;
            }
            else
            {
                // Hole rings should be CCW https://docs.mapbox.com/vector-tiles/specification/#winding-order
                if (shell is null)
                {
                    if (sequences.Length is 1)
                    {
                        // WARNING: this is not according to the spec but tiles exists like this in the wild
                        // that are rendered just fine by other tools, we can ignore them if we want to but
                        // should not throw an exception. The solution preferred here is to just read them
                        // but reverse them so the user gets what they expect according to the spec.
                        shell = ring.Reverse() as LinearRing;
                    }
                    else
                    {
                        throw new InvalidOperationException("No shell defined.");
                    }
                }
                else
                {
                    holes.Add(ring);
                }
            }
        }

        polygons.Add(_factory.CreatePolygon(shell, [.. holes]));

        if (polygons.Count is 1)
        {
            return polygons[0];
        }

        return _factory.CreateMultiPolygon([.. polygons]);
    }

    private CoordinateSequence[] ReadCoordinateSequences(TileGeometryTransform tgs, RepeatedField<uint> geometry, ref int currentIndex, ref int currentX, ref int currentY, int buffer = 0, bool forPoint = false)
    {
        var (command, count) = ParseCommandInteger(geometry[currentIndex]);
        Debug.Assert(command is MapboxCommandType.MoveTo);
        if (count > 1)
        {
            currentIndex++;
            return ReadSinglePointSequences(tgs, geometry, count, ref currentIndex, ref currentX, ref currentY);
        }

        var sequences = new List<CoordinateSequence>();
        var currentPosition = (currentX, currentY);
        while (currentIndex < geometry.Count)
        {
            (command, count) = ParseCommandInteger(geometry[currentIndex++]);
            Debug.Assert(command is MapboxCommandType.MoveTo);
            Debug.Assert(count is 1);

            // Read the current position
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

            if (!forPoint)
            {
                // Read the next command (should be LineTo)
                (command, count) = ParseCommandInteger(geometry[currentIndex++]);
                if (command != MapboxCommandType.LineTo)
                {
                    count = 0;
                }
            }
            else
            {
                count = 0;
            }

            // Create sequence, add starting point
            var sequence = _factory.CoordinateSequenceFactory.Create(1 + count + buffer, 2);
            var sequenceIndex = 0;
            TransformOffsetAndAddToSequence(tgs, currentPosition, sequence, sequenceIndex++);

            // Read and add offsets
            for (var i = 1; i <= count; i++)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                TransformOffsetAndAddToSequence(tgs, currentPosition, sequence, sequenceIndex++);
            }

            // Check for ClosePath command
            if (currentIndex < geometry.Count)
            {
                (command, _) = ParseCommandInteger(geometry[currentIndex]);
                if (command is MapboxCommandType.ClosePath)
                {
                    Debug.Assert(buffer > 0);
                    sequence.SetOrdinate(sequenceIndex, Ordinate.X, sequence.GetOrdinate(0, Ordinate.X));
                    sequence.SetOrdinate(sequenceIndex, Ordinate.Y, sequence.GetOrdinate(0, Ordinate.Y));

                    currentIndex++;
                    sequenceIndex++;
                }
            }

            Debug.Assert(sequenceIndex == sequence.Count);

            sequences.Add(sequence);
        }

        // update current position values
        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;

        return [.. sequences];
    }

    private CoordinateSequence[] ReadSinglePointSequences(TileGeometryTransform tgs, RepeatedField<uint> geometry,
        int numSequences, ref int currentIndex, ref int currentX, ref int currentY)
    {
        var res = new CoordinateSequence[numSequences];
        var currentPosition = (currentX, currentY);
        for (var i = 0; i < numSequences; i++)
        {
            res[i] = _factory.CoordinateSequenceFactory.Create(1, 2);

            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
            TransformOffsetAndAddToSequence(tgs, currentPosition, res[i], 0);
        }

        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;
        return res;
    }

    private static void TransformOffsetAndAddToSequence(TileGeometryTransform tgs, (int x, int y) localPosition, CoordinateSequence sequence, int index)
    {
        var (longitude, latitude) = tgs.TransformInverse(localPosition.x, localPosition.y);
        sequence.SetOrdinate(index, Ordinate.X, longitude);
        sequence.SetOrdinate(index, Ordinate.Y, latitude);
    }

    private static (int, int) ParseOffset((int x, int y) currentPosition, RepeatedField<uint> parameterIntegers, ref int offset)
        => (currentPosition.x + Decode(parameterIntegers[offset++]),
            currentPosition.y + Decode(parameterIntegers[offset++]));

    private static int Decode(uint parameterInteger)
        => (int)(parameterInteger >> 1) ^ ((int)-(parameterInteger & 1));

    private static (MapboxCommandType, int) ParseCommandInteger(uint commandInteger)
        => unchecked(((MapboxCommandType)(commandInteger & 0x07U), (int)(commandInteger >> 3)));

    private static AttributesTable ReadAttributeTable(global::VectorTile.Tile.Types.Feature mbTileFeature, RepeatedField<string> keys, RepeatedField<global::VectorTile.Tile.Types.Value> values)
    {
        var att = new AttributesTable();

        for (var i = 0; i < mbTileFeature.Tags.Count; i += 2)
        {
            var key = keys[(int)mbTileFeature.Tags[i]];
            var value = values[(int)mbTileFeature.Tags[i + 1]];

            if (value.HasBoolValue)
            {
                att.Add(key, value.BoolValue);
            }
            else if (value.HasDoubleValue)
            {
                att.Add(key, value.DoubleValue);
            }
            else if (value.HasFloatValue)
            {
                att.Add(key, value.FloatValue);
            }
            else if (value.HasIntValue)
            {
                att.Add(key, value.IntValue);
            }
            else if (value.HasSintValue)
            {
                att.Add(key, value.SintValue);
            }
            else if (value.HasStringValue)
            {
                att.Add(key, value.StringValue);
            }
            else if (value.HasUintValue)
            {
                att.Add(key, value.UintValue);
            }
            else
            {
                att.Add(key, null);
            }
        }

        return att;
    }
}