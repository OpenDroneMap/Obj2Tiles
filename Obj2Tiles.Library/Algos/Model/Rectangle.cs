using System.ComponentModel;

namespace Obj2Tiles.Library.Algos.Model;

public class Rectangle : IEquatable<Rectangle>
{
    public Rectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rectangle()
    {
            
    }
        
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
        
    public bool Contains(Rectangle rect) =>
        (X <= rect.X) && (rect.X + rect.Width <= X + Width) &&
        (Y <= rect.Y) && (rect.Y + rect.Height <= Y + Height);

    [Browsable(false)]
    public int Left => X;

    [Browsable(false)]
    public int Top => Y;

    [Browsable(false)]
    public int Right => unchecked(X + Width);

    [Browsable(false)]
    public int Bottom => unchecked(Y + Height);
        
    public override string ToString() => $"{{X={X},Y={Y},Width={Width},Height={Height}}}";


    public bool Equals(Rectangle? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Rectangle)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Width, Height);
    }

    public Rectangle Clone()
    {
        return new Rectangle { X = X, Y = Y, Width = Width, Height = Height };
    }
}