using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SilentWave.Obj2Gltf.Geom
{
    internal class PolygonPipeline
    {
        class Node
        {
            public Node(Int32 i, Single x, Single y)
            {
                Index = i;
                X = x;
                Y = y;
            }
            public Int32 Index { get; set; }

            public Single X { get; set; }

            public Single Y { get; set; }

            public Node Prev { get; set; }

            public Node Next { get; set; }

            public Single? Z { get; set; }

            public Node PrevZ { get; set; }

            public Node NextZ { get; set; }

            public Boolean Steiner { get; set; }
        }
        public static Int32[] Triangulate(IList<SVec2> positions, Int32[] holes)
        {
            var arr = new Single[positions.Count * 2];
            for (var i = 0; i < positions.Count; i++)
            {
                var pa = positions[i].ToArray();
                arr[2 * i] = pa[0];
                arr[2 * i + 1] = pa[1];
            }
            return Earcut(arr, holes, 2).ToArray();
        }

        private static Single SignedArea(Single[] data, Int32 start, Int32 end, Int32 dim)
        {
            Single sum = 0;
            for (Int32 i = start, j = end - dim; i < end; i += dim)
            {
                sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]);
                j = i;
            }
            return sum;
        }

        private static void RemoveNode(Node p)
        {
            p.Next.Prev = p.Prev;
            p.Prev.Next = p.Next;

            if (p.PrevZ != null) p.PrevZ.NextZ = p.NextZ;
            if (p.NextZ != null) p.NextZ.PrevZ = p.PrevZ;
        }

        private static Node InsertNode(Int32 index, Single x, Single y, Node last)
        {
            var p = new Node(index, x, y);
            if (last == null)
            {
                p.Prev = p;
                p.Next = p;
            }
            else
            {
                p.Next = last.Next;
                p.Prev = last;
                last.Next.Prev = p;
                last.Next = p;
            }
            return p;

        }
        /// <summary>
        /// check if two points are equal
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        private static Boolean Equal(Node p1, Node p2)
        {
            return p1.X == p2.X && p1.Y == p2.Y;
        }

        private static Boolean Intersects(Node p1, Node q1, Node p2, Node q2)
        {
            if ((Equal(p1, q1) && Equal(p2, q2)) || (Equal(p1, q2) && Equal(p2, q1)))
            {
                return true;
            }

            return (Area(p1, q1, p2) > 0) != (Area(p1, q1, q2) > 0) &&
                (Area(p2, q2, p1) > 0) != (Area(p2, q2, q1) > 0);
        }
        /// <summary>
        /// check if a polygon diagonal intersects any polygon segments
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static Boolean IntersectsPolygon(Node a, Node b)
        {
            var p = a;
            do
            {
                if (p.Index != a.Index && p.Next.Index != a.Index && p.Index != b.Index && p.Next.Index != b.Index &&
                    Intersects(p, p.Next, a, b)) return true;
                p = p.Next;
            } while (p != a);

            return false;
        }
        /// <summary>
        /// check if a polygon diagonal is locally inside the polygon
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static Boolean LocallyInside(Node a, Node b)
        {
            return Area(a.Prev, a, a.Next) < 0 ?
                Area(a, b, a.Next) >= 0 && Area(a, a.Prev, b) >= 0 :
                Area(a, b, a.Prev) < 0 || Area(a, a.Next, b) < 0;
        }

        private static Boolean MiddleInside(Node a, Node b)
        {
            var p = a;
            var inside = false;
            var px = (a.X + b.X) / 2;
            var py = (a.Y + b.Y) / 2;

            do
            {
                if ((p.Y > py) != (p.Next.Y > py) && (px < (p.Next.X - p.X) * (py - p.Y) / (p.Next.Y - p.Y) + p.X))
                {
                    inside = !inside;
                }
                p = p.Next;
            }
            while (p != a);

            return inside;
        }
        /// <summary>
        /// link two polygon vertices with a bridge; if the vertices belong to the same ring, it splits polygon into two;
        /// if one belongs to the outer ring and another to a hole, it merges it into a single ring
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static Node SplitPolygon(Node a, Node b)
        {
            var a2 = new Node(a.Index, a.X, a.Y);
            var b2 = new Node(b.Index, b.X, b.Y);
            var an = a.Next;
            var bp = b.Prev;

            a.Next = b;
            b.Prev = a;

            a2.Next = an;
            an.Prev = a2;

            b2.Next = a2;
            a2.Prev = b2;

            bp.Next = b2;
            b2.Prev = bp;

            return b2;
        }

        private static Boolean PointInTriangle(Single ax, Single ay, Single bx, Single by,
            Single cx, Single cy, Single px, Single py)
        {
            return (cx - px) * (ay - py) - (ax - px) * (cy - py) >= 0 &&
           (ax - px) * (by - py) - (bx - px) * (ay - py) >= 0 &&
           (bx - px) * (cy - py) - (cx - px) * (by - py) >= 0;
        }

        private static Node LinkedList(Single[] data, Int32 start, Int32 end, Int32 dim, Boolean clockWise)
        {
            Node last = null;
            if (clockWise == (SignedArea(data, start, end, dim) > 0))
            {
                for (var i = start; i < end; i += dim)
                {
                    last = InsertNode(i, data[i], data[i + 1], last);
                }
            }
            else
            {
                for (var i = end - dim; i >= start; i -= dim)
                {
                    last = InsertNode(i, data[i], data[i + 1], last);
                }
            }

            if (last != null && Equal(last, last.Next))
            {
                RemoveNode(last);
                last = last.Next;
            }

            return last;
        }

        private static Node GetLeftmost(Node start)
        {
            var p = start;
            var leftMost = start;

            do
            {
                if (p.X < leftMost.X) leftMost = p;
            } while (p != start);

            return leftMost;
        }
        // TODO:
        private static Node EliminateHoles(Single[] data, Int32[] holes, Node outerNode, Int32 dim)
        {
            //return outerNode;
            throw new NotImplementedException();
        }

        private static Boolean IsEarHashed(Node ear, Single minX, Single minY, Single size)
        {
            var a = ear.Prev;
            var b = ear;
            var c = ear.Next;

            if (Area(a, b, c) >= 0) return false; // reflex, can't be an ear

            // triangle bbox; min & max are calculated like this for speed
            var minTX = a.X < b.X ? (a.X < c.X ? a.X : c.X) : (b.X < c.X ? b.X : c.X);
            var minTY = a.Y < b.Y ? (a.Y < c.Y ? a.Y : c.Y) : (b.Y < c.Y ? b.Y : c.Y);
            var maxTX = a.X > b.X ? (a.X > c.X ? a.X : c.X) : (b.X > c.X ? b.X : c.X);
            var maxTY = a.Y > b.Y ? (a.Y > c.Y ? a.Y : c.Y) : (b.Y > c.Y ? b.Y : c.Y);

            // z-order range for the current triangle bbox;
            var minZ = ZOrder(minTX, minTY, minX, minY, size);
            var maxZ = ZOrder(maxTX, maxTY, minX, minY, size);

            // first look for points inside the triangle in increasing z-order
            var p = ear.NextZ;

            while (p != null && p.Z <= maxZ)
            {
                if (p != ear.Prev && p != ear.Next &&
                    PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) &&
                    Area(p.Prev, p, p.Next) >= 0) return false;
                p = p.NextZ;
            }

            // then look for points in decreasing z-order
            p = ear.PrevZ;

            while (p != null && p.Z >= minZ)
            {
                if (p != ear.Prev && p != ear.Next &&
                    PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) &&
                    Area(p.Prev, p, p.Next) >= 0) return false;
                p = p.PrevZ;
            }
            return true;
        }
        /// <summary>
        /// check whether a polygon node forms a valid ear with adjacent nodes
        /// </summary>
        /// <param name="ear"></param>
        /// <returns></returns>
        private static Boolean IsEar(Node ear)
        {
            var a = ear.Prev;
            var b = ear;
            var c = ear.Next;

            if (Area(a, b, c) >= 0) return false;  // reflex, can't be an ear

            // now make sure we don't have other points inside the potential ear
            var p = ear.Next.Next;

            while (p != ear.Prev)
            {
                if (PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0)
                {
                    return false;
                }
                p = p.Next;
            }
            return true;
        }

        private static Int32 ZOrder(Single x0, Single y0, Single minX, Single minY, Single size)
        {
            var x = (Int32)(32767 * (x0 - minX) / size);
            var y = (Int32)(32767 * (y0 - minY) / size);

            x = (x | (x << 8)) & 0x00FF00FF;
            x = (x | (x << 4)) & 0x0F0F0F0F;
            x = (x | (x << 2)) & 0x33333333;
            x = (x | (x << 1)) & 0x55555555;

            y = (y | (y << 8)) & 0x00FF00FF;
            y = (y | (y << 4)) & 0x0F0F0F0F;
            y = (y | (y << 2)) & 0x33333333;
            y = (y | (y << 1)) & 0x55555555;

            return x | (y << 1);
        }

        private static Single Area(Node p, Node q, Node r)
        {
            return (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
        }

        private static Node FilterPoints(Node start, Node end = null)
        {
            if (start == null) return start;
            if (end == null) end = start;

            var p = start;
            Boolean again;
            do
            {
                again = false;
                if (!p.Steiner && (Equal(p, p.Next) || Area(p.Prev, p, p.Next) == 0))
                {
                    RemoveNode(p);
                    p = end = p.Prev;
                    if (p == p.Next)
                    {
                        return null;
                    }
                    again = true;
                }
                else
                {
                    p = p.Next;
                }
            } while (again || p != end);

            return end;
        }

        private static Node CureLocalIntersections(Node start, List<Int32> triangles, Int32 dim)
        {
            var p = start;
            do
            {
                var a = p.Prev;
                var b = p.Next.Next;

                if (!Equal(a, b) && Intersects(a, p, p.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
                {
                    triangles.Add(a.Index / dim);
                    triangles.Add(p.Index / dim);
                    triangles.Add(b.Index / dim);

                    // remove two nodes involved
                    RemoveNode(p);
                    RemoveNode(p.Next);

                    p = start = b;
                }
                p = p.Next;
            } while (p != start);

            return p;
        }

        private static Boolean IsValidDiagonal(Node a, Node b)
        {
            return a.Next.Index != b.Index && a.Prev.Index != b.Index && !IntersectsPolygon(a, b) &&
                LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b);
        }
        /// <summary>
        /// try splitting polygon into two and triangulate them independently
        /// </summary>
        /// <param name="start"></param>
        /// <param name="triangles"></param>
        /// <param name="dim"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="size"></param>
        private static void SplitEarcut(Node start, List<Int32> triangles, Int32 dim, Single minX, Single minY, Single size)
        {
            // look for a valid diagonal that divides the polygon into two
            var a = start;

            do
            {
                var b = a.Next.Next;
                while (b != a.Prev)
                {
                    if (a.Index != b.Index && IsValidDiagonal(a, b))
                    {
                        // split the polygon in two by the diagonal
                        var c = SplitPolygon(a, b);

                        // filter colinear points around the cuts
                        a = FilterPoints(a, a.Next);
                        c = FilterPoints(c, c.Next);

                        // run earcut on each half
                        EarcutLinked(a, triangles, dim, minX, minY, size);
                        EarcutLinked(c, triangles, dim, minX, minY, size);
                        return;

                    }
                    b = b.Next;
                }
                a = a.Next;
            } while (a != start);
        }

        private static void IndexCurve(Node start, Single minX, Single minY, Single size)
        {
            var p = start;
            do
            {
                if (p.Z == null)
                {
                    p.Z = ZOrder(p.X, p.Y, minX, minY, size);
                    p.PrevZ = p.Prev;
                    p.NextZ = p.Next;
                    p = p.Next;
                }

            } while (p != start);

            p.PrevZ.NextZ = null;
            p.PrevZ = null;

            SortLinked(p);
        }

        private static Node SortLinked(Node list)
        {
            Int32 inSize = 1;
            Int32 numMerges;
            do
            {
                var p = list;
                list = null;
                Node tail = null;
                numMerges = 0;

                while (p != null)
                {
                    numMerges++;
                    var q = p;
                    var pSize = 0;
                    for (var i = 0; i < inSize; i++)
                    {
                        pSize++;
                        q = q.NextZ;
                        if (q == null) break;
                    }

                    var qSize = inSize;

                    while (pSize > 0 || (qSize > 0 && q != null))
                    {
                        Node e;
                        if (pSize == 0)
                        {
                            e = q;
                            q = q.NextZ;
                            qSize--;
                        }
                        else if (qSize == 0 || q == null)
                        {
                            e = p;
                            p = p.NextZ;
                            pSize--;
                        }
                        else if (p.Z <= q.Z)
                        {
                            e = p;
                            p = p.NextZ;
                            pSize--;
                        }
                        else
                        {
                            e = q;
                            q = q.NextZ;
                            qSize--;
                        }

                        if (tail != null) tail.NextZ = e;
                        else list = e;

                        e.PrevZ = tail;
                        tail = e;
                    }

                    p = q;
                }

                tail.NextZ = null;
                inSize *= 2;
            } while (numMerges > 1);

            return list;
        }

        /// <summary>
        /// // main ear slicing loop which triangulates a polygon (given as a linked list)
        /// </summary>
        /// <param name="ear"></param>
        /// <param name="triangles"></param>
        /// <param name="dim"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="size"></param>
        /// <param name="pass"></param>
        private static void EarcutLinked(Node ear, List<Int32> triangles,
            Int32 dim, Single minX, Single minY, Single size, Int32 pass = 0)
        {
            if (ear == null) return;

            var sized = size > 0;

            // interlink polygon nodes in z-order
            if (pass == 0 && sized)
            {
                IndexCurve(ear, minX, minY, size);
            }

            Node stop = ear;

            // iterate through ears, slicing them one by one
            while (ear.Prev != ear.Next)
            {
                var prev = ear.Prev;
                var next = ear.Next;

                if (sized ? IsEarHashed(ear, minX, minY, size) : IsEar(ear))
                {
                    // cut off the triangle
                    triangles.Add(prev.Index / dim);
                    triangles.Add(ear.Index / dim);
                    triangles.Add(next.Index / dim);

                    RemoveNode(ear);

                    // skipping the next vertice leads to less sliver triangles
                    ear = next.Next;
                    stop = next.Next;

                    continue;
                }

                ear = next;

                // if we looped through the whole remaining polygon and can't find any more ears
                if (ear == stop)
                {
                    // try filtering points and slicing again
                    if (pass == 0)
                    {
                        EarcutLinked(FilterPoints(ear), triangles, dim, minX, minY, size, 1);
                    }
                    else if (pass == 1)
                    {
                        ear = CureLocalIntersections(ear, triangles, dim);
                        EarcutLinked(ear, triangles, dim, minX, minY, size, 2);
                    }
                    else if (pass == 2)
                    {
                        SplitEarcut(ear, triangles, dim, minX, minY, size);
                    }
                    break;
                }
            }

        }

        private static List<Int32> Earcut(Single[] data, Int32[] holes, Int32 dim)
        {
            var hasHoles = holes != null && holes.Length > 0;
            var outerLen = hasHoles ? holes[0] * dim : data.Length;
            var outerNode = LinkedList(data, 0, outerLen, dim, true);

            var triangles = new List<Int32>();

            if (outerNode == null) return triangles;

            if (hasHoles)
            {
                outerNode = EliminateHoles(data, holes, outerNode, dim);
            }

            Single minX = 0, minY = 0, size = 0;

            // if the shape is not too simple, we'll use z-order curve hash later; calculate polygon bbox
            if (data.Length > 80 * dim)
            {
                Single maxX;
                minX = maxX = data[0];
                Single maxY;
                minY = maxY = data[1];

                for (var i = dim; i < outerLen; i += dim)
                {
                    var x = data[i];
                    var y = data[i + 1];
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

                // minX, minY and size are later used to transform coords into integers for z-order calculation
                size = new[] { maxX - minX, maxY - minY }.Max();
            }

            EarcutLinked(outerNode, triangles, dim, minX, minY, size);

            return triangles;

        }
    }
}
