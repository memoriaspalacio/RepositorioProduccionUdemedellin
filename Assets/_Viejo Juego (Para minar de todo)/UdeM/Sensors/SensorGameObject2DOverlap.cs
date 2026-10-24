using System.Collections;
using System.Collections.Generic;
using UdeM.Base;
using UnityEngine;

namespace UdeM.Sensors
{
    public class SensorGameObject2DOverlap : Sensor 
    {
        protected float _radiusSize;
        
        protected override void Awake()
        {
            base.Awake();
            _radiusSize = 0.5f;
        }

        public float radiusSize {
            get { return _radiusSize; }
            set { _radiusSize = value; }
        }

        protected override void ShowSensorDebug()
        {
            Gizmos.color = GetDebugColor();
            Gizmos.DrawWireSphere(_objectOrigin.transform.position, _radiusSize);
        }

        public override void Sensate()
        {
            if (_showSensorDebug) {
                ShowSensorDebug();
            }
            GameObject obj = Physics2D.OverlapCircle(_objectOrigin.transform.position, _radiusSize, _layerMaskFilter)?.gameObject;
            checkFound(obj);
        }
    }
}
