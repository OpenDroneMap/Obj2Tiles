namespace Obj2Tiles.Stages.Model;

public class GpsCoords
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Scale { get; set; }
    public bool YUpToZUp { get; set; }
    public GpsCoords(double latitude, double longitude, double altitude, double scale, bool yUpToZUp)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
        Scale = scale;
        YUpToZUp = yUpToZUp;
    }

    public GpsCoords()
    {
        Latitude = 0;
        Longitude = 0;
        Altitude = 0;
        Scale = 1;
        YUpToZUp = true;
    }

    public double[] ToEcefTransform()
    {
        var s = Scale;
        var lat = Latitude * Math.PI / 180;
        var lon = Longitude * Math.PI / 180;
        var alt = Altitude;

        var a = 6378137.0 / s;
        var b = 6356752.3142 / s;
        var f = (a - b) / a;

        var eSq = 2 * f - f * f;

        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var sinLon = Math.Sin(lon);
        var cosLon = Math.Cos(lon);

        var nu = a / Math.Sqrt(1 - eSq * sinLat * sinLat);

        var x = (nu + alt) * cosLat * cosLon;
        var y = (nu + alt) * cosLat * sinLon;
        var z = (nu * (1 - eSq) + alt) * sinLat;

        var xr = -sinLon;
        var yr = cosLon;
        var zr = 0;

        var xe = -cosLon * sinLat;
        var ye = -sinLon * sinLat;
        var ze = cosLat;

        var xs = cosLat * cosLon;
        var ys = cosLat * sinLon;
        var zs = sinLat;

        var res = new[]
        {
            xr, xe, xs, x,
            yr, ye, ys, y,
            zr, ze, zs, z,
            0, 0, 0, 1
        };

        var scale = new double[]
        {
            s, 0, 0, 0,
            0, s, 0, 0,
            0, 0, s, 0,
            0, 0, 0, 1
        };

        var mult = res;
        if (YUpToZUp)
        {
            mult = MultiplyMatrix(res, rot);
        }
        return MultiplyMatrix(ConvertToColumnMajorOrder(mult), scale);
    }

    public static readonly double[] rot = {
        1, 0, 0, 0,
        0, 0, 1, 0,
        0, -1, 0, 0,
        0, 0, 0, 1
    };

    public static double[] ConvertToColumnMajorOrder(double[] m)
    {
        var res = new double[16];

        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                res[j * 4 + i] = m[i * 4 + j];
            }
        }

        return res;
    }

    public static double[] MultiplyMatrix(double[] m1, double[] m2)
    {
        var res = new double[16];

        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                res[i * 4 + j] = 0;
                for (var k = 0; k < 4; k++)
                {
                    res[i * 4 + j] += m1[i * 4 + k] * m2[k * 4 + j];
                }
            }
        }

        return res;
    }


    public override string ToString()
    {
        return $"{Latitude}, {Longitude}, {Altitude}, {Scale}";
    }
}