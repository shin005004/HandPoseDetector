using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;

namespace MediaPipe.HandPose {
    public sealed class HandAnimator : MonoBehaviour
    {
        [SerializeField] List<Vector4> pointList = new List<Vector4>();

        #region Editable attributes

        [SerializeField] WebcamInput _webcam = null;
        [SerializeField] ResourceSet _resources = null;
        [SerializeField] bool _useAsyncReadback = true;
        [Space]
        [SerializeField] Mesh _jointMesh = null;
        [SerializeField] Mesh _boneMesh = null;
        [Space]
        [SerializeField] Material _jointMaterial = null;
        [SerializeField] Material _boneMaterial = null;
        [Space]
        [SerializeField] RawImage _monitorUI = null;

        #endregion

        #region Private members

        HandPipeline _pipeline;

        static readonly (int, int)[] BonePairs =
        {
        (0, 1), (1, 2), (1, 2), (2, 3), (3, 4),     // Thumb
        (5, 6), (6, 7), (7, 8),                     // Index finger
        (9, 10), (10, 11), (11, 12),                // Middle finger
        (13, 14), (14, 15), (15, 16),               // Ring finger
        (17, 18), (18, 19), (19, 20),               // Pinky
        (0, 17), (2, 5), (5, 9), (9, 13), (13, 17)  // Palm
    };

        Matrix4x4 CalculateJointXform(Vector3 pos)
          => Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.07f);

        Matrix4x4 CalculateBoneXform(Vector3 p1, Vector3 p2)
        {
            var length = Vector3.Distance(p1, p2) / 2;
            var radius = 0.03f;

            var center = (p1 + p2) / 2;
            var rotation = Quaternion.FromToRotation(Vector3.up, p2 - p1);
            var scale = new Vector3(radius, length, radius);

            return Matrix4x4.TRS(center, rotation, scale);
        }

        #endregion

        #region MonoBehaviour implementation

        private void Awake()
        {
            cameraDistance = Camera.main.transform.position.z;
        }

        void Start()
          => _pipeline = new HandPipeline(_resources);

        void OnDestroy()
          => _pipeline.Dispose();

        void LateUpdate()
        {
            // Feed the input image to the Hand pose pipeline.
            _pipeline.UseAsyncReadback = _useAsyncReadback;
            _pipeline.ProcessImage(_webcam.Texture);

            var layer = gameObject.layer;

            // Joint balls
            pointList.Clear();
            for (var i = 0; i < HandPipeline.KeyPointCount; i++)
            {
                Vector4 tempPoint = _pipeline.GetKeyPoint(i);
                tempPoint += new Vector4(0f, 0f, GetDistanceByBase(), 0f);
                var xform = CalculateJointXform(tempPoint);
                pointList.Add(_pipeline.GetKeyPoint(i));
                Graphics.DrawMesh(_jointMesh, xform, _jointMaterial, layer);
            }

            // Bones
            foreach (var pair in BonePairs)
            {
                Vector4 p1 = _pipeline.GetKeyPoint(pair.Item1);
                Vector4 p2 = _pipeline.GetKeyPoint(pair.Item2);

                p1 += new Vector4(0f, 0f, GetDistanceByBase(), 0f);
                p2 += new Vector4(0f, 0f, GetDistanceByBase(), 0f);

                var xform = CalculateBoneXform(p1, p2);
                Graphics.DrawMesh(_boneMesh, xform, _boneMaterial, layer);
            }

            // UI update
            _monitorUI.texture = _webcam.Texture;
        }

        #endregion

        #region HandBase Chooser

        private float cameraDistance;
        private List<float> baseDistance = new List<float>();
        public bool calculateByBase = false;

        private (int, int)[] distancePairs = {
                (0, 17), (0, 13), (0, 9), (0, 5)
            };

        public void Test()
        {
            Debug.Log("Test");
        }
        public void SetBaseDistance()
        {
            baseDistance.Clear();

            for (int i = 0; i < distancePairs.Length; i++)
            {
                var pair = distancePairs[i];
                Vector4 p1 = _pipeline.GetKeyPoint(pair.Item1);
                Vector4 p2 = _pipeline.GetKeyPoint(pair.Item2);

                baseDistance.Add((p1 - p2).magnitude);
            }
            calculateByBase = true;

            Debug.Log(baseDistance[0]);
        }

        public float GetDistanceByBase()
        {
            if (calculateByBase == false)
                return 0f;

            List<float> scaleList = new List<float>();
            for (int i = 0; i < distancePairs.Length; i++)
            {
                var pair = distancePairs[i];
                Vector4 p1 = _pipeline.GetKeyPoint(pair.Item1);
                Vector4 p2 = _pipeline.GetKeyPoint(pair.Item2);

                scaleList.Add((p1 - p2).magnitude / baseDistance[i]);
            }

            scaleList.Remove(scaleList.Max());
            scaleList.Remove(scaleList.Min());

            float meanScale = 0f;
            for (int i = 0; i < scaleList.Count; i++)
                meanScale += scaleList[i];

            meanScale = meanScale / scaleList.Count;

            

            return cameraDistance * (1 - 1 / meanScale);
        }
        #endregion
    }
} // namespace MediaPipe.HandPose
