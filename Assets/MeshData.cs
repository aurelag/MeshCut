using System.Collections.Generic;
using UnityEngine;

class MeshData
{
    public List<Vector3> points;
    public List<int> triangles;
    private List<Vector3> intersections;
    private List<int> intersectionIndexes;

    private Transform camTransform;

    public MeshData()
    {
        points = new List<Vector3>();
        triangles = new List<int>();
        intersections = new List<Vector3>();
        intersectionIndexes = new List<int>();
        camTransform = Camera.main.transform;
    }

    public void AddRange(IEnumerable<Vector3> points)
    {
        this.points.AddRange(points);
        AddPointsToTriangle();
    }

    public void AddAlonePoint(in Vector3 alone, in Vector3 firstIntersection, in Vector3 secondIntersection, bool isFacing)
    {
        points.AddRange(new Vector3[] { alone, firstIntersection, secondIntersection });
        bool thisFaceFacing = isFaceTowardCamera(alone, firstIntersection, secondIntersection);
        AddPointsToTriangle(isFacing, thisFaceFacing);
        intersections.Add(firstIntersection);
        intersections.Add(secondIntersection);
        intersectionIndexes.Add(points.Count - 2);
        intersectionIndexes.Add(points.Count - 1);
    }

    public void AddTogetherPoints(in Vector3 first, in Vector3 second, in Vector3 firstIntersection, in Vector3 secondIntersection, bool isFacing)
    {
        points.AddRange(new Vector3[] { firstIntersection, first, second });
        
        bool thisFaceFacing = isFaceTowardCamera(firstIntersection, first, second);
        AddPointsToTriangle(isFacing, thisFaceFacing);
        points.AddRange(new Vector3[] { firstIntersection, secondIntersection, second });
        bool thisFaceFacing2 = isFaceTowardCamera(firstIntersection, secondIntersection, second);
        AddPointsToTriangle(isFacing, thisFaceFacing2);
        intersections.Add(firstIntersection);
        intersections.Add(secondIntersection);
        intersectionIndexes.Add(points.Count - 3);
        intersectionIndexes.Add(points.Count - 2);
    }

    public void ComputeIntersectionTriangles(bool isFacingCam)
    {
        Vector3 barycenter = Vector3.zero;
        foreach (Vector3 intersection in intersections)
        {
            barycenter += intersection;
        }
        barycenter /= intersections.Count;
        points.Add(barycenter);
        int barycenterIndex = points.Count - 1;

        for (int i = 0; i < intersectionIndexes.Count; i+=2)
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
        triangles.AddRange(new int[] { points.Count - 3, points.Count - 2, points.Count - 1 });
    }

    private void AddPointsToTriangle(bool previousFaceFacing, bool thisFaceFacing)
    {
        //add calculus of triangle order
        if(previousFaceFacing == thisFaceFacing) 
            triangles.AddRange(new int[] { points.Count - 3, points.Count - 2, points.Count - 1 });
        else
            triangles.AddRange(new int[] { points.Count - 3, points.Count - 1, points.Count - 2 });
    }

    bool isFaceTowardCamera(params Vector3[] pointsPositions)
    {
        Vector3 side1 = pointsPositions[1] - pointsPositions[0];
        Vector3 side2 = pointsPositions[2] - pointsPositions[0];

        Vector3 cross = Vector3.Cross(side1, side2);

        return Vector3.Dot(cross, camTransform.forward) > 0;
    }
}
