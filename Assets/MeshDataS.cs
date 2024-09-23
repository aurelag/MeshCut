using System.Linq;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

[BurstCompile]
public struct MeshDataS
{
    public NativeList<Vector3> points;
    public NativeList<int> triangles;

    private NativeList<Vector3> intersections;
    private NativeList<int> intersectionIndexes;
    private Vector3 camForward;

    public MeshDataS(in Vector3 camForward, Allocator allocator)
    {
        points = new NativeList<Vector3>(allocator);
        triangles = new NativeList<int>(allocator);
        intersections = new NativeList<Vector3>(allocator);
        intersectionIndexes = new NativeList<int>(allocator);

        this.camForward = camForward;
    }

    public void AddRange(in Vector3 point0, in Vector3 point1, in Vector3 point2)
    {
        points.Add(point0);
        points.Add(point1);
        points.Add(point2);
        AddPointsToTriangle();
    }

    public void AddAlonePoint(in Vector3 alone, in Vector3 firstIntersection, in Vector3 secondIntersection, bool isFacing)
    {
        points.Add(alone);
        points.Add(firstIntersection);
        points.Add(secondIntersection);
        bool thisFaceFacing = isFaceTowardCamera(alone, firstIntersection, secondIntersection);
        AddPointsToTriangle(isFacing, thisFaceFacing);
        intersections.Add(firstIntersection);
        intersections.Add(secondIntersection);
        intersectionIndexes.Add(points.Length - 2);
        intersectionIndexes.Add(points.Length - 1);
    }

    public void AddTogetherPoints(in Vector3 first, in Vector3 second, in Vector3 firstIntersection, in Vector3 secondIntersection, bool isFacing)
    {
        points.Add(firstIntersection);
        points.Add(first);
        points.Add(second);

        bool thisFaceFacing = isFaceTowardCamera(firstIntersection, first, second);
        AddPointsToTriangle(isFacing, thisFaceFacing);
        points.Add(firstIntersection);
        points.Add(secondIntersection);
        points.Add(second);
        bool thisFaceFacing2 = isFaceTowardCamera(firstIntersection, secondIntersection, second);
        AddPointsToTriangle(isFacing, thisFaceFacing2);
        intersections.Add(firstIntersection);
        intersections.Add(secondIntersection);
        intersectionIndexes.Add(points.Length - 3);
        intersectionIndexes.Add(points.Length - 2);
    }

    public void ComputeIntersectionTriangles(bool isFacingCam)
    {
        Vector3 barycenter = Vector3.zero;
        foreach (Vector3 intersection in intersections)
        {
            barycenter += intersection;
        }
        barycenter /= intersections.Length;
        points.Add(barycenter);
        int barycenterIndex = points.Length - 1;

        for (int i = 0; i < intersectionIndexes.Length; i += 2)
        {
            bool isFacing = isFaceTowardCamera(points[intersectionIndexes[i]], points[barycenterIndex], points[intersectionIndexes[i + 1]]);
            if (isFacing == isFacingCam)
            {
                triangles.Add(intersectionIndexes[i]);
                triangles.Add(barycenterIndex);
                triangles.Add(intersectionIndexes[i + 1]);
            }
            else
            {
                triangles.Add(intersectionIndexes[i]);
                triangles.Add(intersectionIndexes[i + 1]);
                triangles.Add(barycenterIndex);
            }
        }
    }

    private void AddPointsToTriangle()
    {
        triangles.Add(points.Length - 3);
        triangles.Add(points.Length - 2);
        triangles.Add(points.Length - 1);
    }

    private void AddPointsToTriangle(bool previousFaceFacing, bool thisFaceFacing)
    {
        //add calculus of triangle order
        if (previousFaceFacing == thisFaceFacing)
        {
            triangles.Add(points.Length - 3);
            triangles.Add(points.Length - 2);
            triangles.Add(points.Length - 1);
        }
        else
        {
            triangles.Add(points.Length - 3);
            triangles.Add(points.Length - 1);
            triangles.Add(points.Length - 2);
        }
    }

    bool isFaceTowardCamera(in Vector3 pos0, in Vector3 pos1, in Vector3 pos2)
    {
        Vector3 side1 = pos1 - pos0;
        Vector3 side2 = pos2 - pos0;

        Vector3 cross = Vector3.Cross(side1, side2);

        return Vector3.Dot(cross, camForward) > 0;
    }
}

