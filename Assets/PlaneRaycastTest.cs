using UnityEngine;

public class PlaneRaycastTest : MonoBehaviour
{
    [SerializeField] private Transform[] points;

    public void ArePositionsCounterClockwise()
    {
        Camera cam = Camera.main;

        Vector3 side1 = points[1].position - points[0].position;
        Vector3 side2 = points[2].position - points[0].position;

        Vector3 cross = Vector3.Cross(side1, side2);

        Debug.Log("dot product cam " + Vector3.Dot(cross, cam.transform.forward));
    }
}
