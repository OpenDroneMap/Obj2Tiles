/*
    Based on the Public Domain SkylineBinPack.cpp source by Jukka Jylänki
    https://github.com/juj/RectangleBinPack/
*/

using Obj2Tiles.Library.Algos.Model;

namespace Obj2Tiles.Library.Algos
{
    /// <summary>
    /// Implements bin packing algorithms that use the SKYLINE data structure to store the bin contents.
    /// Ported from juj/RectangleBinPack SkylineBinPack (bottom-left placement heuristic with rotations).
    /// </summary>
    public class SkylineBinPack
    {
        public int binWidth = 0;
        public int binHeight = 0;
        public bool allowRotations;

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

            skyLine.Clear();
            skyLine.Add(new SkylineNode { X = 0, Y = 0, Width = width });
        }

        /// <summary>
        /// Inserts a single rectangle into the bin using the bottom-left heuristic, possibly rotated if
        /// <see cref="allowRotations"/> is set. If the rectangle does not fit, a Rectangle with Height == 0
        /// is returned (same convention as MaxRectanglesBinPack).
        /// </summary>
        /// <param name="rotated">
        /// Set to <c>true</c> when the winning placement swapped the requested width/height
        /// (a 90Â° rotation), <c>false</c> for an upright placement or when nothing fit.
        /// </param>
        public Rectangle Insert(int width, int height, out bool rotated)
        {
            rotated = false;

            if (width <= 0 || height <= 0)
                return new Rectangle();

            Rectangle newNode = FindPositionForNewNodeBottomLeft(width, height, out _, out _, out int index, out rotated);

            if (index == -1)
            {
                rotated = false;
                return new Rectangle();
            }

            // Perform the actual packing.
            AddSkylineLevel(index, newNode);
            usedSurfaceArea += (ulong)width * (ulong)height;

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
