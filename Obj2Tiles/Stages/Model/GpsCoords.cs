namespace Obj2Tiles.Stages.Model;

public class GpsCoords
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }

    public double[] ToEcef()
    {
        var lat = Latitude * Math.PI / 180;
        var lon = Longitude * Math.PI / 180;
        var alt = Altitude;

        const double a = 6378137;
        const double b = 6356752.3142;
        const double f = (a - b) / a;

        const double eSq = 2 * f - f * f;

        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var sinLon = Math.Sin(lon);
        var cosLon = Math.Cos(lon);

        var nu = a / Math.Sqrt(1 - eSq * sinLat * sinLat);

        var x = (nu + alt) * cosLat * cosLon;
        var y = (nu + alt) * cosLat * sinLon;
        var z = (nu * (1 - eSq) + alt) * sinLat;

        return new[] { x, y, z };
    }
}