/*
    Based on the Public Domain SkylineBinPack.cpp source by Jukka Jylänki
    https://github.com/juj/RectangleBinPack/
*/

using Obj2Tiles.Library.Algos.Model;

namespace Obj2Tiles.Library.Algos
{
    /// Defines the different heuristic rules that can be used to decide how to make the rectangle placements.
    public enum LevelChoiceHeuristic
    {
        LevelBottomLeft,
        LevelMinWasteFit
    }

    /// <summary>
    /// Implements bin packing algorithms that use the SKYLINE data structure to store the bin contents.
    /// Ported from juj/RectangleBinPack SkylineBinPack.
    /// </summary>
    /// <remarks>
    /// The waste-map variant from the original is not implemented here, but would be a nice later addition.
    /// </remarks>
    public class SkylineBinPack
    {
        public int binWidth = 0;
        public int binHeight = 0;
        public bool allowRotations;

        public readonly List<Rectangle> usedRectangles = [];

        /// A single level (a horizontal line) of the skyline.
        private struct SkylineNode
        {
            /// The starting x-coordinate (leftmost).
            public int X;

            /// The y-coordinate of the skyline level line.
            public int Y;

            /// The line width. The ending coordinate (inclusive) will be X+Width-1.
            public int Width;
        }

        private readonly List<SkylineNode> skyLine = [];

        private ulong usedSurfaceArea;

        public SkylineBinPack(int width, int height, bool rotations = true)
        {
            Init(width, height, rotations);
        }

        /// (Re)initializes the packer to an empty bin of width x height units. Call whenever
        /// you need to restart with a new bin.
        public void Init(int width, int height, bool rotations = true)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            binWidth = width;
            binHeight = height;
            allowRotations = rotations;
            usedSurfaceArea = 0;

            usedRectangles.Clear();
            skyLine.Clear();
            skyLine.Add(new SkylineNode { X = 0, Y = 0, Width = width });
        }

        /// <summary>
        /// Inserts a single rectangle into the bin, possibly rotated if <see cref="allowRotations"/> is set.
        /// If the rectangle does not fit, a Rectangle with Height == 0 is returned
        /// (same convention as MaxRectanglesBinPack).
        /// </summary>
        public Rectangle Insert(int width, int height, LevelChoiceHeuristic method) =>
            Insert(width, height, method, out _);

        /// <summary>
        /// Inserts a single rectangle into the bin, possibly rotated if <see cref="allowRotations"/> is set.
        /// If the rectangle does not fit, a Rectangle with Height == 0 is returned
        /// (same convention as MaxRectanglesBinPack).
        /// </summary>
        /// <param name="rotated">
        /// Set to <c>true</c> when the winning placement swapped the requested width/height
        /// (a 90° rotation), <c>false</c> for an upright placement or when nothing fit.
        /// </param>
        public Rectangle Insert(int width, int height, LevelChoiceHeuristic method, out bool rotated)
        {
            rotated = false;

            if (width <= 0 || height <= 0)
                return new Rectangle();

            Rectangle newNode;
            int index = -1;

            switch (method)
            {
                case LevelChoiceHeuristic.LevelBottomLeft:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, out _, out _, out index, out rotated);
                    break;
                case LevelChoiceHeuristic.LevelMinWasteFit:
                    newNode = FindPositionForNewNodeMinWaste(width, height, out _, out _, out index, out rotated);
                    break;
                default:
                    return new Rectangle();
            }

            if (index == -1)
            {
                rotated = false;
                return new Rectangle();
            }

            // Perform the actual packing.
            AddSkylineLevel(index, newNode);
            usedSurfaceArea += (ulong)width * (ulong)height;
            usedRectangles.Add(newNode);

            return newNode;
        }

        /// <summary>
        /// Packs a whole set of rectangles at once using global best-fit selection: 
        /// At each step the rectangle + position pair with the best score across all still-unplaced rectangles is chosen.
        /// This can improve packing density (occupancy) compared with inserting rectangles in a fixed order,
        /// at the cost of extra scoring work.
        /// </summary>
        /// <param name="rects"> The rectangle sizes (width, height) to pack. </param>
        /// <param name="method"> The skyline level choice heuristic. </param>
        /// <returns>
        /// An array parallel to <paramref name="rects"/>: element i holds the placement of rects[i],
        /// or <c>null</c> if that rectangle could not be placed.
        /// </returns>
        public Rectangle?[] Insert(IReadOnlyList<(int Width, int Height)> rects, LevelChoiceHeuristic method) =>
            Insert(rects, method, out _);

        /// <summary>
        /// Packs a whole set of rectangles at once using global best-fit selection, additionally
        /// reporting for each rectangle whether its winning placement was rotated 90°.
        /// </summary>
        /// <param name="rects"> The rectangle sizes (width, height) to pack. </param>
        /// <param name="method"> The skyline level choice heuristic.  /// </param>
        /// <param name="rotated">
        /// An array parallel to <paramref name="rects"/>: element i is <c>true</c> when rects[i] was
        /// placed with its width/height swapped (a 90° rotation), <c>false</c> for upright or unplaced.
        /// </param>
        /// 
        /// <returns>
        /// An array parallel to <paramref name="rects"/>: element i holds the placement of rects[i],
        /// or <c>null</c> if that rectangle could not be placed.
        /// </returns>
        public Rectangle?[] Insert(IReadOnlyList<(int Width, int Height)> rects, 
                                   LevelChoiceHeuristic method, 
                                   out bool[] rotated)
        {
            Rectangle?[] result = new Rectangle?[rects.Count];
            rotated = new bool[rects.Count];
            bool[] placed = new bool[rects.Count];
            int remaining = rects.Count;

            while (remaining > 0)
            {
                int bestScore1 = int.MaxValue;
                int bestScore2 = int.MaxValue;
                int bestSkylineIndex = -1;
                int bestRectIndex = -1;
                Rectangle bestNode = new Rectangle();
                bool bestRotated = false;

                for (int i = 0; i < rects.Count; ++i)
                {
                    if (placed[i]) continue;
                    if (rects[i].Width <= 0 || rects[i].Height <= 0) continue; // degenerate, never placeable

                    Rectangle newNode;
                    int score1;
                    int score2;
                    int index;
                    bool rectRotated;

                    switch (method)
                    {
                        case LevelChoiceHeuristic.LevelBottomLeft:
                            newNode = FindPositionForNewNodeBottomLeft(rects[i].Width, rects[i].Height,
                                out score1, out score2, out index, out rectRotated);
                            break;
                        case LevelChoiceHeuristic.LevelMinWasteFit:
                            newNode = FindPositionForNewNodeMinWaste(rects[i].Width, rects[i].Height,
                                out score2, out score1, out index, out rectRotated);
                            break;
                        default:
                            continue;
                    }

                    if (newNode.Height == 0) continue; // does not fit anywhere right now

                    if (score1 < bestScore1 || (score1 == bestScore1 && score2 < bestScore2))
                    {
                        bestNode = newNode;
                        bestScore1 = score1;
                        bestScore2 = score2;
                        bestSkylineIndex = index;
                        bestRectIndex = i;
                        bestRotated = rectRotated;
                    }
                }

                if (bestRectIndex == -1)
                    break; // none of the remaining rectangles fit

                // Perform the actual packing.
                AddSkylineLevel(bestSkylineIndex, bestNode);
                usedSurfaceArea += (ulong)rects[bestRectIndex].Width * (ulong)rects[bestRectIndex].Height;
                usedRectangles.Add(bestNode);
                result[bestRectIndex] = bestNode;
                rotated[bestRectIndex] = bestRotated;
                placed[bestRectIndex] = true;
                remaining--;
            }

            return result;
        }

        /// <summary>
        /// Computes the placement a rectangle would get without modifying the bin, together with the
        /// heuristic scores (lower is better; score1 is the primary key, score2 the tie-breaker).
        /// Returns a Rectangle with Height == 0 if it does not fit, its dimensions are non-positive,
        /// or <paramref name="method"/> is unsupported. In these cases both scores are <see cref="int.MaxValue"/>.
        /// </summary>
        public Rectangle ScoreRectangle(int width, int height, 
                                        LevelChoiceHeuristic method, 
                                        out int score1, out int score2)
        {
            Rectangle newNode = new Rectangle();
            score1 = int.MaxValue;
            score2 = int.MaxValue;

            if (width <= 0 || height <= 0)
                return newNode;

            switch (method)
            {
                case LevelChoiceHeuristic.LevelBottomLeft:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, out score1, out score2, out _, out _);
                    break;
                case LevelChoiceHeuristic.LevelMinWasteFit:
                    newNode = FindPositionForNewNodeMinWaste(width, height, out score2, out score1, out _, out _);
                    break;
            }

            // Cannot fit the current rectangle.
            if (newNode.Height == 0)
            {
                score1 = int.MaxValue;
                score2 = int.MaxValue;
            }

            return newNode;
        }

        /// Computes the ratio of used surface area to the total bin area.
        public float Occupancy() =>
            binWidth > 0 && binHeight > 0 ? (float)usedSurfaceArea / ((long)binWidth * binHeight) : 0f;

        private Rectangle FindPositionForNewNodeBottomLeft(int width, int height, out int bestHeight,
            out int bestWidth, out int bestIndex, out bool rotated)
        {
            Rectangle bestNode = new Rectangle();

            bestHeight = int.MaxValue;
            bestIndex = -1;
            // Used to break ties if there are nodes at the same level. Then pick the narrowest one.
            bestWidth = int.MaxValue;
            rotated = false;

            for (int i = 0; i < skyLine.Count; ++i)
            {
                // Try the rectangle in its original (non-rotated) orientation.
                if (RectangleFits(i, width, height, out int y))
                {
                    if (y + height < bestHeight || (y + height == bestHeight && skyLine[i].Width < bestWidth))
                    {
                        bestHeight = y + height;
                        bestIndex = i;
                        bestWidth = skyLine[i].Width;
                        bestNode.X = skyLine[i].X;
                        bestNode.Y = y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        rotated = false;
                    }
                }

                if (allowRotations && RectangleFits(i, height, width, out y))
                {
                    if (y + width < bestHeight || (y + width == bestHeight && skyLine[i].Width < bestWidth))
                    {
                        bestHeight = y + width;
                        bestIndex = i;
                        bestWidth = skyLine[i].Width;
                        bestNode.X = skyLine[i].X;
                        bestNode.Y = y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        rotated = true;
                    }
                }
            }

            return bestNode;
        }

        private Rectangle FindPositionForNewNodeMinWaste(int width, int height, 
                                                         out int bestHeight, out int bestWastedArea, 
                                                         out int bestIndex, out bool rotated)
        {
            Rectangle bestNode = new Rectangle();
            bestHeight = int.MaxValue;
            bestWastedArea = int.MaxValue;
            bestIndex = -1;
            rotated = false;

            for (int i = 0; i < skyLine.Count; ++i)
            {
                // Try the rectangle in its original (non-rotated) orientation.
                if (RectangleFits(i, width, height, out int y, out int wastedArea))
                {
                    if (wastedArea < bestWastedArea || (wastedArea == bestWastedArea && y + height < bestHeight))
                    {
                        bestHeight = y + height;
                        bestWastedArea = wastedArea;
                        bestIndex = i;
                        bestNode.X = skyLine[i].X;
                        bestNode.Y = y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        rotated = false;
                    }
                }

                if (allowRotations && RectangleFits(i, height, width, out y, out wastedArea))
                {
                    if (wastedArea < bestWastedArea || (wastedArea == bestWastedArea && y + width < bestHeight))
                    {
                        bestHeight = y + width;
                        bestWastedArea = wastedArea;
                        bestIndex = i;
                        bestNode.X = skyLine[i].X;
                        bestNode.Y = y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        rotated = true;
                    }
                }
            }

            return bestNode;
        }

        /// Returns true if the rectangle of width x height fits when placed at the given skyline node,
        /// and outputs the y coordinate of the placement.
        private bool RectangleFits(int skylineNodeIndex, int width, int height, out int y)
        {
            int x = skyLine[skylineNodeIndex].X;
            if (x + width > binWidth)
            {
                y = 0;
                return false;
            }

            int widthLeft = width;
            int i = skylineNodeIndex;
            y = skyLine[skylineNodeIndex].Y;

            while (widthLeft > 0)
            {
                y = Math.Max(y, skyLine[i].Y);
                if (y + height > binHeight)
                    return false;
                widthLeft -= skyLine[i].Width;
                ++i;
            }

            return true;
        }

        /// Returns true if the rectangle fits, and additionally computes the wasted area that placing
        /// the rectangle here would produce.
        private bool RectangleFits(int skylineNodeIndex, int width, int height, out int y, out int wastedArea)
        {
            bool fits = RectangleFits(skylineNodeIndex, width, height, out y);

            if (fits)
                wastedArea = ComputeWastedArea(skylineNodeIndex, width, height, y);
            else
                wastedArea = 0;

            return fits;
        }

        /// Computes the amount of wasted area (in square units) that would be created when placing
        /// a rectangle of width x height at the given skyline node at level y.
        private int ComputeWastedArea(int skylineNodeIndex, int width, int height, int y)
        {
            int wastedArea = 0;
            int rectLeft = skyLine[skylineNodeIndex].X;
            int rectRight = rectLeft + width;

            for (; skylineNodeIndex < skyLine.Count && skyLine[skylineNodeIndex].X < rectRight; ++skylineNodeIndex)
            {
                if (skyLine[skylineNodeIndex].X >= rectRight ||
                    skyLine[skylineNodeIndex].X + skyLine[skylineNodeIndex].Width <= rectLeft)
                    break;

                int leftSide = skyLine[skylineNodeIndex].X;
                int rightSide = Math.Min(rectRight, leftSide + skyLine[skylineNodeIndex].Width);

                wastedArea += (rightSide - leftSide) * (y - skyLine[skylineNodeIndex].Y);
            }

            return wastedArea;
        }

        /// Adds a new skyline level for a placed rectangle, consuming any nodes it covers.
        private void AddSkylineLevel(int skylineNodeIndex, in Rectangle rect)
        {
            SkylineNode newNode = new SkylineNode
            {
                X = rect.X,
                Y = rect.Y + rect.Height,
                Width = rect.Width
            };
            skyLine.Insert(skylineNodeIndex, newNode);

            for (int i = skylineNodeIndex + 1; i < skyLine.Count; ++i)
            {
                if (skyLine[i].X >= skyLine[i - 1].X + skyLine[i - 1].Width)
                    break;

                int shrink = skyLine[i - 1].X + skyLine[i - 1].Width - skyLine[i].X;

                SkylineNode node = skyLine[i];
                node.X += shrink;
                node.Width -= shrink;
                skyLine[i] = node;

                if (skyLine[i].Width <= 0)
                {
                    skyLine.RemoveAt(i);
                    --i;
                }
                else
                {
                    break;
                }
            }

            MergeSkylines();
        }

        /// Merges adjacent skyline nodes that are at the same level.
        private void MergeSkylines()
        {
            for (int i = 0; i < skyLine.Count - 1; ++i)
            {
                if (skyLine[i].Y != skyLine[i + 1].Y) continue;

                SkylineNode node = skyLine[i];
                node.Width += skyLine[i + 1].Width;
                skyLine[i] = node;
                skyLine.RemoveAt(i + 1);
                --i;
            }
        }
    }
}
