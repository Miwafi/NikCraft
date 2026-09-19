using OpenTK.Mathematics;

namespace NikCraft.Core;

/// <summary>View frustum extracted from a view-projection matrix, used for chunk culling.</summary>
public sealed class Frustum
{
    // Plane order: left, right, bottom, top, near, far. Stored as (normalX, normalY, normalZ, distance).
    private readonly Vector4[] _planes = new Vector4[6];

    public void Update(Matrix4 viewProjection)
    {
        // Gribb/Hartmann plane extraction needs the clip-space rows of the matrix the shader sees.
        // The uploaded matrix is this one transposed, so those rows are this matrix's columns.
        _planes[0] = Normalize(viewProjection.Column3 + viewProjection.Column0);
        _planes[1] = Normalize(viewProjection.Column3 - viewProjection.Column0);
        _planes[2] = Normalize(viewProjection.Column3 + viewProjection.Column1);
        _planes[3] = Normalize(viewProjection.Column3 - viewProjection.Column1);
        _planes[4] = Normalize(viewProjection.Column3 + viewProjection.Column2);
        _planes[5] = Normalize(viewProjection.Column3 - viewProjection.Column2);
    }

    private static Vector4 Normalize(Vector4 plane)
    {
        float length = new Vector3(plane.X, plane.Y, plane.Z).Length;
        if (length <= 0.00001f)
        {
            return plane;
        }

        return plane / length;
    }

    /// <summary>Conservative test: returns false only when the box is fully outside at least one plane.</summary>
    public bool IntersectsAabb(Vector3 min, Vector3 max)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector4 plane = _planes[i];
            Vector3 positive = new(
                plane.X >= 0 ? max.X : min.X,
                plane.Y >= 0 ? max.Y : min.Y,
                plane.Z >= 0 ? max.Z : min.Z);

            if (Vector3.Dot(new Vector3(plane.X, plane.Y, plane.Z), positive) + plane.W < 0f)
            {
                return false;
            }
        }

        return true;
    }
}
