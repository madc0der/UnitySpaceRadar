using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace radar
{
    public class TargetsPool
    {
        private static readonly int allocatedSize = 100;
        
        private readonly Dictionary<long, RadarTarget> goIdToTargetMap = new Dictionary<long, RadarTarget>(allocatedSize);
        private readonly List<RadarTarget> freeTargets = new List<RadarTarget>(allocatedSize);
        private readonly List<RadarTarget> activeTargets = new List<RadarTarget>(allocatedSize);

        private static TargetsPool instance;

        public static void Init()
        {
            //Note: it's better to add pre-allocation for better performance
            if (instance == null)
            {
                GetInstance();
            }
            else
            {
                instance.InitImpl();
            }
        }

        public static List<RadarTarget> GetActiveTargets()
        {
            return GetInstance().activeTargets;
        }

        public static RadarTarget Acquire(GameObject go, bool isEnemy, out bool isNew)
        {
            return GetInstance().AcquireImpl(go, isEnemy, out isNew);
        }

        public static void ReleaseNotUpdated()
        {
            GetInstance().ReleaseNotUpdatedImpl();
        }

        private void InitImpl()
        {
            goIdToTargetMap.Clear();
            freeTargets.Clear();
        }

        private void ReleaseNotUpdatedImpl()
        {
            for (int i = activeTargets.Count - 1; i >= 0; i--)
            {
                RadarTarget target = activeTargets[i];
                if (target.IsActive())
                {
                    continue;
                }
                
                freeTargets.Add(target);
                goIdToTargetMap.Remove(target.goId);
                
                activeTargets.RemoveAt(i);

                if (target.radarPoint)
                {
                    target.radarPoint.gameObject.SetActive(false);
                }
            }
        }

        private RadarTarget AcquireImpl(GameObject go, bool isEnemy, out bool isNew)
        {
            long goId = go.GetInstanceID();
            if (goIdToTargetMap.ContainsKey(goId))
            {
                isNew = false;
                return goIdToTargetMap[goId];
            }

            isNew = true;

            RadarTarget target;
            if (freeTargets.Count > 0)
            {
                target = freeTargets[0];
                target.UpdateToNewTarget(goId, isEnemy);
                freeTargets.RemoveAt(0);
                activeTargets.Add(target);
            }
            else
            {
                target = CreateTarget(goId, isEnemy);
                activeTargets.Add(target);
            }
            
            goIdToTargetMap.Add(goId, target);
            return target;
        }

        private RadarTarget CreateTarget(long goId, bool isEnemy)
        {
            return new RadarTarget(goId, isEnemy);
        }
        
        private static TargetsPool GetInstance()
        {
            if (instance == null)
            {
                instance = new TargetsPool();
            }

            return instance;
        } 
    }

    public class RadarTarget
    {
        public long goId;
        public bool isEnemy;
        public RawImage radarPoint;
        public RectTransform pointRect;
        public Vector3 projected;

        private bool updatedOnLastFrame;
        
        public RadarTarget(long goId, bool isEnemy)
        {
            this.goId = goId;
            this.isEnemy = isEnemy;
            updatedOnLastFrame = true;
        }

        public void UpdateToNewTarget(long goId, bool isEnemy)
        {
            this.goId = goId;
            this.isEnemy = isEnemy;
        }

        public void SetRadarTargetUI(RawImage radarPoint)
        {
            this.radarPoint = radarPoint;
            pointRect = radarPoint.GetComponent<RectTransform>();
        }

        public void MarkActive(bool isActive)
        {
            updatedOnLastFrame = isActive;
        }

        public void UpdatePosition(Vector3 targetPositionInScanPlane)
        {
            projected = targetPositionInScanPlane;
            updatedOnLastFrame = true;
        }

        public bool IsActive()
        {
            return updatedOnLastFrame;
        }
    }
}