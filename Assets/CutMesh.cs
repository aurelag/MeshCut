using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

public class CutMesh : MonoBehaviour
{
    [SerializeField] private MeshFilter cutMesh;
    [SerializeField] private MeshFilter mesh1;
    [SerializeField] private MeshFilter mesh2;

    private MeshData positiveMeshData;
    private MeshData negativeMeshData;
    private Transform camTransform;
    private Vector3 camForward;
    private List<Vector3> vertices;


    static readonly ProfilerMarker s_perfMarker = new ProfilerMarker("CutMeshButton");
    static readonly ProfilerMarker s_perfMarkerLoop = new ProfilerMarker("CutMeshButtonLoop");
    static readonly ProfilerMarker s_perfMarkerInit = new ProfilerMarker("CutMeshButtonInit");
    static readonly ProfilerMarker s_perfMarkerIntersection = new ProfilerMarker("CutMeshButtonIntersection");
    static readonly ProfilerMarker s_perfMarkerAddRange = new ProfilerMarker("CutMeshButtonAddRange");
    static readonly ProfilerMarker s_perfMarkerJobInit = new ProfilerMarker("CutMeshButtonMeshDataSInit");
    static readonly ProfilerMarker s_perfMarkerDataArray = new ProfilerMarker("CutMeshButtonMeshDataArray");
    static readonly ProfilerMarker s_perfMarkerVertices = new ProfilerMarker("CutMeshButtonMeshSchedule");

    private void Awake()
    {
        positiveMeshData = new MeshData();
        negativeMeshData = new MeshData();
        camTransform = Camera.main.transform;
        vertices = new List<Vector3>();
    }


    public void CutMeshButton()
    {
        s_perfMarker.Begin();

        s_perfMarkerInit.Begin();
        camForward = camTransform.forward;
        Plane plane = new Plane(transform.up, transform.position);
        cutMesh.mesh.GetVertices(vertices);
        int[] triangles = cutMesh.mesh.triangles;
        Vector3 meshPos = cutMesh.transform.position;
        int triangleCount = triangles.Length;
        positiveMeshData.InitForward();
        negativeMeshData.InitForward();
        s_perfMarkerInit.End();

        s_perfMarkerLoop.Begin();
        for (int i = 0; i < triangleCount; i+=3)
        {
            //we go 3 by 3 for triangles
            bool side1 = plane.GetSide(vertices[triangles[i]] + meshPos);
            bool side2 = plane.GetSide(vertices[triangles[i+1]] + meshPos);
            bool side3 = plane.GetSide(vertices[triangles[i+2]] + meshPos);
            if (!(side1 == side2 && side2 == side3))
            {
                s_perfMarkerIntersection.Begin();
                //all the vertices are not on the same side, meaning the triangle is cut by the plane
                CreateIntersections(plane, vertices, triangles, i, new bool[] { side1, side2, side3 }, 
                    positiveMeshData, negativeMeshData);
                s_perfMarkerIntersection.End();
            }
            else
            {
                s_perfMarkerAddRange.Begin();
                //vertices are all on the same side, so we just add them to their respective meshes.
                if (side1)
                {
                    positiveMeshData.AddRange(new Vector3[] { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] });
                }
                else
                {
                    negativeMeshData.AddRange(new Vector3[] { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] });
                }
                s_perfMarkerAddRange.End();
            }
        }
        s_perfMarkerLoop.End();

        bool ispositiveFacingCamera = Vector3.Dot(transform.up * 1e-2f, camForward) > 0;

        positiveMeshData.ComputeIntersectionTriangles(!ispositiveFacingCamera);
        negativeMeshData.ComputeIntersectionTriangles(ispositiveFacingCamera);

        Mesh mesh1Data = new Mesh();
        mesh1Data.vertices = positiveMeshData.points.ToArray();
        mesh1Data.triangles = positiveMeshData.triangles.ToArray();
        mesh1.mesh = mesh1Data;

        Mesh mesh2Data = new Mesh();
        mesh2Data.vertices = negativeMeshData.points.ToArray();
        mesh2Data.triangles = negativeMeshData.triangles.ToArray();
        mesh2.mesh = mesh2Data;

        s_perfMarker.End();
    }

    public void CutMeshButtonJob()
    {
        Debug.Log("boop");
        s_perfMarker.Begin();

        s_perfMarkerInit.Begin();
        s_perfMarkerDataArray.Begin();
        var dataArray = Mesh.AcquireReadOnlyMeshData(cutMesh.mesh);
        var writableDataArray = Mesh.AllocateWritableMeshData(2);
        s_perfMarkerDataArray.End();

        CutMeshJob cutMeshJob = new CutMeshJob
        {
            vertexCount = cutMesh.mesh.vertexCount,
            meshDataArray = dataArray,
            plane = new Plane(transform.up, transform.position),
            forward = camTransform.forward,
            meshPos = cutMesh.transform.position,
            transformUp = transform.up,
            writableMeshDataArray = writableDataArray,
        };
        
        s_perfMarkerInit.End();
        s_perfMarkerVertices.Begin();
        var handle = cutMeshJob.Schedule();
        s_perfMarkerVertices.End();
        handle.Complete();        

        Mesh.ApplyAndDisposeWritableMeshData(writableDataArray, new Mesh[] { mesh1.mesh, mesh2.mesh });
        dataArray.Dispose();
        s_perfMarker.End();
    }

    [BurstCompile]
    public struct CutMeshJob : IJob
    {
        [ReadOnly] public int vertexCount;
        [ReadOnly] public Mesh.MeshDataArray meshDataArray;
        [ReadOnly] public Plane plane;
        [ReadOnly] public Vector3 forward;
        [ReadOnly] public Vector3 meshPos;
        [ReadOnly] public Vector3 transformUp;

        public Mesh.MeshDataArray writableMeshDataArray;

        public void Execute()
        {
            NativeArray<Vector3> vertices = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
            meshDataArray[0].GetVertices(vertices);

            NativeArray<int> triangles = new NativeArray<int>(meshDataArray[0].GetSubMesh(0).indexCount, Allocator.Temp);
            meshDataArray[0].GetIndices(triangles, 0);

            MeshDataS positiveMesh = new MeshDataS(forward, Allocator.Temp);
            MeshDataS negativeMesh = new MeshDataS(forward, Allocator.Temp);

            int triangleCount = triangles.Length;
            for (int i = 0; i < triangleCount; i += 3)
            {
                //we go 3 by 3 for triangles
                bool side1 = plane.GetSide(vertices[triangles[i]] + meshPos);
                bool side2 = plane.GetSide(vertices[triangles[i + 1]] + meshPos);
                bool side3 = plane.GetSide(vertices[triangles[i + 2]] + meshPos);
                if (!(side1 == side2 && side2 == side3))
                {
                    s_perfMarkerIntersection.Begin();
                    NativeArray<bool> bools = new NativeArray<bool>(3, Allocator.Temp);
                    bools[0] = side1;
                    bools[1] = side2;
                    bools[2] = side3;
                    //all the vertices are not on the same side, meaning the triangle is cut by the plane
                    CreateIntersections(plane, vertices, triangles, i, bools,
                        positiveMesh, negativeMesh);
                    s_perfMarkerIntersection.End();
                }
                else
                {
                    s_perfMarkerAddRange.Begin();
                    //vertices are all on the same side, so we just add them to their respective meshes.
                    if (side1)
                    {
                        positiveMesh.AddRange( vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                    }
                    else
                    {
                        negativeMesh.AddRange( vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] );
                    }
                    s_perfMarkerAddRange.End();
                }
            }

            bool ispositiveFacingCamera = Vector3.Dot(transformUp * 1e-2f, forward) > 0;

            positiveMesh.ComputeIntersectionTriangles(!ispositiveFacingCamera);
            negativeMesh.ComputeIntersectionTriangles(ispositiveFacingCamera);

            var vertexAttribute = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Temp);
            vertexAttribute[0] = new VertexAttributeDescriptor(VertexAttribute.Position, dimension: 3, stream: 0);

            /*SetMeshData(0, ref writableMeshDataArray, positiveMesh, vertexAttribute);
            SetMeshData(1, ref writableMeshDataArray, negativeMesh, vertexAttribute);*/
            var datapos = writableMeshDataArray[0];
            datapos.SetVertexBufferParams(positiveMesh.points.Length, vertexAttribute);
            var verticesData = datapos.GetVertexData<Vector3>();
            for (int i = 0; i < verticesData.Length; i++)
            {
                verticesData[i] = positiveMesh.points[i];
            }     
            datapos.SetIndexBufferParams(positiveMesh.triangles.Length, IndexFormat.UInt32);
            var trianglesData = datapos.GetIndexData<int>();
            for (int i = 0; i < trianglesData.Length; i++)
            {
                trianglesData[i] = positiveMesh.triangles[i];
            }
            datapos.subMeshCount = 1;
            datapos.SetSubMesh(0, new SubMeshDescriptor(0, trianglesData.Length, MeshTopology.Triangles));

            var dataneg = writableMeshDataArray[1];
            dataneg.SetVertexBufferParams(negativeMesh.points.Length, vertexAttribute);
            var vertivesDataNegative = dataneg.GetVertexData<Vector3>();
            for (int i = 0; i < vertivesDataNegative.Length; i++)
            {
                vertivesDataNegative[i] = negativeMesh.points[i];
            }
            //vertivesDataNegative = negativeMesh.points.AsArray();
            dataneg.SetIndexBufferParams(negativeMesh.triangles.Length, IndexFormat.UInt32);
            var negativeTrianglesData = dataneg.GetIndexData<int>();
            for (int i = 0; i < negativeTrianglesData.Length; i++)
            {
                negativeTrianglesData[i] = negativeMesh.triangles[i];
            }
            //negativeTrianglesData = negativeMesh.triangles.AsArray();
            dataneg.subMeshCount = 1;
            dataneg.SetSubMesh(0, new SubMeshDescriptor(0, negativeTrianglesData.Length, MeshTopology.Triangles));
        }

        private void CreateIntersections(in Plane plane, NativeArray<Vector3> vertices, NativeArray<int> triangles, int index, NativeArray<bool> sides,
        in MeshDataS positiveSide, in MeshDataS negativeSide)
        {
            int alone, sameSide1, sameSide2;
            bool aloneB;
            if (sides[0] == sides[1])
            {
                aloneB = sides[2];
                alone = index + 2;
                sameSide1 = index;
                sameSide2 = index + 1;
            }
            else if (sides[0] == sides[2])
            {
                aloneB = sides[1];
                alone = index + 1;
                sameSide1 = index;
                sameSide2 = index + 2;
            }
            else //if (side2 == side3)
            {
                aloneB = sides[0];
                alone = index;
                sameSide1 = index + 1;
                sameSide2 = index + 2;
            }

            Vector3 firstIntersection = ComputeIntersection(vertices[triangles[alone]], vertices[triangles[sameSide1]], plane);
            Vector3 secondIntersection = ComputeIntersection(vertices[triangles[alone]], vertices[triangles[sameSide2]], plane);

            //ATTENTION, il faut que les nouvelles faces pointent du même côté que l'ancienne

            bool isFacingCamera = isFaceTowardCamera(vertices[triangles[index]], vertices[triangles[index + 1]], vertices[triangles[index + 2]]);

            if (aloneB)
            {
                //si le point seul est du côté positif du plan
                positiveSide.AddAlonePoint(vertices[triangles[alone]], firstIntersection, secondIntersection, isFacingCamera);
                negativeSide.AddTogetherPoints(vertices[triangles[sameSide1]], vertices[triangles[sameSide2]], firstIntersection, secondIntersection, isFacingCamera);
            }
            else
            {
                negativeSide.AddAlonePoint(vertices[triangles[alone]], firstIntersection, secondIntersection, isFacingCamera);
                positiveSide.AddTogetherPoints(vertices[triangles[sameSide1]], vertices[triangles[sameSide2]], firstIntersection, secondIntersection, isFacingCamera);
            }
        }

        Vector3 ComputeIntersection(in Vector3 alone, in Vector3 otherSide, in Plane plane)
        {
            Ray ray = new Ray(alone, otherSide - alone);//tester si le raycast fonctionne bien
            plane.Raycast(ray, out float enter);
            return alone + (otherSide - alone).normalized * Mathf.Abs(enter);
        }

        bool isFaceTowardCamera(in Vector3 pos0, in Vector3 pos1, in Vector3 pos2)
        {
            Vector3 side1 = pos1 - pos0;
            Vector3 side2 = pos2 - pos0;

            Vector3 cross = Vector3.Cross(side1, side2);

            return Vector3.Dot(cross, forward) > 0;
        }
    }

    private void CreateIntersections(in Plane plane, List<Vector3> vertices, int[] triangles, int index, bool[] sides, 
        MeshData positiveSide, MeshData negativeSide)
    {
        int alone, sameSide1, sameSide2;
        bool aloneB;
        if (sides[0] == sides[1])
        {
            aloneB = sides[2];
            alone = index + 2;
            sameSide1 = index;
            sameSide2 = index + 1;
        }
        else if (sides[0] == sides[2])
        {
            aloneB = sides[1];
            alone = index + 1;
            sameSide1 = index;
            sameSide2 = index + 2;
        }
        else //if (side2 == side3)
        {
            aloneB = sides[0];
            alone = index;
            sameSide1 = index + 1;
            sameSide2 = index + 2;
        }

        Vector3 firstIntersection = ComputeIntersection(vertices[triangles[alone]], vertices[triangles[sameSide1]], plane);
        Vector3 secondIntersection = ComputeIntersection(vertices[triangles[alone]], vertices[triangles[sameSide2]], plane);

        //ATTENTION, il faut que les nouvelles faces pointent du même côté que l'ancienne

        bool isFacingCamera = isFaceTowardCamera(new Vector3[] { vertices[triangles[index]], vertices[triangles[index + 1]], vertices[triangles[index + 2]] });

        if (aloneB)
        {
            //si le point seul est du côté positif du plan
            positiveMeshData.AddAlonePoint(vertices[triangles[alone]], firstIntersection, secondIntersection, isFacingCamera);
            negativeMeshData.AddTogetherPoints(vertices[triangles[sameSide1]], vertices[triangles[sameSide2]], firstIntersection, secondIntersection, isFacingCamera);
        }
        else
        {
            negativeMeshData.AddAlonePoint(vertices[triangles[alone]], firstIntersection, secondIntersection, isFacingCamera);
            positiveMeshData.AddTogetherPoints(vertices[triangles[sameSide1]], vertices[triangles[sameSide2]], firstIntersection, secondIntersection, isFacingCamera);
        }
    }

    Vector3 ComputeIntersection(in Vector3 alone, in Vector3 otherSide, in Plane plane)
    {
        Ray ray = new Ray(alone, otherSide - alone);//tester si le raycast fonctionne bien
        plane.Raycast(ray, out float enter);
        return alone + (otherSide - alone).normalized*Mathf.Abs(enter);
    }

    bool isFaceTowardCamera(Vector3[] pointsPositions)
    {
        Vector3 side1 = pointsPositions[1] - pointsPositions[0];
        Vector3 side2 = pointsPositions[2] - pointsPositions[0];

        Vector3 cross = Vector3.Cross(side1, side2);

        return Vector3.Dot(cross, camForward) > 0;
    }
}
