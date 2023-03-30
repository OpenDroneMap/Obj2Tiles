/*
    Based on the Public Domain MaxRectanglesBinPack.cpp source by Jukka Jylänki
    https://github.com/juj/RectangleBinPack/
 
    Originally Ported to C# by Sven Magnus
    Updated to .Net 4.5 by Stefan Gordon
*/

using Obj2Tiles.Library.Algos.Model;
using Rectangle = Obj2Tiles.Library.Algos.Model.Rectangle;

namespace Obj2Tiles.Library.Algos
{
    public enum FreeRectangleChoiceHeuristic
    {
        RectangleBestShortSideFit, // -BSSF: Positions the Rectangle against the short side of a free Rectangle into which it fits the best.
        RectangleBestLongSideFit, // -BLSF: Positions the Rectangle against the long side of a free Rectangle into which it fits the best.
        RectangleBestAreaFit, // -BAF: Positions the Rectangle into the smallest free Rectangle into which it fits.
        RectangleBottomLeftRule, // -BL: Does the Tetris placement.
        RectangleContactPointRule // -CP: Choosest the placement where the Rectangle touches other Rectangles as much as possible.
    };

    public class MaxRectanglesBinPack
    {
        public int binWidth = 0;
        public int binHeight = 0;
        public bool allowRotations;

        public readonly List<Rectangle> usedRectangles = new();
        public readonly List<Rectangle> freeRectangles = new();


        public MaxRectanglesBinPack(int width, int height, bool rotations = true)
        {
            Init(width, height, rotations);
        }

        public void Init(int width, int height, bool rotations = true)
        {
            binWidth = width;
            binHeight = height;
            allowRotations = rotations;

            var n = new Rectangle
            {
                X = 0,
                Y = 0,
                Width = width,
                Height = height
            };

            usedRectangles.Clear();

            freeRectangles.Clear();
            freeRectangles.Add(n);
        }

        public Rectangle Insert(int width, int height, FreeRectangleChoiceHeuristic method)
        {
            var newNode = new Rectangle();
            var score1 = 0; // Unused in this function. We don't need to know the score after finding the position.
            var score2 = 0;
            newNode = method switch
            {
                FreeRectangleChoiceHeuristic.RectangleBestShortSideFit => FindPositionForNewNodeBestShortSideFit(width,
                    height, out score1, ref score2),
                FreeRectangleChoiceHeuristic.RectangleBottomLeftRule => FindPositionForNewNodeBottomLeft(width, height,
                    out score1, ref score2),
                FreeRectangleChoiceHeuristic.RectangleContactPointRule => FindPositionForNewNodeContactPoint(width,
                    height, out score1),
                FreeRectangleChoiceHeuristic.RectangleBestLongSideFit => FindPositionForNewNodeBestLongSideFit(width,
                    height, ref score2, out score1),
                FreeRectangleChoiceHeuristic.RectangleBestAreaFit => FindPositionForNewNodeBestAreaFit(width, height,
                    out score1, ref score2),
                _ => newNode
            };

            if (newNode.Height == 0)
                return newNode;

            var numRectangleanglesToProcess = freeRectangles.Count;
            for (var i = 0; i < numRectangleanglesToProcess; ++i)
            {
                if (SplitFreeNode(freeRectangles[i], ref newNode))
                {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectangleanglesToProcess;
                }
            }

            PruneFreeList();

            usedRectangles.Add(newNode);
            return newNode;
        }

        private void PlaceRectangle(Rectangle node)
        {
            var numRectangleanglesToProcess = freeRectangles.Count;
            for (var i = 0; i < numRectangleanglesToProcess; ++i)
            {
                if (SplitFreeNode(freeRectangles[i], ref node))
                {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectangleanglesToProcess;
                }
            }

            PruneFreeList();

            usedRectangles.Add(node);
        }

        public Rectangle ScoreRectangle(int width, int height, FreeRectangleChoiceHeuristic method, out int score1,
            out int score2)
        {
            var newNode = new Rectangle();
            score1 = int.MaxValue;
            score2 = int.MaxValue;
            switch (method)
            {
                case FreeRectangleChoiceHeuristic.RectangleBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, out score1, ref score2);
                    break;
                case FreeRectangleChoiceHeuristic.RectangleBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, out score1, ref score2);
                    break;
                case FreeRectangleChoiceHeuristic.RectangleContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, out score1);
                    score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
                    break;
                case FreeRectangleChoiceHeuristic.RectangleBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, out score1);
                    break;
                case FreeRectangleChoiceHeuristic.RectangleBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, out score1, ref score2);
                    break;
            }

            // Cannot fit the current Rectangleangle.
            if (newNode.Height == 0)
            {
                score1 = int.MaxValue;
                score2 = int.MaxValue;
            }

            return newNode;
        }

        /// Computes the ratio of used surface area.
        public float Occupancy()
        {
            ulong usedSurfaceArea = 0;
            for (var i = 0; i < usedRectangles.Count; ++i)
                usedSurfaceArea += (uint)usedRectangles[i].Width * (uint)usedRectangles[i].Height;

            return (float)usedSurfaceArea / (binWidth * binHeight);
        }

        private Rectangle FindPositionForNewNodeBottomLeft(int width, int height, out int bestY, ref int bestX)
        {
            var bestNode = new Rectangle();
            //memset(bestNode, 0, sizeof(Rectangle));

            bestY = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // Try to place the Rectangleangle in upright (non-flipped) orientation.
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var topSideY = freeRectangles[i].Y + height;
                    if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].X < bestX))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestY = topSideY;
                        bestX = freeRectangles[i].X;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var topSideY = freeRectangles[i].Y + width;
                    if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].X < bestX))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestY = topSideY;
                        bestX = freeRectangles[i].X;
                    }
                }
            }

            return bestNode;
        }

        private Rectangle FindPositionForNewNodeBestShortSideFit(int width, int height, out int bestShortSideFit,
            ref int bestLongSideFit)
        {
            var bestNode = new Rectangle();
            //memset(&bestNode, 0, sizeof(Rectangle));

            bestShortSideFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // Try to place the Rectangleangle in upright (non-flipped) orientation.
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit ||
                        (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var flippedLeftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
                    var flippedLeftoverVert = Math.Abs(freeRectangles[i].Height - width);
                    var flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                    var flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

                    if (flippedShortSideFit < bestShortSideFit || (flippedShortSideFit == bestShortSideFit &&
                                                                   flippedLongSideFit < bestLongSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = flippedShortSideFit;
                        bestLongSideFit = flippedLongSideFit;
                    }
                }
            }

            return bestNode;
        }

        private Rectangle FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit,
            out int bestLongSideFit)
        {
            var bestNode = new Rectangle();
            //memset(&bestNode, 0, sizeof(Rectangle));

            bestLongSideFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // Try to place the Rectangleangle in upright (non-flipped) orientation.
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit ||
                        (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - width);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit ||
                        (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }

            return bestNode;
        }

        private Rectangle FindPositionForNewNodeBestAreaFit(int width, int height, out int bestAreaFit,
            ref int bestShortSideFit)
        {
            var bestNode = new Rectangle();
            //memset(&bestNode, 0, sizeof(Rectangle));

            bestAreaFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                var areaFit = freeRectangles[i].Width * freeRectangles[i].Height - width * height;

                // Try to place the Rectangleangle in upright (non-flipped) orientation.
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - width);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }

            return bestNode;
        }

        /// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
        private static int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
        {
            if (i1end < i2start || i2end < i1start)
                return 0;
            return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
        }

        private int ContactPointScoreNode(int x, int y, int width, int height)
        {
            var score = 0;

            if (x == 0 || x + width == binWidth)
                score += height;
            if (y == 0 || y + height == binHeight)
                score += width;

            for (var i = 0; i < usedRectangles.Count; ++i)
            {
                if (usedRectangles[i].X == x + width || usedRectangles[i].X + usedRectangles[i].Width == x)
                    score += CommonIntervalLength(usedRectangles[i].Y, usedRectangles[i].Y + usedRectangles[i].Height,
                        y, y + height);
                if (usedRectangles[i].Y == y + height || usedRectangles[i].Y + usedRectangles[i].Height == y)
                    score += CommonIntervalLength(usedRectangles[i].X, usedRectangles[i].X + usedRectangles[i].Width, x,
                        x + width);
            }

            return score;
        }

        private Rectangle FindPositionForNewNodeContactPoint(int width, int height, out int bestContactScore)
        {
            var bestNode = new Rectangle();
            //memset(&bestNode, 0, sizeof(Rectangle));

            bestContactScore = -1;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // Try to place the Rectangleangle in upright (non-flipped) orientation.
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var score = ContactPointScoreNode(freeRectangles[i].X, freeRectangles[i].Y, width, height);
                    if (score > bestContactScore)
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestContactScore = score;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var score = ContactPointScoreNode(freeRectangles[i].X, freeRectangles[i].Y, height, width);
                    if (score > bestContactScore)
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestContactScore = score;
                    }
                }
            }

            return bestNode;
        }

        private bool SplitFreeNode(Rectangle freeNode, ref Rectangle usedNode)
        {
            // Test with SAT if the Rectangleangles even intersect.
            if (usedNode.X >= freeNode.X + freeNode.Width || usedNode.X + usedNode.Width <= freeNode.X ||
                usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
                return false;

            if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
            {
                // New node at the top side of the used node.
                if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
                {
                    var newNode = freeNode.Clone();
                    newNode.Height = usedNode.Y - newNode.Y;
                    freeRectangles.Add(newNode);
                }

                // New node at the bottom side of the used node.
                if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
                {
                    var newNode = freeNode.Clone();
                    newNode.Y = usedNode.Y + usedNode.Height;
                    newNode.Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height);
                    freeRectangles.Add(newNode);
                }
            }

            if (usedNode.Y < freeNode.Y + freeNode.Height && usedNode.Y + usedNode.Height > freeNode.Y)
            {
                // New node at the left side of the used node.
                if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
                {
                    var newNode = freeNode.Clone();
                    newNode.Width = usedNode.X - newNode.X;
                    freeRectangles.Add(newNode);
                }

                // New node at the right side of the used node.
                if (usedNode.X + usedNode.Width < freeNode.X + freeNode.Width)
                {
                    var newNode = freeNode.Clone();
                    newNode.X = usedNode.X + usedNode.Width;
                    newNode.Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width);
                    freeRectangles.Add(newNode);
                }
            }

            return true;
        }

        private void PruneFreeList()
        {
            var rectanglesToRemove = new SortedSet<int>();

            for (var i = 0; i < freeRectangles.Count; i++)
            {
                for (var j = i + 1; j < freeRectangles.Count; ++j)
                {
                    if (rectanglesToRemove.Contains(j) || rectanglesToRemove.Contains(i))
                    {
                        break;
                    }

                    if (freeRectangles[j].Contains(freeRectangles[i]))
                    {
                        lock (rectanglesToRemove)
                        {
                            rectanglesToRemove.Add(i);
                        }

                        break;
                    }

                    if (freeRectangles[i].Contains(freeRectangles[j]))
                    {
                        lock (rectanglesToRemove)
                        {
                            rectanglesToRemove.Add(j);
                        }
                    }
                }
            }

            foreach (var rectangleToRemove in rectanglesToRemove.Reverse())
            {
                freeRectangles.RemoveAt(rectangleToRemove);
            }
        }
    }
}