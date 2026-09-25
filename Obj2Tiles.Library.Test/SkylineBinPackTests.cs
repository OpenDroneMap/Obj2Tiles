using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Library.Algos;
using Shouldly;

namespace Obj2Tiles.Library.Test;

public class SkylineBinPackTests
{
    [Test]
    public void Insert_RandomRects_PlacementsNeverOverlapAndStayInBounds()
    {
        var rnd = new Random(42);
        for (int trial = 0; trial < 20; trial++)
        {
            var packer = new SkylineBinPack(256, 256, true);
            var placed = new List<Algos.Model.Rectangle>();
            for (int i = 0; i < 60; i++)
            {
                var rect = packer.Insert(rnd.Next(1, 60), rnd.Next(1, 60), out bool rotated);
                if (rotated)
                    (rect.Width > 0 && rect.Height > 0).ShouldBeTrue("rotated placements report swapped dims");
                if (rect.Height == 0)
                    continue; // did not fit

                (rect.X >= 0 && rect.Y >= 0 &&
                 rect.X + rect.Width <= 256 && rect.Y + rect.Height <= 256)
                    .ShouldBeTrue($"placement {rect} out of bounds (trial {trial})");

                foreach (var other in placed)
                {
                    bool overlap = rect.X < other.X + other.Width && other.X < rect.X + rect.Width &&
                                   rect.Y < other.Y + other.Height && other.Y < rect.Y + rect.Height;
                    overlap.ShouldBeFalse(
                        $"overlap between ({rect.X},{rect.Y},{rect.Width},{rect.Height}) and " +
                        $"({other.X},{other.Y},{other.Width},{other.Height}) in trial {trial}");
                }

                placed.Add(rect);
            }
        }
    }

    [Test]
    public void Insert_OverfullBin_ReportsUnplacedWithZeroHeight()
    {
        var packer = new SkylineBinPack(16, 16, false);
        packer.Insert(16, 16, out _).Height.ShouldNotBe(0);
        packer.Insert(16, 16, out bool rotatedSecond).Height.ShouldBe(0);
        rotatedSecond.ShouldBeFalse();
    }
}
