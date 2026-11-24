using System.Diagnostics;
using System.Globalization;

namespace Solace.WebPortal.Client.Features.Seasons;

public static class SeasonUtils
{
    public const int Columns = 3;
    public const double CellSize = 115.0;

    public static List<(double X, double Y)> ComputeConnectionPath(int sourceIndex, int destinationIndex, IReadOnlySet<int> emptyCells)
    {
        Debug.Assert(sourceIndex > 0);
        Debug.Assert(destinationIndex > 0);

        var sourceRow = sourceIndex / Columns;
        var sourceColumn = sourceIndex % Columns;
        var destinationRow = destinationIndex / Columns;
        var destinationColumn = destinationIndex % Columns;

        static double MapX(int col)
        {
            return col * CellSize + CellSize / 2.0;
        }

        static double MapY(int row)
        {
            return row * CellSize + CellSize / 2.0;
        }

        if (sourceColumn == destinationColumn || sourceRow == destinationRow)
        {
            return [(MapX(sourceColumn), MapY(sourceRow)), (MapX(destinationColumn), MapY(destinationRow))];
        }

        var stepRow = destinationRow > sourceRow ? 1 : -1;
        var stepColumn = destinationColumn > sourceColumn ? 1 : -1;
        var isMiddleColumn = sourceColumn > 0 && sourceColumn < Columns - 1;

        var lPathClear = true;
        if (isMiddleColumn)
        {
            for (var column = sourceColumn + stepColumn; ; column += stepColumn)
            {
                if (!emptyCells.Contains(sourceRow * Columns + column))
                {
                    lPathClear = false;
                    break;
                }

                if (column == destinationColumn)
                {
                    break;
                }
            }

            for (var row = sourceRow + stepRow; lPathClear && row != destinationRow; row += stepRow)
            {
                if (!emptyCells.Contains(row * Columns + destinationColumn))
                {
                    lPathClear = false;
                }
            }
        }
        else
        {
            for (var row = sourceRow + stepRow; ; row += stepRow)
            {
                if (!emptyCells.Contains(row * Columns + sourceColumn))
                {
                    lPathClear = false;
                    break;
                }

                if (row == destinationRow)
                {
                    break;
                }
            }

            for (var column = sourceColumn + stepColumn; lPathClear && column != destinationColumn; column += stepColumn)
            {
                if (!emptyCells.Contains(destinationRow * Columns + column))
                {
                    lPathClear = false;
                }
            }
        }

        if (lPathClear)
        {
            return isMiddleColumn
                ? [(MapX(sourceColumn), MapY(sourceRow)), (MapX(destinationColumn), MapY(sourceRow)), (MapX(destinationColumn), MapY(destinationRow))]
                : [(MapX(sourceColumn), MapY(sourceRow)), (MapX(sourceColumn), MapY(destinationRow)), (MapX(destinationColumn), MapY(destinationRow))];
        }

        var gutterX = destinationColumn < sourceColumn ? sourceColumn * CellSize : (sourceColumn + 1) * CellSize;

        return
        [
            (MapX(sourceColumn), MapY(sourceRow)),
            (gutterX, MapY(sourceRow)),
            (gutterX, MapY(destinationRow)),
            (MapX(destinationColumn), MapY(destinationRow)),
        ];
    }

    public static string PathToSvg(IReadOnlyList<(double X, double Y)> points)
        => string.Join(' ', points.Select(point => string.Create(CultureInfo.InvariantCulture, $"{point.X:0.##},{point.Y:0.##}")));
}
