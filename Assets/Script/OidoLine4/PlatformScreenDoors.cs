using UnityEngine;

namespace LineFour.Oido
{
    public sealed class PlatformScreenDoors : MonoBehaviour
    {
        [SerializeField] Transform[] frontLeaves = System.Array.Empty<Transform>();
        [SerializeField] Transform[] rearLeaves = System.Array.Empty<Transform>();
        Vector3[] frontClosed;
        Vector3[] rearClosed;
        const float SlideDistance = .88f;

        void Awake()
        {
            frontClosed = ClosedPositions(frontLeaves);
            rearClosed = ClosedPositions(rearLeaves);
        }

        public void SetOpening(float opening)
        {
            MoveLeaves(frontLeaves, frontClosed, SlideDistance * opening);
            MoveLeaves(rearLeaves, rearClosed, -SlideDistance * opening);
        }

        static Vector3[] ClosedPositions(Transform[] leaves)
        {
            var positions = new Vector3[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
                positions[i] = leaves[i].localPosition;
            return positions;
        }

        static void MoveLeaves(Transform[] leaves, Vector3[] closed, float offset)
        {
            Vector3 slide = Vector3.forward * offset;
            for (int i = 0; i < leaves.Length; i++)
                leaves[i].localPosition = closed[i] + leaves[i].parent.InverseTransformVector(slide);
        }
    }
}
