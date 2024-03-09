using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace radar
{
    [ExecuteAlways]
    public class RadarController : MonoBehaviour
    {
        [Header("Tags to find enemies and friends")]
        public string friendTag;
        public string enemyTag;
        [Header("Radar configuration")]
        [Tooltip("Scan plane transform")]
        public Transform scanPlaneTransform;
        public RectTransform background;
        public RawImage targetTemplate;
        public Color friendColor;
        public Color enemyColor;
        public float radarRange = 100f;
        [Tooltip("Image offset from border of radar texture")]
        public float radarBorderOffset = 10f;

        private void OnEnable()
        {
#if UNITY_EDITOR
            //Cleanup auto-created game objects after scene reload
            TargetsPool.Init();
            CleanupRadarPoints();
#endif
        }

        private void FixedUpdate()
        {
            HandleIncomingTargets(friendTag, false);
            HandleIncomingTargets(enemyTag, true);
            //Return objects not updated in this frame to pool
            TargetsPool.ReleaseNotUpdated();
            //Update active target transforms
            UpdateActiveTargets();
        }

        private void HandleIncomingTargets(string tag, bool isEnemies)
        {
            //Yep, it's slow, but we have no information about the scene.
            //Alternative: cache references to friends/enemies in Dictionary right after spawn and use this info here
            GameObject[] targets = GameObject.FindGameObjectsWithTag(tag);

            for (int i = 0; i < targets.Length; i++)
            {
                GameObject targetGo = targets[i];
                Transform targetTransform = targetGo.transform;
                //Perform projection to radar plane and cut off targets outside of radar range
                if (ProjectAndCheckIfOutside(targetTransform, out Vector3 targetPositionInScanPlane))
                {
                    continue;
                }

                RadarTarget radarTarget = TargetsPool.Acquire(targetGo, isEnemies, out bool isNew);
                if (isNew)
                {
                    PostCreateTargetUI(radarTarget);
                }

                if (isEnemies != radarTarget.isEnemy)
                {
                    radarTarget.isEnemy = isEnemies;
                    UpdateColor(radarTarget);
                }
            
                radarTarget.UpdatePosition(targetPositionInScanPlane);
            }
        }

        private bool ProjectAndCheckIfOutside(Transform targetTransform,
                                              out Vector3 targetPositionInScanPlane)
        {
            Vector3 targetPosition = targetTransform.position;

            //It's better to call InverseTransformPoints if there'll be a lot of targets for better performance
            targetPositionInScanPlane = scanPlaneTransform.InverseTransformPoint(targetPosition);
            //Simple projection to radar plane
            targetPositionInScanPlane.z = 0f;
        
            return targetPositionInScanPlane.sqrMagnitude > radarRange * radarRange;
        }

        private void PostCreateTargetUI(RadarTarget target)
        {
            if (!target.radarPoint)
            {
                RawImage radarPoint = Instantiate(targetTemplate, targetTemplate.transform.parent, true);
                target.SetRadarTargetUI(radarPoint);
            }
            UpdateColor(target);
            target.radarPoint.gameObject.SetActive(true);
        }

        private void UpdateColor(RadarTarget target)
        {
            target.radarPoint.color = target.isEnemy ? enemyColor : friendColor;
        }

        private void UpdateActiveTargets()
        {
            Vector2 radarSize = background.rect.size / 2f - new Vector2(radarBorderOffset, radarBorderOffset);
            List<RadarTarget> activeTargets = TargetsPool.GetActiveTargets();
            for (int i = 0; i < activeTargets.Count; i++)
            {
                UpdateTarget(activeTargets[i], radarSize);
            }
        }

        private void UpdateTarget(RadarTarget target, Vector2 radarSize)
        {
            Vector2 targetRadarSpace = radarSize *
                new Vector2(target.projected.x, target.projected.y) / radarRange;
        
            target.pointRect.anchoredPosition = targetRadarSpace;
            //Mark inactive for next update to release inactive targets
            target.MarkActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                //Emulate in Edit mode, because FixedUpdate is not called in Editor
                FixedUpdate();
            }
        }
    
#if UNITY_EDITOR
        private void CleanupRadarPoints()
        {
            Transform canvasTransform = background.transform.parent;
            for (var i = canvasTransform.childCount - 1; i >= 0 ; i--)
            {
                Transform targetDotTransform = canvasTransform.GetChild(i);
                if (!targetDotTransform.name.StartsWith("Template(Clone"))
                {
                    continue;
                }
                DestroyImmediate(targetDotTransform.gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3[] lines = new Vector3[]
            {
                new Vector3(-1, -1, 0),
                new Vector3(-1, 1, 0),

                new Vector3(-1, 1, 0),
                new Vector3(1, 1, 0),

                new Vector3(1, 1, 0),
                new Vector3(1, -1, 0),

                new Vector3(1, -1, 0),
                new Vector3(-1, -1, 0)
            };
            float scanPlaneScale = background.rect.size.x / 2f;
            Matrix4x4 scaleMat = Matrix4x4.Scale(new Vector3(scanPlaneScale, scanPlaneScale, scanPlaneScale));
            Gizmos.matrix = scanPlaneTransform.localToWorldMatrix * scaleMat;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLineList(lines);
        }
#endif
    }
}
